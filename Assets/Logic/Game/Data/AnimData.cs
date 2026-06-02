using Lockstep.Math;

namespace Lockstep.Game.Data
{
    /// <summary>动画的一帧（≈ AIR 里一行 + 其上的 Clsn 块）。静态数据，导入期产出。</summary>
    public sealed class AnimFrame
    {
        public int SpriteGroup;     // → SFF group
        public int SpriteImage;     // → SFF image
        public FFloat OffX;         // 绘制偏移（表现层用；逻辑只用 Clsn）
        public FFloat OffY;
        public int Duration;        // 持续 tick；-1 = 永久
        public FlipFlags Flip;
        public ClsnBox[] Clsn1;     // 攻击框（active 时启用），可为 null
        public ClsnBox[] Clsn2;     // 受击框，可为 null
    }

    /// <summary>一段动画（≈ AIR [Begin Action N]）。</summary>
    public sealed class AnimData
    {
        public int Id;
        public AnimFrame[] Frames;
        public int LoopStart;       // 循环起始帧索引（AIR LoopStart）
    }
}
