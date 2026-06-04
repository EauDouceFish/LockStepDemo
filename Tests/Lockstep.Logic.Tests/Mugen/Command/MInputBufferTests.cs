using NUnit.Framework;
using Lockstep.Mugen.Command;

namespace Lockstep.Tests.Mugen
{
    /// <summary>模块 A：输入边沿缓冲（移植 input.go InputBuffer）。计数语义 + 朝向 B/F + SOCD。</summary>
    [TestFixture]
    public sealed class MInputBufferTests
    {
        [Test]
        public void HeldDirection_CounterIncrementsEachFrame()
        {
            MInputBuffer buf = new MInputBuffer();
            buf.Update(MInput.Right, facingRight: true);
            Assert.That(buf.Rb, Is.EqualTo(1), "首帧按住=1");
            buf.Update(MInput.Right, facingRight: true);
            Assert.That(buf.Rb, Is.EqualTo(2), "连按第2帧=2");
            buf.Update(MInput.Right, facingRight: true);
            Assert.That(buf.Rb, Is.EqualTo(3));
        }

        [Test]
        public void Release_CounterFlipsNegativeAndDecrements()
        {
            MInputBuffer buf = new MInputBuffer();
            buf.Update(MInput.Right, facingRight: true);   // Rb=1
            buf.Update(MInput.Right, facingRight: true);   // Rb=2
            buf.Update(MInput.None, facingRight: true);
            Assert.That(buf.Rb, Is.EqualTo(-1), "松开首帧翻转为 -1");
            buf.Update(MInput.None, facingRight: true);
            Assert.That(buf.Rb, Is.EqualTo(-2), "持续松开递减");
        }

        [Test]
        public void FacingRight_RightIsForwardLeftIsBack()
        {
            MInputBuffer buf = new MInputBuffer();
            buf.Update(MInput.Right, facingRight: true);
            Assert.That(buf.Fb, Is.GreaterThan(0), "面右时 Right=前 Fb>0");
            Assert.That(buf.Bb, Is.LessThan(0), "Back 未按 Bb<0");

            MInputBuffer buf2 = new MInputBuffer();
            buf2.Update(MInput.Left, facingRight: true);
            Assert.That(buf2.Bb, Is.GreaterThan(0), "面右时 Left=后 Bb>0");
            Assert.That(buf2.Fb, Is.LessThan(0));
        }

        [Test]
        public void FacingLeft_DirectionsSwap()
        {
            MInputBuffer buf = new MInputBuffer();
            buf.Update(MInput.Right, facingRight: false);
            Assert.That(buf.Bb, Is.GreaterThan(0), "面左时 Right=后 Bb>0");
            Assert.That(buf.Fb, Is.LessThan(0));
        }

        [Test]
        public void Socd_OppositeDirectionsCancelToNeutral()
        {
            MInputBuffer buf = new MInputBuffer();
            buf.Update(MInput.Left | MInput.Right, facingRight: true);
            Assert.That(buf.Lb, Is.LessThan(0), "左右同按对消，Left 视为未按");
            Assert.That(buf.Rb, Is.LessThan(0), "Right 视为未按");
            Assert.That(buf.Nb, Is.GreaterThan(0), "无有效方向 → 中立 Nb>0");
        }

        [Test]
        public void UpPressedThisFrame_UbEqualsOne()
        {
            MInputBuffer buf = new MInputBuffer();
            buf.Update(MInput.None, facingRight: true);
            buf.Update(MInput.Up, facingRight: true);
            Assert.That(buf.Ub, Is.EqualTo(1), "刚按上一帧 Ub==1（air jump 用 ==1 判定边沿）");
            buf.Update(MInput.Up, facingRight: true);
            Assert.That(buf.Ub, Is.EqualTo(2), "持续按住不再 ==1");
        }

        [Test]
        public void Button_CounterTracked()
        {
            MInputBuffer buf = new MInputBuffer();
            buf.Update(MInput.A, facingRight: true);
            Assert.That(buf.Ab, Is.EqualTo(1));
            buf.Update(MInput.A | MInput.B, facingRight: true);
            Assert.That(buf.Ab, Is.EqualTo(2));
            Assert.That(buf.Bbtn, Is.EqualTo(1));
        }
    }
}
