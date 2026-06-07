using NUnit.Framework;
using Lockstep.Mugen.Command;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M6：命令解析 + 匹配（QCF/蓄力/释放/朝向相对/$4way/greater）。</summary>
    [TestFixture]
    public sealed class MCommandMatcherTests
    {
        static MCommandBuffer Buf(params MInput[] frames)
        {
            MCommandBuffer b = new MCommandBuffer(60);
            for (int i = 0; i < frames.Length; i++)
            {
                b.Push(frames[i]);
            }
            return b;
        }

        const MInput R = MInput.Right, L = MInput.Left, D = MInput.Down, U = MInput.Up, A = MInput.A;

        [Test]
        public void Qcf_Punch_FacingRight()
        {
            // "D, DF, F, a" 面向右：D → DF → F → a
            MCommandDef cmd = MCommandParser.Parse("qcf", "D, DF, F, a");
            MCommandBuffer b = Buf(D, D | R, R, R | A);
            Assert.IsTrue(MCommandMatcher.Matches(cmd, b, true));
        }

        [Test]
        public void Qcf_FailsWithoutSequence()
        {
            MCommandDef cmd = MCommandParser.Parse("qcf", "D, DF, F, a");
            // 缺中间 DF
            MCommandBuffer b = Buf(D, R, R | A);
            Assert.IsFalse(MCommandMatcher.Matches(cmd, b, true));
        }

        [Test]
        public void Facing_FlipsForwardBack()
        {
            // "F, a"：面向左时 F = Left
            MCommandDef cmd = MCommandParser.Parse("fa", "F, a");
            MCommandBuffer b = Buf(L, L | A);
            Assert.IsTrue(MCommandMatcher.Matches(cmd, b, false), "面左 F=Left");
            Assert.IsFalse(MCommandMatcher.Matches(cmd, b, true), "面右 F=Right，本输入不匹配");
        }

        [Test]
        public void Charge_BackThenForward()
        {
            // "~5B, F, a"：蓄 Back 5 帧 → F → a（面向右：B=Left, F=Right）
            MCommandDef cmd = MCommandParser.Parse("charge", "~5B, F, a");
            MCommandBuffer b = Buf(L, L, L, L, L, R, R | A);
            Assert.IsTrue(MCommandMatcher.Matches(cmd, b, true));
        }

        [Test]
        public void Charge_FailsIfNotHeldLongEnough()
        {
            MCommandDef cmd = MCommandParser.Parse("charge", "~5B, F, a");
            MCommandBuffer b = Buf(L, L, R, R | A);   // 只蓄 2 帧
            Assert.IsFalse(MCommandMatcher.Matches(cmd, b, true));
        }

        [Test]
        public void Release_Button()
        {
            // "~a"：A 释放（上一帧按下、本帧松开）
            MCommandDef cmd = MCommandParser.Parse("rel", "~a");
            MCommandBuffer b = Buf(A, MInput.None);
            Assert.IsTrue(MCommandMatcher.Matches(cmd, b, true));
            // 一直按住不算释放
            Assert.IsFalse(MCommandMatcher.Matches(cmd, Buf(A, A), true));
        }

        [Test]
        public void ReleaseCharge_DoesNotBecomeHoldCommand()
        {
            MCommandDef cmd = MCommandParser.Parse("rel", "~3a");

            Assert.IsFalse(MCommandMatcher.Matches(cmd, Buf(A, A, A), true));
            Assert.IsTrue(MCommandMatcher.Matches(cmd, Buf(A, A, A, MInput.None), true));
            Assert.IsFalse(MCommandMatcher.Matches(cmd, Buf(A, A, MInput.None), true));
        }

        [Test]
        public void Dollar_FourWay_IgnoresOtherAxis()
        {
            // "$D, a"：下方向(含对角)+a
            MCommandDef dollar = MCommandParser.Parse("d", "$D, a");
            MCommandBuffer b = Buf(D | R, D | R | A);   // DF 含 Down
            Assert.IsTrue(MCommandMatcher.Matches(dollar, b, true), "$D 子集匹配 DF");

            // 非 $ 的精确 "D, a" 不匹配 DF
            MCommandDef exact = MCommandParser.Parse("d2", "D, a");
            Assert.IsFalse(MCommandMatcher.Matches(exact, b, true), "精确 D 不匹配 DF");
        }

        [Test]
        public void OrLogic_AnyKeySatisfies()
        {
            // "a|b"：按 a 或 b 任一即可
            MCommandDef cmd = MCommandParser.Parse("ab", "a|b");
            Assert.IsTrue(MCommandMatcher.Matches(cmd, Buf(MInput.None, A), true), "按 a 触发");
            Assert.IsTrue(MCommandMatcher.Matches(cmd, Buf(MInput.None, MInput.B), true), "按 b 触发");
            Assert.IsFalse(MCommandMatcher.Matches(cmd, Buf(MInput.None, MInput.C), true), "按 c 不触发");
        }

        [Test]
        public void AndLogic_RequiresAllKeys()
        {
            // "x+y"：须同时按 x 和 y
            MCommandDef cmd = MCommandParser.Parse("xy", "x+y");
            Assert.IsTrue(MCommandMatcher.Matches(cmd, Buf(MInput.None, MInput.X | MInput.Y), true));
            Assert.IsFalse(MCommandMatcher.Matches(cmd, Buf(MInput.None, MInput.X), true), "只按 x 不够");
        }

        [Test]
        public void SingleButton_EdgeTrigger()
        {
            MCommandDef cmd = MCommandParser.Parse("a", "a");
            Assert.IsTrue(MCommandMatcher.Matches(cmd, Buf(MInput.None, A), true), "按下边沿");
            Assert.IsFalse(MCommandMatcher.Matches(cmd, Buf(A, A), true), "持续按住非边沿");
        }
    }
}
