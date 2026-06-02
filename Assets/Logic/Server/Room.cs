using System.Collections.Generic;
using Lockstep.Input;
using Lockstep.Network;

namespace Lockstep.Server
{
    public enum RoomState
    {
        WaitingPlayers,
        Running,
        Finished,
    }

    public interface IRoomSink
    {
        void SendTo(int playerId, IMessage msg);
    }

    public struct PlayerSlot
    {
        public int PlayerId;
        public FrameInput LastInput;
    }

    public class Room
    {
        public const int MaxPlayers = 2;
        public const int TickIntervalMs = 33;
        public const int MaxFrames = 30 * 60 * 5;

        public readonly string RoomId;
        public readonly int RoomSeed;
        public RoomState State { get; private set; } = RoomState.WaitingPlayers;
        public int CurrentFrame { get; private set; }
        public int PlayerCount => _players.Count;

        readonly List<PlayerSlot> _players = new List<PlayerSlot>(MaxPlayers);
        readonly Dictionary<long, FrameInput> _frameInputs = new Dictionary<long, FrameInput>(64);
        readonly IRoomSink _sink;
        long _lastTickMs;

        static long Key(int playerId, int frame) => ((long)playerId << 32) | (uint)frame;

        public Room(string roomId, int seed, IRoomSink sink)
        {
            RoomId = roomId;
            RoomSeed = seed;
            _sink = sink;
        }

        public void OnJoin(int playerId, long nowMs)
        {
            if (State != RoomState.WaitingPlayers) return;
            _players.Add(new PlayerSlot { PlayerId = playerId });
            if (_players.Count < MaxPlayers) return;

            State = RoomState.Running;
            CurrentFrame = 0;
            _lastTickMs = nowMs;
            for (int i = 0; i < _players.Count; i++)
            {
                _sink.SendTo(_players[i].PlayerId, new RoomReadyMsg
                {
                    RoomSeed = RoomSeed,
                    LocalPlayerId = _players[i].PlayerId,
                    PlayerCount = MaxPlayers,
                });
            }
        }

        public void OnInput(int playerId, int frame, FrameInput input)
        {
            if (State != RoomState.Running) return;
            if (frame < CurrentFrame) return;
            _frameInputs[Key(playerId, frame)] = input;
        }

        public void Update(long nowMs)
        {
            if (State != RoomState.Running) return;
            if (nowMs - _lastTickMs < TickIntervalMs) return;
            Tick();
            _lastTickMs = nowMs;
        }

        public void EndGame(int winnerPlayerId)
        {
            if (State == RoomState.Finished) return;
            State = RoomState.Finished;
            BroadcastToAll(new GameOverMsg { WinnerPlayerId = winnerPlayerId });
        }

        void BroadcastToAll(IMessage msg)
        {
            for (int i = 0; i < _players.Count; i++)
                _sink.SendTo(_players[i].PlayerId, msg);
        }

        void Tick()
        {
            var inputs = new FrameInput[_players.Count];
            for (int i = 0; i < _players.Count; i++)
            {
                var slot = _players[i];
                long key = Key(slot.PlayerId, CurrentFrame);
                if (_frameInputs.TryGetValue(key, out var input))
                {
                    slot.LastInput = input;
                    _players[i] = slot;
                    inputs[i] = input;
                    _frameInputs.Remove(key);
                }
                else
                {
                    inputs[i] = slot.LastInput;
                }
            }

            BroadcastToAll(new FrameAuthMsg { Frame = CurrentFrame, Inputs = inputs });
            CurrentFrame++;

            if (CurrentFrame >= MaxFrames) EndGame(-1);
        }
    }
}
