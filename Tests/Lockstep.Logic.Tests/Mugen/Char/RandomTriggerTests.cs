using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Char
{
    /// <summary>
    /// random trigger（用量 2411，全角色 AI/随机分支依赖）。修复前 OC_random 在编译器有映射但
    /// Char.ReadTrigger 不消费 → 恒返回 0 → 所有 `trigger = random &lt; N` 走死同一分支。
    /// 用 Ikemen common.go 的 Park-Miller 最小标准 LCG（纯整数 → 确定性、可哈希）1:1 实现。
    /// </summary>
    [TestFixture]
    public sealed class RandomTriggerTests
    {
        [Test]
        public void Random_Seed1_MatchesMinstdSequence()
        {
            // oracle: 经典 MINSTD（Ikemen common.go Random()）从种子 1 的序列。
            MRandom rng = new MRandom(1);
            Assert.That(rng.Random(), Is.EqualTo(16807));
            Assert.That(rng.Random(), Is.EqualTo(282475249));
            Assert.That(rng.Random(), Is.EqualTo(1622650073));
        }

        [Test]
        public void Rand0To999_Seed1_MatchesHandComputed()
        {
            // oracle: Rand(0,999) = Random()/(IMax/1000+1)，手算 0,131,755。
            MRandom rng = new MRandom(1);
            Assert.That(rng.Rand(0, 999), Is.EqualTo(0));
            Assert.That(rng.Rand(0, 999), Is.EqualTo(131));
            Assert.That(rng.Rand(0, 999), Is.EqualTo(755));
        }

        [Test]
        public void Rand_AlwaysInRange()
        {
            MRandom rng = new MRandom(12345);
            for (int i = 0; i < 2000; i++)
            {
                int value = rng.Rand(0, 999);
                Assert.That(value, Is.InRange(0, 999));
            }
        }

        [Test]
        public void Random_IsDeterministic_SameSeedSameSequence()
        {
            MRandom a = new MRandom(7);
            MRandom b = new MRandom(7);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(a.Random(), Is.EqualTo(b.Random()));
            }
        }

        [Test]
        public void ZeroSeed_NormalizedToNonZero()
        {
            // 种子 0 会让 LCG 卡死 → 归一到 1（对齐 Ikemen randseed<=0 修正逻辑的退化保护）。
            MRandom rng = new MRandom(0);
            Assert.That(rng.Random(), Is.Not.EqualTo(0));
        }

        // ───────── 集成：compiler 编 `random` → MChar.ReadTrigger 求值 ─────────

        [Test]
        public void Compiled_RandomTrigger_EvaluatesInRangeAndAdvances()
        {
            MugenExprCompiler compiler = new MugenExprCompiler();
            BytecodeExp exp = compiler.Compile("random");
            MChar c = new MChar { Rng = new MRandom(1) };

            int first = exp.Run(c).ToI();
            int second = exp.Run(c).ToI();
            Assert.That(first, Is.EqualTo(0), "种子1首个 random = 0");
            Assert.That(second, Is.EqualTo(131), "第二个 random = 131（序列推进）");
        }

        [Test]
        public void Compiled_RandomTrigger_NullRng_ReturnsZeroNotCrash()
        {
            MugenExprCompiler compiler = new MugenExprCompiler();
            BytecodeExp exp = compiler.Compile("random");
            MChar c = new MChar();   // 无 Rng
            Assert.That(exp.Run(c).ToI(), Is.EqualTo(0));
        }
    }
}
