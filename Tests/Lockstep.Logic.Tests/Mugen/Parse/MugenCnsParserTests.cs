using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M5-b3：CNS 文本 → MStateDef + 实例化控制器 + 编译触发，端到端跑状态机。</summary>
    [TestFixture]
    public sealed class MugenCnsParserTests
    {
        [Test]
        public void ParsesStatedefHeader()
        {
            string cns = "[Statedef 200]\ntype = A\nmovetype = A\nphysics = N\nctrl = 0\nanim = 200\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MStateDef s = states[200];
            Assert.That(s.StateType, Is.EqualTo(4), "type=A → 4");
            Assert.That(s.MoveType, Is.EqualTo(4));
            Assert.That(s.Physics, Is.EqualTo(16), "physics=N → 16");
            // ctrl/anim 现为编译表达式，进入状态时求值（对齐 Ikemen）。
            MChar probe = new MChar();
            Assert.That(s.Ctrl.Run(probe).ToB(), Is.False, "ctrl=0 求值为 false");
            Assert.That(s.Anim.Run(probe).ToI(), Is.EqualTo(200), "anim=200 求值为 200");
        }

        [Test]
        public void ParsesStatedefHeader_FirstNumberBeforeComma()
        {
            string cns =
                "[Statedef 0]\ntype = S\n" +
                "[State 0, stand]\ntype = ChangeAnim\ntrigger1 = 1\nvalue = 0\n" +
                "[Statedef 5150, 0]\ntype = L\n" +
                "[State 5150, dead]\ntype = ChangeAnim\ntrigger1 = !time\nvalue = 5150\n";

            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);

            Assert.That(states.ContainsKey(0), Is.True);
            Assert.That(states.ContainsKey(5150), Is.True, "MUGEN allows labels after a comma; state number is the first integer.");
            Assert.That(states[0].Controllers.Count, Is.EqualTo(1), "state 5150 controllers must not be merged into state 0.");
            Assert.That(states[5150].Controllers.Count, Is.EqualTo(1));
        }

        [Test]
        public void ParsesNegativeStatedefHeader()
        {
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse("[Statedef -1]\n[State -1, cmd]\ntype = Null\ntrigger1 = 1\n");

            Assert.That(states.ContainsKey(-1), Is.True);
        }

        [Test]
        public void StatedefHeader_EvaluatesAnimExpression()
        {
            // 关键回归：anim=40+var(11) 这类表达式必须在进入状态时按角色上下文求值（jump/land 状态用）。
            string cns = "[Statedef 40]\ntype = S\nanim = 40+var(11)\nctrl = 0\n";
            MStateDef s = MugenCnsParser.Parse(cns)[40];
            MChar c = new MChar();                       // var(11)=0 → anim 40
            s.RunInit(c);
            Assert.That(c.AnimNo, Is.EqualTo(40), "anim=40+var(11)，var11=0 → 动画 40");
            Assert.That(c.Ctrl, Is.False, "ctrl=0");
            c.IntVars[11] = 1;                           // var(11)=1 → anim 41（调色板变体）
            s.RunInit(c);
            Assert.That(c.AnimNo, Is.EqualTo(41), "var11=1 → 动画 41");
        }

        [Test]
        public void ParsesChangeStateController_WithTriggers()
        {
            string cns =
                "[Statedef 0]\ntype = S\nctrl = 1\n" +
                "[State 0, walk]\ntype = ChangeState\ntriggerall = alive\ntrigger1 = time >= 2\nvalue = 20\n" +
                "[Statedef 20]\ntype = S\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MStateMachine sm = new MStateMachine();

            MChar c = new MChar { StateNo = 0, Time = 0, Life = 100 };
            sm.RunFrame(c, states);   // time0
            sm.RunFrame(c, states);   // time1
            sm.RunFrame(c, states);   // time2 >=2 → 切 20
            Assert.That(c.StateNo, Is.EqualTo(20));
        }

        [Test]
        public void ParsesVelSetAndPosAdd()
        {
            string cns =
                "[Statedef 100]\ntype = S\n" +
                "[State 100, vel]\ntype = VelSet\ntrigger1 = 1\nx = 2.5\ny = 0 - 3\n" +
                "[State 100, pos]\ntype = PosAdd\ntrigger1 = 1\nx = 5\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MChar c = new MChar { StateNo = 100, Facing = Lockstep.Math.FFloat.One,
                Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(10), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero) };

            new MStateMachine().RunFrame(c, states);
            Assert.That(c.Vel.X.Raw, Is.EqualTo((Lockstep.Math.FFloat.FromInt(5) / Lockstep.Math.FFloat.FromInt(2)).Raw), "vel.x=2.5");
            Assert.That(c.Vel.Y.Raw, Is.EqualTo(Lockstep.Math.FFloat.FromInt(-3).Raw));
            Assert.That(c.Pos.X.Raw, Is.EqualTo(Lockstep.Math.FFloat.FromInt(15).Raw), "posadd 5*facing(+1) → 15");
        }

        [Test]
        public void ParsesVarSet_RoundTrip()
        {
            string cns =
                "[Statedef 0]\n" +
                "[State 0, setvar]\ntype = VarSet\ntrigger1 = 1\nv = 3\nvalue = 42\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MChar c = new MChar { StateNo = 0 };
            new MStateMachine().RunFrame(c, states);
            Assert.That(c.IntVars[3], Is.EqualTo(42));
        }

        [Test]
        public void StatetypeTriggerCompiles_InCns()
        {
            // trigger 用 statetype 字母枚举：在状态 S 时切到 50
            string cns =
                "[Statedef 0]\ntype = S\n" +
                "[State 0, x]\ntype = ChangeState\ntrigger1 = statetype = S\nvalue = 50\n" +
                "[Statedef 50]\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MChar c = new MChar { StateNo = 0, StateType = 1 };
            new MStateMachine().RunFrame(c, states);
            Assert.That(c.StateNo, Is.EqualTo(50));
        }

        [Test]
        public void UnknownControllerType_DegradesToNull()
        {
            string cns =
                "[Statedef 0]\n" +
                "[State 0, snd]\ntype = PlaySnd\ntrigger1 = 1\nvalue = S1,0\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            // 不崩，控制器存在(降级 Null)，状态机能跑
            Assert.That(states[0].Controllers.Count, Is.EqualTo(1));
            MChar c = new MChar { StateNo = 0, Life = 100 };
            Assert.DoesNotThrow(() => new MStateMachine().RunFrame(c, states));
            Assert.That(c.Life, Is.EqualTo(100));
        }
    }
}
