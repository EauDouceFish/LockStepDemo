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
    /// <summary>T3.4：命中结算验收——扣血/硬直/击退/切受击态 + 攻击方 hitstop。</summary>
    [TestFixture]
    public sealed class HitSystemTests
    {
        World BuildWorld(out Entity attacker, out Entity defender)
        {
            World world = new World();
            world.Init(1);
            world.GameData = new MugenGameData(new Dictionary<int, CharacterDef>());

            attacker = world.CreateEntity();
            attacker.Add(new TransformC { Pos = new FVector3(), FacingX = FFloat.One });   // 面向右
            attacker.Add(new MugenStateC { StateNo = 200, MoveType = MoveType.Attack });

            defender = world.CreateEntity();
            defender.Add(new TransformC { Pos = new FVector3(FFloat.FromInt(20), FFloat.Zero, FFloat.Zero), FacingX = FFloat.MinusOne });
            defender.Add(new MugenStateC { StateNo = 0, MoveType = MoveType.Idle });
            defender.Add(new HealthC { HP = 100, MaxHP = 100 });
            defender.Add(new VelocityC());
            defender.Add(new PendingHitC
            {
                HasHit = true,
                AttackerEntityIndex = 0,
                Hit = new HitDef
                {
                    Damage = 15,
                    PauseTimeAttacker = 8,
                    PauseTimeDefender = 10,
                    GroundVelocity = new FVector2(FFloat.FromInt(-4), FFloat.Zero),
                    GroundHitTime = 12,
                },
            });

            world.RegisterSystem(new HitSystem());
            return world;
        }

        [Test]
        public void Hit_AppliesDamageHitstopKnockbackAndGetHitState()
        {
            World world = BuildWorld(out Entity attacker, out Entity defender);
            world.Tick();

            // 扣血
            Assert.That(defender.Get<HealthC>().HP, Is.EqualTo(85));
            // 受击方硬直 + 切受击态
            MugenStateC ds = defender.Get<MugenStateC>();
            Assert.That(ds.Hitstop, Is.EqualTo(10));
            Assert.That(ds.MoveType, Is.EqualTo(MoveType.BeingHit));
            Assert.That(ds.PendingStateNo, Is.EqualTo(HitSystem.GetHitStateNo));
            // 攻击方硬直
            Assert.That(attacker.Get<MugenStateC>().Hitstop, Is.EqualTo(8));
            // 击退按攻击方朝向(+1)镜像：vel.x = -4 * 1 = -4
            Assert.That(defender.Get<VelocityC>().Vel.X.Raw, Is.EqualTo(FFloat.FromInt(-4).Raw));
            // 已结算
            Assert.IsFalse(defender.Get<PendingHitC>().HasHit);
        }

        [Test]
        public void Hit_KnockbackMirrorsByAttackerFacing()
        {
            World world = BuildWorld(out Entity attacker, out Entity defender);
            attacker.Get<TransformC>().FacingX = FFloat.MinusOne;   // 攻击方面向左
            world.Tick();
            // vel.x = -4 * -1 = +4
            Assert.That(defender.Get<VelocityC>().Vel.X.Raw, Is.EqualTo(FFloat.FromInt(4).Raw));
        }

        [Test]
        public void NoPendingHit_NoChange()
        {
            World world = BuildWorld(out Entity attacker, out Entity defender);
            defender.Get<PendingHitC>().HasHit = false;
            world.Tick();
            Assert.That(defender.Get<HealthC>().HP, Is.EqualTo(100), "无待结算命中时不应扣血");
        }
    }
}
