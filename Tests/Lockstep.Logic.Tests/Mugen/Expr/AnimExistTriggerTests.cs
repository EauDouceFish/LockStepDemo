using NUnit.Framework;
using System.Collections.Generic;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Anim;

namespace Lockstep.Tests.Mugen
{
    /// <summary>
    /// R-TRG-SAE：AnimExist(n) / SelfAnimExist(n) trigger 端到端
    /// （CNS 字符串 → 字节码 → 对 MChar 求值）。对齐 Ikemen char.go:5088 animExist /
    /// char.go:5102 selfAnimExist——查角色动画表是否存在编号 n。解锁换标准 common1 的存在性守卫。
    /// </summary>
    [TestFixture]
    public sealed class AnimExistTriggerTests
    {
        static readonly MugenExprCompiler C = new MugenExprCompiler();

        static BytecodeValue Eval(string expr, MChar c)
        {
            return C.Compile(expr).Run(c);
        }

        // 构造只含指定编号的动画表（镜像 BasicControllersTests.AnimTableWith）。
        static Dictionary<int, MAnimData> AnimTableWith(params int[] nos)
        {
            Dictionary<int, MAnimData> table = new Dictionary<int, MAnimData>();
            for (int k = 0; k < nos.Length; k++)
            {
                table[nos[k]] = new MAnimData { No = nos[k], Frames = new MAnimFrame[1] };
            }
            return table;
        }

        [Test]
        public void SelfAnimExist_Existing_IsTrue()
        {
            MChar c = new MChar { AnimTable = AnimTableWith(0, 20, 44) };
            Assert.IsTrue(Eval("selfanimexist(44)", c).ToB(), "44 在表中");
            Assert.IsTrue(Eval("selfanimexist(0)", c).ToB(), "0 在表中");
        }

        [Test]
        public void SelfAnimExist_Missing_IsFalse()
        {
            MChar c = new MChar { AnimTable = AnimTableWith(0, 20) };
            Assert.IsFalse(Eval("selfanimexist(44)", c).ToB(), "44 不在表中");
        }

        [Test]
        public void AnimExist_Existing_IsTrue()
        {
            MChar c = new MChar { AnimTable = AnimTableWith(0, 5, 5000) };
            Assert.IsTrue(Eval("animexist(5000)", c).ToB(), "5000 在表中");
        }

        [Test]
        public void AnimExist_Missing_IsFalse()
        {
            MChar c = new MChar { AnimTable = AnimTableWith(0, 5) };
            Assert.IsFalse(Eval("animexist(5000)", c).ToB(), "5000 不在表中");
        }

        [Test]
        public void AnimExist_ArgIsExpression_Evaluated()
        {
            // 参数是表达式而非字面量：40 + 4 = 44
            MChar c = new MChar { AnimTable = AnimTableWith(44) };
            Assert.IsTrue(Eval("selfanimexist(40 + 4)", c).ToB(), "参数表达式应先求值");
        }

        [Test]
        public void AnimExist_GuardsChangeAnim_CommonPattern()
        {
            // 复刻标准 common1 守卫用法：trigger = SelfAnimExist(44)
            MChar has = new MChar { AnimTable = AnimTableWith(40, 41, 44) };
            MChar lacks = new MChar { AnimTable = AnimTableWith(40, 41) };
            Assert.IsTrue(Eval("selfanimexist(44)", has).ToB());
            Assert.IsFalse(Eval("selfanimexist(44)", lacks).ToB(), "缺 44 时守卫拦截（KFM 浮空 bug 的正解）");
        }
    }
}
