using NUnit.Framework;
using Lockstep.Game.Anim;
using Lockstep.Game.Data;

namespace Lockstep.Tests
{
    /// <summary>T1.3：动画推进纯逻辑验收（帧切换 + 循环 + 永久帧）。</summary>
    [TestFixture]
    public sealed class AnimAdvanceTests
    {
        static AnimData Make(int loopStart, params int[] durations)
        {
            AnimFrame[] frames = new AnimFrame[durations.Length];
            for (int i = 0; i < durations.Length; i++)
            {
                frames[i] = new AnimFrame { SpriteGroup = 0, SpriteImage = i, Duration = durations[i] };
            }
            return new AnimData { Id = 0, Frames = frames, LoopStart = loopStart };
        }

        [Test]
        public void Advances_AfterDurationTicks()
        {
            AnimData anim = Make(0, 2, 3);
            int frame = 0;
            int elem = 0;
            int animTime = 0;

            AnimAdvance.Step(anim, ref frame, ref elem, ref animTime);   // elem 1
            Assert.That(frame, Is.EqualTo(0));
            AnimAdvance.Step(anim, ref frame, ref elem, ref animTime);   // elem 2 >= 2 → frame 1
            Assert.That(frame, Is.EqualTo(1));
            Assert.That(elem, Is.EqualTo(0));
        }

        [Test]
        public void Loops_BackToLoopStart()
        {
            AnimData anim = Make(0, 2, 3);
            int frame = 0;
            int elem = 0;
            int animTime = 0;

            // 共需 2+3=5 tick 走完一轮，第 5 次后回到 frame 0
            for (int i = 0; i < 5; i++)
            {
                AnimAdvance.Step(anim, ref frame, ref elem, ref animTime);
            }
            Assert.That(frame, Is.EqualTo(0), "末帧后应循环回 LoopStart");
            Assert.That(elem, Is.EqualTo(0));
            Assert.That(animTime, Is.EqualTo(0), "循环时 animTime 重置");
        }

        [Test]
        public void ForeverFrame_DoesNotAdvance()
        {
            AnimData anim = Make(0, -1);
            int frame = 0;
            int elem = 0;
            int animTime = 0;

            for (int i = 0; i < 100; i++)
            {
                AnimAdvance.Step(anim, ref frame, ref elem, ref animTime);
            }
            Assert.That(frame, Is.EqualTo(0), "永久帧不前进");
        }

        [Test]
        public void LoopStartNonZero_ReturnsToMidAnim()
        {
            AnimData anim = Make(1, 1, 1, 1);   // loopStart=1
            int frame = 0;
            int elem = 0;
            int animTime = 0;

            // 3 帧各 1 tick：第 3 次后越界 → 回到 loopStart=1
            AnimAdvance.Step(anim, ref frame, ref elem, ref animTime);   // →frame1
            AnimAdvance.Step(anim, ref frame, ref elem, ref animTime);   // →frame2
            AnimAdvance.Step(anim, ref frame, ref elem, ref animTime);   // 越界→loopStart 1
            Assert.That(frame, Is.EqualTo(1));
        }
    }
}
