// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (AssertSpecial flag 列表 / ASF_*) — 逻辑相关子集。
// 表现/音频类标志(nomusic/nobardisplay/...)不影响逻辑，按需后补。See Docs/移植方案_Ikemen.md.
using System;

namespace Lockstep.Mugen.Char
{
    /// <summary>AssertSpecial 标志位（逻辑相关子集，位标志便于 OR 与哈希）。</summary>
    [Flags]
    public enum MAssertFlag
    {
        None = 0,
        Intro = 1 << 0,
        Invisible = 1 << 1,
        NoAutoTurn = 1 << 2,
        NoStandGuard = 1 << 3,
        NoCrouchGuard = 1 << 4,
        NoAirGuard = 1 << 5,
        NoWalk = 1 << 6,
        NoJuggleCheck = 1 << 7,
        Unguardable = 1 << 8,
        NoKO = 1 << 9,
        NoShadow = 1 << 10,
        NoAutoGuard = 1 << 11,
    }
}
