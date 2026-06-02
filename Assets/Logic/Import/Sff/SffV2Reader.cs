using System.IO;
using System.Text;

namespace Lockstep.Import.Sff
{
    /// <summary>
    /// SFF v2 目录读取器 + 像素解码器。解析 v2 头（精灵头偏移/数量、调色板头偏移/数量、ldata/tdata 基址），
    /// 读独立调色板库，再读 28 字节定长精灵头链。像素按格式解码：0 raw / 2 Rle8 / 3 Rle5 / 4 Lz5；
    /// PNG(10-12) 不在逻辑层处理（交表现层用 Unity 兜）。纯字节运算，无 Unity、无浮点，可 dotnet 测。
    /// 算法移植自 Ikemen GO src/image.go（readHeaderV2 / readV2 / Lz5Decode / Rle8Decode / Rle5Decode），只抄设计不引代码。
    /// </summary>
    public static class SffV2Reader
    {
        const int SpriteHeaderSize = 28;
        const int PaletteHeaderSize = 16;

        public static SffV2File ReadDirectory(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return ReadDirectory(stream);
            }
        }

        public static SffV2File ReadDirectory(Stream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                byte[] signature = reader.ReadBytes(12);
                if (Encoding.ASCII.GetString(signature, 0, 11) != "ElecbyteSpr")
                {
                    throw new SffException("非 SFF 文件");
                }

                byte[] version = reader.ReadBytes(4);   // [lo3,lo2,lo1,hi]
                if (version[3] != 2)
                {
                    throw new SffException("不是 SFF v2（版本高字节=" + version[3] + "），请用 SffV1Reader");
                }

                reader.ReadUInt32();                     // reserved
                for (int skip = 0; skip < 4; skip++)
                {
                    reader.ReadUInt32();                 // reserved ×4
                }
                uint firstSpriteHeaderOffset = reader.ReadUInt32();
                uint numberOfSprites = reader.ReadUInt32();
                uint firstPaletteHeaderOffset = reader.ReadUInt32();
                uint numberOfPalettes = reader.ReadUInt32();
                uint ldataOffset = reader.ReadUInt32();
                reader.ReadUInt32();                     // reserved
                uint tdataOffset = reader.ReadUInt32();

                SffV2File file = new SffV2File
                {
                    NumberOfSprites = (int)numberOfSprites,
                    NumberOfPalettes = (int)numberOfPalettes,
                };

                ReadPalettes(stream, reader, file, firstPaletteHeaderOffset, ldataOffset);
                ReadSpriteHeaders(stream, reader, file, firstSpriteHeaderOffset, ldataOffset, tdataOffset);
                return file;
            }
        }

        static void ReadPalettes(Stream stream, BinaryReader reader, SffV2File file,
            uint firstPaletteHeaderOffset, uint ldataOffset)
        {
            for (int paletteIndex = 0; paletteIndex < file.NumberOfPalettes; paletteIndex++)
            {
                stream.Seek(firstPaletteHeaderOffset + (long)paletteIndex * PaletteHeaderSize, SeekOrigin.Begin);
                reader.ReadUInt16();                     // group
                reader.ReadUInt16();                     // number
                reader.ReadUInt16();                     // numcols
                ushort link = reader.ReadUInt16();
                uint dataOffset = reader.ReadUInt32();
                uint dataSize = reader.ReadUInt32();

                if (dataSize == 0)
                {
                    // 链接调色板：复用此前某个库项（越界则给空板）
                    byte[] linkedPalette = link < file.Palettes.Count ? file.Palettes[link] : new byte[768];
                    file.Palettes.Add(linkedPalette);
                    continue;
                }

                file.Palettes.Add(ReadPaletteData(stream, reader, ldataOffset + dataOffset, dataSize));
            }
        }

        // 调色板数据：每色 4 字节 RGBA，取 RGB 存为 768 字节（index 0 透明交表现层处理）。
        static byte[] ReadPaletteData(Stream stream, BinaryReader reader, long offset, uint dataSize)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            int colorCount = (int)(dataSize / 4);
            byte[] palette = new byte[768];
            for (int colorIndex = 0; colorIndex < colorCount && colorIndex < 256; colorIndex++)
            {
                byte red = reader.ReadByte();
                byte green = reader.ReadByte();
                byte blue = reader.ReadByte();
                reader.ReadByte();                       // alpha（弃，表现层按 index 0 算透明）
                palette[colorIndex * 3] = red;
                palette[colorIndex * 3 + 1] = green;
                palette[colorIndex * 3 + 2] = blue;
            }
            return palette;
        }

        static void ReadSpriteHeaders(Stream stream, BinaryReader reader, SffV2File file,
            uint firstSpriteHeaderOffset, uint ldataOffset, uint tdataOffset)
        {
            for (int spriteIndex = 0; spriteIndex < file.NumberOfSprites; spriteIndex++)
            {
                stream.Seek(firstSpriteHeaderOffset + (long)spriteIndex * SpriteHeaderSize, SeekOrigin.Begin);
                ushort group = reader.ReadUInt16();
                ushort number = reader.ReadUInt16();
                ushort width = reader.ReadUInt16();
                ushort height = reader.ReadUInt16();
                short axisX = reader.ReadInt16();
                short axisY = reader.ReadInt16();
                ushort link = reader.ReadUInt16();
                byte format = reader.ReadByte();
                byte colDepth = reader.ReadByte();
                uint dataOffset = reader.ReadUInt32();
                uint dataSize = reader.ReadUInt32();
                ushort palIndex = reader.ReadUInt16();
                ushort flags = reader.ReadUInt16();

                // flag bit0：0=数据在 ldata 区，1=在 tdata（翻译后）区
                long absoluteOffset = dataOffset + ((flags & 1) == 0 ? ldataOffset : tdataOffset);

                file.Sprites.Add(new SffV2Sprite
                {
                    Group = group,
                    Number = number,
                    Width = width,
                    Height = height,
                    AxisX = axisX,
                    AxisY = axisY,
                    Format = format,
                    ColDepth = colDepth,
                    PalIndex = palIndex,
                    LinkedIndex = link,
                    DataOffset = absoluteOffset,
                    DataLength = (int)dataSize,
                });
            }
        }

        /// <summary>把某精灵解码成索引位图 + 调色板（PcxImage 复用 v1 的解码结果类型）。PNG 格式抛异常。</summary>
        public static PcxImage Decode(string path, SffV2File file, SffV2Sprite sprite)
        {
            SffV2Sprite pixelSprite = sprite;
            if (sprite.DataLength == 0 && sprite.LinkedIndex >= 0 && sprite.LinkedIndex < file.Sprites.Count)
            {
                pixelSprite = file.Sprites[sprite.LinkedIndex];
            }
            if (pixelSprite.DataLength == 0)
            {
                throw new SffException("精灵无像素数据（空链接）");
            }
            if (pixelSprite.Format >= 10)
            {
                throw new SffException("PNG 格式(" + pixelSprite.Format + ")不在逻辑层解码");
            }

            byte[] indices = DecodePixels(path, pixelSprite);

            int paletteIndex = sprite.PalIndex >= 0 && sprite.PalIndex < file.Palettes.Count ? sprite.PalIndex : 0;
            byte[] palette = file.Palettes.Count > 0 ? file.Palettes[paletteIndex] : new byte[768];

            return new PcxImage
            {
                Width = pixelSprite.Width,
                Height = pixelSprite.Height,
                Indices = indices,
                Palette = palette,
            };
        }

        static byte[] DecodePixels(string path, SffV2Sprite sprite)
        {
            int pixelCount = sprite.Width * sprite.Height;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                if (sprite.Format == 0)
                {
                    // raw：8 位索引直接就是像素（24/32 位 raw 不是索引图，本管线不支持）
                    if (sprite.ColDepth != 8)
                    {
                        throw new SffException("raw 非 8 位色深(" + sprite.ColDepth + ")，本管线只解索引图");
                    }
                    stream.Seek(sprite.DataOffset, SeekOrigin.Begin);
                    return ReadExact(stream, pixelCount);
                }

                // 压缩格式(2/3/4)：数据前有 4 字节未压缩长度前缀，跳过后读 DataLength-4 字节
                stream.Seek(sprite.DataOffset + 4, SeekOrigin.Begin);
                int compressedLength = sprite.DataLength >= 4 ? sprite.DataLength - 4 : 0;
                byte[] compressed = ReadExact(stream, compressedLength);

                switch (sprite.Format)
                {
                    case 2:
                        return Rle8Decode(compressed, pixelCount);
                    case 3:
                        return Rle5Decode(compressed, pixelCount);
                    case 4:
                        return Lz5Decode(compressed, pixelCount);
                    default:
                        throw new SffException("未知 SFF v2 像素格式：" + sprite.Format);
                }
            }
        }

        static byte[] ReadExact(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int readNow = stream.Read(buffer, read, count - read);
                if (readNow <= 0)
                {
                    break;
                }
                read += readNow;
            }
            return buffer;
        }

        // ---- 以下三个解码器移植自 Ikemen image.go，逐位运算与原实现一致 ----

        static byte[] Rle8Decode(byte[] rle, int pixelCount)
        {
            byte[] output = new byte[pixelCount];
            if (rle.Length == 0)
            {
                return output;
            }
            int src = 0;
            int dst = 0;
            while (dst < output.Length)
            {
                int count = 1;
                byte value = rle[src];
                if (src < rle.Length - 1)
                {
                    src++;
                }
                if ((value & 0xc0) == 0x40)
                {
                    count = value & 0x3f;
                    value = rle[src];
                    if (src < rle.Length - 1)
                    {
                        src++;
                    }
                }
                for (; count > 0; count--)
                {
                    if (dst < output.Length)
                    {
                        output[dst] = value;
                        dst++;
                    }
                }
            }
            return output;
        }

        static byte[] Rle5Decode(byte[] rle, int pixelCount)
        {
            byte[] output = new byte[pixelCount];
            if (rle.Length == 0)
            {
                return output;
            }
            int src = 0;
            int dst = 0;
            while (dst < output.Length)
            {
                int runLength = rle[src];
                if (src < rle.Length - 1)
                {
                    src++;
                }
                int dataLength = rle[src] & 0x7f;
                byte color = 0;
                if ((rle[src] >> 7) != 0)
                {
                    if (src < rle.Length - 1)
                    {
                        src++;
                    }
                    color = rle[src];
                }
                if (src < rle.Length - 1)
                {
                    src++;
                }
                while (true)
                {
                    if (dst < output.Length)
                    {
                        output[dst] = color;
                        dst++;
                    }
                    runLength--;
                    if (runLength < 0)
                    {
                        dataLength--;
                        if (dataLength < 0)
                        {
                            break;
                        }
                        color = (byte)(rle[src] & 0x1f);
                        runLength = rle[src] >> 5;
                        if (src < rle.Length - 1)
                        {
                            src++;
                        }
                    }
                }
            }
            return output;
        }

        static byte[] Lz5Decode(byte[] rle, int pixelCount)
        {
            byte[] output = new byte[pixelCount];
            if (rle.Length == 0)
            {
                return output;
            }
            int src = 0;
            int dst = 0;
            int count = 0;
            byte control = rle[src];
            int controlBit = 0;
            byte recycleBits = 0;
            int recycleCount = 0;
            if (src < rle.Length - 1)
            {
                src++;
            }
            while (dst < output.Length)
            {
                int value = rle[src];
                if (src < rle.Length - 1)
                {
                    src++;
                }
                if ((control & (1 << controlBit)) != 0)
                {
                    // 回拷（LZ 复制）分支
                    if ((value & 0x3f) == 0)
                    {
                        value = ((value << 2) | rle[src]) + 1;
                        if (src < rle.Length - 1)
                        {
                            src++;
                        }
                        count = rle[src] + 2;
                        if (src < rle.Length - 1)
                        {
                            src++;
                        }
                    }
                    else
                    {
                        recycleBits |= (byte)((value & 0xc0) >> recycleCount);
                        recycleCount += 2;
                        count = value & 0x3f;
                        if (recycleCount < 8)
                        {
                            value = rle[src] + 1;
                            if (src < rle.Length - 1)
                            {
                                src++;
                            }
                        }
                        else
                        {
                            value = recycleBits + 1;
                            recycleBits = 0;
                            recycleCount = 0;
                        }
                    }
                    while (true)
                    {
                        if (dst < output.Length)
                        {
                            output[dst] = output[dst - value];
                            dst++;
                        }
                        count--;
                        if (count < 0)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    // 直写（RLE 填充）分支
                    if ((value & 0xe0) == 0)
                    {
                        count = rle[src] + 8;
                        if (src < rle.Length - 1)
                        {
                            src++;
                        }
                    }
                    else
                    {
                        count = value >> 5;
                        value &= 0x1f;
                    }
                    for (; count > 0; count--)
                    {
                        if (dst < output.Length)
                        {
                            output[dst] = (byte)value;
                            dst++;
                        }
                    }
                }
                controlBit++;
                if (controlBit >= 8)
                {
                    control = rle[src];
                    controlBit = 0;
                    if (src < rle.Length - 1)
                    {
                        src++;
                    }
                }
            }
            return output;
        }
    }
}
