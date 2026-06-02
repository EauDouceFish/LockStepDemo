using System;
using System.IO;
using System.Text;

namespace Lockstep.Import.Sff
{
    /// <summary>SFF 解析异常。</summary>
    public sealed class SffException : Exception
    {
        public SffException(string message) : base(message) { }
    }

    /// <summary>
    /// SFF v1 目录读取器。解析头（签名 ElecbyteSpr + 版本 + 组数/图数/首偏移/节点大小），
    /// 沿链表读出每个精灵节点的元数据（组/图/轴/数据偏移/长度/复用下标）。纯字节运算，无 Unity。
    /// 像素解码见 <see cref="PcxDecoder"/>；Unity Texture/Sprite 生成在编辑器层。
    /// </summary>
    public static class SffV1Reader
    {
        const int SubheaderSize = 32;

        /// <summary>打开 .sff 文件并读取其精灵目录（头 + 全部节点元数据）。</summary>
        public static SffFile ReadDirectory(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return ReadDirectory(fs);
            }
        }

        /// <summary>从流读取 SFF v1 精灵目录。沿链表遍历，以 NumImages 计数封顶。</summary>
        public static SffFile ReadDirectory(Stream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                byte[] sig = reader.ReadBytes(12);
                string signature = Encoding.ASCII.GetString(sig, 0, 11);
                if (signature != "ElecbyteSpr")
                {
                    throw new SffException("非 SFF 文件，签名=" + signature);
                }

                byte[] ver = reader.ReadBytes(4);   // [lo3,lo2,lo1,hi]
                int versionHi = ver[3];

                uint numGroups = reader.ReadUInt32();
                uint numImages = reader.ReadUInt32();
                uint firstOffset = reader.ReadUInt32();
                uint subheaderSize = reader.ReadUInt32();
                if (subheaderSize == 0)
                {
                    subheaderSize = SubheaderSize;
                }

                SffFile file = new SffFile
                {
                    VersionHi = versionHi,
                    NumGroups = (int)numGroups,
                    NumImages = (int)numImages,
                };

                long length = stream.Length;
                long offset = firstOffset;
                // 以 NumImages 封顶（读满即停，不追越界/未归零的尾指针）；并守住偏移在文件范围内。
                while (offset > 0 && offset + subheaderSize <= length && file.Nodes.Count < file.NumImages)
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                    uint next = reader.ReadUInt32();
                    uint dataLen = reader.ReadUInt32();
                    short axisX = reader.ReadInt16();
                    short axisY = reader.ReadInt16();
                    short group = reader.ReadInt16();
                    short image = reader.ReadInt16();
                    short linked = reader.ReadInt16();
                    byte samePalette = reader.ReadByte();
                    // 已读 4+4+2+2+2+2+2+1 = 19 字节；节点头剩余补齐到 subheaderSize

                    file.Nodes.Add(new SffNode
                    {
                        Group = group,
                        Image = image,
                        AxisX = axisX,
                        AxisY = axisY,
                        LinkedIndex = linked,
                        SamePaletteAsPrevious = samePalette != 0,
                        DataOffset = offset + subheaderSize,
                        DataLength = (int)dataLen,
                    });

                    if (next == 0)
                    {
                        break;
                    }
                    offset = next;
                }

                return file;
            }
        }

        /// <summary>读取某节点的原始 PCX 字节（用于喂给 PcxDecoder）。</summary>
        public static byte[] ReadNodeData(string path, SffNode node)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                fs.Seek(node.DataOffset, SeekOrigin.Begin);
                byte[] buffer = new byte[node.DataLength];
                int read = 0;
                while (read < buffer.Length)
                {
                    int readNow = fs.Read(buffer, read, buffer.Length - read);
                    if (readNow <= 0)
                    {
                        break;
                    }
                    read += readNow;
                }
                return buffer;
            }
        }
    }
}
