using NUnit.Framework;
using Lockstep.Game.Components;

namespace Lockstep.Tests
{
    /// <summary>T3.1：输入历史环形缓冲——ReadRecent 应按时间从旧到新返回最近 N 帧。</summary>
    [TestFixture]
    public sealed class CommandInputCTests
    {
        [Test]
        public void ReadRecent_ReturnsChronologicalOrder()
        {
            CommandInputC history = new CommandInputC();
            for (byte v = 1; v <= 5; v++)
            {
                history.Push(v, (byte)(v + 100));
            }

            byte[] dir = new byte[CommandInputC.Capacity];
            byte[] btn = new byte[CommandInputC.Capacity];
            int count = history.ReadRecent(3, dir, btn);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(dir[0], Is.EqualTo(3));   // 最旧
            Assert.That(dir[1], Is.EqualTo(4));
            Assert.That(dir[2], Is.EqualTo(5));   // 最新
            Assert.That(btn[2], Is.EqualTo(105));
        }

        [Test]
        public void ReadRecent_WrapsAroundRingBuffer()
        {
            CommandInputC history = new CommandInputC();
            // 推满一圈多，确保环绕
            for (int i = 1; i <= CommandInputC.Capacity + 3; i++)
            {
                history.Push((byte)(i & 0xFF), 0);
            }

            byte[] dir = new byte[CommandInputC.Capacity];
            byte[] btn = new byte[CommandInputC.Capacity];
            int count = history.ReadRecent(2, dir, btn);

            Assert.That(count, Is.EqualTo(2));
            // 最后两次推入的是 Capacity+2、Capacity+3
            Assert.That(dir[0], Is.EqualTo((byte)((CommandInputC.Capacity + 2) & 0xFF)));
            Assert.That(dir[1], Is.EqualTo((byte)((CommandInputC.Capacity + 3) & 0xFF)));
        }

        [Test]
        public void Clone_IsDeepCopy()
        {
            CommandInputC history = new CommandInputC();
            history.Push(7, 9);
            CommandInputC clone = (CommandInputC)history.Clone();
            history.Push(3, 3);

            // 克隆后再推不应影响克隆体
            Assert.That(clone.Cursor, Is.EqualTo(1));
            Assert.That(clone.Dir[0], Is.EqualTo(7));
            Assert.That(clone.Btn[0], Is.EqualTo(9));
        }
    }
}
