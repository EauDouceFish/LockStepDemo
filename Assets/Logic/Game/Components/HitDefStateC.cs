using Lockstep.Core;
using Lockstep.Game.Data;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 当前激活的 HitDef 运行态（对应旧 ActiveMoveC 思路的通用版）。
    /// Current 指向静态 HitDef 数据：Clone 只复制引用（静态不可变）；不入哈希——它由
    /// "当前状态+帧"决定，两端必然一致，无需哈希其身份。运行态（是否激活、已命中谁）入哈希。
    /// </summary>
    public sealed class HitDefStateC : IComponent
    {
        public bool Active;
        public ulong HitTargetsBits;   // 已命中目标位（防一招对同一目标多次命中）
        public HitDef Current;         // 静态数据引用

        public IComponent Clone()
        {
            return new HitDefStateC
            {
                Active = Active,
                HitTargetsBits = HitTargetsBits,
                Current = Current,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddBool(Active);
            hash.AddUInt64(HitTargetsBits);
        }
    }
}
