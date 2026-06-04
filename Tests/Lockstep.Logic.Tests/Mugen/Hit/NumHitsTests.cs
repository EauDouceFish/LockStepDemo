using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;

namespace Lockstep.Tests.Mugen
{
    /// <summary>
    /// R-HITDEF chunk-3 Part A：numhits 连击计数。
    /// 命中 → 攻方 HitCount += numhits（char.go:12189）、守方 ReceivedHits += numhits（char.go:11170）；
    /// 被防 → 攻方 GuardCount += numhits（char.go:12191）；脱离受击态 ReceivedHits/GuardCount 清零（char.go:11807-11809）。
    /// </summary>
    [TestFixture]
    public sealed class NumHitsTests
    {
        static FFloat F(int v) => FFloat.FromInt(v);
        static MClsnBox Box(int x1, int y1, int x2, int y2) => new MClsnBox(F(x1), F(y1), F(x2), F(y2));

        static (MChar atk, MChar def) Pair()
        {
            MChar atk = new MChar
            {
                Id = 1, Facing = FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(0), F(0), F(0)), Clsn1 = new[] { Box(10, -40, 30, 0) },
            };
            MChar def = new MChar
            {
                Id = 2, Facing = -FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(20), F(0), F(0)), Clsn2 = new[] { Box(-10, -40, 10, 0) },
            };
            return (atk, def);
        }

        static MHitDef Hit(int damage, int numhits)
        {
            return new MHitDef
            {
                HitHigh = true, HitLow = true, HitAir = true,
                HitDamage = damage, GuardDamage = 5,
                P1PauseTime = 8, P2PauseTime = 8, GroundHitTime = 14,
                GroundVelX = F(-4), AnimType = MReaction.Medium,
                NumHits = numhits, Active = true,
            };
        }

        [Test]
        public void Hit_AddsNumHitsToCounters()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = Hit(50, 3);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(atk.HitCount, Is.EqualTo(3), "攻方 HitCount += numhits");
            Assert.That(atk.UniqHitCount, Is.EqualTo(1), "去重命中数仍 +1");
            Assert.That(def.ReceivedHits, Is.EqualTo(3), "守方 ReceivedHits += numhits");
        }

        [Test]
        public void DefaultNumHits_IsOne()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = Hit(50, 1);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(atk.HitCount, Is.EqualTo(1));
            Assert.That(def.ReceivedHits, Is.EqualTo(1));
        }

        [Test]
        public void Guard_AddsNumHitsToGuardCount()
        {
            (MChar atk, MChar def) = Pair();
            def.Guarding = true;
            atk.HitDef = Hit(50, 2);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(atk.GuardCount, Is.EqualTo(2), "被防 GuardCount += numhits");
            Assert.That(atk.HitCount, Is.EqualTo(0), "被防不计 HitCount");
            Assert.That(def.ReceivedHits, Is.EqualTo(0), "被防不计连段");
        }

        [Test]
        public void ReceivedHits_ResetsWhenLeavingHitState()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = Hit(50, 2);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.ReceivedHits, Is.EqualTo(2));

            // 脱离受击态（movetype 非 H）→ UpdateGetHitTimers else 分支清零
            def.MoveType = 0;
            def.PendingStateNo = -1;
            new MStateMachine().RunFrame(def, new Dictionary<int, MStateDef>(), new Dictionary<int, MStateDef>());
            Assert.That(def.ReceivedHits, Is.EqualTo(0), "脱离受击 → ReceivedHits 清零");
        }

        [Test]
        public void Cns_ParsesNumHits()
        {
            string cns =
                "[Statedef 200]\ntype = S\nmovetype = A\n" +
                "[State 200, hit]\ntype = HitDef\ntrigger1 = 1\n" +
                "attr = S, NA\ndamage = 50\nnumhits = 4\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MChar atk = new MChar { StateNo = 200, StateType = 1 };
            new MStateMachine().RunFrame(atk, states);
            Assert.That(atk.HitDef.NumHits, Is.EqualTo(4));
        }
    }
}
