using Lockstep.Mugen.Char;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Expr;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Expr
{
    /// <summary>
    /// animelem 触发器（用量 3553，最高频缺口）。`animelem = n` = 动画到达元素 n 的「首帧」
    /// （AnimElemNo==n 且 AnimElemTime==0）。修复前 animelem 未特判 → 落到压 0 → `animelem = N` 恒假
    /// → 大量用其计时的 HitDef/取消哑火。同时补 animelemno（之前 OpCode 有但 Char 不消费）。
    /// </summary>
    [TestFixture]
    public sealed class AnimElemTriggerTests
    {
        static MChar CharOnElem(int elemNo, int elemTime)
        {
            return new MChar { AnimElemNo = elemNo, AnimElemTime = elemTime };
        }

        static int Eval(string expr, MChar c)
        {
            return new MugenExprCompiler().Compile(expr).Run(c).ToI();
        }

        [Test]
        public void AnimElemEquals_FirstTickOfElement_True()
        {
            Assert.That(Eval("animelem = 2", CharOnElem(2, 0)), Is.EqualTo(1), "元素2首帧 → 真");
        }

        [Test]
        public void AnimElemEquals_LaterTickOfSameElement_False()
        {
            Assert.That(Eval("animelem = 2", CharOnElem(2, 1)), Is.EqualTo(0), "元素2非首帧 → 假（避免每帧重复触发 HitDef）");
        }

        [Test]
        public void AnimElemEquals_DifferentElement_False()
        {
            Assert.That(Eval("animelem = 2", CharOnElem(3, 0)), Is.EqualTo(0), "不在元素2 → 假");
        }

        [Test]
        public void AnimElemNotEquals_Negates()
        {
            Assert.That(Eval("animelem != 2", CharOnElem(2, 0)), Is.EqualTo(0), "元素2首帧 → != 为假");
            Assert.That(Eval("animelem != 2", CharOnElem(3, 0)), Is.EqualTo(1), "不在元素2 → != 为真");
        }

        [Test]
        public void AnimElemNo_ReturnsCurrentElement()
        {
            Assert.That(Eval("animelemno", CharOnElem(5, 7)), Is.EqualTo(5));
        }

        [Test]
        public void AnimElemNoFunction_UsesRelativeTimeArgument()
        {
            MAnimData anim = new MAnimData
            {
                No = 0,
                LoopStart = 0,
                Frames = new[]
                {
                    new MAnimFrame { Time = 2 },
                    new MAnimFrame { Time = 3 },
                },
            };
            anim.ComputePacing();
            MChar c = new MChar
            {
                AnimNo = 0,
                AnimRunningNo = 0,
                AnimElem = 1,
                AnimElemNo = 2,
                AnimElemTime = 0,
                AnimTable = new System.Collections.Generic.Dictionary<int, MAnimData> { [0] = anim },
            };

            Assert.That(Eval("animelemno(0)", c), Is.EqualTo(2));
            Assert.That(Eval("animelemno(-1)", c), Is.EqualTo(1));
            Assert.That(Eval("animelemno(3)", c), Is.EqualTo(2));
            Assert.That(Eval("animelemno(4)", c), Is.EqualTo(1));
        }

        [Test]
        public void AnimElemSecondArgument_UsesAnimElemTimeComparison()
        {
            MAnimData anim = new MAnimData
            {
                No = 0,
                LoopStart = 0,
                Frames = new[]
                {
                    new MAnimFrame { Time = 4 },
                    new MAnimFrame { Time = 4 },
                    new MAnimFrame { Time = 4 },
                },
            };
            MChar c = new MChar
            {
                AnimNo = 0,
                AnimRunningNo = 0,
                AnimElem = 1,
                AnimElemNo = 2,
                AnimElemTime = 1,
                AnimCurTime = 5,
                AnimTable = new System.Collections.Generic.Dictionary<int, MAnimData> { [0] = anim },
            };

            Assert.That(Eval("animelem = 2, = 1", c), Is.EqualTo(1));
            Assert.That(Eval("animelem = 2, >= 0", c), Is.EqualTo(1));
            Assert.That(Eval("animelem = 3, < 0", c), Is.EqualTo(1));
            Assert.That(Eval("animelem = 2, < 1", c), Is.EqualTo(0));
        }

        [Test]
        public void AnimElem_WithRelationalOperator_FallsBackToElementNumber()
        {
            // animelem >= 3：无 =/!= → 退化为当前元素号交外层比较。
            Assert.That(Eval("animelem >= 3", CharOnElem(4, 2)), Is.EqualTo(1), "元素4 >= 3 → 真");
            Assert.That(Eval("animelem >= 3", CharOnElem(2, 0)), Is.EqualTo(0), "元素2 >= 3 → 假");
        }

        [Test]
        public void AnimElem_DoesNotConsumeOuterLogicalOperator()
        {
            // 确保 animelem = 2 的操作数解析不吞掉外层 ||。元素2首帧 → 真，整体 || 真。
            Assert.That(Eval("animelem = 2 || animelem = 9", CharOnElem(2, 0)), Is.EqualTo(1));
            // 都不满足 → 假。
            Assert.That(Eval("animelem = 2 || animelem = 9", CharOnElem(5, 0)), Is.EqualTo(0));
        }

        [Test]
        public void AnimElemEquals_ExpressionOperand()
        {
            // 操作数可为算术表达式：animelem = 1+1。
            Assert.That(Eval("animelem = 1 + 1", CharOnElem(2, 0)), Is.EqualTo(1));
        }
    }
}
