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
            Assert.That(s.Ctrl, Is.EqualTo(0));
            Assert.That(s.Anim, Is.EqualTo(200));
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
