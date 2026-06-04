using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Tests;

namespace Lockstep.Tests.Mugen
{
    /// <summary>
    /// R-GHV：受击状态机 5000-5160。验证受击触发器（HitShakeOver/HitOver/HitFall/CanRecover）、
    /// 受击计时逐帧递减、新增 GetHitVar 字段读取，以及"命中→进 5000→hitshake→slide(5001)→recover(0)"
    /// 完整反应周期（用真实标准 common1 数据驱动）。离散对照状态号；oracle 来源：手算 Ikemen char.go 逻辑。
    /// </summary>
    [TestFixture]
    public sealed class GetHitStateMachineTests
    {
        static readonly MugenExprCompiler Comp = new MugenExprCompiler();
        static FFloat F(int v) => FFloat.FromInt(v);

        // ───────── 受击触发器单测（直接拨 ghv）─────────

        [Test]
        public void HitShakeOver_TrueWhenShakeTimeZeroOrLess()
        {
            MChar c = new MChar();
            c.Ghv.HitShakeTime = 3;
            Assert.IsFalse(c.HitShakeOver());
            c.Ghv.HitShakeTime = 0;
            Assert.IsTrue(c.HitShakeOver(), "hitshaketime<=0 即抖动结束（char.go:5342）");
        }

        [Test]
        public void HitOver_TrueWhenHitTimeNegative()
        {
            MChar c = new MChar();
            c.Ghv.HitTime = 0;
            Assert.IsFalse(c.HitOver(), "hittime=0 仍未结束");
            c.Ghv.HitTime = -1;
            Assert.IsTrue(c.HitOver(), "hittime<0 即硬直结束（char.go:5338）");
        }

        [Test]
        public void HitFall_ReflectsGhvFall()
        {
            MChar c = new MChar();
            c.Ghv.Fall = false;
            Assert.IsFalse(Comp.Compile("hitfall").Run(c).ToB());
            c.Ghv.Fall = true;
            Assert.IsTrue(Comp.Compile("hitfall").Run(c).ToB());
        }

        [Test]
        public void CanRecover_RequiresRecoverFlagAndEnoughFallTime()
        {
            MChar c = new MChar();
            c.Ghv.FallRecover = true;
            c.Ghv.FallRecoverTime = 4;
            c.FallTime = 3;
            Assert.IsFalse(c.CanRecover(), "浮空帧不足");
            c.FallTime = 4;
            Assert.IsTrue(c.CanRecover(), "达到 recovertime 可起身（char.go:5165）");
            c.Ghv.FallRecover = false;
            Assert.IsFalse(c.CanRecover(), "fall.recover=0 永不可起身");
        }

        [Test]
        public void Triggers_CompileAndEvaluate_EndToEnd()
        {
            MChar c = new MChar();
            c.Ghv.HitShakeTime = 0;
            c.Ghv.HitTime = -1;
            c.Ghv.Fall = true;
            Assert.IsTrue(Comp.Compile("hitshakeover").Run(c).ToB());
            Assert.IsTrue(Comp.Compile("hitover").Run(c).ToB());
            Assert.IsTrue(Comp.Compile("hitfall").Run(c).ToB());
        }

        [Test]
        public void GetHitVar_NewFields_ReadViaCompiler()
        {
            MChar c = new MChar();
            c.Ghv.GroundType = (int)MHitType.Low;
            c.Ghv.AirType = (int)MHitType.High;
            c.Ghv.YAccel = F(1) / F(2);          // 0.5
            c.Ghv.FallYVel = F(-7) / F(2);        // -3.5
            Assert.That(Comp.Compile("gethitvar(groundtype)").Run(c).ToI(), Is.EqualTo((int)MHitType.Low));
            Assert.That(Comp.Compile("gethitvar(airtype)").Run(c).ToI(), Is.EqualTo((int)MHitType.High));
            Assert.That(Comp.Compile("gethitvar(yaccel)").Run(c).ToF().Raw, Is.EqualTo((F(1) / F(2)).Raw));
            Assert.That(Comp.Compile("gethitvar(fall.yvel)").Run(c).ToF().Raw, Is.EqualTo((F(-7) / F(2)).Raw),
                "点分字段 fall.yvel 不与 fall 标志混淆");
            // fall（标志）与 fall.yvel（速度）须区分
            c.Ghv.Fall = true;
            Assert.IsTrue(Comp.Compile("gethitvar(fall)").Run(c).ToB());
        }

        // ───────── 受击计时逐帧递减（MStateMachine.UpdateGetHitTimers）─────────

        [Test]
        public void Timers_ShakeCountsDownThenHitTime()
        {
            MChar c = new MChar { StateType = 1, MoveType = 2, StateNo = 7777 };   // 受击 movetype=H，状态号不存在→只跑计时
            c.Ghv.HitShakeTime = 2;
            c.Ghv.HitTime = 3;
            MStateMachine sm = new MStateMachine();
            Dictionary<int, MStateDef> empty = new Dictionary<int, MStateDef>();

            sm.RunFrame(c, empty);   // F1: HST 2→1（HST 未到 0，hittime 不减）
            Assert.That(c.Ghv.HitShakeTime, Is.EqualTo(1));
            Assert.That(c.Ghv.HitTime, Is.EqualTo(3));

            sm.RunFrame(c, empty);   // F2: HST 1→0，同帧 hittime 3→2
            Assert.That(c.Ghv.HitShakeTime, Is.EqualTo(0));
            Assert.That(c.Ghv.HitTime, Is.EqualTo(2));

            sm.RunFrame(c, empty);   // F3: HST 已 0，hittime 2→1
            Assert.That(c.Ghv.HitTime, Is.EqualTo(1));
            Assert.IsFalse(c.HitOver());

            sm.RunFrame(c, empty);   // F4: hittime 1→0
            sm.RunFrame(c, empty);   // F5: hittime 0→-1 → HitOver
            Assert.IsTrue(c.HitOver(), "hittime 走到 <0");
        }

        [Test]
        public void Timers_FallTime_IncrementsWhileFalling()
        {
            MChar c = new MChar { StateType = 4, MoveType = 2, StateNo = 7777 };
            c.Ghv.HitShakeTime = 0;
            c.Ghv.HitTime = 100;   // 不让 hittime 干扰
            c.Ghv.Fall = true;
            MStateMachine sm = new MStateMachine();
            Dictionary<int, MStateDef> empty = new Dictionary<int, MStateDef>();
            for (int f = 0; f < 5; f++)
            {
                sm.RunFrame(c, empty);
            }
            Assert.That(c.FallTime, Is.EqualTo(5), "浮空每帧 FallTime++");
        }

        [Test]
        public void Timers_LeavingGetHit_ClearsResidualState()
        {
            MChar c = new MChar { StateType = 1, MoveType = 0, StateNo = 7777 };   // movetype 非 H
            c.Ghv.HitShakeTime = 5;
            c.Ghv.Fall = true;
            c.FallTime = 9;
            new MStateMachine().RunFrame(c, new Dictionary<int, MStateDef>());
            Assert.That(c.Ghv.HitShakeTime, Is.EqualTo(0), "离开受击清抖动");
            Assert.IsFalse(c.Ghv.Fall, "离开受击清 fall 标志");
            Assert.That(c.FallTime, Is.EqualTo(0));
        }

        // ───────── 受击状态路由（MHitSystem）─────────

        static MHitDef GroundHit()
        {
            return new MHitDef
            {
                Active = true, HitHigh = true, HitLow = true, HitDown = true, HitAir = true,
                HitDamage = 80, P1PauseTime = 4, P2PauseTime = 4,
                GroundHitTime = 6, AirHitTime = 8, GroundSlideTime = 3,
                GroundVelX = F(-4), AnimType = MReaction.Medium, GroundType = MHitType.High,
            };
        }

        static (MChar atk, MChar def) Pair(int defStateType)
        {
            MChar atk = new MChar
            {
                Id = 1, Facing = FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(0), F(0), F(0)), Clsn1 = new[] { new MClsnBox(F(10), F(-40), F(30), F(0)) },
            };
            MChar def = new MChar
            {
                Id = 2, Facing = -FFloat.One, StateType = defStateType, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(20), F(0), F(0)), Clsn2 = new[] { new MClsnBox(F(-10), F(-40), F(10), F(0)) },
            };
            return (atk, def);
        }

        [Test]
        public void Routing_StandToCrouchToAir_PicksCorrectGetHitState()
        {
            // S→5000
            (MChar atk1, MChar def1) = Pair(1);
            atk1.HitDef = GroundHit();
            Assert.IsTrue(MHitSystem.TryHit(atk1, def1));
            Assert.That(def1.PendingStateNo, Is.EqualTo(5000), "立受击 → 5000");

            // C→5010
            (MChar atk2, MChar def2) = Pair(2);
            atk2.HitDef = GroundHit();
            Assert.IsTrue(MHitSystem.TryHit(atk2, def2));
            Assert.That(def2.PendingStateNo, Is.EqualTo(5010), "蹲受击 → 5010");

            // A→5020
            (MChar atk3, MChar def3) = Pair(4);
            atk3.HitDef = GroundHit();
            Assert.IsTrue(MHitSystem.TryHit(atk3, def3));
            Assert.That(def3.PendingStateNo, Is.EqualTo(5020), "空中受击 → 5020");
        }

        [Test]
        public void Routing_TripType_GoesToTripState5070()
        {
            (MChar atk, MChar def) = Pair(1);
            atk.HitDef = GroundHit();
            atk.HitDef.GroundType = MHitType.Trip;
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.PendingStateNo, Is.EqualTo(5070), "摔绊类 → 5070");
        }

        [Test]
        public void ApplyHit_PopulatesGetHitVarFully()
        {
            (MChar atk, MChar def) = Pair(1);
            atk.HitDef = GroundHit();
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            MGetHitVar ghv = def.Ghv;
            Assert.That(ghv.HitShakeTime, Is.EqualTo(4), "= P2PauseTime");
            Assert.That(ghv.HitTime, Is.EqualTo(6), "= ground.hittime");
            Assert.That(ghv.SlideTime, Is.EqualTo(3));
            Assert.That(ghv.GroundType, Is.EqualTo((int)MHitType.High));
            Assert.That(ghv.AnimType, Is.EqualTo((int)MReaction.Medium), "派生实际 animtype");
            Assert.That(def.Hitstop, Is.EqualTo(0), "守方不进 hitpause");
            Assert.That(def.MoveType, Is.EqualTo(2), "movetype=H");
            Assert.IsFalse(def.Ctrl);
        }

        // ───────── 核心 acceptance：真实标准 common1 跑完整反应周期 ─────────

        [Test]
        public void FullCycle_StandingHit_RunsThroughReactionBackToStand()
        {
            string commonFile = TestAssets.Common1Cns();
            if (!File.Exists(commonFile))
            {
                Assert.Ignore("common1 素材缺失（../MugenSource/Terrarian），跳过。");
            }
            Dictionary<int, MStateDef> common = MugenCnsParser.Parse(File.ReadAllText(commonFile));
            Assert.That(common.ContainsKey(5000), Is.True, "标准 common1 含立受击 5000");
            Assert.That(common.ContainsKey(5001), Is.True, "含滑行 5001");
            Assert.That(common.ContainsKey(0), Is.True, "含站立 0");

            // 只取非负状态，隔离受击周期（负状态=每帧全局逻辑/命令，非 R-GHV 范畴）
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef>();
            foreach (KeyValuePair<int, MStateDef> kv in common)
            {
                if (kv.Key >= 0)
                {
                    states[kv.Key] = kv.Value;
                }
            }

            (MChar atk, MChar def) = Pair(1);
            def.StateNo = 0;
            def.Ctrl = true;
            atk.HitDef = GroundHit();
            Assert.IsTrue(MHitSystem.TryHit(atk, def), "命中");
            Assert.That(def.PendingStateNo, Is.EqualTo(5000));

            MStateMachine sm = new MStateMachine();
            List<int> seq = new List<int>();
            for (int f = 0; f < 40; f++)
            {
                sm.RunFrame(def, states);
                if (seq.Count == 0 || seq[seq.Count - 1] != def.StateNo)
                {
                    seq.Add(def.StateNo);
                }
            }

            Assert.That(seq, Does.Contain(5000), "进入立受击 5000");
            Assert.That(seq, Does.Contain(5001), "抖动结束后进滑行 5001");
            Assert.That(seq[seq.Count - 1], Is.EqualTo(0), "硬直结束后恢复站立 0");
            Assert.IsTrue(def.Ctrl, "恢复后重获控制权");
            Assert.That(def.MoveType, Is.Not.EqualTo(2), "已脱离受击 movetype");
            Assert.That(def.Life, Is.EqualTo(920), "扣 80 血");
        }
    }
}
