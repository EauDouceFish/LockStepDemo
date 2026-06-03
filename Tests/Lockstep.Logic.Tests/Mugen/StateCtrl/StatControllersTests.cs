using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M5 第二批：Life/Power/Turn/AssertSpecial 控制器 + Ikemen clamp/kill 语义。</summary>
    [TestFixture]
    public sealed class StatControllersTests
    {
        static readonly MugenExprCompiler C = new MugenExprCompiler();
        static BytecodeExp E(string expr) => C.Compile(expr);

        [Test]
        public void LifeAdd_Damage_Clamps()
        {
            MChar c = new MChar { Life = 100, LifeMax = 1000 };
            new LifeAddController { Value = E("0 - 30") }.Run(c);
            Assert.That(c.Life, Is.EqualTo(70));
            // 超量伤害夹到 0（kill 默认 true）
            new LifeAddController { Value = E("0 - 999") }.Run(c);
            Assert.That(c.Life, Is.EqualTo(0));
        }

        [Test]
        public void LifeAdd_NoKill_FloorsAtOne()
        {
            MChar c = new MChar { Life = 50, LifeMax = 1000 };
            new LifeAddController { Value = E("0 - 999"), Kill = false }.Run(c);
            Assert.That(c.Life, Is.EqualTo(1), "kill=false 时致死伤害保命到 1");
        }

        [Test]
        public void LifeAdd_Heal_ClampsToMax()
        {
            MChar c = new MChar { Life = 900, LifeMax = 1000 };
            new LifeAddController { Value = E("500") }.Run(c);
            Assert.That(c.Life, Is.EqualTo(1000), "回血夹到 LifeMax");
        }

        [Test]
        public void LifeSet_Clamps()
        {
            MChar c = new MChar { Life = 100, LifeMax = 1000 };
            new LifeSetController { Value = E("1500") }.Run(c);
            Assert.That(c.Life, Is.EqualTo(1000));
            new LifeSetController { Value = E("0 - 5") }.Run(c);
            Assert.That(c.Life, Is.EqualTo(0));
        }

        [Test]
        public void PowerAddAndSet_Clamp()
        {
            MChar c = new MChar { Power = 100, PowerMax = 3000 };
            new PowerAddController { Value = E("5000") }.Run(c);
            Assert.That(c.Power, Is.EqualTo(3000), "PowerAdd 夹到 PowerMax");
            new PowerSetController { Value = E("0 - 1") }.Run(c);
            Assert.That(c.Power, Is.EqualTo(0), "PowerSet 夹到 0");
        }

        [Test]
        public void Turn_FlipsFacing()
        {
            MChar c = new MChar { Facing = Lockstep.Math.FFloat.One };
            new TurnController().Run(c);
            Assert.That(c.Facing.Raw, Is.EqualTo((-Lockstep.Math.FFloat.One).Raw));
            new TurnController().Run(c);
            Assert.That(c.Facing.Raw, Is.EqualTo(Lockstep.Math.FFloat.One.Raw));
        }

        [Test]
        public void AssertSpecial_SetsFlag_ClearedNextFrame()
        {
            // AssertSpecial 断言 Intro；状态机帧首清空，须每帧重断言才保持
            MStateDef s0 = new MStateDef { No = 0 };
            s0.Controllers.Add(new AssertSpecialController { Triggers = null, Flags = (int)MAssertFlag.Intro });
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef> { [0] = s0 };
            MChar c = new MChar { StateNo = 0 };
            MStateMachine sm = new MStateMachine();

            sm.RunFrame(c, states);
            Assert.That(c.AssertFlags & (int)MAssertFlag.Intro, Is.Not.EqualTo(0), "本帧断言生效");

            // 换到无断言的状态后，下一帧帧首清空
            c.StateNo = 9;
            states[9] = new MStateDef { No = 9 };
            sm.RunFrame(c, states);
            Assert.That(c.AssertFlags & (int)MAssertFlag.Intro, Is.EqualTo(0), "未重断言则清空");
        }
    }
}
