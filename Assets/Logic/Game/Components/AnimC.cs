using Lockstep.Core;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 动画播放运行态。AnimSystem 维护。ElemTime/AnimTime 供 AnimElem/AnimTime trigger 使用
    /// —— 若只"播帧"不记这两个值，大量 MUGEN trigger 会失效（见架构设计 §12 烂尾登记）。
    /// </summary>
    public sealed class AnimC : IComponent
    {
        public int AnimNo;
        public int FrameIndex;     // 当前在 AnimData.Frames 的索引
        public int ElemTime;       // 当前帧已停留 tick
        public int AnimTime;       // 整段动画已播 tick

        public IComponent Clone()
        {
            return new AnimC
            {
                AnimNo = AnimNo,
                FrameIndex = FrameIndex,
                ElemTime = ElemTime,
                AnimTime = AnimTime,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(AnimNo);
            hash.AddInt32(FrameIndex);
            hash.AddInt32(ElemTime);
            hash.AddInt32(AnimTime);
        }

        public override string ToString()
        {
            return string.Format("Anim[{0} f{1} e{2} t{3}]", AnimNo, FrameIndex, ElemTime, AnimTime);
        }
    }
}
