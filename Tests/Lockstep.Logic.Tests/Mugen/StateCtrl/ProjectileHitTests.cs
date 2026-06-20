using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Expr;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    /// <summary>
    /// R-ENT 切片 3b：弹幕命中——P1 发射带 HitDef 的弹幕，飞到 P2 处命中使其掉血，命中后弹幕移除。
    /// </summary>
    [TestFixture]
    public sealed class ProjectileHitTests
    {
        const string OwnerCns =
            "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State -1, fire]\ntype = Projectile\ntrigger1 = time = 1\nprojid = 2\nprojanim = 0\n" +
            "velocity = 5, 0\noffset = 0, 0\nprojremovetime = 60\n" +
            "attr = S, NA\ndamage = 40\nhitflag = MAF\nground.type = High\nground.velocity = -4\n" +
            "animtype = Light\nground.hittime = 10\n";
        const string DefCns = "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n";
        const string Air =
            "[Begin Action 0]\nClsn1: 1\n Clsn1[0] = -12,-100, 12, 0\nClsn2: 1\n Clsn2[0] = -12,-100, 12, 0\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        static MBattleEngine Engine()
        {
            MCharData owner = MCharLoader.Load(new[] { OwnerCns }, OwnerCns, null, Air, null, "Owner");
            MCharData def = MCharLoader.Load(new[] { DefCns }, DefCns, null, Air, null, "Dummy");
            MBattleEngine engine = new MBattleEngine();
            MChar p1 = MCharLoader.SpawnChar(owner, 0, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(def, 1, startStateNo: 0, startAnimNo: 0);
            p1.Pos = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            p2.Pos = new FVector3(FFloat.FromInt(60), FFloat.Zero, FFloat.Zero);   // 远些，弹幕飞几帧可观测
            engine.Add(p1, owner);
            engine.Add(p2, def);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static List<MInput> NoInput()
        {
            return new List<MInput> { MInput.None, MInput.None };
        }

        [Test]
        public void Projectile_HitsEnemyAndDamages()
        {
            MBattleEngine engine = Engine();
            MChar p2 = engine.Chars[1];
            int lifeBefore = p2.Life;
            for (int f = 0; f < 12; f++) { engine.Tick(NoInput()); }
            Assert.That(p2.Life, Is.LessThan(lifeBefore), "弹幕命中敌方 P2 掉血");
        }

        [Test]
        public void Projectile_RemovedAfterHit()
        {
            MBattleEngine engine = Engine();
            bool sawProj = false;
            for (int f = 0; f < 12; f++)
            {
                engine.Tick(NoInput());
                if (engine.World.Projectiles.Count > 0) { sawProj = true; }
            }
            Assert.That(sawProj, Is.True, "曾有弹幕在飞");
            Assert.That(engine.World.Projectiles.Count, Is.EqualTo(0), "命中后弹幕被移除");
        }

        [Test]
        public void ProjectileContactTime_IsZeroOnHitFrameAndIncrementsNextTick()
        {
            MBattleEngine engine = Engine();
            MChar owner = engine.Chars[0];
            BytecodeExp hitTime = new MugenExprCompiler().Compile("projhittime(2)");
            int previous = -1;
            bool sawHitFrame = false;

            for (int frame = 0; frame < 20; frame++)
            {
                engine.Tick(NoInput());
                int current = hitTime.Run(owner).ToI();
                if (current == 0)
                {
                    Assert.That(previous, Is.EqualTo(-1));
                    sawHitFrame = true;
                    engine.Tick(NoInput());
                    Assert.That(hitTime.Run(owner).ToI(), Is.EqualTo(1));
                    break;
                }
                previous = current;
            }

            Assert.That(sawHitFrame, Is.True, "projectile should report contact on the hit frame");
        }

        [Test]
        public void HelperProjectileContact_IsRecordedOnRootOnly()
        {
            const string ownerCns =
                "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
                "[State -1, spawn]\ntype = Helper\ntrigger1 = time = 1\nid = 4\nstateno = 1000\n\n" +
                "[Statedef 1000]\ntype = S\nphysics = N\nanim = 0\n\n" +
                "[State 1000, fire]\ntype = Projectile\ntrigger1 = time = 0\nprojid = 7\nprojanim = 0\n" +
                "velocity = 0, 0\noffset = 20, 0\nprojremovetime = 20\n" +
                "attr = S, NA\ndamage = 25\nhitflag = MAF\nground.type = High\nground.velocity = -4\n" +
                "animtype = Light\nground.hittime = 10\n";
            MCharData ownerData = MCharLoader.Load(new[] { ownerCns }, ownerCns, null, Air, null, "Owner");
            MCharData defenderData = MCharLoader.Load(new[] { DefCns }, DefCns, null, Air, null, "Dummy");
            MBattleEngine engine = new MBattleEngine();
            MChar root = MCharLoader.SpawnChar(ownerData, 0, startStateNo: 0, startAnimNo: 0);
            MChar defender = MCharLoader.SpawnChar(defenderData, 1, startStateNo: 0, startAnimNo: 0);
            defender.Pos = new FVector3(FFloat.FromInt(20), FFloat.Zero, FFloat.Zero);
            engine.Add(root, ownerData);
            engine.Add(defender, defenderData);
            engine.LinkPair();
            engine.StartRound();

            BytecodeExp hitTime = new MugenExprCompiler().Compile("projhittime(7)");
            bool sawHitFrame = false;
            for (int frame = 0; frame < 8; frame++)
            {
                engine.Tick(NoInput());
                if (hitTime.Run(root).ToI() == 0)
                {
                    sawHitFrame = true;
                    break;
                }
            }

            Assert.That(sawHitFrame, Is.True);
            Assert.That(engine.Helpers.Count, Is.EqualTo(1));
            Assert.That(hitTime.Run(root).ToI(), Is.EqualTo(0));
            Assert.That(hitTime.Run(engine.Helpers[0]).ToI(), Is.EqualTo(-1));
            Assert.That(root.ProjectileContactId, Is.EqualTo(7));
            Assert.That(engine.Helpers[0].ProjectileContactTime, Is.EqualTo(-1));
        }

        [Test]
        public void ProjectileHit_IsDeterministic()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = Engine();
                for (int f = 0; f < 12; f++) { engine.Tick(NoInput()); }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()));
        }
    }
}
