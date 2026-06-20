using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.State;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class MoveContactTimingTests
    {
        static readonly Dictionary<int, MStateDef> EmptyStates = new Dictionary<int, MStateDef>();

        static FFloat F(int value)
        {
            return FFloat.FromInt(value);
        }

        static MClsnBox Box(int x1, int y1, int x2, int y2)
        {
            return new MClsnBox(F(x1), F(y1), F(x2), F(y2));
        }

        static (MChar attacker, MChar defender) Pair(bool guarding = false)
        {
            MChar attacker = new MChar
            {
                Id = 1,
                Life = 1000,
                LifeMax = 1000,
                Facing = FFloat.One,
                StateType = 1,
                Pos = new FVector3(F(0), F(0), F(0)),
                Clsn1 = new[] { Box(0, -80, 60, 0) },
                HitDef = BasicHitDef(),
            };
            MChar defender = new MChar
            {
                Id = 2,
                Life = 1000,
                LifeMax = 1000,
                Facing = -FFloat.One,
                StateType = 1,
                Pos = new FVector3(F(20), F(0), F(0)),
                Clsn2 = new[] { Box(-10, -80, 10, 0) },
                Guarding = guarding,
            };
            return (attacker, defender);
        }

        static MHitDef BasicHitDef()
        {
            return new MHitDef
            {
                Active = true,
                HitDamage = 20,
                GuardDamage = 0,
                HitHigh = true,
                HitLow = true,
                HitAir = true,
                GuardHigh = true,
                GuardLow = true,
                GuardAir = true,
                GroundHitTime = 12,
                GroundVelX = F(-4),
                GuardHitTime = 10,
                GuardCtrlTime = 8,
                GuardVelX = F(-2),
            };
        }

        [Test]
        public void MoveHit_AdvancesFromFirstContactFrame()
        {
            (MChar attacker, MChar defender) = Pair();

            Assert.That(MHitSystem.TryHit(attacker, defender), Is.True);
            Assert.That(attacker.MoveContactTime, Is.EqualTo(1));
            Assert.That(attacker.MoveContact, Is.EqualTo(1));
            Assert.That(attacker.MoveHit, Is.EqualTo(1));
            Assert.That(attacker.MoveGuarded, Is.EqualTo(0));

            new MStateMachine().RunFrame(attacker, EmptyStates);

            Assert.That(attacker.MoveContactTime, Is.EqualTo(2));
            Assert.That(attacker.MoveContact, Is.EqualTo(2));
            Assert.That(attacker.MoveHit, Is.EqualTo(2));
            Assert.That(attacker.MoveGuarded, Is.EqualTo(0));
        }

        [Test]
        public void MoveGuarded_AdvancesContactWithoutMoveHit()
        {
            (MChar attacker, MChar defender) = Pair(guarding: true);

            Assert.That(MHitSystem.TryHit(attacker, defender), Is.True);
            Assert.That(attacker.MoveContactTime, Is.EqualTo(1));
            Assert.That(attacker.MoveContact, Is.EqualTo(1));
            Assert.That(attacker.MoveHit, Is.EqualTo(0));
            Assert.That(attacker.MoveGuarded, Is.EqualTo(1));

            new MStateMachine().RunFrame(attacker, EmptyStates);

            Assert.That(attacker.MoveContactTime, Is.EqualTo(2));
            Assert.That(attacker.MoveContact, Is.EqualTo(2));
            Assert.That(attacker.MoveHit, Is.EqualTo(0));
            Assert.That(attacker.MoveGuarded, Is.EqualTo(2));
        }

        [Test]
        public void MoveContactTime_DoesNotAdvanceDuringHitpause()
        {
            (MChar attacker, MChar defender) = Pair();

            Assert.That(MHitSystem.TryHit(attacker, defender), Is.True);
            attacker.Hitstop = 1;

            new MStateMachine().RunFrame(attacker, EmptyStates);

            Assert.That(attacker.MoveContactTime, Is.EqualTo(1));
            Assert.That(attacker.MoveContact, Is.EqualTo(1));
            Assert.That(attacker.MoveHit, Is.EqualTo(1));

            new MStateMachine().RunFrame(attacker, EmptyStates);

            Assert.That(attacker.MoveContactTime, Is.EqualTo(2));
            Assert.That(attacker.MoveContact, Is.EqualTo(2));
            Assert.That(attacker.MoveHit, Is.EqualTo(2));
        }
    }
}
