using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
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
