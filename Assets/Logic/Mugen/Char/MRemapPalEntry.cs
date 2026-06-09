// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go:9143 remapPal + remapSpr (RemapPreset map[int32]RemapTable).
namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// 单条调色板重映射（移植 Ikemen remapSpr 项）：把源 (group,index) 调色板替换为目标 (group,index)。
    /// 纯表现态，但入 Clone/Hash 以保回滚后渲染一致。值类型，列表浅拷即深拷。
    /// </summary>
    public struct MRemapPalEntry
    {
        public int SrcGroup;
        public int SrcIndex;
        public int DstGroup;
        public int DstIndex;
    }
}
