using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen
{
    /// <summary>修复4：守招(guardflag/chip/守方击退/moveguarded) + HitBy/NotHitBy 属性免疫过滤 + 免疫窗口递减。</summary>
    [TestFixture]
    public sealed class GuardAndHitByTests
    {
        static FFloat F(int v) => FFloat.FromInt(v);
        static MClsnBox Box(int x1, int y1, int x2, int y2) => new MClsnBox(F(x1), F(y1), F(x2), F(y2));

        static (MChar atk, MChar def) Pair()
        {
            MChar atk = new MChar
            {
                Id = 1, Facing = FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(0), F(0), F(0)),
                Clsn1 = new[] { Box(10, -40, 30, 0) },
            };
            MChar def = new MChar
            {
                Id = 2, Facing = -FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(20), F(0), F(0)),
                Clsn2 = new[] { Box(-10, -40, 10, 0) },
            };
            return (atk, def);
        }

        static MHitDef GuardableHitDef()
        {
            return new MHitDef
            {
                Active = true,
                Attr = (int)MAttackType.NA,
                HitHigh = true, HitLow = true,
                GuardHigh = true, GuardLow = true, GuardAir = true,
                HitDamage = 80, GuardDamage = 10,
                P1PauseTime = 8, P2PauseTime = 8,
                GroundHitTime = 14,
                GroundVelX = F(-4),
                GuardHitTime = 12, GuardCtrlTime = 9, GuardVelX = F(-2),
            };
        }

        // ───────── 守招 ─────────

        [Test]
        public void Guarding_AppliesGuardChip_NotFullHit()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = GuardableHitDef();
            def.Guarding = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def), "命中检测仍成立(被防也算接触)");
            Assert.That(def.Life, Is.EqualTo(990), "守招只吃 chip 伤害 10");
            Assert.That(def.PendingStateNo, Is.EqualTo(-1), "守招不进受击状态 5000");
            Assert.That(def.Vel.X.Raw, Is.EqualTo(F(-2).Raw), "守招用 guard.velocity 击退");
            Assert.That(def.Ghv.HitTime, Is.EqualTo(12), "GetHitVar 用 guard.hittime");
            Assert.That(def.Ghv.CtrlTime, Is.EqualTo(9), "guard.ctrltime");
            Assert.IsTrue(def.Ghv.Guarded, "ghv.guarded");
            Assert.That(atk.MoveGuarded, Is.EqualTo(1), "攻方 moveguarded");
            Assert.That(atk.MoveHit, Is.EqualTo(0), "守招不算 movehit");
            Assert.That(atk.MoveContact, Is.EqualTo(1), "仍算 movecontact");
        }

        [Test]
        public void Guarding_WrongFlag_FallsThroughToHit()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = GuardableHitDef();
            atk.HitDef.GuardHigh = false;   // 站立不可防
            def.Guarding = true;            // 守方站立(StateType=1)

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(920), "guardflag 不含 H → 防不住，吃满 80 伤");
            Assert.That(def.PendingStateNo, Is.EqualTo(5000), "进受击状态");
        }

        // ───────── HitBy / NotHitBy ─────────

        [Test]
        public void HitBy_BlocksNonMatchingAttr()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = GuardableHitDef();        // Attr = NA
            def.HitByAttr = (int)MAttackType.SA;   // 只允许 SA 命中
            def.HitByTime = 5;
            def.HitByIsNot = false;
            Assert.IsFalse(MHitSystem.TryHit(atk, def), "NA 不在 HitBy(SA) 允许集 → 被挡");
            Assert.That(def.Life, Is.EqualTo(1000));
        }

        [Test]
        public void HitBy_AllowsMatchingAttr()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = GuardableHitDef();        // Attr = NA
            def.HitByAttr = (int)MAttackType.AA;   // AA 含 NA
            def.HitByTime = 5;
            def.HitByIsNot = false;
            Assert.IsTrue(MHitSystem.TryHit(atk, def), "NA ∈ AA → 放行");
        }

        [Test]
        public void NotHitBy_BlocksMatchingAttr()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = GuardableHitDef();        // Attr = NA
            def.HitByAttr = (int)MAttackType.NA;   // NotHitBy NA → NA 被免疫
            def.HitByTime = 5;
            def.HitByIsNot = true;
            Assert.IsFalse(MHitSystem.TryHit(atk, def), "NotHitBy 匹配 → 挡下");
        }

        [Test]
        public void HitByTime_Zero_NoFilter()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = GuardableHitDef();
            def.HitByAttr = (int)MAttackType.SA;   // 不匹配，但窗口已过期
            def.HitByTime = 0;
            Assert.IsTrue(MHitSystem.TryHit(atk, def), "HitByTime=0 → 过滤不生效");
        }

        [Test]
        public void StateMachine_DecrementsHitByTime()
        {
            MChar c = new MChar { StateNo = 0, HitByTime = 3 };
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { { 0, new MStateDef { No = 0 } } };
            MStateMachine sm = new MStateMachine();
            sm.RunFrame(c, states);
            Assert.That(c.HitByTime, Is.EqualTo(2), "每帧递减");
            sm.RunFrame(c, states);
            sm.RunFrame(c, states);
            Assert.That(c.HitByTime, Is.EqualTo(0), "递减到 0 停住");
            sm.RunFrame(c, states);
            Assert.That(c.HitByTime, Is.EqualTo(0), "不降为负");
        }

        [Test]
        public void HitByTime_FrozenDuringHitpause()
        {
            MChar c = new MChar { StateNo = 0, HitByTime = 3, Hitstop = 2 };
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { { 0, new MStateDef { No = 0 } } };
            new MStateMachine().RunFrame(c, states);
            Assert.That(c.HitByTime, Is.EqualTo(3), "hitpause 期间免疫窗口冻结");
            Assert.That(c.Hitstop, Is.EqualTo(1), "hitstop 递减");
        }

        // ───────── CNS 端到端 ─────────

        [Test]
        public void Cns_ParsesGuardFlagAndGuardFields()
        {
            string cns =
                "[Statedef 200]\ntype = S\nmovetype = A\n" +
                "[State 200, hit]\ntype = HitDef\ntrigger1 = 1\n" +
                "attr = S, NA\nhitflag = MAF\nguardflag = MA\ndamage = 80, 10\npausetime = 8, 8\n" +
                "ground.velocity = -4, 0\nground.hittime = 14\n" +
                "guard.velocity = -2\nguard.hittime = 12\nguard.ctrltime = 9\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);

            MChar atk = new MChar { StateNo = 200, StateType = 1 };
            new MStateMachine().RunFrame(atk, states);
            Assert.IsTrue(atk.HitDef.Active);
            Assert.That(atk.HitDef.Attr, Is.EqualTo((int)MAttackType.NA), "attr=S,NA → NA bitmask");
            Assert.IsTrue(atk.HitDef.GuardHigh, "guardflag MA 含 H(M)");
            Assert.IsTrue(atk.HitDef.GuardLow, "M 含 L");
            Assert.IsTrue(atk.HitDef.GuardAir, "guardflag 含 A");
            Assert.That(atk.HitDef.GuardHitTime, Is.EqualTo(12));
            Assert.That(atk.HitDef.GuardCtrlTime, Is.EqualTo(9));
            Assert.That(atk.HitDef.GuardVelX.Raw, Is.EqualTo(F(-2).Raw));
        }

        [Test]
        public void Cns_ParsesHitBy_AppliesFilter()
        {
            string cns =
                "[Statedef 400]\ntype = S\n" +
                "[State 400, immune]\ntype = HitBy\ntrigger1 = 1\nvalue = SCA, AA\ntime = 30\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);

            MChar c = new MChar { StateNo = 400, StateType = 1 };
            new MStateMachine().RunFrame(c, states);
            // 递减发生在控制器执行之前，故 HitBy 控制器本帧设的 30 不会被同帧扣掉
            Assert.That(c.HitByTime, Is.EqualTo(30), "HitBy 控制器设 time=30");
            Assert.That(c.HitByAttr, Is.EqualTo((int)MAttackType.AA), "value SCA,AA → AA");
            Assert.IsFalse(c.HitByIsNot, "HitBy 非 NotHitBy");
        }

        [Test]
        public void Cns_ParsesNotHitBy()
        {
            string cns =
                "[Statedef 401]\ntype = S\n" +
                "[State 401, immune]\ntype = NotHitBy\ntrigger1 = 1\nvalue = S, NA\ntime = 10\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);

            MChar c = new MChar { StateNo = 401, StateType = 1 };
            new MStateMachine().RunFrame(c, states);
            Assert.IsTrue(c.HitByIsNot, "NotHitBy → IsNot");
            Assert.That(c.HitByAttr, Is.EqualTo((int)MAttackType.NA));
        }
    }
}
