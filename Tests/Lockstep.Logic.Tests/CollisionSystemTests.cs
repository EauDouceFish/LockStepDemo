using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Game;
using Lockstep.Game.Components;
using Lockstep.Game.Data;
using Lockstep.Game.Systems;
using Lockstep.Math;

namespace Lockstep.Tests
{
    /// <summary>T3.3：碰撞检测验收——Clsn1×Clsn2 重叠/不重叠 + 防一招多次命中。</summary>
    [TestFixture]
    public sealed class CollisionSystemTests
    {
        static ClsnBox Box(int x1, int y1, int x2, int y2)
        {
            return new ClsnBox(FFloat.FromInt(x1), FFloat.FromInt(y1), FFloat.FromInt(x2), FFloat.FromInt(y2));
        }

        [Test]
        public void AnyOverlap_DetectsHitAndMiss()
        {
            ClsnBox[] attack = { Box(10, -30, 30, 0) };
            ClsnBox[] hurt = { Box(-10, -30, 10, 0) };

            // 受击方原点 x=20 → 受击框世界 [10,30]，与攻击框 [10,30] 重叠
            Assert.IsTrue(ClsnWorld.AnyOverlap(
                attack, FFloat.Zero, FFloat.Zero, FFloat.One,
                hurt, FFloat.FromInt(20), FFloat.Zero, FFloat.One));

            // 受击方远离 x=200 → 不重叠
            Assert.IsFalse(ClsnWorld.AnyOverlap(
                attack, FFloat.Zero, FFloat.Zero, FFloat.One,
                hurt, FFloat.FromInt(200), FFloat.Zero, FFloat.One));
        }

        [Test]
        public void AnyOverlap_EmptyGroup_NoHit()
        {
            ClsnBox[] attack = { Box(0, 0, 4, 4) };
            Assert.IsFalse(ClsnWorld.AnyOverlap(
                attack, FFloat.Zero, FFloat.Zero, FFloat.One,
                null, FFloat.Zero, FFloat.Zero, FFloat.One));
            Assert.IsFalse(ClsnWorld.AnyOverlap(
                attack, FFloat.Zero, FFloat.Zero, FFloat.One,
                new ClsnBox[0], FFloat.Zero, FFloat.Zero, FFloat.One));
        }

        static CharacterDef BuildCharacter()
        {
            // 单帧动画：同时带攻击框 Clsn1 与受击框 Clsn2（攻方用前者、守方用后者）
            AnimFrame frame = new AnimFrame
            {
                SpriteGroup = 0,
                SpriteImage = 0,
                Duration = 1,
                Clsn1 = new[] { Box(10, -30, 30, 0) },
                Clsn2 = new[] { Box(-10, -30, 10, 0) },
            };
            AnimData anim = new AnimData { Id = 0, Frames = new[] { frame }, LoopStart = 0 };
            return new CharacterDef
            {
                Id = 0,
                Name = "Tester",
                States = new Dictionary<int, StateDef>(),
                Anims = new Dictionary<int, AnimData> { [0] = anim },
                Commands = new CommandData[0],
                Const = new CharConstants(),
            };
        }

        static Entity AddFighter(World world, int posX, bool attacker)
        {
            Entity e = world.CreateEntity();
            e.Add(new CharacterRefC { CharacterId = 0 });
            e.Add(new AnimC { AnimNo = 0, FrameIndex = 0 });
            e.Add(new TransformC { Pos = new FVector3(FFloat.FromInt(posX), FFloat.Zero, FFloat.Zero), FacingX = FFloat.One });
            e.Add(new PendingHitC());
            if (attacker)
            {
                e.Add(new HitDefStateC { Active = true, Current = new HitDef { Damage = 10 } });
            }
            return e;
        }

        World BuildWorld(int defenderX)
        {
            World world = new World();
            world.Init(1);
            world.GameData = new MugenGameData(new Dictionary<int, CharacterDef> { [0] = BuildCharacter() });
            AddFighter(world, 0, attacker: true);     // entity 0 攻击方
            AddFighter(world, defenderX, attacker: false);  // entity 1 受击方
            world.RegisterSystem(new CollisionSystem());
            return world;
        }

        [Test]
        public void Overlapping_RegistersHitOnDefender()
        {
            World world = BuildWorld(defenderX: 20);
            world.Tick();

            PendingHitC pending = world.Entities[1].Get<PendingHitC>();
            Assert.IsTrue(pending.HasHit, "受击框与攻击框重叠应命中");
            Assert.That(pending.AttackerEntityIndex, Is.EqualTo(0));
            Assert.That(pending.Hit.Damage, Is.EqualTo(10));
        }

        [Test]
        public void NonOverlapping_NoHit()
        {
            World world = BuildWorld(defenderX: 200);
            world.Tick();
            Assert.IsFalse(world.Entities[1].Get<PendingHitC>().HasHit);
        }

        [Test]
        public void SameAttack_DoesNotHitTwice()
        {
            World world = BuildWorld(defenderX: 20);

            world.Tick();
            Assert.IsTrue(world.Entities[1].Get<PendingHitC>().HasHit, "第一帧命中");

            world.Tick();
            Assert.IsFalse(world.Entities[1].Get<PendingHitC>().HasHit,
                "同一招(HitTargetsBits 已标记)不应在后续帧重复命中");
        }
    }
}
