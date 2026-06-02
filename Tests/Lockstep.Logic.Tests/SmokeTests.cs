using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Tests
{
    /// <summary>
    /// Phase 0 / T0.1 冒烟测试：证明测试底座能脱 Unity 跑通，并验证
    /// "定点确定性 + Hash64 可复现" —— 这是后续黄金哈希测试的基石。
    /// </summary>
    [TestFixture]
    public sealed class SmokeTests
    {
        [Test]
        public void FFloat_DivideThenMultiply_IsExactlyReversible()
        {
            FFloat threeHalves = FFloat.FromInt(3) / FFloat.Two;
            FFloat back = threeHalves * FFloat.Two;
            Assert.That(back.Raw, Is.EqualTo(FFloat.FromInt(3).Raw),
                "定点数 3/2*2 必须精确回到 3 的 raw 值");
        }

        [Test]
        public void Hash64_SameInputs_ProduceSameValue()
        {
            ulong first = ComputeSampleHash();
            ulong second = ComputeSampleHash();

            Assert.That(second, Is.EqualTo(first), "确定性哈希必须可复现（黄金测试的前提）");
            Assert.That(first, Is.Not.EqualTo(Hash64.Create().Value), "哈希应已混入数据，不等于初始值");
        }

        [Test]
        public void Hash64_DifferentInputs_ProduceDifferentValue()
        {
            Hash64 a = Hash64.Create();
            a.AddInt32(1);
            Hash64 b = Hash64.Create();
            b.AddInt32(2);

            Assert.That(a.Value, Is.Not.EqualTo(b.Value), "不同输入应得到不同哈希");
        }

        static ulong ComputeSampleHash()
        {
            Hash64 hash = Hash64.Create();
            hash.AddInt32(42);
            hash.AddFixed(FFloat.FromInt(3) / FFloat.Two);
            hash.AddString("Entity");
            return hash.Value;
        }
    }
}
