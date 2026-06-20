using System;
using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;
using Lockstep.Network;

namespace Lockstep.Logic.Tests.Mugen.Battle.Net
{
    /// <summary>
    /// KCP/loopback 延迟锁步会话：两端各自本地模拟、经通道交换输入，
    /// 给定逐帧相同输入 → 每帧 engine 哈希逐位相等（联机确定性的核心保证）。
    /// </summary>
    [TestFixture]
    public sealed class MugenLockstepSessionTests
    {
        const string Cns = "[Statedef 0]\ntype=S\nphysics=S\nanim=0\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n";

        static (MBattleEngine, MRoundSystem) Build()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "Net");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Chars[0].Id = 1;
            engine.Chars[1].Id = 2;
            engine.Chars[0].Life = engine.Chars[0].LifeMax = 1000;
            engine.Chars[1].Life = engine.Chars[1].LifeMax = 1000;
            engine.LinkPair();
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 3 };
            return (engine, round);
        }

        // 一个脚本化输入序列（按帧），驱动某玩家本地输入。
        static MInput Scripted(int playerId, int frame)
        {
            // 两端不同节奏的按键，确保不是平凡全 None。
            if (playerId == 0)
            {
                return (frame % 6) < 3 ? MInput.Right : MInput.A;
            }
            return (frame % 4) < 2 ? MInput.Left : MInput.B;
        }

        [Test]
        public void TwoSessions_OverLoopback_ProduceIdenticalPerFrameHashes()
        {
            LoopbackNetBus bus = new LoopbackNetBus();

            (MBattleEngine engA, MRoundSystem roundA) = Build();
            (MBattleEngine engB, MRoundSystem roundB) = Build();
            MugenLockstepSession a = new MugenLockstepSession(engA, roundA, bus.CreateChannel(0), localPlayerId: 0);
            MugenLockstepSession b = new MugenLockstepSession(engB, roundB, bus.CreateChannel(1), localPlayerId: 1);

            Dictionary<int, ulong> hashA = new Dictionary<int, ulong>();
            Dictionary<int, ulong> hashB = new Dictionary<int, ulong>();
            a.OnFrameSimulated += (f, h) => hashA[f] = h;
            b.OnFrameSimulated += (f, h) => hashB[f] = h;

            // 跑 150 个逻辑步；每端用各自脚本输入。注意发送帧号 = 已采样序号，两端一致。
            for (int tick = 0; tick < 150; tick++)
            {
                a.Step(Scripted(0, tick));
                b.Step(Scripted(1, tick));
            }
            // 收尾：让双方把最后到达的输入排空并推进。
            for (int drain = 0; drain < 4; drain++)
            {
                a.Step(MInput.None);
                b.Step(MInput.None);
            }

            Assert.That(hashA.Count, Is.GreaterThan(100), "应模拟了足够多帧");

            int compared = 0;
            foreach (KeyValuePair<int, ulong> kv in hashA)
            {
                if (hashB.TryGetValue(kv.Key, out ulong hb))
                {
                    Assert.That(hb, Is.EqualTo(kv.Value), "帧 " + kv.Key + " 两端哈希必须逐位相等（确定性）");
                    compared++;
                }
            }
            Assert.That(compared, Is.GreaterThan(100), "两端应有大量共同帧可对账");
        }

        [Test]
        public void Session_FirstInputLagFrames_AreNoneAndDeterministic()
        {
            LoopbackNetBus bus = new LoopbackNetBus();
            (MBattleEngine engA, MRoundSystem roundA) = Build();
            (MBattleEngine engB, MRoundSystem roundB) = Build();
            MugenLockstepSession a = new MugenLockstepSession(engA, roundA, bus.CreateChannel(0), 0, inputLag: 2);
            MugenLockstepSession b = new MugenLockstepSession(engB, roundB, bus.CreateChannel(1), 1, inputLag: 2);

            a.Step(MInput.Right);
            Assert.That(a.SimulatedFrame, Is.EqualTo(1), "default session step cap should avoid multi-frame fast-forward");
            a.Step(MInput.Right);
            Assert.That(a.SimulatedFrame, Is.EqualTo(2));
            b.Step(MInput.Left);
            Assert.That(b.SimulatedFrame, Is.EqualTo(1));
            b.Step(MInput.Left);
            Assert.That(b.SimulatedFrame, Is.EqualTo(2));
        }

        [Test]
        public void Session_StallsWhenRemoteInputMissing()
        {
            LoopbackNetBus bus = new LoopbackNetBus();
            (MBattleEngine engA, MRoundSystem roundA) = Build();
            MugenLockstepSession a = new MugenLockstepSession(engA, roundA, bus.CreateChannel(0), 0,
                playerCount: 2, inputLag: 1);
            // 对端从不发送 → A 只能跑完预置的 1 帧，之后停等。
            for (int i = 0; i < 10; i++)
            {
                a.Step(MInput.Right);
            }
            Assert.That(a.SimulatedFrame, Is.EqualTo(1), "缺远端输入应卡在 InputLag 预置帧之后停等");
            Assert.IsFalse(a.CanAdvance(), "无远端输入不可推进");
        }

        [Test]
        public void Session_BackpressureStopsBeforeOverwritingUnsimulatedFrames()
        {
            LoopbackNetBus bus = new LoopbackNetBus();
            (MBattleEngine engA, MRoundSystem roundA) = Build();
            MugenLockstepSession a = new MugenLockstepSession(engA, roundA, bus.CreateChannel(0), 0,
                playerCount: 2, inputLag: 1);

            for (int i = 0; i < 400; i++)
            {
                a.Step(MInput.Right);
            }

            Assert.That(a.SimulatedFrame, Is.EqualTo(1));
            Assert.That(a.PendingInputFrames, Is.LessThanOrEqualTo(a.MaxPendingInputFrames));
            Assert.That(a.IsInputBackedUp, Is.True);
        }

        [Test]
        public void Session_UnpredictedLeadCapStopsFutureInputQueueGrowth()
        {
            LoopbackNetBus bus = new LoopbackNetBus();
            (MBattleEngine engA, MRoundSystem roundA) = Build();
            MugenLockstepSession a = new MugenLockstepSession(engA, roundA, bus.CreateChannel(0), 0,
                playerCount: 2, inputLag: 1);
            a.MaxUnpredictedInputLead = 3;

            for (int i = 0; i < 50; i++)
            {
                a.Step(MInput.Right);
            }

            Assert.That(a.SimulatedFrame, Is.EqualTo(1));
            Assert.That(a.PendingInputFrames, Is.LessThanOrEqualTo(a.InputLag + a.MaxUnpredictedInputLead + 1));
            Assert.That(a.IsInputBackedUp, Is.False);
        }

        [Test]
        public void MugenInputMsg_CodecRoundTrips()
        {
            MugenInputMsg msg = new MugenInputMsg { Frame = 12345, PlayerId = 1, Input = (int)(MInput.Right | MInput.A) };
            byte[] bytes = MessageCodec.Encode(msg);
            IMessage back = MessageCodec.Decode(bytes, 0, bytes.Length);
            Assert.That(back, Is.InstanceOf<MugenInputMsg>());
            MugenInputMsg r = (MugenInputMsg)back;
            Assert.That(r.Frame, Is.EqualTo(12345));
            Assert.That(r.PlayerId, Is.EqualTo(1));
            Assert.That((MInput)r.Input, Is.EqualTo(MInput.Right | MInput.A));
        }

        [Test]
        public void TwoSessions_OverTransportRelay_WithSerialization_AreDeterministic()
        {
            // 经 ITransport + 实际 MessageCodec 序列化的中继（= KCP 的线格式路径），两端仍逐帧哈希相等。
            FakeRelay relay = new FakeRelay();
            (MBattleEngine engA, MRoundSystem roundA) = Build();
            (MBattleEngine engB, MRoundSystem roundB) = Build();
            MugenLockstepSession a = new MugenLockstepSession(engA, roundA,
                new TransportNetChannel(relay.Client(0)), localPlayerId: 0);
            MugenLockstepSession b = new MugenLockstepSession(engB, roundB,
                new TransportNetChannel(relay.Client(1)), localPlayerId: 1);

            Dictionary<int, ulong> hashA = new Dictionary<int, ulong>();
            Dictionary<int, ulong> hashB = new Dictionary<int, ulong>();
            a.OnFrameSimulated += (f, h) => hashA[f] = h;
            b.OnFrameSimulated += (f, h) => hashB[f] = h;

            for (int tick = 0; tick < 120; tick++)
            {
                a.Step(Scripted(0, tick));
                b.Step(Scripted(1, tick));
            }
            for (int drain = 0; drain < 4; drain++) { a.Step(MInput.None); b.Step(MInput.None); }

            int compared = 0;
            foreach (KeyValuePair<int, ulong> kv in hashA)
            {
                if (hashB.TryGetValue(kv.Key, out ulong hb))
                {
                    Assert.That(hb, Is.EqualTo(kv.Value), "经序列化中继后帧 " + kv.Key + " 两端哈希仍须相等");
                    compared++;
                }
            }
            Assert.That(compared, Is.GreaterThan(80), "应有大量共同帧");
        }

        [Test]
        public void Prediction_RollsBackAndReplaysWhenConfirmedRemoteInputDiffers()
        {
            ManualNetChannel channel = new ManualNetChannel();
            int state = 0;
            MugenLockstepSession session = new MugenLockstepSession(
                inputs =>
                {
                    if ((inputs[0] & MInput.A) != 0) { state += 1; }
                    if ((inputs[1] & MInput.A) != 0) { state += 10; }
                },
                () => (ulong)state,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0,
                captureSimulation: () => state,
                restoreSimulation: snapshot => state = (int)snapshot,
                canPredict: () => true);

            session.PredictionEnabled = true;
            session.MaxPredictFrameCount = 4;

            session.Step(MInput.None);
            Assert.That(session.SimulatedFrame, Is.GreaterThanOrEqualTo(1));
            Assert.That(state, Is.EqualTo(0), "remote frame 0 was predicted as None");

            channel.Enqueue(0, 1, MInput.A);
            session.Step(MInput.None);

            Assert.That(session.RollbackCount, Is.EqualTo(1));
            Assert.That(session.LastRollbackFrame, Is.EqualTo(0));
            Assert.That(state, Is.GreaterThanOrEqualTo(10), "confirmed remote A must be replayed after rollback");
        }

        [Test]
        public void Prediction_ConfirmedMatchingFrameKeepsCachedHashWithoutRollback()
        {
            ManualNetChannel channel = new ManualNetChannel();
            int state = 0;
            MugenLockstepSession session = new MugenLockstepSession(
                inputs =>
                {
                    if ((inputs[0] & MInput.A) != 0) { state += 1; }
                    if ((inputs[1] & MInput.A) != 0) { state += 10; }
                },
                () => (ulong)state,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0,
                captureSimulation: () => state,
                restoreSimulation: snapshot => state = (int)snapshot,
                canPredict: () => true);

            session.PredictionEnabled = true;
            session.Step(MInput.None);

            Assert.That(session.TryGetFrameHash(0, out ulong predictedHash), Is.True);
            Assert.That(predictedHash, Is.EqualTo(0UL));
            Assert.That(session.IsFrameAuthoritative(0), Is.False);
            Assert.That(session.TryGetAuthoritativeFrameHash(0, out _), Is.False);

            channel.Enqueue(0, 1, MInput.None);
            session.Step(MInput.None);

            Assert.That(session.RollbackCount, Is.EqualTo(0));
            Assert.That(session.IsFrameAuthoritative(0), Is.True);
            Assert.That(session.TryGetFrameHash(0, out ulong confirmedHash), Is.True);
            Assert.That(confirmedHash, Is.EqualTo(predictedHash));
            Assert.That(session.TryGetAuthoritativeFrameHash(0, out ulong authoritativeHash), Is.True);
            Assert.That(authoritativeHash, Is.EqualTo(predictedHash));
        }

        [Test]
        public void Prediction_PendingRollbackReplaysEvenAfterPredictionDisabled()
        {
            ManualNetChannel channel = new ManualNetChannel();
            int state = 0;
            MugenLockstepSession session = new MugenLockstepSession(
                inputs =>
                {
                    if ((inputs[0] & MInput.A) != 0) { state += 1; }
                    if ((inputs[1] & MInput.A) != 0) { state += 10; }
                },
                () => (ulong)state,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0,
                captureSimulation: () => state,
                restoreSimulation: snapshot => state = (int)snapshot,
                canPredict: () => true);

            session.PredictionEnabled = true;
            session.Step(MInput.None);
            Assert.That(session.SimulatedFrame, Is.EqualTo(1));

            session.PredictionEnabled = false;
            channel.Enqueue(0, 1, MInput.A);
            session.Step(MInput.None);

            Assert.That(session.RollbackCount, Is.EqualTo(1));
            Assert.That(session.LastRollbackFrame, Is.EqualTo(0));
            Assert.That(state, Is.EqualTo(10));
            Assert.That(session.TryGetAuthoritativeFrameHash(0, out ulong hash), Is.True);
            Assert.That(hash, Is.EqualTo(10UL));
        }

        [Test]
        public void Prediction_AuthoritativeHashUsesReplayedStateAfterRollback()
        {
            ManualNetChannel channel = new ManualNetChannel();
            int state = 0;
            MugenLockstepSession session = new MugenLockstepSession(
                inputs =>
                {
                    if ((inputs[0] & MInput.A) != 0) { state += 1; }
                    if ((inputs[1] & MInput.A) != 0) { state += 10; }
                },
                () => (ulong)state,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0,
                captureSimulation: () => state,
                restoreSimulation: snapshot => state = (int)snapshot,
                canPredict: () => true);

            session.PredictionEnabled = true;
            session.MaxPredictFrameCount = 4;
            session.Step(MInput.None);
            session.Step(MInput.None);

            Assert.That(session.TryGetFrameHash(1, out ulong predictedHash), Is.True);
            Assert.That(predictedHash, Is.EqualTo(0UL));
            Assert.That(session.TryGetAuthoritativeFrameHash(1, out _), Is.False);

            channel.Enqueue(0, 1, MInput.A);
            channel.Enqueue(1, 1, MInput.None);
            session.Step(MInput.None);

            Assert.That(session.RollbackCount, Is.EqualTo(1));
            Assert.That(session.TryGetAuthoritativeFrameHash(1, out _), Is.False);
            session.Step(MInput.None);
            Assert.That(session.TryGetAuthoritativeFrameHash(1, out ulong replayedHash), Is.True);
            Assert.That(replayedHash, Is.EqualTo(10UL));
        }

        [Test]
        public void Prediction_StopsAtMaxPredictFrameCount()
        {
            ManualNetChannel channel = new ManualNetChannel();
            MugenLockstepSession session = new MugenLockstepSession(
                inputs => { },
                () => 0UL,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0,
                captureSimulation: () => 0,
                restoreSimulation: snapshot => { },
                canPredict: () => true);

            session.PredictionEnabled = true;
            session.MaxPredictFrameCount = 1;
            for (int i = 0; i < 8; i++)
            {
                session.Step(MInput.None);
            }

            Assert.That(session.SimulatedFrame, Is.EqualTo(1));
        }

        [Test]
        public void Prediction_SendLeadCapStopsFutureInputQueueGrowth()
        {
            ManualNetChannel channel = new ManualNetChannel();
            MugenLockstepSession session = new MugenLockstepSession(
                inputs => { },
                () => 0UL,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0,
                captureSimulation: () => 0,
                restoreSimulation: snapshot => { },
                canPredict: () => true);

            session.PredictionEnabled = true;
            session.MaxPredictFrameCount = 4;
            session.MaxPredictedInputLead = 6;

            for (int i = 0; i < 200; i++)
            {
                session.Step(MInput.None);
            }

            Assert.That(session.SimulatedFrame, Is.EqualTo(4));
            Assert.That(session.PendingInputFrames, Is.LessThanOrEqualTo(session.MaxPredictedInputLead + 1));
            Assert.That(session.IsInputBackedUp, Is.False);
        }

        [Test]
        public void Prediction_DoesNotSimulatePastLocalSentInputFrame()
        {
            ManualNetChannel channel = new ManualNetChannel();
            MugenLockstepSession session = new MugenLockstepSession(
                inputs => { },
                () => 0UL,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0,
                captureSimulation: () => 0,
                restoreSimulation: snapshot => { },
                canPredict: () => true);

            session.PredictionEnabled = true;
            session.MaxPredictFrameCount = 8;

            session.Step(MInput.None);
            Assert.That(session.InputFrame, Is.EqualTo(1));
            Assert.That(session.SimulatedFrame, Is.EqualTo(1));
            Assert.That(session.PendingInputFrames, Is.GreaterThanOrEqualTo(0));

            session.Step(MInput.A);
            Assert.That(session.InputFrame, Is.EqualTo(2));
            Assert.That(session.SimulatedFrame, Is.EqualTo(2));
            Assert.That(session.PendingInputFrames, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Prediction_DoesNotPromoteLaterHashBeforeEarlierPredictedFrameConfirmed()
        {
            ManualNetChannel channel = new ManualNetChannel();
            int simulated = 0;
            MugenLockstepSession session = new MugenLockstepSession(
                inputs => simulated++,
                () => (ulong)simulated,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0,
                captureSimulation: () => simulated,
                restoreSimulation: snapshot => simulated = (int)snapshot,
                canPredict: () => true);

            session.PredictionEnabled = true;
            session.MaxPredictFrameCount = 4;

            session.Step(MInput.None);
            session.Step(MInput.None);

            Assert.That(session.TryGetFrameHash(0, out _), Is.True);
            Assert.That(session.TryGetFrameHash(1, out _), Is.True);
            Assert.That(session.TryGetAuthoritativeFrameHash(1, out _), Is.False);

            channel.Enqueue(1, 1, MInput.None);
            session.Step(MInput.None);

            Assert.That(session.IsFrameAuthoritative(1), Is.True);
            Assert.That(session.TryGetAuthoritativeFrameHash(0, out _), Is.False);
            Assert.That(session.TryGetAuthoritativeFrameHash(1, out _), Is.False);

            channel.Enqueue(0, 1, MInput.None);
            session.Step(MInput.None);

            Assert.That(session.TryGetAuthoritativeFrameHash(0, out _), Is.True);
            Assert.That(session.TryGetAuthoritativeFrameHash(1, out _), Is.True);
        }

        // 内存中继：模拟服务器把一端发来的消息序列化→rebroadcast 给其余端（走真实 MessageCodec 线格式）。
        sealed class FakeRelay
        {
            readonly List<Queue<IMessage>> _inboxes = new List<Queue<IMessage>>();

            public ITransport Client(int id)
            {
                while (_inboxes.Count <= id) { _inboxes.Add(new Queue<IMessage>()); }
                return new ClientTransport(this, id);
            }

            void Relay(int from, IMessage msg)
            {
                byte[] bytes = MessageCodec.Encode(msg);   // 走真实序列化
                for (int i = 0; i < _inboxes.Count; i++)
                {
                    if (i != from)
                    {
                        _inboxes[i].Enqueue(MessageCodec.Decode(bytes, 0, bytes.Length));
                    }
                }
            }

            bool Poll(int id, out IMessage msg)
            {
                if (id >= 0 && id < _inboxes.Count && _inboxes[id].Count > 0)
                {
                    msg = _inboxes[id].Dequeue();
                    return true;
                }
                msg = null;
                return false;
            }

            sealed class ClientTransport : ITransport
            {
                readonly FakeRelay _relay;
                readonly int _id;
                public ClientTransport(FakeRelay relay, int id) { _relay = relay; _id = id; }
#pragma warning disable 67  // 接口要求的事件，本回环 fake 不触发
                public event Action<int> OnPlayerConnected;
                public event Action<int> OnPlayerDisconnected;
#pragma warning restore 67
                public void Send(int playerId, IMessage msg) { _relay.Relay(_id, msg); }
                public bool Poll(out int playerId, out IMessage msg) { playerId = _id; return _relay.Poll(_id, out msg); }
            }
        }

        [Test]
        public void Step_DefaultCapPreventsFastForwardWhenRemoteBacklogArrives()
        {
            ManualNetChannel channel = new ManualNetChannel();
            int simulated = 0;
            MugenLockstepSession session = new MugenLockstepSession(
                inputs => simulated++,
                () => (ulong)simulated,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0);
            for (int frame = 0; frame < 5; frame++)
            {
                channel.Enqueue(frame, 1, MInput.None);
            }

            session.Step(MInput.None);
            Assert.That(session.LastStepSimulatedFrames, Is.EqualTo(1));
            Assert.That(session.SimulatedFrame, Is.EqualTo(1));
            Assert.That(simulated, Is.EqualTo(1));

            session.Step(MInput.None);
            Assert.That(session.LastStepSimulatedFrames, Is.EqualTo(1));
            Assert.That(session.SimulatedFrame, Is.EqualTo(2));
            Assert.That(simulated, Is.EqualTo(2));
        }

        [Test]
        public void Step_CatchUpCapSimulatesReadyBacklogDeterministically()
        {
            ManualNetChannel channel = new ManualNetChannel();
            int simulated = 0;
            MugenLockstepSession session = new MugenLockstepSession(
                inputs => simulated++,
                () => (ulong)simulated,
                channel,
                localPlayerId: 0,
                playerCount: 2,
                inputLag: 0);

            for (int i = 0; i < 6; i++)
            {
                session.Step(MInput.None);
            }

            Assert.That(session.SimulatedFrame, Is.EqualTo(0));
            for (int frame = 0; frame < 6; frame++)
            {
                channel.Enqueue(frame, 1, MInput.None);
            }

            session.MaxSimulatedFramesPerStep = 4;
            session.Step(MInput.None);

            Assert.That(session.LastStepSimulatedFrames, Is.EqualTo(4));
            Assert.That(session.SimulatedFrame, Is.EqualTo(4));
            Assert.That(simulated, Is.EqualTo(4));
        }

        sealed class ManualNetChannel : IMugenNetChannel
        {
            readonly Queue<MugenInputMsg> _queue = new Queue<MugenInputMsg>();

            public void Enqueue(int frame, int playerId, MInput input)
            {
                _queue.Enqueue(new MugenInputMsg
                {
                    Frame = frame,
                    PlayerId = playerId,
                    Input = (int)input,
                });
            }

            public void SendInput(int frame, int playerId, MInput input)
            {
            }

            public bool TryReceiveInput(out int frame, out int playerId, out MInput input)
            {
                if (_queue.Count > 0)
                {
                    MugenInputMsg message = _queue.Dequeue();
                    frame = message.Frame;
                    playerId = message.PlayerId;
                    input = (MInput)message.Input;
                    return true;
                }

                frame = 0;
                playerId = 0;
                input = MInput.None;
                return false;
            }
        }
    }
}
