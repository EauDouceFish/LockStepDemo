using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Battle;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    /// <summary>
    /// R-ENT 切片 3a：Projectile 控制器 → 弹幕实体（运动 + 生命周期）+ numproj 触发器。命中归 3b。
    /// </summary>
    [TestFixture]
    public sealed class ProjectileControllerTests
    {
        const string OwnerCns =
            "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State -1, fire once]\ntype = Projectile\ntrigger1 = time = 1\nprojid = 3\nvelocity = 5, 0\n" +
            "offset = 20, 0\nprojremovetime = 6\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        static MBattleEngine Engine()
        {
            MCharData data = MCharLoader.Load(new[] { OwnerCns }, OwnerCns, null, Air, null, "Dummy");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static List<MInput> NoInput()
        {
            return new List<MInput> { MInput.None };
        }

        [Test]
        public void Projectile_FiresAndMoves()
        {
            MBattleEngine engine = Engine();
            for (int f = 0; f < 3; f++) { engine.Tick(NoInput()); }
            Assert.That(engine.World.Projectiles.Count, Is.EqualTo(1), "Projectile 控制器发射弹幕");
            MProjectile proj = engine.World.Projectiles[0];
            Assert.That(proj.ProjId, Is.EqualTo(3));
            FFloat x0 = proj.Pos.X;
            engine.Tick(NoInput());
            Assert.That(proj.Pos.X.Raw, Is.GreaterThan(x0.Raw), "弹幕随速度前移");
        }

        [Test]
        public void NumProj_CountsProjectiles()
        {
            MBattleEngine engine = Engine();
            MChar owner = engine.Chars[0];
            BytecodeExp numproj = new MugenExprCompiler().Compile("numproj");
            Assert.That(numproj.Run(owner).ToI(), Is.EqualTo(0));
            for (int f = 0; f < 3; f++) { engine.Tick(NoInput()); }
            Assert.That(numproj.Run(owner).ToI(), Is.EqualTo(1), "发射后 numproj=1");
        }

        [Test]
        public void HelperProjectile_IsOwnedByRootForNumProjAndHashState()
        {
            const string cns =
                "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
                "[State -1, spawn]\ntype = Helper\ntrigger1 = time = 1\nid = 4\nstateno = 1000\n\n" +
                "[Statedef 1000]\ntype = S\nphysics = N\nanim = 0\n\n" +
                "[State 1000, fire]\ntype = Projectile\ntrigger1 = time = 0\nprojid = 7\nvelocity = 0, 0\n" +
                "offset = 0, 0\nprojremovetime = 20\n";
            MCharData data = MCharLoader.Load(new[] { cns }, cns, null, Air, null, "Owner");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);
            engine.LinkPair();
            engine.StartRound();

            for (int frame = 0; frame < 3; frame++) { engine.Tick(NoInput()); }

            Assert.That(engine.Helpers.Count, Is.EqualTo(1));
            Assert.That(engine.World.Projectiles.Count, Is.EqualTo(1));
            MChar root = engine.Chars[0];
            MChar helper = engine.Helpers[0];
            MProjectile projectile = engine.World.Projectiles[0];
            Assert.That(ReferenceEquals(projectile.Owner, root), Is.True);
            Assert.That(projectile.OwnerId, Is.EqualTo(root.Id));

            BytecodeExp numproj = new MugenExprCompiler().Compile("numproj");
            Assert.That(numproj.Run(root).ToI(), Is.EqualTo(1));
            Assert.That(numproj.Run(helper).ToI(), Is.EqualTo(1));
        }

        [Test]
        public void Projectile_ExpiresAfterRemoveTime()
        {
            MBattleEngine engine = Engine();
            int maxProj = 0;
            for (int f = 0; f < 20; f++)
            {
                engine.Tick(NoInput());
                if (engine.World.Projectiles.Count > maxProj) { maxProj = engine.World.Projectiles.Count; }
            }
            Assert.That(maxProj, Is.EqualTo(1), "曾有一个弹幕");
            Assert.That(engine.World.Projectiles.Count, Is.EqualTo(0), "projremovetime 到期后移除");
        }

        [Test]
        public void Projectile_IsDeterministic()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = Engine();
                for (int f = 0; f < 10; f++) { engine.Tick(NoInput()); }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()));
        }
    }
}
