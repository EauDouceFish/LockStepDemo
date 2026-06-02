using Lockstep.Game.Data;

namespace Lockstep.Game.Anim
{
    /// <summary>
    /// 动画推进纯逻辑（无 Unity、无 float）。表现层 SpriteAnimator 与未来的 AnimSystem 共用，
    /// 保证"逻辑帧驱动的动画"在两端确定一致。
    /// 约定：Duration=N 表示该帧显示 N 个 tick；Duration&lt;0 表示永久帧（不前进）。
    /// 播放到末帧后回到 LoopStart。
    /// </summary>
    public static class AnimAdvance
    {
        /// <summary>推进一个 tick。frameIndex/elemTime/animTime 为运行态，按引用更新。</summary>
        public static void Step(AnimData anim, ref int frameIndex, ref int elemTime, ref int animTime)
        {
            if (anim == null || anim.Frames == null || anim.Frames.Length == 0)
            {
                return;
            }
            if (frameIndex < 0 || frameIndex >= anim.Frames.Length)
            {
                frameIndex = 0;
                elemTime = 0;
            }

            animTime++;
            elemTime++;

            int duration = anim.Frames[frameIndex].Duration;
            if (duration < 0)
            {
                return; // 永久帧
            }
            if (elemTime >= duration)
            {
                elemTime = 0;
                frameIndex++;
                if (frameIndex >= anim.Frames.Length)
                {
                    frameIndex = (anim.LoopStart >= 0 && anim.LoopStart < anim.Frames.Length) ? anim.LoopStart : 0;
                    animTime = 0;
                }
            }
        }

        /// <summary>取当前应显示的帧。</summary>
        public static AnimFrame CurrentFrame(AnimData anim, int frameIndex)
        {
            if (anim == null || anim.Frames == null || anim.Frames.Length == 0)
            {
                return null;
            }
            int index = (frameIndex >= 0 && frameIndex < anim.Frames.Length) ? frameIndex : 0;
            return anim.Frames[index];
        }
    }
}
