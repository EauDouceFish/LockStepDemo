using System.IO;
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
    }
}
