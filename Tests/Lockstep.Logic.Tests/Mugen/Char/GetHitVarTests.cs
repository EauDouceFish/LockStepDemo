using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M3：GetHitVar 受击变量容器——深拷隔离 + 哈希敏感（回滚快照需要）。</summary>
    [TestFixture]
    public sealed class GetHitVarTests
    {
        static ulong HashOf(MChar c)
        {
            Hash64 h = new Hash64();
            c.WriteHash(ref h);
            return h.Value;
        }

        [Test]
        public void Clone_DeepCopiesGhv()
        {
            MChar c = new MChar();
            c.Ghv.HitTime = 12;
            c.Ghv.XVel = FFloat.FromInt(3);
            c.Ghv.Fall = true;

            MChar clone = c.Clone();
            // 改原 Char 的 Ghv 不应影响克隆
            c.Ghv.HitTime = 99;
            c.Ghv.XVel = FFloat.Zero;
            c.Ghv.Fall = false;

            Assert.That(clone.Ghv.HitTime, Is.EqualTo(12), "Ghv 应深拷");
            Assert.That(clone.Ghv.XVel.Raw, Is.EqualTo(FFloat.FromInt(3).Raw));
            Assert.IsTrue(clone.Ghv.Fall);
        }

        [Test]
        public void WriteHash_SensitiveToGhv()
        {
            MChar a = new MChar();
            MChar b = new MChar();
            Assert.That(HashOf(a), Is.EqualTo(HashOf(b)), "初始相同");

            b.Ghv.HitTime = 7;
            Assert.That(HashOf(a), Is.Not.EqualTo(HashOf(b)), "Ghv 变化应改哈希");
        }
    }
}
