// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go hit damage accumulation around 11063/11078 and damage commit around 11743.
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Math;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class DamagePipelineTests
    {
        static MChar Attacker(int damage)
        {
            return new MChar
            {
                Life = 1000,
                Pos = new FVector3(F(0), F(0), F(0)),
                Clsn1 = new[] { new MClsnBox(F(10), F(-40), F(30), F(0)) },
                PowerMax = 3000,
                HitDef = new MHitDef
                {
                    Active = true,
                    HitHigh = true,
                    HitDamage = damage,
                    P1PauseTime = 1,
                    P2PauseTime = 1,
                    GroundHitTime = 5,
                    Kill = true,
                    NumHits = 1,
                },
            };
        }

        static FFloat F(int value)
        {
            return FFloat.FromInt(value);
        }

        [Test]
        public void DeferredHitDamage_AccumulatesAndFlushesAfterAllHits()
        {
            MChar first = Attacker(30);
            MChar second = Attacker(40);
            MChar defender = new MChar
            {
                Life = 100,
                LifeMax = 1000,
                StateType = 1,
                Pos = new FVector3(F(20), F(0), F(0)),
                Clsn2 = new[] { new MClsnBox(F(-10), F(-40), F(10), F(0)) },
            };

            // oracle: char.go accumulates ghv.damage during hit resolution and applies lifeAdd once in the defender tick.
            MHitSystem.TryHit(first, defender, deferDamage: true);
            MHitSystem.TryHit(second, defender, deferDamage: true);

            Assert.That(defender.Life, Is.EqualTo(100));
            Assert.That(defender.PendingLifeDamage, Is.EqualTo(70));

            MHitSystem.ApplyPendingDamage(defender);

            Assert.That(defender.Life, Is.EqualTo(30));
            Assert.That(defender.PendingLifeDamage, Is.EqualTo(0));
        }
    }
}
