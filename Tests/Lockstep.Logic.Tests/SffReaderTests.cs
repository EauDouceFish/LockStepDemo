using System.IO;
using NUnit.Framework;
using Lockstep.Import.Sff;

namespace Lockstep.Tests
{
    /// <summary>T1.2：SFF v1 目录 + PCX 解码验收（真实 Terrarian.sff）。</summary>
    [TestFixture]
    public sealed class SffReaderTests
    {
        [Test]
        public void RealTerrarian_DirectoryParses()
        {
            string path = TestAssets.Sff();
            if (!File.Exists(path))
            {
                Assert.Ignore("MugenSource/Terrarian.sff 不在，跳过");
            }

            SffFile sff = SffV1Reader.ReadDirectory(path);

            // 来自头部：187 组 / 1935 图（见执行日志记录的头字节）
            Assert.That(sff.NumGroups, Is.EqualTo(187));
            Assert.That(sff.NumImages, Is.EqualTo(1935));
            // 链表应读出与 NumImages 一致的节点数
            Assert.That(sff.Nodes.Count, Is.EqualTo(1935));

            // 每个节点的数据偏移应在文件范围内
            long fileLen = new FileInfo(path).Length;
            foreach (SffNode node in sff.Nodes)
            {
                Assert.That(node.DataOffset, Is.LessThanOrEqualTo(fileLen));
            }
        }

        [Test]
        public void RealTerrarian_FirstNonEmptySprite_DecodesPcx()
        {
            string path = TestAssets.Sff();
            if (!File.Exists(path))
            {
                Assert.Ignore("MugenSource/Terrarian.sff 不在，跳过");
            }

            SffFile sff = SffV1Reader.ReadDirectory(path);

            SffNode target = null;
            foreach (SffNode node in sff.Nodes)
            {
                if (node.DataLength > 0)
                {
                    target = node;
                    break;
                }
            }
            Assert.That(target, Is.Not.Null, "应有至少一个非复用精灵");

            byte[] pcx = SffV1Reader.ReadNodeData(path, target);
            PcxImage image = PcxDecoder.Decode(pcx);

            Assert.That(image.Width, Is.GreaterThan(0));
            Assert.That(image.Height, Is.GreaterThan(0));
            Assert.That(image.Indices.Length, Is.EqualTo(image.Width * image.Height));
        }
    }
}
