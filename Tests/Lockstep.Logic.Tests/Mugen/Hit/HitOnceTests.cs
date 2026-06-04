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
    /// R-HITDEF chunk-3 Part B：hitonce。
    /// hitonce>0 命中/被防（接触）后立即停用本 HitDef（只命中一个目标，char.go:11283/12172）；
    /// 默认值：投技(attr 含 throw 类)→1，否则 0（char.go:848）。
    /// </summary>
    [TestFixture]
    public sealed class HitOnceTests
    {
        static FFloat F(int v) => FFloat.FromInt(v);
        static MClsnBox Box(int x1, int y1, int x2, int y2) => new MClsnBox(F(x1), F(y1), F(x2), F(y2));

        static MChar Attacker()
        {
            return new MChar
            {
                Id = 1, Facing = FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(0), F(0), F(0)), Clsn1 = new[] { Box(0, -80, 60, 0) },
            };
        }

        static MChar Defender(int id, int x)
        {
            return new MChar
            {
                Id = id, Facing = -FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(x), F(0), F(0)), Clsn2 = new[] { Box(-10, -80, 10, 0) },
            };
        }

        static MHitDef Hit(int hitonce)
        {
            return new MHitDef
            {
                HitHigh = true, HitLow = true, HitAir = true,
                HitDamage = 50, P1PauseTime = 8, P2PauseTime = 8, GroundHitTime = 14,
                GroundVelX = F(-4), AnimType = MReaction.Medium,
                HitOnce = hitonce, Active = true,
            };
        }

        [Test]
        public void HitOnce_DeactivatesAfterFirstTarget()
        {
            MChar atk = Attacker();
            MChar def1 = Defender(2, 20);
            MChar def2 = Defender(3, 40);
            atk.HitDef = Hit(1);

            Assert.IsTrue(MHitSystem.TryHit(atk, def1), "命中第一个目标");
            Assert.IsFalse(atk.HitDef.Active, "hitonce：命中后停用");
            Assert.IsFalse(MHitSystem.TryHit(atk, def2), "第二个目标不再被命中");
            Assert.That(def2.Life, Is.EqualTo(1000), "第二目标未掉血");
        }

        [Test]
        public void NotHitOnce_CanHitMultipleTargets()
        {
            MChar atk = Attacker();
            MChar def1 = Defender(2, 20);
            MChar def2 = Defender(3, 40);
            atk.HitDef = Hit(0);

            Assert.IsTrue(MHitSystem.TryHit(atk, def1), "命中第一个目标");
            Assert.IsTrue(atk.HitDef.Active, "非 hitonce：仍激活");
            Assert.IsTrue(MHitSystem.TryHit(atk, def2), "可命中第二个目标");
            Assert.That(def2.Life, Is.EqualTo(950), "第二目标也掉血");
        }

        [Test]
        public void HitOnce_DeactivatesOnGuardContact()
        {
            MChar atk = Attacker();
            MChar def1 = Defender(2, 20);
            MChar def2 = Defender(3, 40);
            def1.Guarding = true;
            atk.HitDef = Hit(1);

            Assert.IsTrue(MHitSystem.TryHit(atk, def1), "被第一个目标防御（接触）");
            Assert.IsFalse(atk.HitDef.Active, "hitonce：被防接触后同样停用");
            Assert.IsFalse(MHitSystem.TryHit(atk, def2), "第二目标不再被命中");
        }

        [Test]
        public void Cns_ThrowAttr_DefaultsHitOnceTrue()
        {
            MHitDef hd = ParseHitDef("attr = S, NT\ndamage = 0\n");
            Assert.That(hd.HitOnce, Is.EqualTo(1), "投技(NT) 默认 hitonce=1");
        }

        [Test]
        public void Cns_NormalAttr_DefaultsHitOnceFalse()
        {
            MHitDef hd = ParseHitDef("attr = S, NA\ndamage = 50\n");
            Assert.That(hd.HitOnce, Is.EqualTo(0), "普通攻击 默认 hitonce=0");
        }

        [Test]
        public void Cns_ExplicitHitOnce_Overrides()
        {
            MHitDef hd = ParseHitDef("attr = S, NA\ndamage = 50\nhitonce = 1\n");
            Assert.That(hd.HitOnce, Is.EqualTo(1), "显式 hitonce=1 覆盖默认");
        }

        static MHitDef ParseHitDef(string extra)
        {
            string cns =
                "[Statedef 200]\ntype = S\nmovetype = A\n" +
                "[State 200, hit]\ntype = HitDef\ntrigger1 = 1\n" + extra;
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MChar atk = new MChar { StateNo = 200, StateType = 1 };
            new MStateMachine().RunFrame(atk, states);
            return atk.HitDef;
        }
    }
}
