// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go jugglePoints around 5438, canjuggle around 13139, and deduction around 11363.
using Lockstep.Math;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;
using System.Collections.Generic;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class JugglePipelineTests
    {
        static FFloat F(int value)
        {
            return FFloat.FromInt(value);
        }

        static MHitDef Hit(int damage)
        {
            return new MHitDef
            {
                Active = true,
                HitHigh = true,
                HitAir = true,
                HitDamage = damage,
                P1PauseTime = 1,
                P2PauseTime = 1,
                AirHitTime = 10,
                GroundHitTime = 10,
                NumHits = 1,
                Kill = true,
            };
        }

        static MChar Attacker(int id, int juggle)
        {
            return new MChar
            {
                Id = id,
                Life = 1000,
                Constants = new MConstants { Airjuggle = 15 },
                Juggle = juggle,
                HitDef = Hit(10),
                Pos = new FVector3(F(0), F(0), F(0)),
                Clsn1 = new[] { new MClsnBox(F(10), F(-40), F(30), F(0)) },
            };
        }

        static MChar AirTarget()
        {
            return new MChar
            {
                Id = 22,
                Life = 100,
                LifeMax = 1000,
                StateType = 4,
                Ghv = { Fall = true },
                Pos = new FVector3(F(20), F(0), F(0)),
                Clsn2 = new[] { new MClsnBox(F(-10), F(-40), F(10), F(0)) },
            };
        }

        [Test]
        public void AirHit_DeductsJugglePointsAndResetsStateJuggle()
        {
            MChar attacker = Attacker(7, 4);
            MChar target = AirTarget();

            // oracle: char.go:11363 deducts current StateDef juggle from target ghv targetedBy when target is falling.
            bool hit = MHitSystem.TryHit(attacker, target);

            Assert.That(hit, Is.True);
            Assert.That(target.Ghv.GetJuggle(7, 15), Is.EqualTo(11));
            Assert.That(new MugenExprCompiler().Compile("jugglepoints(22)").Run(attacker).ToI(), Is.EqualTo(11));
            Assert.That(attacker.Juggle, Is.EqualTo(0), "Ikemen resets c.juggle after a falling hit");
        }

        [Test]
        public void AirHit_WhenJuggleCostExceedsRemaining_IsRejected()
        {
            MChar attacker = Attacker(7, 4);
            MChar target = AirTarget();
            MHitSystem.TryHit(attacker, target);

            attacker.Targets.Clear();
            attacker.HitDef = Hit(10);
            attacker.Juggle = 12;
            int lifeBefore = target.Life;

            bool hit = MHitSystem.TryHit(attacker, target);

            Assert.That(hit, Is.False);
            Assert.That(target.Life, Is.EqualTo(lifeBefore));
            Assert.That(target.Ghv.GetJuggle(7, 15), Is.EqualTo(11));
        }

        [Test]
        public void NoJuggleCheck_AllowsHitButStillResetsStateJuggle()
        {
            MChar attacker = Attacker(7, 20);
            MChar target = AirTarget();
            target.Ghv.SetJuggle(7, 1);
            attacker.AssertFlags = (int)MAssertFlag.NoJuggleCheck;

            bool hit = MHitSystem.TryHit(attacker, target);

            Assert.That(hit, Is.True);
            Assert.That(attacker.Juggle, Is.EqualTo(0));
            Assert.That(target.Ghv.GetJuggle(7, 15), Is.EqualTo(1), "NoJuggleCheck skips point deduction");
        }

        [Test]
        public void HitDef_ParsesAirJuggle()
        {
            string text =
@"[Statedef 200]
type = S

[State 200, hit]
type = HitDef
trigger1 = 1
attr = S, NP
damage = 10
air.juggle = 6
";

            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(text);
            HitDefController controller = (HitDefController)states[200].Controllers[0];

            Assert.That(controller.Template.AirJuggle, Is.EqualTo(6));
        }
    }
}
