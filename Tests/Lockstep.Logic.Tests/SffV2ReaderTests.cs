using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using Lockstep.Import.Sff;

namespace Lockstep.Tests
{
    /// <summary>SFF v2 目录 + Lz5/调色板解码验收（真实 kfm.sff）。</summary>
    [TestFixture]
    public sealed class SffV2ReaderTests
    {
        [Test]
        public void RealKfm_DirectoryParses()
        {
            string path = TestAssets.KfmSff();
            if (!File.Exists(path))
            {
                Assert.Ignore("MugenSource/kfm/kfm.sff 不在，跳过");
            }

            SffV2File sff = SffV2Reader.ReadDirectory(path);

            // 来自头部：281 精灵 / 13 调色板（python 预探得）
            Assert.That(sff.NumberOfSprites, Is.EqualTo(281));
            Assert.That(sff.NumberOfPalettes, Is.EqualTo(13));
            Assert.That(sff.Sprites.Count, Is.EqualTo(281));
            Assert.That(sff.Palettes.Count, Is.EqualTo(13));
            // 每个调色板都是 768 字节 RGB
            foreach (byte[] palette in sff.Palettes)
            {
                Assert.That(palette.Length, Is.EqualTo(768));
            }
        }

        [Test]
        public void RealKfm_FirstLz5Sprite_Decodes()
        {
            string path = TestAssets.KfmSff();
            if (!File.Exists(path))
            {
                Assert.Ignore("MugenSource/kfm/kfm.sff 不在，跳过");
            }

            SffV2File sff = SffV2Reader.ReadDirectory(path);

            SffV2Sprite target = null;
            foreach (SffV2Sprite sprite in sff.Sprites)
            {
                if (sprite.Format == 4 && sprite.DataLength > 0)   // Lz5
                {
                    target = sprite;
                    break;
                }
            }
            Assert.That(target, Is.Not.Null, "kfm 应有 Lz5 精灵");

            PcxImage image = SffV2Reader.Decode(path, sff, target);

            Assert.That(image.Width, Is.GreaterThan(0));
            Assert.That(image.Height, Is.GreaterThan(0));
            Assert.That(image.Indices.Length, Is.EqualTo(image.Width * image.Height));
            Assert.That(image.Palette.Length, Is.EqualTo(768));
        }

        [Test]
        public void RealKfm_AllNonPngSprites_DecodeToCorrectSize()
        {
            string path = TestAssets.KfmSff();
            if (!File.Exists(path))
            {
                Assert.Ignore("MugenSource/kfm/kfm.sff 不在，跳过");
            }

            SffV2File sff = SffV2Reader.ReadDirectory(path);

            int decoded = 0;
            foreach (SffV2Sprite sprite in sff.Sprites)
            {
                // 跳过 PNG(头像)与纯空链接精灵
                SffV2Sprite pixelSprite = sprite;
                if (sprite.DataLength == 0 && sprite.LinkedIndex < sff.Sprites.Count)
                {
                    pixelSprite = sff.Sprites[sprite.LinkedIndex];
                }
                if (pixelSprite.DataLength == 0 || pixelSprite.Format >= 10)
                {
                    continue;
                }

                PcxImage image = SffV2Reader.Decode(path, sff, sprite);
                Assert.That(image.Indices.Length, Is.EqualTo(image.Width * image.Height),
                    "精灵 " + sprite.Group + "," + sprite.Number + " 解码尺寸不符");
                decoded++;
            }
            // 应解出大量精灵（kfm 有 ~239 个 Lz5）
            Assert.That(decoded, Is.GreaterThan(200));
        }

        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        public void PngFormats_DecodeWithoutUnity(int format)
        {
            byte colorType = format == 10 ? (byte)3 : format == 11 ? (byte)2 : (byte)6;
            byte[] scanline = format == 10
                ? new byte[] { 0, 1 }
                : format == 11
                    ? new byte[] { 10, 20, 30, 40, 50, 60 }
                    : new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            byte[] pngPalette = format == 10 ? new byte[] { 0, 0, 0, 255, 0, 0 } : null;
            byte[] png = BuildPng(2, 1, colorType, scanline, pngPalette);
            byte[] payload = PrefixLength(png, 2);

            string path = WriteTemporary(payload);
            try
            {
                SffV2File file = new SffV2File();
                file.Palettes.Add(new byte[768]);
                SffV2Sprite sprite = Sprite(format, 2, 1, 0, payload.Length);
                file.Sprites.Add(sprite);

                PcxImage image = SffV2Reader.Decode(path, file, sprite);

                Assert.That(image.Width, Is.EqualTo(2));
                Assert.That(image.Height, Is.EqualTo(1));
                if (format == 10)
                {
                    Assert.That(image.IsTrueColor, Is.False);
                    Assert.That(image.Indices, Is.EqualTo(new byte[] { 0, 1 }));
                }
                else
                {
                    Assert.That(image.IsTrueColor, Is.True);
                    Assert.That(image.Rgba.Length, Is.EqualTo(8));
                    Assert.That(image.Rgba[0], Is.EqualTo(10));
                    Assert.That(image.Rgba[3], Is.EqualTo(format == 11 ? 255 : 40));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void LinkedSpriteChain_ResolvesLinkedToLinked()
        {
            string path = WriteTemporary(new byte[] { 7, 8 });
            try
            {
                SffV2File file = new SffV2File();
                file.Palettes.Add(new byte[768]);
                SffV2Sprite pixels = Sprite(0, 2, 1, 0, 2);
                SffV2Sprite firstLink = Sprite(0, 0, 0, 0, 0);
                firstLink.LinkedIndex = 0;
                SffV2Sprite secondLink = Sprite(0, 0, 0, 0, 0);
                secondLink.LinkedIndex = 1;
                file.Sprites.Add(pixels);
                file.Sprites.Add(firstLink);
                file.Sprites.Add(secondLink);

                PcxImage image = SffV2Reader.Decode(path, file, secondLink);

                Assert.That(image.Indices, Is.EqualTo(new byte[] { 7, 8 }));
                Assert.That(image.Width, Is.EqualTo(2));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void LinkedSpriteCycle_IsRejected()
        {
            string path = WriteTemporary(new byte[0]);
            try
            {
                SffV2File file = new SffV2File();
                SffV2Sprite a = Sprite(0, 0, 0, 0, 0);
                SffV2Sprite b = Sprite(0, 0, 0, 0, 0);
                a.LinkedIndex = 1;
                b.LinkedIndex = 0;
                file.Sprites.Add(a);
                file.Sprites.Add(b);

                Assert.Throws<SffException>(() => SffV2Reader.Decode(path, file, a));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void TruncatedSpriteData_IsRejectedInsteadOfZeroFilled()
        {
            string path = WriteTemporary(new byte[] { 7 });
            try
            {
                SffV2File file = new SffV2File();
                file.Palettes.Add(new byte[768]);
                SffV2Sprite sprite = Sprite(0, 2, 1, 0, 2);
                file.Sprites.Add(sprite);

                Assert.Throws<SffException>(() => SffV2Reader.Decode(path, file, sprite));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void TruncatedDirectory_IsRejected()
        {
            Assert.Throws<SffException>(() => SffV2Reader.ReadDirectory(new MemoryStream(new byte[20])));
        }

        static SffV2Sprite Sprite(int format, int width, int height, long offset, int length)
        {
            return new SffV2Sprite
            {
                Width = width,
                Height = height,
                Format = format,
                ColDepth = 8,
                PalIndex = 0,
                DataOffset = offset,
                DataLength = length,
            };
        }

        static byte[] PrefixLength(byte[] data, int pixelCount)
        {
            byte[] result = new byte[data.Length + 4];
            result[0] = (byte)pixelCount;
            Buffer.BlockCopy(data, 0, result, 4, data.Length);
            return result;
        }

        static byte[] BuildPng(int width, int height, byte colorType, byte[] scanline, byte[] palette)
        {
            using (MemoryStream output = new MemoryStream())
            {
                output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                byte[] header = new byte[13];
                WriteBigEndian(header, 0, width);
                WriteBigEndian(header, 4, height);
                header[8] = 8;
                header[9] = colorType;
                WriteChunk(output, "IHDR", header);
                if (palette != null)
                {
                    WriteChunk(output, "PLTE", palette);
                }

                byte[] filtered = new byte[scanline.Length + 1];
                Buffer.BlockCopy(scanline, 0, filtered, 1, scanline.Length);
                byte[] compressed;
                using (MemoryStream compressedStream = new MemoryStream())
                {
                    using (ZLibStream zlib = new ZLibStream(compressedStream, CompressionLevel.Optimal, true))
                    {
                        zlib.Write(filtered, 0, filtered.Length);
                    }
                    compressed = compressedStream.ToArray();
                }
                WriteChunk(output, "IDAT", compressed);
                WriteChunk(output, "IEND", new byte[0]);
                return output.ToArray();
            }
        }

        static void WriteChunk(Stream output, string type, byte[] data)
        {
            byte[] length = new byte[4];
            WriteBigEndian(length, 0, data.Length);
            output.Write(length, 0, length.Length);
            byte[] name = Encoding.ASCII.GetBytes(type);
            output.Write(name, 0, name.Length);
            output.Write(data, 0, data.Length);
            output.Write(new byte[4], 0, 4); // Decoder validates bounds; CRC is outside this import slice.
        }

        static void WriteBigEndian(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }

        static string WriteTemporary(byte[] data)
        {
            string path = Path.GetTempFileName();
            File.WriteAllBytes(path, data);
            return path;
        }
    }
}
