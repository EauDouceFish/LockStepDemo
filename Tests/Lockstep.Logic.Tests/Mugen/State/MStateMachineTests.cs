using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M4：状态机——用 M2 编译的真实触发条件驱动状态切换 + 同帧重入 + Time + hitstop。</summary>
    [TestFixture]
    public sealed class MStateMachineTests
    {
        static readonly MugenExprCompiler Comp = new MugenExprCompiler();

        static ChangeStateController ChangeState(int target, string trigger, int ctrl = -1)
        {
            return new ChangeStateController
            {
                Triggers = MTriggerSet.Single(Comp.Compile(trigger)),
                Value = Comp.Compile(target.ToString()),
                Ctrl = ctrl,
            };
        }

        static Dictionary<int, MStateDef> StandWalk()
        {
            MStateDef stand = new MStateDef { No = 0, StateType = 1, Ctrl = 1 };
            stand.Controllers.Add(ChangeState(20, "time >= 2"));   // 站立 2 帧后走
            MStateDef walk = new MStateDef { No = 20, StateType = 1 };
            walk.Controllers.Add(ChangeState(0, "time >= 3"));     // 走 3 帧后回站立
            return new Dictionary<int, MStateDef> { [0] = stand, [20] = walk };
        }

        [Test]
        public void Transitions_OnCompiledTriggers()
        {
            MStateMachine sm = new MStateMachine();
            Dictionary<int, MStateDef> states = StandWalk();
            MChar c = new MChar { StateNo = 0, Time = 0 };

            sm.RunFrame(c, states);   // time 0 -> ++1
            Assert.That(c.StateNo, Is.EqualTo(0));
            sm.RunFrame(c, states);   // time 1 -> ++2
            Assert.That(c.StateNo, Is.EqualTo(0));
            sm.RunFrame(c, states);   // time 2 >= 2 -> 切 20, 重入(20 的 time0<3), ++ -> walk time1
            Assert.That(c.StateNo, Is.EqualTo(20));
            Assert.That(c.Time, Is.EqualTo(1));

            sm.RunFrame(c, states);   // walk time1
            sm.RunFrame(c, states);   // walk time2
            sm.RunFrame(c, states);   // walk time3 >=3 -> 回 0
            Assert.That(c.StateNo, Is.EqualTo(0));
        }

        [Test]
        public void Transition_AppliesStatedefHeader()
        {
            MStateMachine sm = new MStateMachine();
            MStateDef a = new MStateDef { No = 0 };
            a.Controllers.Add(ChangeState(100, "1"));   // 恒切到 100
            MStateDef b = new MStateDef { No = 100, StateType = 2, MoveType = 1, Ctrl = 0 };
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = a, [100] = b };
            MChar c = new MChar { StateNo = 0, Ctrl = true };

            sm.RunFrame(c, states);
            Assert.That(c.StateNo, Is.EqualTo(100));
            Assert.That(c.StateType, Is.EqualTo(2), "应用 statedef 的 statetype");
            Assert.That(c.MoveType, Is.EqualTo(1));
            Assert.IsFalse(c.Ctrl, "statedef ctrl=0");
        }

        [Test]
        public void Hitstop_FreezesStateAndTime()
        {
            MStateMachine sm = new MStateMachine();
            Dictionary<int, MStateDef> states = StandWalk();
            MChar c = new MChar { StateNo = 0, Time = 5, Hitstop = 2 };

            sm.RunFrame(c, states);
            Assert.That(c.Hitstop, Is.EqualTo(1), "hitstop 递减");
            Assert.That(c.Time, Is.EqualTo(5), "hitstop 期间 Time 冻结");
            Assert.That(c.StateNo, Is.EqualTo(0), "hitstop 期间不跑状态(虽 time>=2 也不切)");
        }

        [Test]
        public void NullTrigger_AlwaysRuns()
        {
            MStateMachine sm = new MStateMachine();
            ChangeStateController always = new ChangeStateController { Triggers = null, Value = Comp.Compile("50") };
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(always);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0, [50] = new MStateDef { No = 50 } };
            MChar c = new MChar { StateNo = 0 };

            sm.RunFrame(c, states);
            Assert.That(c.StateNo, Is.EqualTo(50), "trigger=null 恒执行");
        }

        // ───────── M4 补全：triggerall + trigger1..n（OR-of-ANDs）─────────

        [Test]
        public void TriggerAll_AndsWithGroups()
        {
            // triggerall: alive；trigger1: time>=2 ；只有都满足才切
            ChangeStateController sc = new ChangeStateController { Value = Comp.Compile("99") };
            sc.Triggers = new MTriggerSet();
            sc.Triggers.TriggerAll.Add(Comp.Compile("alive"));
            sc.Triggers.Groups.Add(new List<BytecodeExp> { Comp.Compile("time >= 2") });

            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(sc);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0, [99] = new MStateDef { No = 99 } };

            MChar dead = new MChar { StateNo = 0, Time = 5, Life = 0 };   // time 够但 !alive
            new MStateMachine().RunFrame(dead, states);
            Assert.That(dead.StateNo, Is.EqualTo(0), "triggerall(alive) 不过则不切");

            MChar live = new MChar { StateNo = 0, Time = 5, Life = 100 };
            new MStateMachine().RunFrame(live, states);
            Assert.That(live.StateNo, Is.EqualTo(99), "triggerall+trigger1 都过则切");
        }

        [Test]
        public void TriggerGroups_AreOred()
        {
            // trigger1: time=1 ; trigger2: time=3 → 组间 OR
            ChangeStateController sc = new ChangeStateController { Value = Comp.Compile("7") };
            sc.Triggers = new MTriggerSet();
            sc.Triggers.Groups.Add(new List<BytecodeExp> { Comp.Compile("time = 1") });
            sc.Triggers.Groups.Add(new List<BytecodeExp> { Comp.Compile("time = 3") });
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(sc);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0, [7] = new MStateDef { No = 7 } };

            MChar c = new MChar { StateNo = 0, Time = 3 };   // 命中 trigger2
            new MStateMachine().RunFrame(c, states);
            Assert.That(c.StateNo, Is.EqualTo(7));
        }

        // ───────── M4 补全：persistent ─────────

        [Test]
        public void Persistent0_RunsOncePerStateEntry()
        {
            // persistent=0 的 VarAdd 风格控制器：进状态只执行一次。用一个计数控制器验证。
            CountController counter = new CountController { Persistent = 0, Triggers = null };
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(counter);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0 };
            MChar c = new MChar { StateNo = 0 };
            MStateMachine sm = new MStateMachine();

            sm.RunFrame(c, states);
            sm.RunFrame(c, states);
            sm.RunFrame(c, states);
            Assert.That(counter.Count, Is.EqualTo(1), "persistent=0 进状态后只执行一次");
        }

        [Test]
        public void Persistent1_RunsEveryFrame()
        {
            CountController counter = new CountController { Persistent = 1, Triggers = null };
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(counter);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0 };
            MChar c = new MChar { StateNo = 0 };
            MStateMachine sm = new MStateMachine();

            sm.RunFrame(c, states);
            sm.RunFrame(c, states);
            sm.RunFrame(c, states);
            Assert.That(counter.Count, Is.EqualTo(3), "persistent=1 每帧执行");
        }

        [Test]
        public void PersistentN_FrameCooldown()
        {
            // persistent=3：执行后冷却 3 帧（按在状态内的帧计），故 7 帧内在第 1/4/7 帧执行 = 3 次。
            CountController counter = new CountController { Persistent = 3, Triggers = null };
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(counter);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0 };
            MChar c = new MChar { StateNo = 0 };
            MStateMachine sm = new MStateMachine();

            for (int frame = 0; frame < 7; frame++)
            {
                sm.RunFrame(c, states);
            }
            Assert.That(counter.Count, Is.EqualTo(3), "persistent=3 在第 1/4/7 帧执行");
        }

        [Test]
        public void PersistentN_CountsFramesNotTriggerHits()
        {
            // 关键差异（对齐 Ikemen）：冷却按"在状态内的帧"计，与 trigger 真值无关。
            // trigger = (time != 1)：第 1 帧(time=1)假，其余真。persistent=2。
            // 帧0(time0,真): 计数器0→-1 开放, trigger真, 执行, 重置2
            // 帧1(time1,假): 计数器2→1 >0 冷却跳过(trigger 都不求值)
            // 帧2(time2,真): 计数器1→0 开放, trigger真, 执行, 重置2
            // 帧3(time3,假): 1 冷却; 帧4: 0 开放执行 → 共 3 次（帧0/2/4）
            CountController counter = new CountController
            {
                Persistent = 2,
                Triggers = MTriggerSet.Single(Comp.Compile("time != 1")),
            };
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(counter);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0 };
            MChar c = new MChar { StateNo = 0 };
            MStateMachine sm = new MStateMachine();

            for (int frame = 0; frame < 5; frame++)
            {
                sm.RunFrame(c, states);
            }
            Assert.That(counter.Count, Is.EqualTo(3), "冷却按帧计而非按 trigger 真次数");
        }

        [Test]
        public void Persistent0_RerunsAfterStateReentry()
        {
            // persistent=0 锁定后只在"重新进入状态"时复位（ClearStatePersist 清该状态计数器）。
            CountController counter = new CountController { Persistent = 0, Triggers = null };
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(counter);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0 };
            MChar c = new MChar { StateNo = 0 };
            MStateMachine sm = new MStateMachine();

            sm.RunFrame(c, states);   // 帧0：counter 执行(1)，锁定
            sm.RunFrame(c, states);   // 帧1：已锁定，跳过
            Assert.That(counter.Count, Is.EqualTo(1), "锁定后不再执行");

            c.PendingStateNo = 0;     // 强制重进状态 0（命中/ChangeState 同效果）
            sm.RunFrame(c, states);   // 帧2：进状态重置计数器 → counter 再次执行(2)
            Assert.That(counter.Count, Is.EqualTo(2), "重进状态后 persistent=0 控制器再次执行一次");
        }

        // ───────── M4 补全：ignorehitpause ─────────

        [Test]
        public void IgnoreHitPause_RunsDuringHitstop()
        {
            CountController normal = new CountController { Persistent = 1, Triggers = null };
            CountController ihp = new CountController { Persistent = 1, Triggers = null, IgnoreHitPause = true };
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(normal);
            s0.Controllers.Add(ihp);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0 };
            MChar c = new MChar { StateNo = 0, Hitstop = 2 };

            new MStateMachine().RunFrame(c, states);
            Assert.That(normal.Count, Is.EqualTo(0), "hitpause 期间普通控制器不跑");
            Assert.That(ihp.Count, Is.EqualTo(1), "ignorehitpause 控制器照跑");
        }

        // ───────── M4 补全：负状态 -1 每帧跑 ─────────

        [Test]
        public void NegativeState_RunsEveryFrame()
        {
            // 状态 -1：time(当前状态时间)=... 用恒真把 c 切到 200；当前状态 0 无控制器
            MStateDef neg1 = new MStateDef { No = -1 };
            neg1.Controllers.Add(new ChangeStateController { Triggers = MTriggerSet.Single(Comp.Compile("stateno = 0")), Value = Comp.Compile("200") });
            MStateDef s0 = new MStateDef { No = 0 };
            MStateDef s200 = new MStateDef { No = 200 };
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [-1] = neg1, [0] = s0, [200] = s200 };
            MChar c = new MChar { StateNo = 0 };

            new MStateMachine().RunFrame(c, states);
            Assert.That(c.StateNo, Is.EqualTo(200), "负状态 -1 每帧跑并触发切换");
        }

        // ───────── M4 补全：SelfState ─────────

        [Test]
        public void SelfState_TransitionsAndClearsFlag()
        {
            SelfStateController self = new SelfStateController { Triggers = null, Value = Comp.Compile("0") };
            MStateDef s10 = new MStateDef { No = 10 };
            s10.Controllers.Add(self);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [10] = s10, [0] = new MStateDef { No = 0 } };
            MChar c = new MChar { StateNo = 10 };

            new MStateMachine().RunFrame(c, states);
            Assert.That(c.StateNo, Is.EqualTo(0), "SelfState 切到自身状态 0");
            Assert.IsFalse(c.PendingIsSelf, "切换应用后标志清除");
        }

        // ───────── M4 补全：common states 回退 ─────────

        [Test]
        public void CommonStates_FallbackLookup()
        {
            // 角色自身无状态 5, common 提供状态 5(切到 0)；角色当前 5 → 走 common
            Dictionary<int, MStateDef> own = new Dictionary<int, MStateDef> { [0] = new MStateDef { No = 0 } };
            MStateDef common5 = new MStateDef { No = 5 };
            common5.Controllers.Add(new ChangeStateController { Triggers = null, Value = Comp.Compile("0") });
            Dictionary<int, MStateDef> common = new Dictionary<int, MStateDef> { [5] = common5 };
            MChar c = new MChar { StateNo = 5 };

            new MStateMachine().RunFrame(c, own, common);
            Assert.That(c.StateNo, Is.EqualTo(0), "自身无状态 5 → 回退 common 状态 5");
        }

        /// <summary>测试用：每次执行计数（模拟 VarAdd 类副作用控制器，验证 persistent/ignorehitpause）。</summary>
        sealed class CountController : MStateController
        {
            public int Count;
            public override bool Run(MChar c) { Count++; return false; }
        }
    }
}
