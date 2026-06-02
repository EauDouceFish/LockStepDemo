using NUnit.Framework;
using Lockstep.Game.Command;
using Lockstep.Game.Data;
using Lockstep.Input;

namespace Lockstep.Tests
{
    /// <summary>T3.1：搓招匹配验收——方向推导 + 正确匹配 + 超时不匹配。</summary>
    [TestFixture]
    public sealed class CommandMatcherTests
    {
        static byte D(InputSymbol s)
        {
            return (byte)s;
        }

        [Test]
        public void Direction_RespectsFacing()
        {
            // 面向右：moveX+1=前
            Assert.That(CommandMatcher.Direction(1, 0, true), Is.EqualTo(D(InputSymbol.Fwd)));
            Assert.That(CommandMatcher.Direction(-1, 0, true), Is.EqualTo(D(InputSymbol.Back)));
            // 面向左：moveX+1=后（镜像）
            Assert.That(CommandMatcher.Direction(1, 0, false), Is.EqualTo(D(InputSymbol.Back)));
            Assert.That(CommandMatcher.Direction(-1, 0, false), Is.EqualTo(D(InputSymbol.Fwd)));
            // 斜向：下+前
            Assert.That(CommandMatcher.Direction(1, -1, true), Is.EqualTo(D(InputSymbol.DownFwd)));
            Assert.That(CommandMatcher.Direction(0, -1, true), Is.EqualTo(D(InputSymbol.Down)));
            Assert.That(CommandMatcher.Direction(0, 1, true), Is.EqualTo(D(InputSymbol.Up)));
            Assert.That(CommandMatcher.Direction(0, 0, true), Is.EqualTo(D(InputSymbol.Neutral)));
        }

        static CommandData Qcf()
        {
            return new CommandData
            {
                Name = "qcf_a",
                Motion = new[] { InputSymbol.Down, InputSymbol.DownFwd, InputSymbol.Fwd, InputSymbol.BtnA },
                TimeWindow = 15,
            };
        }

        [Test]
        public void Qcf_CleanSequence_Matches()
        {
            // 时间从旧到新：N, D, DF, F, F+A（末帧按下轻拳）
            byte[] dir = { D(InputSymbol.Neutral), D(InputSymbol.Down), D(InputSymbol.DownFwd), D(InputSymbol.Fwd), D(InputSymbol.Fwd) };
            byte[] btn = { 0, 0, 0, 0, (byte)InputButton.LightPunch };

            Assert.That(CommandMatcher.Matches(Qcf(), dir, btn, 5), Is.True);
        }

        [Test]
        public void Qcf_Timeout_DroppedDown_DoesNotMatch()
        {
            // 时间窗只剩 3 帧（最早的 Down 已滑出窗口）：DF, F, F+A → 缺 Down，不成立
            byte[] dir = { D(InputSymbol.DownFwd), D(InputSymbol.Fwd), D(InputSymbol.Fwd) };
            byte[] btn = { 0, 0, (byte)InputButton.LightPunch };

            Assert.That(CommandMatcher.Matches(Qcf(), dir, btn, 3), Is.False);
        }

        [Test]
        public void Qcf_ButtonNotPressedThisFrame_DoesNotMatch()
        {
            // 方向齐全但末帧没按键 → 不在本帧触发
            byte[] dir = { D(InputSymbol.Down), D(InputSymbol.DownFwd), D(InputSymbol.Fwd) };
            byte[] btn = { 0, 0, 0 };

            Assert.That(CommandMatcher.Matches(Qcf(), dir, btn, 3), Is.False);
        }

        [Test]
        public void Qcf_AllowsNeutralFramesBetween()
        {
            // 各方向之间夹无关帧仍应识别（贪心跳过）
            byte[] dir =
            {
                D(InputSymbol.Down), D(InputSymbol.Neutral), D(InputSymbol.DownFwd),
                D(InputSymbol.Neutral), D(InputSymbol.Fwd), D(InputSymbol.Fwd),
            };
            byte[] btn = { 0, 0, 0, 0, 0, (byte)InputButton.LightPunch };

            Assert.That(CommandMatcher.Matches(Qcf(), dir, btn, 6), Is.True);
        }

        [Test]
        public void SingleButton_Command_FiresOnPress()
        {
            CommandData jab = new CommandData
            {
                Name = "a",
                Motion = new[] { InputSymbol.BtnA },
                TimeWindow = 1,
            };
            byte[] dir = { D(InputSymbol.Neutral) };
            byte[] btnPressed = { (byte)InputButton.LightPunch };
            byte[] btnIdle = { 0 };

            Assert.That(CommandMatcher.Matches(jab, dir, btnPressed, 1), Is.True);
            Assert.That(CommandMatcher.Matches(jab, dir, btnIdle, 1), Is.False);
        }
    }
}
