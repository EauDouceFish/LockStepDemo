using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Lockstep.Import.Air;
using Lockstep.Game.Data;
using Lockstep.Math;

namespace Lockstep.Tests
{
    /// <summary>T1.1：AIR 解析器验收（合成样例 + 真实 Terrarian.air）。</summary>
    [TestFixture]
    public sealed class AirParserTests
    {
        const string SampleAir =
            "; comment\n" +
            "[Begin Action 0]\n" +
            "Clsn2Default: 1\n" +
            "  Clsn2[0] = -8, -23, 6, 1\n" +
            "0,0, 0,0, 120\n" +
            "0,1, 0,0, 2\n" +
            "\n" +
            "[Begin Action 20]\n" +
            "Clsn2Default: 1\n" +
            "  Clsn2[0] = -7, -23, 7, 0\n" +
            "20,0, 0,0, 4,H\n" +
            "20,1, 0,0, 4,H\n";

        [Test]
        public void Sample_ParsesTwoActions_WithFramesAndClsn()
        {
            List<AnimData> anims = AirParser.Parse(SampleAir);
            Assert.That(anims.Count, Is.EqualTo(2));

            AnimData a0 = anims[0];
            Assert.That(a0.Id, Is.EqualTo(0));
            Assert.That(a0.Frames.Length, Is.EqualTo(2));
            Assert.That(a0.Frames[0].SpriteGroup, Is.EqualTo(0));
            Assert.That(a0.Frames[0].Duration, Is.EqualTo(120));
            Assert.That(a0.Frames[1].Duration, Is.EqualTo(2));

            // Clsn2 默认框作用到该 action 所有帧
            Assert.That(a0.Frames[0].Clsn2.Length, Is.EqualTo(1));
            Assert.That(a0.Frames[1].Clsn2.Length, Is.EqualTo(1));
            ClsnBox box = a0.Frames[0].Clsn2[0];
            Assert.That(box.X1.Raw, Is.EqualTo(FFloat.FromInt(-8).Raw));
            Assert.That(box.Y1.Raw, Is.EqualTo(FFloat.FromInt(-23).Raw));
            Assert.That(box.X2.Raw, Is.EqualTo(FFloat.FromInt(6).Raw));
            Assert.That(box.Y2.Raw, Is.EqualTo(FFloat.FromInt(1).Raw));
        }

        [Test]
        public void Sample_ParsesFlipFlag()
        {
            List<AnimData> anims = AirParser.Parse(SampleAir);
            AnimData a20 = anims[1];
            Assert.That(a20.Id, Is.EqualTo(20));
            Assert.That(a20.Frames.Length, Is.EqualTo(2));
            Assert.That((a20.Frames[0].Flip & FlipFlags.Horizontal), Is.EqualTo(FlipFlags.Horizontal));
        }

        [Test]
        public void RealTerrarian_HasExpectedActions()
        {
            string path = TestAssets.Air();
            if (!File.Exists(path))
            {
                Assert.Ignore("MugenSource/Terrarian.air 不在，跳过真实文件测试");
            }

            Dictionary<int, AnimData> anims = AirParser.ToDictionary(AirParser.ParseFile(path));

            Assert.That(anims.ContainsKey(0), "应含站立动画 Action 0");
            Assert.That(anims.ContainsKey(20), "应含走路动画 Action 20");

            // 站立首帧 dur=120（来自真实文件）
            Assert.That(anims[0].Frames[0].Duration, Is.EqualTo(120));
            // 走路 13 帧，每帧 dur=4
            Assert.That(anims[20].Frames.Length, Is.EqualTo(13));
            Assert.That(anims[20].Frames[0].Duration, Is.EqualTo(4));
            // 每帧都有受击框
            Assert.That(anims[20].Frames[0].Clsn2.Length, Is.GreaterThan(0));
        }

        [Test]
        public void CopyAction_ResolvesMultiLevelChainAndPreservesDestinationId()
        {
            const string air =
                "[Begin Action 10]\nCopy Action 20\n" +
                "[Begin Action 20]\nCopy Action 30\n" +
                "[Begin Action 30]\n30,4, 5,6, 7,H\n";

            Dictionary<int, AnimData> anims = AirParser.ToDictionary(AirParser.Parse(air));

            Assert.That(anims[10].Id, Is.EqualTo(10));
            Assert.That(anims[10].Frames.Length, Is.EqualTo(1));
            Assert.That(anims[10].Frames[0].SpriteGroup, Is.EqualTo(30));
            Assert.That(anims[10].Frames[0].SpriteImage, Is.EqualTo(4));
            Assert.That(anims[10].Frames[0].Duration, Is.EqualTo(7));
        }

        [Test]
        public void CopyAction_MissingTargetAndCycleAreRemoved()
        {
            const string air =
                "[Begin Action 1]\nCopy Action 99\n" +
                "[Begin Action 2]\nCopy Action 3\n" +
                "[Begin Action 3]\nCopy Action 2\n" +
                "[Begin Action 4]\n4,0,0,0,1\n";

            Dictionary<int, AnimData> anims = AirParser.ToDictionary(AirParser.Parse(air));

            Assert.That(anims.ContainsKey(1), Is.False);
            Assert.That(anims.ContainsKey(2), Is.False);
            Assert.That(anims.ContainsKey(3), Is.False);
            Assert.That(anims.ContainsKey(4), Is.True);
        }

        [Test]
        public void DuplicateAction_FirstDefinitionWinsEvenWhenFirstIsCopy()
        {
            const string air =
                "[Begin Action 5]\nCopy Action 6\n" +
                "[Begin Action 5]\n5,9,0,0,1\n" +
                "[Begin Action 6]\n6,1,0,0,2\n";

            List<AnimData> parsed = AirParser.Parse(air);
            Dictionary<int, AnimData> anims = AirParser.ToDictionary(parsed);

            Assert.That(parsed.Count, Is.EqualTo(2));
            Assert.That(anims[5].Frames[0].SpriteGroup, Is.EqualTo(6));
            Assert.That(anims[5].Frames[0].SpriteImage, Is.EqualTo(1));
        }

        [Test]
        public void EmptyAction_CopiesNextActionLikeIkemenReadAction()
        {
            const string air =
                "[Begin Action 7]\n" +
                "[Begin Action 8]\n8,2,0,0,3\n";

            Dictionary<int, AnimData> anims = AirParser.ToDictionary(AirParser.Parse(air));

            Assert.That(anims[7].Frames.Length, Is.EqualTo(1));
            Assert.That(anims[7].Frames[0].SpriteGroup, Is.EqualTo(8));
            Assert.That(anims[8].Frames.Length, Is.EqualTo(1));
        }

        [Test]
        public void NonActionSection_DoesNotCreateActionZero()
        {
            const string air = "[Info]\nfoo = bar\n[Begin Action 9]\n9,0,0,0,1\n";

            Dictionary<int, AnimData> anims = AirParser.ToDictionary(AirParser.Parse(air));

            Assert.That(anims.ContainsKey(0), Is.False);
            Assert.That(anims.ContainsKey(9), Is.True);
        }
    }
}
