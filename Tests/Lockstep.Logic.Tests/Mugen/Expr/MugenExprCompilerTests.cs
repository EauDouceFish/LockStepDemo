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

        // ───────── M2 补全：statetype/movetype 字母枚举比较 ─────────

        [Test]
        public void StateType_LetterCompare()
        {
            MChar c = new MChar { StateType = 1 };   // S
            Assert.IsTrue(Eval("statetype = S", c).ToB());
            Assert.IsFalse(Eval("statetype = A", c).ToB());
            Assert.IsTrue(Eval("statetype != A", c).ToB());
        }

        [Test]
        public void StateType_LetterList_IsOr()
        {
            MChar crouch = new MChar { StateType = 2 };   // C
            Assert.IsTrue(Eval("statetype = S, C", crouch).ToB(), "C ∈ {S,C}");
            MChar air = new MChar { StateType = 4 };       // A
            Assert.IsFalse(Eval("statetype = S, C", air).ToB(), "A ∉ {S,C}");
        }

        [Test]
        public void MoveType_LetterCompare()
        {
            MChar attacking = new MChar { MoveType = 4 };   // A
            Assert.IsTrue(Eval("movetype = A", attacking).ToB());
            Assert.IsFalse(Eval("movetype = I", attacking).ToB());
        }

        [Test]
        public void Physics_LetterCompare()
        {
            MChar noPhysics = new MChar { Physics = 16 };   // N
            Assert.IsTrue(Eval("physics = N", noPhysics).ToB());
            Assert.IsFalse(Eval("physics = S", noPhysics).ToB());
            Assert.IsTrue(Eval("physics != S", noPhysics).ToB());
        }

        // ───────── M2 补全：=[a,b] 区间语法 ─────────

        [Test]
        public void Range_Syntax_InclusiveExclusive()
        {
            MChar c = new MChar { Time = 3 };
            Assert.IsTrue(Eval("time = [2,5]", c).ToB(), "3 ∈ [2,5]");
            Assert.IsFalse(Eval("time = [4,5]", c).ToB(), "3 ∉ [4,5]");
            Assert.IsFalse(Eval("time = (3,5]", c).ToB(), "3 ∉ (3,5]（左开）");
            Assert.IsTrue(Eval("time != [4,5]", c).ToB(), "!= 区间取反");
        }

        // ───────── M2 补全：redirect 编译（p2/root/parent）─────────

        [Test]
        public void Redirect_P2_And_Root()
        {
            MChar opp = new MChar { Life = 250 };
            MChar root = new MChar { Power = 60 };
            MChar c = new MChar { Life = 1000, P2 = opp, Root = root };
            Assert.That(Eval("p2, life", c).ToI(), Is.EqualTo(250));
            Assert.That(Eval("root, power", c).ToI(), Is.EqualTo(60));
        }

        [Test]
        public void Redirect_BindsSingleValue_ThenOuterOp()
        {
            MChar opp = new MChar { Life = 200, Power = 50 };
            MChar c = new MChar { Life = 1000, P2 = opp };
            Assert.That(Eval("p2, (life + power)", c).ToI(), Is.EqualTo(250), "括号→整体重定向");
            Assert.That(Eval("p2, life + 5", c).ToI(), Is.EqualTo(205), "(p2,life)+5：redirect 只绑单值");
        }

        // ───────── 修复5-2：enemy / enemynear redirect ─────────

        [Test]
        public void Redirect_Enemy_DefaultIndex_ToP2()
        {
            MChar opp = new MChar { Life = 333 };
            MChar c = new MChar { Life = 1000, P2 = opp };
            Assert.That(Eval("enemy, life", c).ToI(), Is.EqualTo(333), "省略索引 enemy = P2");
            Assert.That(Eval("enemy(0), life", c).ToI(), Is.EqualTo(333), "enemy(0) = P2");
            Assert.That(Eval("enemynear(0), life", c).ToI(), Is.EqualTo(333), "1v1 enemynear(0) = P2");
        }

        [Test]
        public void Redirect_Enemy_OutOfRangeIndex_Undefined()
        {
            MChar opp = new MChar { Life = 333 };
            MChar c = new MChar { Life = 1000, P2 = opp };
            // 1v1 中只有索引 0 对应敌人；enemy(1) 无对应 → 整块重定向失败，回退默认（容错 0/false）
            Assert.That(Eval("enemy(1), life", c).IsUndefined(), Is.True, "越界索引 → Undefined");
        }

        [Test]
        public void Redirect_Enemy_BindsSingleValue()
        {
            MChar opp = new MChar { Life = 200, Power = 50 };
            MChar c = new MChar { Life = 1000, P2 = opp };
            Assert.That(Eval("enemy, (life + power)", c).ToI(), Is.EqualTo(250), "括号→整体重定向到 enemy");
            Assert.That(Eval("enemy, life + 5", c).ToI(), Is.EqualTo(205), "(enemy,life)+5：只绑单值");
        }
    }
}
