using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Tests;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class CommandMoveTests
    {
        static MBattleEngine LoadKfm()
        {
            MCharData data = LoadKfmData();
            MChar kfm = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            MBattleEngine engine = new MBattleEngine();
            engine.Add(kfm, data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static MCharData LoadKfmData()
        {
            string kfmDir = TestAssets.KfmDir();
            string common = TestAssets.Common1Cns();
            string cns = Path.Combine(kfmDir, "kfm.cns");
            string cmd = Path.Combine(kfmDir, "kfm.cmd");
            string air = Path.Combine(kfmDir, "kfm.air");
            if (!File.Exists(cns) || !File.Exists(air) || !File.Exists(common) || !File.Exists(cmd))
            {
                Assert.Ignore("KFM/common1 assets are missing.");
            }

            MCharData data = MCharLoader.Load(
                new[] { File.ReadAllText(cns) },
                File.ReadAllText(cns),
                File.ReadAllText(common),
                File.ReadAllText(air),
                File.ReadAllText(cmd),
                "kfm");
            return data;
        }

        [Test]
        public void Loader_ParsesCmdStatedefMinus1_AsCommandInterpreter()
        {
            MBattleEngine engine = LoadKfm();
            Assert.That(engine.Data[0].States.ContainsKey(-1), Is.True,
                ".cmd [Statedef -1] must be imported into character states");
            Assert.That(engine.Data[0].Commands.Count, Is.GreaterThan(0), "command defs should be parsed");
        }

        static bool PressAfterWarmup(MBattleEngine engine, MInput button, int targetState, int frames = 12)
        {
            MChar c = engine.Chars[0];
            List<MInput> none = new List<MInput> { MInput.None };
            for (int f = 0; f < 5; f++)
            {
                engine.Tick(none);
            }

            for (int f = 0; f < frames; f++)
            {
                engine.Tick(f == 0 ? new List<MInput> { button } : none);
                if (c.StateNo == targetState)
                {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void PressKick_A_EntersLightKick230()
        {
            MBattleEngine engine = LoadKfm();
            Assert.That(PressAfterWarmup(engine, MInput.A, 230), Is.True);
        }

        [Test]
        public void PressPunch_X_EntersLightPunch200()
        {
            MBattleEngine engine = LoadKfm();
            Assert.That(PressAfterWarmup(engine, MInput.X, 200), Is.True);
        }

        [Test]
        public void PressPunch_Y_EntersStrongPunch210()
        {
            MBattleEngine engine = LoadKfm();
            Assert.That(PressAfterWarmup(engine, MInput.Y, 210), Is.True);
        }

        [TestCase("QCF_x", 1000, TestName = "Motion_QCF_X_EntersLightKungFuPalm1000")]
        [TestCase("upper_x", 1100, TestName = "Motion_Upper_X_EntersLightKungFuUpper1100")]
        [TestCase("FF", 100, TestName = "Motion_FF_EntersRunForward100")]
        public void KfmMotionCommands_EnterExpectedStates(string commandName, int targetState)
        {
            MBattleEngine engine = LoadKfm();
            MCommandDef command = FirstCommand(engine.Data[0], commandName);
            MMoveTestResult result = MMoveTestRunner.Run(
                engine.Data[0],
                new[] { command },
                targetState,
                new[] { new MMovePrerequisiteProfile { Name = "standing-close", Distance = 40, ActorPower = -1 } });

            Assert.That(result.CommandMatched, Is.True, commandName + " should become active");
            Assert.That(result.Status, Is.EqualTo(MMoveTestStatus.Passed),
                commandName + " should enter state " + targetState + ", final=" + result.FinalStateNo);
        }

        [Test]
        public void MovePreviewSession_PlaysButtonMoveOnceThenReturnsToDefault()
        {
            MCharData data = LoadKfmData();
            MBattleEngine engine = new MBattleEngine();
            MChar p1 = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            p2.Facing = -Lockstep.Math.FFloat.One;
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();

            MMovePreviewSession preview = new MMovePreviewSession(
                new[] { FirstCommand(data, "x") },
                facingRight: true,
                targetStateNo: 200,
                label: "轻拳");

            int entries = 0;
            int previous = p1.StateNo;
            for (int frame = 0; frame < 180 && !preview.Done; frame++)
            {
                MInput input = preview.NextInput();
                engine.Tick(new[] { input, MInput.None });
                preview.AfterTick(engine);
                if (p1.StateNo == 200 && previous != 200)
                {
                    entries++;
                }
                previous = p1.StateNo;
            }

            Assert.That(preview.EnteredTarget, Is.True, "预览脚本应进入轻拳 state 200。");
            Assert.That(entries, Is.EqualTo(1), "同一次展馆按钮脚本不能在返回 state0 后重复触发 state 200。");
            Assert.That(preview.Done, Is.True, "招式播放结束后预览 session 应结束。");
            Assert.That(p1.StateNo, Is.EqualTo(0), "KFM 轻拳状态自然结束后应回默认 state 0。");
            Assert.That(p1.Ctrl, Is.True);
        }

        [Test]
        public void MovePreviewSession_ReportsTimeout_WhenMoveDoesNotNaturallyRecover()
        {
            MCharData data = LoadSyntheticStuckMoveData();
            MBattleEngine engine = LoadTwoPlayerEngine(data);
            MChar p1 = engine.Chars[0];

            MMovePreviewSession preview = new MMovePreviewSession(
                new[] { FirstCommand(data, "x") },
                facingRight: true,
                targetStateNo: 200,
                label: "stuck",
                maxMoveFrames: 3);

            for (int frame = 0; frame < 30 && !preview.Done; frame++)
            {
                MInput input = preview.NextInput();
                engine.Tick(new[] { input, MInput.None });
                preview.AfterTick(engine);
            }

            Assert.That(preview.EnteredTarget, Is.True);
            Assert.That(preview.TimedOutInMoveState, Is.True);
            Assert.That(preview.Done, Is.True);
            Assert.That(p1.StateNo, Is.EqualTo(200), "The synthetic state has no ChangeState back to 0.");
        }

        [Test]
        public void MovePreviewSession_WaitsForNaturalRecoveryThroughIntermediateState()
        {
            MCharData data = LoadSyntheticRecoveryChainData();
            MBattleEngine engine = LoadTwoPlayerEngine(data);
            MChar p1 = engine.Chars[0];

            MMovePreviewSession preview = new MMovePreviewSession(
                new[] { FirstCommand(data, "x") },
                facingRight: true,
                targetStateNo: 200,
                label: "chain",
                maxMoveFrames: 20);

            bool sawRecoveryState = false;
            for (int frame = 0; frame < 30 && !preview.Done; frame++)
            {
                MInput input = preview.NextInput();
                engine.Tick(new[] { input, MInput.None });
                preview.AfterTick(engine);
                if (p1.StateNo == 201)
                {
                    sawRecoveryState = true;
                }
            }

            Assert.That(preview.EnteredTarget, Is.True);
            Assert.That(preview.TimedOutInMoveState, Is.False);
            Assert.That(sawRecoveryState, Is.True, "Preview must keep waiting after target state enters a recovery state.");
            Assert.That(preview.Done, Is.True);
            Assert.That(p1.StateNo, Is.EqualTo(0));
            Assert.That(p1.Ctrl, Is.True);
        }

        static MCharData LoadSyntheticStuckMoveData()
        {
            const string cns = @"
[Statedef 0]
type = S
movetype = I
physics = S
ctrl = 1

[Statedef 200]
type = S
movetype = A
physics = S
ctrl = 0
";
            const string cmd = @"
[Command]
name = ""x""
command = x
time = 1

[Statedef -1]
[State -1, x]
type = ChangeState
trigger1 = command = ""x""
value = 200
";
            return MCharLoader.Load(new[] { cns }, cns, null, null, cmd, "SyntheticStuck");
        }

        static MCharData LoadSyntheticRecoveryChainData()
        {
            const string cns = @"
[Statedef 0]
type = S
movetype = I
physics = S
ctrl = 1

[Statedef 200]
type = S
movetype = A
physics = S
ctrl = 0
[State 200, recovery]
type = ChangeState
trigger1 = Time >= 2
value = 201

[Statedef 201]
type = S
movetype = A
physics = S
ctrl = 0
[State 201, done]
type = ChangeState
trigger1 = Time >= 2
value = 0
ctrl = 1
";
            const string cmd = @"
[Command]
name = ""x""
command = x
time = 1

[Statedef -1]
[State -1, x]
type = ChangeState
trigger1 = command = ""x""
value = 200
";
            return MCharLoader.Load(new[] { cns }, cns, null, null, cmd, "SyntheticChain");
        }

        static MBattleEngine LoadTwoPlayerEngine(MCharData data)
        {
            MBattleEngine engine = new MBattleEngine();
            MChar p1 = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            p2.Facing = -Lockstep.Math.FFloat.One;
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static MCommandDef FirstCommand(MCharData data, string name)
        {
            for (int i = 0; i < data.Commands.Count; i++)
            {
                if (data.Commands[i].Name == name)
                {
                    return data.Commands[i];
                }
            }
            Assert.Fail("Missing command " + name);
            return null;
        }
    }
}
