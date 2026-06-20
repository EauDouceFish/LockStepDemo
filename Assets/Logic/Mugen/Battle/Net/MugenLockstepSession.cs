using System;
using System.Collections.Generic;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle.Net
{
    public sealed class MugenLockstepSessionSnapshot
    {
        public int InputFrame;
        public int SimFrame;
        public MInput[] Inputs;
        public bool[] Present;
        public bool[] Predicted;
        public int[] FrameStamp;
        public int[] Received;
        public ulong[] FrameHashes;
        public int[] FrameHashStamp;
        public bool[] FrameHashAuthoritative;
        public int LastAuthoritativeHashFrame;
        public MInput[] LastKnownInputs;
        public ulong LastHash;
        public bool PredictionEnabled;
        public int RollbackFrame;
        public int RollbackCount;
        public int LastRollbackFrame;
    }

    sealed class MugenRoundBattleSnapshot
    {
        public MBattleEngineSnapshot Engine;
        public MRoundSystemSnapshot Round;
    }

    /// <summary>Delay-lockstep session with optional client-side prediction and rollback.</summary>
    public sealed class MugenLockstepSession
    {
        public const int DefaultInputLag = 2;

        const int Capacity = 256;
        const int Mask = Capacity - 1;
        const int BackpressureReserve = 16;

        readonly Action<IReadOnlyList<MInput>> _simulate;
        readonly Func<ulong> _computeHash;
        readonly IMugenNetChannel _channel;
        readonly int _localPlayerId;
        readonly int _playerCount;
        readonly int _inputLag;
        readonly Func<object> _captureSimulation;
        readonly Action<object> _restoreSimulation;
        readonly Func<bool> _canPredict;

        readonly MInput[] _inputs;
        readonly bool[] _present;
        readonly bool[] _predicted;
        readonly int[] _frameStamp;
        readonly int[] _received;
        readonly MInput[] _gather;
        readonly MInput[] _lastKnownInputs;
        readonly object[] _simulationSnapshots = new object[Capacity];
        readonly int[] _simulationSnapshotFrame = new int[Capacity];
        readonly ulong[] _frameHashes = new ulong[Capacity];
        readonly int[] _frameHashStamp = new int[Capacity];
        readonly bool[] _frameHashAuthoritative = new bool[Capacity];

        int _inputFrame;
        int _simFrame;
        int _rollbackFrame = -1;
        int _lastAuthoritativeHashFrame = -1;

        public int LocalPlayerId => _localPlayerId;
        public int PlayerCount => _playerCount;
        public int InputLag => _inputLag;
        public int InputFrame => _inputFrame;
        public int PendingInputFrames => _inputFrame - _simFrame;
        public int MaxPendingInputFrames => Capacity - BackpressureReserve;
        public bool IsInputBackedUp => PendingInputFrames >= MaxPendingInputFrames;
        public int SimulatedFrame => _simFrame;
        public ulong LastHash { get; private set; }
        public MRoundSystem Round { get; }
        public MBattleEngine Engine { get; }
        public bool PredictionEnabled { get; set; }
        public int MaxPredictFrameCount { get; set; } = 8;
        public int MaxUnpredictedInputLead { get; set; } = Capacity - BackpressureReserve;
        public int MaxPredictedInputLead { get; set; }
        public int MaxSimulatedFramesPerStep { get; set; } = 1;
        public int LastStepSimulatedFrames { get; private set; }
        public int RollbackCount { get; private set; }
        public int LastRollbackFrame { get; private set; } = -1;

        public event Action<int, ulong> OnFrameSimulated;
        public event Action<int, MInput[]> OnFrameInputs;
        public event Action<int> OnFrameConfirmed;
        public event Action<int, MInput, MInput> OnPredictionFork;
        public event Action<int> OnRollback;

        public MugenLockstepSession(MBattleEngine engine, MRoundSystem round, IMugenNetChannel channel,
            int localPlayerId, int playerCount = 2, int inputLag = DefaultInputLag)
            : this(round.Tick, engine.ComputeHash, channel, localPlayerId, playerCount, inputLag,
                () => new MugenRoundBattleSnapshot
                {
                    Engine = engine.Snapshot(),
                    Round = round.Snapshot(),
                },
                snapshot =>
                {
                    var s = (MugenRoundBattleSnapshot)snapshot;
                    engine.Restore(s.Engine);
                    round.Restore(s.Round);
                })
        {
            Engine = engine;
            Round = round;
        }

        public MugenLockstepSession(Action<IReadOnlyList<MInput>> simulate, Func<ulong> computeHash,
            IMugenNetChannel channel, int localPlayerId, int playerCount = 2, int inputLag = DefaultInputLag,
            Func<object> captureSimulation = null, Action<object> restoreSimulation = null,
            Func<bool> canPredict = null)
        {
            _simulate = simulate ?? throw new ArgumentNullException(nameof(simulate));
            _computeHash = computeHash ?? throw new ArgumentNullException(nameof(computeHash));
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _localPlayerId = localPlayerId;
            _playerCount = playerCount;
            _inputLag = inputLag < 0 ? 0 : inputLag;
            _captureSimulation = captureSimulation;
            _restoreSimulation = restoreSimulation;
            _canPredict = canPredict;

            _inputs = new MInput[Capacity * playerCount];
            _present = new bool[Capacity * playerCount];
            _predicted = new bool[Capacity * playerCount];
            _frameStamp = new int[Capacity];
            _received = new int[Capacity];
            _gather = new MInput[playerCount];
            _lastKnownInputs = new MInput[playerCount];

            for (int i = 0; i < Capacity; i++)
            {
                _frameStamp[i] = -1;
                _simulationSnapshotFrame[i] = -1;
                _frameHashStamp[i] = -1;
            }

            for (int f = 0; f < _inputLag; f++)
            {
                for (int p = 0; p < _playerCount; p++)
                {
                    StoreConfirmed(f, p, MInput.None);
                }
            }
            _inputFrame = _inputLag;
            _simFrame = 0;
        }

        public void Step(MInput localInput)
        {
            LastStepSimulatedFrames = 0;
            DrainIncoming();
            bool predictionActive = PredictionActive();

            if (!IsInputBackedUp && CanSendNextInput(predictionActive))
            {
                int frame = _inputFrame++;
                StoreConfirmed(frame, _localPlayerId, localInput);
                _channel.SendInput(frame, _localPlayerId, localInput);
            }

            if (predictionActive || CanResolveRollback())
            {
                ResolveRollbackIfNeeded();
            }

            int maxFramesThisStep = MaxSimulatedFramesPerStep <= 0 ? int.MaxValue : MaxSimulatedFramesPerStep;
            while (LastStepSimulatedFrames < maxFramesThisStep && TryGather(_simFrame, predictionActive))
            {
                bool predictedFrame = FrameHasPrediction(_simFrame);
                if (predictedFrame && PredictedLead() > MaxPredictFrameCount)
                {
                    break;
                }

                SaveSimulationSnapshot(_simFrame);
                _simulate(_gather);
                LastHash = _computeHash();
                int hashSlot = _simFrame & Mask;
                _frameHashes[hashSlot] = LastHash;
                _frameHashStamp[hashSlot] = _simFrame;
                _frameHashAuthoritative[hashSlot] = false;
                OnFrameInputs?.Invoke(_simFrame, _gather);
                OnFrameSimulated?.Invoke(_simFrame, LastHash);
                _simFrame++;
                LastStepSimulatedFrames++;
                PromoteAuthoritativeHashes();
            }
        }

        bool CanSendNextInput(bool predictionActive)
        {
            int limit = predictionActive ? PredictedInputLeadLimit() : _inputLag + MaxUnpredictedInputLead;
            limit = System.Math.Min(System.Math.Max(_inputLag, limit), MaxPendingInputFrames - 1);
            return PendingInputFrames <= limit;
        }

        int PredictedInputLeadLimit()
        {
            if (MaxPredictedInputLead > 0)
            {
                return MaxPredictedInputLead;
            }

            int safety = System.Math.Max(2, _inputLag);
            return _inputLag + System.Math.Max(1, MaxPredictFrameCount) + safety;
        }

        public void DrainIncoming()
        {
            while (_channel.TryReceiveInput(out int rf, out int rp, out MInput ri))
            {
                StoreConfirmed(rf, rp, ri);
            }
        }

        public bool CanAdvance() => AuthorityReady(_simFrame);

        public bool IsFrameAuthoritative(int frame) => AuthorityReady(frame);

        public bool IsFramePredicted(int frame) => FrameHasPrediction(frame);

        public bool TryGetFrameHash(int frame, out ulong hash)
        {
            int slot = frame & Mask;
            if (_frameHashStamp[slot] == frame)
            {
                hash = _frameHashes[slot];
                return true;
            }
            hash = 0UL;
            return false;
        }

        public bool TryGetAuthoritativeFrameHash(int frame, out ulong hash)
        {
            int slot = frame & Mask;
            if (_frameHashStamp[slot] == frame && _frameHashAuthoritative[slot] && AuthorityReady(frame))
            {
                hash = _frameHashes[slot];
                return true;
            }
            hash = 0UL;
            return false;
        }

        public MugenLockstepSessionSnapshot Snapshot()
        {
            return new MugenLockstepSessionSnapshot
            {
                InputFrame = _inputFrame,
                SimFrame = _simFrame,
                Inputs = CloneArray(_inputs),
                Present = CloneArray(_present),
                Predicted = CloneArray(_predicted),
                FrameStamp = CloneArray(_frameStamp),
                Received = CloneArray(_received),
                FrameHashes = CloneArray(_frameHashes),
                FrameHashStamp = CloneArray(_frameHashStamp),
                FrameHashAuthoritative = CloneArray(_frameHashAuthoritative),
                LastAuthoritativeHashFrame = _lastAuthoritativeHashFrame,
                LastKnownInputs = CloneArray(_lastKnownInputs),
                LastHash = LastHash,
                PredictionEnabled = PredictionEnabled,
                RollbackFrame = _rollbackFrame,
                RollbackCount = RollbackCount,
                LastRollbackFrame = LastRollbackFrame,
            };
        }

        public void Restore(MugenLockstepSessionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _inputFrame = snapshot.InputFrame;
            _simFrame = snapshot.SimFrame;
            CopyArray(snapshot.Inputs, _inputs);
            CopyArray(snapshot.Present, _present);
            CopyArray(snapshot.Predicted, _predicted);
            CopyArray(snapshot.FrameStamp, _frameStamp);
            CopyArray(snapshot.Received, _received);
            CopyArray(snapshot.FrameHashes, _frameHashes);
            CopyArray(snapshot.FrameHashStamp, _frameHashStamp);
            CopyArray(snapshot.FrameHashAuthoritative, _frameHashAuthoritative);
            _lastAuthoritativeHashFrame = snapshot.LastAuthoritativeHashFrame;
            CopyArray(snapshot.LastKnownInputs, _lastKnownInputs);
            LastHash = snapshot.LastHash;
            PredictionEnabled = snapshot.PredictionEnabled;
            _rollbackFrame = snapshot.RollbackFrame;
            RollbackCount = snapshot.RollbackCount;
            LastRollbackFrame = snapshot.LastRollbackFrame;
        }

        bool PredictionActive()
        {
            return PredictionEnabled &&
                   _captureSimulation != null &&
                   _restoreSimulation != null &&
                   (_canPredict == null || _canPredict());
        }

        bool CanResolveRollback()
        {
            return _rollbackFrame >= 0 &&
                   _captureSimulation != null &&
                   _restoreSimulation != null;
        }

        void ResolveRollbackIfNeeded()
        {
            if (_rollbackFrame < 0)
            {
                return;
            }

            int frame = _rollbackFrame;
            if (RestoreSimulationSnapshot(frame))
            {
                _simFrame = frame;
                RollbackCount++;
                LastRollbackFrame = frame;
                OnRollback?.Invoke(frame);
            }
            _rollbackFrame = -1;
        }

        void SaveSimulationSnapshot(int frame)
        {
            if (_captureSimulation == null)
            {
                return;
            }

            int slot = frame & Mask;
            _simulationSnapshots[slot] = _captureSimulation();
            _simulationSnapshotFrame[slot] = frame;
        }

        bool RestoreSimulationSnapshot(int frame)
        {
            if (_restoreSimulation == null)
            {
                return false;
            }

            int slot = frame & Mask;
            if (_simulationSnapshotFrame[slot] != frame || _simulationSnapshots[slot] == null)
            {
                return false;
            }

            _restoreSimulation(_simulationSnapshots[slot]);
            return true;
        }

        bool TryGather(int frame, bool allowPrediction)
        {
            int slot = frame & Mask;
            if (_frameStamp[slot] != frame)
            {
                if (!allowPrediction || !PrepareSlot(frame))
                {
                    return false;
                }
            }

            int b = slot * _playerCount;
            for (int p = 0; p < _playerCount; p++)
            {
                int idx = b + p;
                if (!_present[idx])
                {
                    if (!allowPrediction)
                    {
                        return false;
                    }
                    if (p == _localPlayerId && frame >= _inputFrame)
                    {
                        return false;
                    }
                    StorePredicted(frame, p, _lastKnownInputs[p]);
                }
            }

            for (int p = 0; p < _playerCount; p++)
            {
                _gather[p] = _inputs[b + p];
            }
            return true;
        }

        void StoreConfirmed(int frame, int player, MInput input)
        {
            if (frame < 0 || player < 0 || player >= _playerCount)
            {
                return;
            }
            if (!PrepareSlot(frame))
            {
                return;
            }

            int slot = frame & Mask;
            int idx = slot * _playerCount + player;
            if (_present[idx])
            {
                if (_inputs[idx] != input && frame < _simFrame)
                {
                    MarkRollback(frame, _inputs[idx], input);
                }
            }
            else
            {
                _present[idx] = true;
                _received[slot]++;
            }

            _inputs[idx] = input;
            _predicted[idx] = false;
            _lastKnownInputs[player] = input;

            if (AuthorityReady(frame))
            {
                PromoteAuthoritativeHashes();
            }
        }

        bool StorePredicted(int frame, int player, MInput input)
        {
            if (frame < 0 || player < 0 || player >= _playerCount)
            {
                return false;
            }
            if (!PrepareSlot(frame))
            {
                return false;
            }

            int slot = frame & Mask;
            int idx = slot * _playerCount + player;
            if (_present[idx] && !_predicted[idx])
            {
                return true;
            }
            if (!_present[idx])
            {
                _present[idx] = true;
                _received[slot]++;
            }
            _inputs[idx] = input;
            _predicted[idx] = true;
            return true;
        }

        bool PrepareSlot(int frame)
        {
            int slot = frame & Mask;
            if (_frameStamp[slot] == frame)
            {
                return true;
            }
            if (_frameStamp[slot] >= _simFrame)
            {
                return false;
            }

            _frameStamp[slot] = frame;
            _received[slot] = 0;
            int b = slot * _playerCount;
            for (int p = 0; p < _playerCount; p++)
            {
                _present[b + p] = false;
                _predicted[b + p] = false;
                _inputs[b + p] = MInput.None;
            }
            _frameHashStamp[slot] = -1;
            _frameHashAuthoritative[slot] = false;
            if (_lastAuthoritativeHashFrame >= frame)
            {
                _lastAuthoritativeHashFrame = frame - 1;
            }
            return true;
        }

        bool AuthorityReady(int frame)
        {
            int slot = frame & Mask;
            if (_frameStamp[slot] != frame || _received[slot] < _playerCount)
            {
                return false;
            }

            int b = slot * _playerCount;
            for (int p = 0; p < _playerCount; p++)
            {
                if (!_present[b + p] || _predicted[b + p])
                {
                    return false;
                }
            }
            return true;
        }

        bool FrameHasPrediction(int frame)
        {
            int slot = frame & Mask;
            if (_frameStamp[slot] != frame)
            {
                return false;
            }

            int b = slot * _playerCount;
            for (int p = 0; p < _playerCount; p++)
            {
                if (_predicted[b + p])
                {
                    return true;
                }
            }
            return false;
        }

        int PredictedLead()
        {
            int count = 0;
            for (int frame = _simFrame; frame >= 0 && count <= MaxPredictFrameCount; frame--)
            {
                if (!FrameHasPrediction(frame))
                {
                    break;
                }
                count++;
            }
            return count;
        }

        void MarkRollback(int frame, MInput predicted, MInput confirmed)
        {
            if (_rollbackFrame < 0 || frame < _rollbackFrame)
            {
                _rollbackFrame = frame;
            }
            InvalidateAuthoritativeHashesFrom(frame);
            OnPredictionFork?.Invoke(frame, predicted, confirmed);
        }

        void InvalidateAuthoritativeHashesFrom(int frame)
        {
            int endFrame = _simFrame;
            if (endFrame > frame + Capacity)
            {
                endFrame = frame + Capacity;
            }

            for (int f = frame; f < endFrame; f++)
            {
                int slot = f & Mask;
                if (_frameHashStamp[slot] == f)
                {
                    _frameHashAuthoritative[slot] = false;
                }
            }
            if (_lastAuthoritativeHashFrame >= frame)
            {
                _lastAuthoritativeHashFrame = frame - 1;
            }
        }

        void PromoteAuthoritativeHashes()
        {
            if (_rollbackFrame >= 0)
            {
                return;
            }

            int frame = _lastAuthoritativeHashFrame + 1;
            while (frame < _simFrame && AuthorityReady(frame))
            {
                int slot = frame & Mask;
                if (_frameHashStamp[slot] != frame)
                {
                    break;
                }

                if (!_frameHashAuthoritative[slot])
                {
                    _frameHashAuthoritative[slot] = true;
                    OnFrameConfirmed?.Invoke(frame);
                }
                _lastAuthoritativeHashFrame = frame;
                frame++;
            }
        }

        static MInput[] CloneArray(MInput[] source)
        {
            return source != null ? (MInput[])source.Clone() : null;
        }

        static bool[] CloneArray(bool[] source)
        {
            return source != null ? (bool[])source.Clone() : null;
        }

        static int[] CloneArray(int[] source)
        {
            return source != null ? (int[])source.Clone() : null;
        }

        static ulong[] CloneArray(ulong[] source)
        {
            return source != null ? (ulong[])source.Clone() : null;
        }

        static void CopyArray(MInput[] source, MInput[] target)
        {
            if (source == null || target == null)
            {
                return;
            }
            int count = source.Length < target.Length ? source.Length : target.Length;
            for (int i = 0; i < count; i++)
            {
                target[i] = source[i];
            }
        }

        static void CopyArray(bool[] source, bool[] target)
        {
            if (source == null || target == null)
            {
                return;
            }
            int count = source.Length < target.Length ? source.Length : target.Length;
            for (int i = 0; i < count; i++)
            {
                target[i] = source[i];
            }
        }

        static void CopyArray(int[] source, int[] target)
        {
            if (source == null || target == null)
            {
                return;
            }
            int count = source.Length < target.Length ? source.Length : target.Length;
            for (int i = 0; i < count; i++)
            {
                target[i] = source[i];
            }
        }

        static void CopyArray(ulong[] source, ulong[] target)
        {
            if (source == null || target == null)
            {
                return;
            }
            int count = source.Length < target.Length ? source.Length : target.Length;
            for (int i = 0; i < count; i++)
            {
                target[i] = source[i];
            }
        }
    }
}
