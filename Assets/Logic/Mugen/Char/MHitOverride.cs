// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go HitOverride 容器（c.ho[8]）+ bytecode.go HitOverride StateController。
// Adapted to fixed-point. See Docs/移植方案_Ikemen.md.
using Lockstep.Core;

namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// 受击改写槽（HitOverride 控制器写入）：命中匹配 Attr 且 Time≠0 时，受击方改去 StateNo 而非常规 5000 受击态。
    /// 每角色 8 槽（对齐 Ikemen c.ho[8]）。Time 每帧递减（-1 表示常驻）；Active=有效。
    /// </summary>
    public struct MHitOverride
    {
        public int Attr;        // 要匹配的攻击属性位掩码（0 = 未设置/不生效）
        public int StateNo;     // 命中时改去的状态号
        public int Time;        // 剩余有效帧（>0 递减；-1 常驻；0 失效）
        public bool ForceAir;   // 强制按空中受击处理
        public bool KeepState;  // 保持当前状态（不强制 ChangeState）

        /// <summary>是否生效（已设置且未到期）。</summary>
        public bool Active => Attr != 0 && Time != 0;

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(Attr);
            hash.AddInt32(StateNo);
            hash.AddInt32(Time);
            hash.AddBool(ForceAir);
            hash.AddBool(KeepState);
        }
    }
}
