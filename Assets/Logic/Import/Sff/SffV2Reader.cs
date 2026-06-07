using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lockstep.Import.Sff
{
    /// <summary>SFF v2 directory and pixel decoder. Pure C#, with no Unity dependency.</summary>
    public static class SffV2Reader
    {
        const int HeaderSize = 68;
        const int SpriteHeaderSize = 28;
        const int PaletteHeaderSize = 16;

        sealed class PaletteHeader
        {
            public ushort Link;
            public uint DataOffset;
            public uint DataSize;
        }

        public static SffV2File ReadDirectory(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return ReadDirectory(stream);
            }
        }

        public static SffV2File ReadDirectory(Stream stream)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek)
            {
                throw new SffException("SFF v2 input must be a readable, seekable stream");
            }
            EnsureRange(stream, 0, HeaderSize, "SFF v2 header");

            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                byte[] signature = ReadExact(stream, 12, "SFF signature");
                if (Encoding.ASCII.GetString(signature, 0, 11) != "ElecbyteSpr")
                {
                    throw new SffException("Not an SFF file");
                }

                byte[] version = ReadExact(stream, 4, "SFF version");
                if (version[3] != 2)
                {
                    throw new SffException("Not an SFF v2 file (major version " + version[3] + ")");
                }

                reader.ReadUInt32();
                for (int i = 0; i < 4; i++)
                {
                    reader.ReadUInt32();
                }
                uint firstSpriteHeaderOffset = reader.ReadUInt32();
                uint numberOfSprites = reader.ReadUInt32();
                uint firstPaletteHeaderOffset = reader.ReadUInt32();
                uint numberOfPalettes = reader.ReadUInt32();
                uint ldataOffset = reader.ReadUInt32();
                reader.ReadUInt32();
                uint tdataOffset = reader.ReadUInt32();

                if (numberOfSprites > int.MaxValue || numberOfPalettes > int.MaxValue)
                {
                    throw new SffException("SFF v2 directory count exceeds supported size");
                }
                EnsureTableRange(stream, firstSpriteHeaderOffset, numberOfSprites, SpriteHeaderSize,
                    "sprite header table");
                EnsureTableRange(stream, firstPaletteHeaderOffset, numberOfPalettes, PaletteHeaderSize,
                    "palette header table");

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
            PaletteHeader[] headers = new PaletteHeader[file.NumberOfPalettes];
            for (int index = 0; index < headers.Length; index++)
            {
                stream.Seek(firstPaletteHeaderOffset + (long)index * PaletteHeaderSize, SeekOrigin.Begin);
                reader.ReadUInt16();
                reader.ReadUInt16();
                reader.ReadUInt16();
                headers[index] = new PaletteHeader
                {
                    Link = reader.ReadUInt16(),
                    DataOffset = reader.ReadUInt32(),
                    DataSize = reader.ReadUInt32(),
                };
                file.Palettes.Add(null);
            }

            for (int index = 0; index < headers.Length; index++)
            {
                ResolvePalette(stream, file, headers, index, ldataOffset, new HashSet<int>());
            }
        }

        static byte[] ResolvePalette(Stream stream, SffV2File file, PaletteHeader[] headers,
            int index, uint ldataOffset, HashSet<int> chain)
        {
            if (index < 0 || index >= headers.Length)
            {
                throw new SffException("SFF v2 palette link is out of range: " + index);
            }
            if (file.Palettes[index] != null)
            {
                return file.Palettes[index];
            }
            if (!chain.Add(index))
            {
                throw new SffException("SFF v2 palette link cycle at index " + index);
            }

            PaletteHeader header = headers[index];
            byte[] palette;
            if (header.DataSize == 0)
            {
                palette = ResolvePalette(stream, file, headers, header.Link, ldataOffset, chain);
            }
            else
            {
                long absoluteOffset = AddOffset(ldataOffset, header.DataOffset, "palette data");
                palette = ReadPaletteData(stream, absoluteOffset, header.DataSize);
            }
            chain.Remove(index);
            file.Palettes[index] = palette;
            return palette;
        }

        static byte[] ReadPaletteData(Stream stream, long offset, uint dataSize)
        {
            if (dataSize % 4 != 0)
            {
                throw new SffException("SFF v2 palette data length is not a multiple of four");
            }
            EnsureRange(stream, offset, dataSize, "palette data");
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] rgba = ReadExact(stream, checked((int)dataSize), "palette data");
            byte[] palette = new byte[768];
            int colorCount = System.Math.Min(rgba.Length / 4, 256);
            for (int i = 0; i < colorCount; i++)
            {
                palette[i * 3] = rgba[i * 4];
                palette[i * 3 + 1] = rgba[i * 4 + 1];
                palette[i * 3 + 2] = rgba[i * 4 + 2];
            }
            return palette;
        }

        static void ReadSpriteHeaders(Stream stream, BinaryReader reader, SffV2File file,
            uint firstSpriteHeaderOffset, uint ldataOffset, uint tdataOffset)
        {
            for (int index = 0; index < file.NumberOfSprites; index++)
            {
                stream.Seek(firstSpriteHeaderOffset + (long)index * SpriteHeaderSize, SeekOrigin.Begin);
                ushort group = reader.ReadUInt16();
                ushort number = reader.ReadUInt16();
                ushort width = reader.ReadUInt16();
                ushort height = reader.ReadUInt16();
                short axisX = reader.ReadInt16();
                short axisY = reader.ReadInt16();
                ushort link = reader.ReadUInt16();
                byte format = reader.ReadByte();
                byte colorDepth = reader.ReadByte();
                uint dataOffset = reader.ReadUInt32();
                uint dataSize = reader.ReadUInt32();
                ushort paletteIndex = reader.ReadUInt16();
                ushort flags = reader.ReadUInt16();

                if (dataSize > int.MaxValue)
                {
                    throw new SffException("SFF v2 sprite data length exceeds supported size at index " + index);
                }
                long absoluteOffset = AddOffset((flags & 1) == 0 ? ldataOffset : tdataOffset,
                    dataOffset, "sprite data");
                if (dataSize > 0)
                {
                    EnsureRange(stream, absoluteOffset, dataSize, "sprite data at index " + index);
                }

                file.Sprites.Add(new SffV2Sprite
                {
                    Group = group,
                    Number = number,
                    Width = width,
                    Height = height,
                    AxisX = axisX,
                    AxisY = axisY,
                    Format = format,
                    ColDepth = colorDepth,
                    PalIndex = paletteIndex,
                    LinkedIndex = link,
                    DataOffset = absoluteOffset,
                    DataLength = (int)dataSize,
                });
            }
        }

        public static PcxImage Decode(string path, SffV2File file, SffV2Sprite sprite)
        {
            if (file == null || sprite == null)
            {
                throw new SffException("SFF v2 file and sprite are required");
            }
            SffV2Sprite pixelSprite = ResolvePixelSprite(file, sprite);
            int paletteIndex = sprite.PalIndex;
            byte[] palette = paletteIndex >= 0 && paletteIndex < file.Palettes.Count
                ? file.Palettes[paletteIndex]
                : new byte[768];

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return Decode(stream, pixelSprite, palette);
            }
        }

        static SffV2Sprite ResolvePixelSprite(SffV2File file, SffV2Sprite sprite)
        {
            HashSet<int> chain = new HashSet<int>();
            SffV2Sprite current = sprite;
            while (current.DataLength == 0)
            {
                int currentIndex = file.Sprites.IndexOf(current);
                if (currentIndex >= 0 && !chain.Add(currentIndex))
                {
                    throw new SffException("SFF v2 sprite link cycle at index " + currentIndex);
                }
                if (current.LinkedIndex < 0 || current.LinkedIndex >= file.Sprites.Count)
                {
                    throw new SffException("SFF v2 sprite link is out of range: " + current.LinkedIndex);
                }
                current = file.Sprites[current.LinkedIndex];
            }
            return current;
        }

        static PcxImage Decode(Stream stream, SffV2Sprite sprite, byte[] palette)
        {
            int pixelCount = CheckedPixelCount(sprite.Width, sprite.Height);
            EnsureRange(stream, sprite.DataOffset, sprite.DataLength, "sprite data");
            stream.Seek(sprite.DataOffset, SeekOrigin.Begin);

            if (sprite.Format == 0)
            {
                if (sprite.ColDepth != 8)
                {
                    throw new SffException("Unsupported raw SFF v2 color depth " + sprite.ColDepth);
                }
                if (sprite.DataLength != pixelCount)
                {
                    throw new SffException("Raw SFF v2 sprite length does not match its dimensions");
                }
                return Indexed(sprite.Width, sprite.Height,
                    ReadExact(stream, pixelCount, "raw sprite data"), palette);
            }

            if (sprite.DataLength < 4)
            {
                throw new SffException("Compressed SFF v2 sprite is missing its length prefix");
            }
            uint declaredLength = ReadUInt32LittleEndian(ReadExact(stream, 4, "sprite length prefix"), 0);
            byte[] encoded = ReadExact(stream, sprite.DataLength - 4, "encoded sprite data");

            if (sprite.Format >= 2 && sprite.Format <= 4)
            {
                if (declaredLength != pixelCount)
                {
                    throw new SffException("SFF v2 decoded length " + declaredLength
                        + " does not match sprite dimensions " + pixelCount);
                }
                byte[] indices;
                switch (sprite.Format)
                {
                    case 2: indices = Rle8Decode(encoded, pixelCount); break;
                    case 3: indices = Rle5Decode(encoded, pixelCount); break;
                    default: indices = Lz5Decode(encoded, pixelCount); break;
                }
                return Indexed(sprite.Width, sprite.Height, indices, palette);
            }

            if (sprite.Format == 10 || sprite.Format == 11 || sprite.Format == 12)
            {
                PngDecoder.Image png = PngDecoder.Decode(encoded);
                if (declaredLength != 0 && declaredLength != png.Width * png.Height)
                {
                    throw new SffException("SFF v2 PNG length prefix does not match PNG dimensions");
                }
                if (sprite.Format == 10)
                {
                    if (png.ColorType != 3 || png.Indices == null)
                    {
                        throw new SffException("SFF v2 PNG format 10 must contain indexed pixels");
                    }
                    return Indexed(png.Width, png.Height, png.Indices, palette);
                }
                return new PcxImage
                {
                    Width = png.Width,
                    Height = png.Height,
                    Indices = null,
                    Palette = null,
                    Rgba = png.Rgba,
                };
            }

            throw new SffException("Unknown SFF v2 pixel format " + sprite.Format);
        }

        static PcxImage Indexed(int width, int height, byte[] indices, byte[] palette)
        {
            return new PcxImage
            {
                Width = width,
                Height = height,
                Indices = indices,
                Palette = palette ?? new byte[768],
                Rgba = null,
            };
        }

        static byte[] Rle8Decode(byte[] source, int pixelCount)
        {
            byte[] output = new byte[pixelCount];
            int input = 0;
            int outputIndex = 0;
            while (outputIndex < output.Length)
            {
                byte value = Next(source, ref input, "RLE8 token");
                int count = 1;
                if ((value & 0xc0) == 0x40)
                {
                    count = value & 0x3f;
                    if (count == 0)
                    {
                        throw new SffException("RLE8 contains a zero-length run");
                    }
                    value = Next(source, ref input, "RLE8 run value");
                }
                EnsureOutput(count, outputIndex, output.Length, "RLE8 run");
                for (int i = 0; i < count; i++)
                {
                    output[outputIndex++] = value;
                }
            }
            return output;
        }

        static byte[] Rle5Decode(byte[] source, int pixelCount)
        {
            byte[] output = new byte[pixelCount];
            int input = 0;
            int outputIndex = 0;
            while (outputIndex < output.Length)
            {
                int runLength = Next(source, ref input, "RLE5 run length");
                byte packet = Next(source, ref input, "RLE5 packet");
                int dataLength = packet & 0x7f;
                byte color = 0;
                if ((packet & 0x80) != 0)
                {
                    color = Next(source, ref input, "RLE5 run color");
                }

                while (true)
                {
                    EnsureOutput(1, outputIndex, output.Length, "RLE5 output");
                    output[outputIndex++] = color;
                    runLength--;
                    if (runLength < 0)
                    {
                        dataLength--;
                        if (dataLength < 0)
                        {
                            break;
                        }
                        byte data = Next(source, ref input, "RLE5 data");
                        color = (byte)(data & 0x1f);
                        runLength = data >> 5;
                    }
                }
            }
            return output;
        }

        static byte[] Lz5Decode(byte[] source, int pixelCount)
        {
            byte[] output = new byte[pixelCount];
            int input = 0;
            int outputIndex = 0;
            byte control = Next(source, ref input, "LZ5 control byte");
            int controlBit = 0;
            byte recycleBits = 0;
            int recycleCount = 0;

            while (outputIndex < output.Length)
            {
                int value = Next(source, ref input, "LZ5 token");
                if ((control & (1 << controlBit)) != 0)
                {
                    int distance;
                    int count;
                    if ((value & 0x3f) == 0)
                    {
                        distance = ((value << 2) | Next(source, ref input, "LZ5 long distance")) + 1;
                        count = Next(source, ref input, "LZ5 long count") + 3;
                    }
                    else
                    {
                        recycleBits |= (byte)((value & 0xc0) >> recycleCount);
                        recycleCount += 2;
                        count = (value & 0x3f) + 1;
                        if (recycleCount < 8)
                        {
                            distance = Next(source, ref input, "LZ5 short distance") + 1;
                        }
                        else
                        {
                            distance = recycleBits + 1;
                            recycleBits = 0;
                            recycleCount = 0;
                        }
                    }
                    if (distance <= 0 || distance > outputIndex)
                    {
                        throw new SffException("LZ5 back-reference is outside decoded data");
                    }
                    EnsureOutput(count, outputIndex, output.Length, "LZ5 back-reference");
                    for (int i = 0; i < count; i++)
                    {
                        output[outputIndex] = output[outputIndex - distance];
                        outputIndex++;
                    }
                }
                else
                {
                    int count;
                    if ((value & 0xe0) == 0)
                    {
                        count = Next(source, ref input, "LZ5 long run") + 8;
                    }
                    else
                    {
                        count = value >> 5;
                        value &= 0x1f;
                    }
                    EnsureOutput(count, outputIndex, output.Length, "LZ5 run");
                    for (int i = 0; i < count; i++)
                    {
                        output[outputIndex++] = (byte)value;
                    }
                }

                controlBit++;
                if (controlBit == 8 && outputIndex < output.Length)
                {
                    control = Next(source, ref input, "LZ5 control byte");
                    controlBit = 0;
                }
            }
            return output;
        }

        static byte Next(byte[] source, ref int index, string context)
        {
            if (index >= source.Length)
            {
                throw new SffException(context + " is truncated");
            }
            return source[index++];
        }

        static void EnsureOutput(int count, int offset, int length, string context)
        {
            if (count < 0 || offset > length - count)
            {
                throw new SffException(context + " exceeds sprite dimensions");
            }
        }

        static int CheckedPixelCount(int width, int height)
        {
            if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
            {
                throw new SffException("Invalid SFF v2 sprite dimensions " + width + "x" + height);
            }
            return width * height;
        }

        static long AddOffset(uint baseOffset, uint relativeOffset, string context)
        {
            long result = (long)baseOffset + relativeOffset;
            if (result < 0)
            {
                throw new SffException(context + " offset overflow");
            }
            return result;
        }

        static void EnsureTableRange(Stream stream, uint offset, uint count, int itemSize, string context)
        {
            long size = (long)count * itemSize;
            EnsureRange(stream, offset, size, context);
        }

        static void EnsureRange(Stream stream, long offset, long count, string context)
        {
            if (offset < 0 || count < 0 || offset > stream.Length - count)
            {
                throw new SffException(context + " is outside the file");
            }
        }

        static byte[] ReadExact(Stream stream, int count, string context)
        {
            if (count < 0)
            {
                throw new SffException(context + " has an invalid length");
            }
            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int current = stream.Read(buffer, read, count - read);
                if (current <= 0)
                {
                    throw new SffException(context + " is truncated");
                }
                read += current;
            }
            return buffer;
        }

        static uint ReadUInt32LittleEndian(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8)
                | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }
    }
}
