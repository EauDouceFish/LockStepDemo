using NUnit.Framework;
using Lockstep.Game.Data;
using Lockstep.Math;

namespace Lockstep.Tests
{
    /// <summary>T1.4：Clsn 框 → 世界 AABB 与重叠判定验收（含 facing 镜像）。</summary>
    [TestFixture]
    public sealed class ClsnWorldTests
    {
        static ClsnBox Box(int x1, int y1, int x2, int y2)
        {
            return new ClsnBox(FFloat.FromInt(x1), FFloat.FromInt(y1), FFloat.FromInt(x2), FFloat.FromInt(y2));
        }

        [Test]
        public void FacingRight_NoMirror()
        {
            RectAabb r = ClsnWorld.ToWorld(Box(-8, -23, 6, 1), FFloat.Zero, FFloat.Zero, FFloat.One);
            Assert.That(r.MinX.Raw, Is.EqualTo(FFloat.FromInt(-8).Raw));
            Assert.That(r.MaxX.Raw, Is.EqualTo(FFloat.FromInt(6).Raw));
            Assert.That(r.MinY.Raw, Is.EqualTo(FFloat.FromInt(-23).Raw));
            Assert.That(r.MaxY.Raw, Is.EqualTo(FFloat.FromInt(1).Raw));
        }

        [Test]
        public void FacingLeft_MirrorsX()
        {
            RectAabb r = ClsnWorld.ToWorld(Box(-8, -23, 6, 1), FFloat.Zero, FFloat.Zero, FFloat.MinusOne);
            // x: -8→8, 6→-6 → min -6, max 8
            Assert.That(r.MinX.Raw, Is.EqualTo(FFloat.FromInt(-6).Raw));
            Assert.That(r.MaxX.Raw, Is.EqualTo(FFloat.FromInt(8).Raw));
        }

        [Test]
        public void Origin_OffsetsBox()
        {
            RectAabb r = ClsnWorld.ToWorld(Box(0, 0, 2, 2), FFloat.FromInt(10), FFloat.FromInt(5), FFloat.One);
            Assert.That(r.MinX.Raw, Is.EqualTo(FFloat.FromInt(10).Raw));
            Assert.That(r.MaxX.Raw, Is.EqualTo(FFloat.FromInt(12).Raw));
            Assert.That(r.MinY.Raw, Is.EqualTo(FFloat.FromInt(5).Raw));
            Assert.That(r.MaxY.Raw, Is.EqualTo(FFloat.FromInt(7).Raw));
        }

        [Test]
        public void Overlap_DetectsHitAndMiss()
        {
            RectAabb a = ClsnWorld.ToWorld(Box(0, 0, 4, 4), FFloat.Zero, FFloat.Zero, FFloat.One);
            RectAabb hit = ClsnWorld.ToWorld(Box(0, 0, 4, 4), FFloat.FromInt(2), FFloat.Zero, FFloat.One);
            RectAabb miss = ClsnWorld.ToWorld(Box(0, 0, 4, 4), FFloat.FromInt(100), FFloat.Zero, FFloat.One);

            Assert.IsTrue(ClsnWorld.Overlap(a, hit));
            Assert.IsFalse(ClsnWorld.Overlap(a, miss));
        }
    }
}
