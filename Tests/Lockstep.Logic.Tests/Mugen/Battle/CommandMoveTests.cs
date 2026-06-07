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
            MChar kfm = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            MBattleEngine engine = new MBattleEngine();
            engine.Add(kfm, data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
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
