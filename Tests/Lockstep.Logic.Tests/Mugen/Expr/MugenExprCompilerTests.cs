using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M2：表达式编译器——CNS 触发条件字符串 → 字节码 → 对 Char 求值（含优先级）。</summary>
    [TestFixture]
    public sealed class MugenExprCompilerTests
    {
        static readonly MugenExprCompiler C = new MugenExprCompiler();

        static BytecodeValue Eval(string expr, MChar c = null)
        {
            return C.Compile(expr).Run(c);
        }

        [Test]
        public void Arithmetic_And_Precedence()
        {
            Assert.That(Eval("(2+3)*4").ToI(), Is.EqualTo(20));
            Assert.That(Eval("2 + 3 * 4").ToI(), Is.EqualTo(14));   // * 先于 +
            Assert.That(Eval("7 / 2").ToI(), Is.EqualTo(3));         // 整数除法
            Assert.That(Eval("2 * 3 ** 2").ToI(), Is.EqualTo(18));   // ** 先于 *
        }

        [Test]
        public void Float_Literal()
        {
            BytecodeValue r = Eval("1.5 + 2.5");
            Assert.That(r.Type, Is.EqualTo(ValueType.Float));
            Assert.That(r.ToF().Raw, Is.EqualTo(FFloat.FromInt(4).Raw));
        }

        [Test]
        public void Comparison_Precedence()
        {
            Assert.IsTrue(Eval("1 + 1 = 2").ToB());     // + 先于 =
            Assert.IsTrue(Eval("5 > 3").ToB());
            Assert.IsFalse(Eval("5 < 3").ToB());
        }

        [Test]
        public void Logical_Precedence()
        {
            // && 紧于 ||：1 || (0 && 0) = 1
            Assert.IsTrue(Eval("1 || 0 && 0").ToB());
            Assert.IsFalse(Eval("0 || 0").ToB());
            Assert.IsTrue(Eval("1 && 1").ToB());
        }

        [Test]
        public void TimeTrigger_EndToEnd()
        {
            MChar c = new MChar { Time = 5 };
            Assert.IsTrue(Eval("time >= 2", c).ToB());
            c.Time = 1;
            Assert.IsFalse(Eval("time >= 2", c).ToB());
        }

        [Test]
        public void CompoundTrigger()
        {
            MChar c = new MChar { Life = 100, Ctrl = true };
            Assert.IsTrue(Eval("life > 0 && ctrl", c).ToB());
            c.Ctrl = false;
            Assert.IsFalse(Eval("life > 0 && ctrl", c).ToB());
        }

        [Test]
        public void IfElse_Function()
        {
            MChar c = new MChar { Time = 5 };
            Assert.That(Eval("ifelse(time > 2, 100, 200)", c).ToI(), Is.EqualTo(100));
            c.Time = 1;
            Assert.That(Eval("ifelse(time > 2, 100, 200)", c).ToI(), Is.EqualTo(200));
        }

        [Test]
        public void Functions_AbsAndVar()
        {
            Assert.That(Eval("abs(0 - 5)").ToI(), Is.EqualTo(5));
            Assert.That(Eval("abs(-7)").ToI(), Is.EqualTo(7));
            MChar c = new MChar();
            c.IntVars[3] = 42;
            Assert.That(Eval("var(3)", c).ToI(), Is.EqualTo(42));
        }

        [Test]
        public void AxisTrigger_PosX()
        {
            MChar c = new MChar { Pos = new FVector3(FFloat.FromInt(7) / FFloat.FromInt(2), FFloat.Zero, FFloat.Zero) };
            BytecodeValue r = Eval("pos x", c);
            Assert.That(r.Type, Is.EqualTo(ValueType.Float));
            Assert.That(r.ToF().Raw, Is.EqualTo((FFloat.FromInt(7) / FFloat.FromInt(2)).Raw));
        }
    }
}
