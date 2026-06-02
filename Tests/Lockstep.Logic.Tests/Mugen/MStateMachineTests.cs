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
                Trigger = Comp.Compile(trigger),
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
            ChangeStateController always = new ChangeStateController { Trigger = null, Value = Comp.Compile("50") };
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(always);
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0, [50] = new MStateDef { No = 50 } };
            MChar c = new MChar { StateNo = 0 };

            sm.RunFrame(c, states);
            Assert.That(c.StateNo, Is.EqualTo(50), "trigger=null 恒执行");
        }
    }
}
