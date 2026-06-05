using System.Collections.Generic;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Expr
{
    /// <summary>
    /// animelemtime(n)（用量 1360）：自元素 n（1-based）起已播 tick。此前 UnaryFuncs 无映射 → 退化为返回参数值（错）。
    /// 当前元素精确返 AnimElemTime；其他元素按累积起始时间推算（对齐 anim.go AnimElemTime）。
    /// </summary>
    [TestFixture]
    public sealed class AnimElemTimeTriggerTests
    {
        // 3 帧动画，每帧 4 tick（元素起始时间：el1=0, el2=4, el3=8）。
        static MChar CharOnAnim(int currentElem0Based, int elemTime, int curTime)
        {
            MAnimData anim = new MAnimData
            {
                Frames = new[]
                {
                    new MAnimFrame { Time = 4 },
                    new MAnimFrame { Time = 4 },
                    new MAnimFrame { Time = 4 },
                },
            };
            return new MChar
            {
                AnimNo = 0,
                AnimRunningNo = 0,
                AnimTable = new Dictionary<int, MAnimData> { { 0, anim } },
                AnimElem = currentElem0Based,
                AnimElemNo = currentElem0Based + 1,
                AnimElemTime = elemTime,
                AnimCurTime = curTime,
            };
        }

        static int Eval(string expr, MChar c)
        {
            return new MugenExprCompiler().Compile(expr).Run(c).ToI();
        }

        [Test]
        public void CurrentElement_ReturnsExactElemTime()
        {
            // 当前在元素2(0-based 1)，已播 1 tick，总时 5。
            MChar c = CharOnAnim(currentElem0Based: 1, elemTime: 1, curTime: 5);
            Assert.That(Eval("animelemtime(2)", c), Is.EqualTo(1), "当前元素精确返 AnimElemTime");
        }

        [Test]
        public void PastElement_ReturnsTimeSinceItStarted()
        {
            // 元素1 起始于 tick0，当前总时 5 → 自元素1 起已 5 tick。
            MChar c = CharOnAnim(currentElem0Based: 1, elemTime: 1, curTime: 5);
            Assert.That(Eval("animelemtime(1)", c), Is.EqualTo(5));
        }

        [Test]
        public void FutureElement_ReturnsNegative()
        {
            // 元素3 起始于 tick8，当前总时 5 → 尚未到达，返 5-8=-3。
            MChar c = CharOnAnim(currentElem0Based: 1, elemTime: 1, curTime: 5);
            Assert.That(Eval("animelemtime(3)", c), Is.EqualTo(-3));
        }

        [Test]
        public void InComparison_TimesActiveFrames()
        {
            // 典型用法：animelemtime(2) >= 0 表示已进入元素2。
            MChar atEl2 = CharOnAnim(currentElem0Based: 1, elemTime: 0, curTime: 4);
            Assert.That(Eval("animelemtime(2) >= 0", atEl2), Is.EqualTo(1));
            MChar atEl1 = CharOnAnim(currentElem0Based: 0, elemTime: 2, curTime: 2);
            Assert.That(Eval("animelemtime(2) >= 0", atEl1), Is.EqualTo(0), "尚在元素1 → 元素2 未到");
        }

        [Test]
        public void NoAnimTable_ReturnsZero_NoCrash()
        {
            MChar c = new MChar { AnimElemNo = 99 };   // 无 AnimTable
            Assert.That(Eval("animelemtime(2)", c), Is.EqualTo(0));
        }
    }
}
