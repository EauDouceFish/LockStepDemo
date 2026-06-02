using System.Collections.Generic;

namespace Lockstep.Import.Sff
{
    /// <summary>SFF v1 一个精灵节点的元数据（不含像素）。</summary>
    public sealed class SffNode
    {
        public int Group;
        public int Image;
        public short AxisX;
        public short AxisY;
        public int LinkedIndex;     // Length==0 时指向被复用的精灵下标
        public bool SamePaletteAsPrevious;
        public long DataOffset;     // PCX 数据在文件中的偏移
        public int DataLength;      // PCX 字节数；0 = 复用 LinkedIndex
    }

    /// <summary>SFF v1 目录（头 + 全部节点元数据）。</summary>
    public sealed class SffFile
    {
        public int VersionHi;
        public int NumGroups;
        public int NumImages;
        public List<SffNode> Nodes = new List<SffNode>();
    }

    /// <summary>解码后的 PCX 图（8 位索引 + 256 色调色板）。</summary>
    public sealed class PcxImage
    {
        public int Width;
        public int Height;
        public byte[] Indices;      // length = Width*Height
        public byte[] Palette;      // 768 字节 RGB（无内嵌调色板时全 0）
    }
}
