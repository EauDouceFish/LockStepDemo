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
        public byte[] Palette;      // 768 bytes RGB for indexed images
        public byte[] Rgba;         // length = Width*Height*4 for true-color images

        public bool IsTrueColor
        {
            get { return Rgba != null; }
        }
    }

    /// <summary>SFF v2 一个精灵的头（28 字节）。宽高自带、像素压缩、调色板按 PalIndex 取自独立库。</summary>
    public sealed class SffV2Sprite
    {
        public int Group;
        public int Number;
        public int Width;
        public int Height;
        public short AxisX;
        public short AxisY;
        public int Format;          // 0 raw / 2 Rle8 / 3 Rle5 / 4 Lz5 / 10,11,12 PNG
        public int ColDepth;
        public int PalIndex;        // 指向 SffV2File.Palettes
        public int LinkedIndex;     // DataLength==0 时指向被复用精灵下标
        public long DataOffset;     // 像素数据绝对偏移（已加 ldata/tdata 基址）
        public int DataLength;      // 像素数据字节数；0 = 复用 LinkedIndex
    }

    /// <summary>SFF v2 目录：精灵头列表 + 独立调色板库（每个 768 字节 RGB）。</summary>
    public sealed class SffV2File
    {
        public int NumberOfSprites;
        public int NumberOfPalettes;
        public List<byte[]> Palettes = new List<byte[]>();   // 每项 768 字节 RGB（index 0 视为透明，交由表现层处理）
        public List<SffV2Sprite> Sprites = new List<SffV2Sprite>();
    }
}
