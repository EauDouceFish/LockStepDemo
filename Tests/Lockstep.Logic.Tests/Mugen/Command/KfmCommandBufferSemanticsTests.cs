using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class KfmCommandBufferSemanticsTests
    {
        [Test]
        public void KfmDownX_HeldThroughCtrlRecovery_DoesNotRepeatCrouchingLightPunch()
        {
            MBattleEngine engine = LoadKfmEngine();
            MChar actor = engine.Chars[0];
            HoldCrouchUntilReady(engine, actor);

            int entries = 0;
            for (int frame = 0; frame < 90; frame++)
            {
                engine.Tick(new[] { MInput.Down | MInput.X });
                if (actor.StateNo == 400 && actor.Time == 1)
                {
                    entries++;
                }
            }

            Assert.That(entries, Is.EqualTo(1),
                "Holding Down+X must not refresh command=\"x\" after state 400 regains ctrl.");
        }

        [Test]
        public void KfmDownX_ReleaseThenPress_CanRepeatCrouchingLightPunch()
        {
            MBattleEngine engine = LoadKfmEngine();
            MChar actor = engine.Chars[0];
            HoldCrouchUntilReady(engine, actor);

            PressDownXUntilEntry(engine, actor);
            ReleasePunchUntilCrouchCtrl(engine, actor);
            PressDownXUntilEntry(engine, actor);

            Assert.That(actor.StateNo, Is.EqualTo(400));
            Assert.That(actor.Time, Is.EqualTo(1));
        }

        [Test]
        public void KfmDownX_BufferTimeControlsActiveWindowWithoutHoldRefresh()
        {
            MCharData data = LoadKfmData();
            MCommandDef x = FirstCommand(data, "x");
            x.BufferTime = 3;

            MCommandList list = new MCommandList();
            list.Commands.Add(x);

            list.Update(MInput.Down | MInput.X, facingRight: true);
            Assert.That(list.IsActive("x"), Is.True, "The press edge should activate x immediately.");

            list.Update(MInput.Down | MInput.X, facingRight: true);
            Assert.That(list.IsActive("x"), Is.True);

            list.Update(MInput.Down | MInput.X, facingRight: true);
            Assert.That(list.IsActive("x"), Is.True);

            list.Update(MInput.Down | MInput.X, facingRight: true);
            Assert.That(list.IsActive("x"), Is.False,
                "buffer.time=3 should expire instead of being refreshed by a held button.");
        }

        static void HoldCrouchUntilReady(MBattleEngine engine, MChar actor)
        {
            for (int frame = 0; frame < 30; frame++)
            {
                engine.Tick(new[] { MInput.Down });
                if (actor.StateNo == 11 && actor.Ctrl)
                {
                    return;
                }
            }
            Assert.Fail("KFM did not reach crouch ctrl state 11.");
        }

        static void PressDownXUntilEntry(MBattleEngine engine, MChar actor)
        {
            for (int frame = 0; frame < 10; frame++)
            {
                engine.Tick(new[] { frame == 0 ? MInput.Down | MInput.X : MInput.Down });
                if (actor.StateNo == 400 && actor.Time == 1)
                {
                    return;
                }
            }
            Assert.Fail("KFM did not enter crouching light punch state 400.");
        }

        static void ReleasePunchUntilCrouchCtrl(MBattleEngine engine, MChar actor)
        {
            for (int frame = 0; frame < 90; frame++)
            {
                engine.Tick(new[] { MInput.Down });
                if (actor.StateNo == 11 && actor.Ctrl)
                {
                    return;
                }
            }
            Assert.Fail("KFM did not recover to crouch ctrl after crouching light punch.");
        }

        static MBattleEngine LoadKfmEngine()
        {
            MCharData data = LoadKfmData();
            MChar actor = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            MBattleEngine engine = new MBattleEngine();
            engine.Add(actor, data);
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

            return MCharLoader.Load(
                new[] { File.ReadAllText(cns) },
                File.ReadAllText(cns),
                File.ReadAllText(common),
                File.ReadAllText(air),
                File.ReadAllText(cmd),
                "kfm");
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
