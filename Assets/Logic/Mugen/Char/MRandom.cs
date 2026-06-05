// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/common.go Random()/Rand()（Park-Miller 最小标准 LCG，a=16807, m=2^31-1，Schrage 分解 q=127773, r=2836）。
// 纯整数运算 → 天然确定性、可入哈希；不用 Framework.FRandom（算法不同，无法与 Ikemen `random` trigger 位级对齐）。
// 种子是模拟状态：由 MBattleEngine 持有、每次 random 求值推进、纳入 ComputeHash。See Docs/移植方案_Ikemen.md.
namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// 确定性随机源，1:1 复刻 Ikemen GO 的全局 randseed RNG。`random` trigger = <see cref="Rand"/>(0,999)。
    /// Schrage 分解保证中间量不溢出 int32（与 Go int32 同），故 C# unchecked int 运算与 Ikemen 逐位一致。
    /// </summary>
    public sealed class MRandom
    {
        // IMax = int32 最大值 = ^uint32(0)>>1（common.go:23）。
        const int IMax = int.MaxValue;

        /// <summary>当前种子（= Ikemen sys.randseed）。模拟状态，纳入引擎哈希。</summary>
        public int Seed;

        public MRandom(int seed)
        {
            // 种子 0 会让 LCG 退化卡死，归一到 1（对齐 Random() 的 randseed<=0 修正语义）。
            Seed = seed == 0 ? 1 : seed;
        }

        /// <summary>推进种子并返回（移植 common.go:27 Random()）。</summary>
        public int Random()
        {
            int w = Seed / 127773;
            Seed = (Seed - w * 127773) * 16807 - w * 2836;
            if (Seed <= 0)
            {
                Seed += IMax - (Seed == 0 ? 1 : 0);
            }
            return Seed;
        }

        /// <summary>返回 [min, max] 内整数（移植 common.go:40 Rand）。</summary>
        public int Rand(int min, int max)
        {
            return min + Random() / (IMax / (max - min + 1) + 1);
        }
    }
}
