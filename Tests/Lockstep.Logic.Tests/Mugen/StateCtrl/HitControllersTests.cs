using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class HitControllersTests
    {
        static readonly MugenExprCompiler Compiler = new MugenExprCompiler();

        static BytecodeExp Expression(string text)
        {
            return Compiler.Compile(text);
        }

        static FFloat Fixed(int value)
        {
            return FFloat.FromInt(value);
        }

        [Test]
        public void HitVelSet_CopiesSelectedGetHitVelocity()
        {
            MChar character = new MChar
            {
                Vel = new FVector3(Fixed(9), Fixed(8), Fixed(7)),
            };
            character.Ghv.XVel = Fixed(-4);
            character.Ghv.YVel = Fixed(5);
            character.Ghv.ZVel = Fixed(6);

            new HitVelSetController
            {
                X = Expression("1"),
                Y = Expression("0"),
                Z = Expression("1"),
            }.Run(character);

            Assert.That(character.Vel.X.Raw, Is.EqualTo(Fixed(-4).Raw));
            Assert.That(character.Vel.Y.Raw, Is.EqualTo(Fixed(8).Raw));
            Assert.That(character.Vel.Z.Raw, Is.EqualTo(Fixed(6).Raw));
        }

        [Test]
        public void VelMul_MultipliesOnlyProvidedAxes()
        {
            MChar character = new MChar
            {
                Vel = new FVector3(Fixed(4), Fixed(-6), Fixed(3)),
            };

            new VelMulController
            {
                X = Expression("2"),
                Z = Expression("0 - 1"),
            }.Run(character);

            Assert.That(character.Vel.X.Raw, Is.EqualTo(Fixed(8).Raw));
            Assert.That(character.Vel.Y.Raw, Is.EqualTo(Fixed(-6).Raw));
            Assert.That(character.Vel.Z.Raw, Is.EqualTo(Fixed(-3).Raw));
        }

        [Test]
        public void HitAdd_UpdatesAttackerAndMostRecentTargetHitCount()
        {
            MChar firstTarget = new MChar();
            MChar recentTarget = new MChar();
            MChar attacker = new MChar
            {
                HitCount = 1,
                UniqHitCount = 1,
                Targets = new List<MChar> { firstTarget, recentTarget },
            };

            new HitAddController { Value = Expression("2") }.Run(attacker);

            Assert.That(attacker.HitCount, Is.EqualTo(3));
            Assert.That(attacker.UniqHitCount, Is.EqualTo(3));
            Assert.That(firstTarget.Ghv.HitCount, Is.EqualTo(0));
            Assert.That(recentTarget.Ghv.HitCount, Is.EqualTo(2));
        }

        [Test]
        public void HitFallSet_UpdatesFallFlagAndVelocityProxy()
        {
            MChar character = new MChar();

            new HitFallSetController
            {
                Value = Expression("1"),
                XVelocity = Expression("0 - 3"),
                YVelocity = Expression("7"),
            }.Run(character);

            Assert.IsTrue(character.Ghv.Fall);
            Assert.That(character.Ghv.XVel.Raw, Is.EqualTo(Fixed(-3).Raw));
            Assert.That(character.Ghv.YVel.Raw, Is.EqualTo(Fixed(7).Raw));
            Assert.That(character.Ghv.ZVel.Raw, Is.EqualTo(FFloat.Zero.Raw));
        }

        [Test]
        public void HitFallVel_AppliesOnlyWhenMoveTypeHit()
        {
            MChar idleCharacter = new MChar
            {
                MoveType = 1,
                Vel = new FVector3(Fixed(1), Fixed(2), Fixed(3)),
            };
            idleCharacter.Ghv.XVel = Fixed(9);
            new HitFallVelController().Run(idleCharacter);
            Assert.That(idleCharacter.Vel.X.Raw, Is.EqualTo(Fixed(1).Raw));

            MChar hitCharacter = new MChar { MoveType = 2 };
            hitCharacter.Ghv.XVel = Fixed(-5);
            hitCharacter.Ghv.YVel = Fixed(6);
            hitCharacter.Ghv.ZVel = Fixed(1);
            new HitFallVelController().Run(hitCharacter);

            Assert.That(hitCharacter.Vel.X.Raw, Is.EqualTo(Fixed(-5).Raw));
            Assert.That(hitCharacter.Vel.Y.Raw, Is.EqualTo(Fixed(6).Raw));
            Assert.That(hitCharacter.Vel.Z.Raw, Is.EqualTo(Fixed(1).Raw));
        }

        [Test]
        public void Gravity_AddsYaccel()
        {
            MChar character = new MChar
            {
                Constants = new MConstants { Yaccel = Fixed(2) },
                Vel = new FVector3(Fixed(0), Fixed(3), Fixed(0)),
            };

            new GravityController().Run(character);

            Assert.That(character.Vel.Y.Raw, Is.EqualTo(Fixed(5).Raw));
        }

        [Test]
        public void CnsParser_BuildsHitControllersFromIkemenNames()
        {
            string text = @"
[Statedef 5000]
[State 5000, hit velocity]
type = HitVelSet
trigger1 = 1
x = 1
y = 1

[State 5000, scale velocity]
type = VelMul
trigger1 = 1
x = 2

[State 5000, gravity]
type = Gravity
trigger1 = 1
";

            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(text);

            Assert.That(states[5000].Controllers[0], Is.TypeOf<HitVelSetController>());
            Assert.That(states[5000].Controllers[1], Is.TypeOf<VelMulController>());
            Assert.That(states[5000].Controllers[2], Is.TypeOf<GravityController>());
        }
    }
}
