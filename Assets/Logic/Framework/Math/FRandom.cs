using System;

namespace Lockstep.Math
{
    // 确定性随机数：xorshift64*。种子完全由 World 控制，回滚时随快照恢复。
    // 严禁在逻辑层使用 UnityEngine.Random / System.Random。
    [Serializable]
    public class FRandom
    {
        public ulong Seed;

        public FRandom(ulong seed)
        {
            // 0 是 xorshift 的不动点，需要避免
            Seed = seed == 0 ? 0xDEADBEEFCAFEBABEUL : seed;
        }

        public ulong NextU64()
        {
            Seed ^= Seed << 13;
            Seed ^= Seed >> 7;
            Seed ^= Seed << 17;
            return Seed;
        }

        public uint NextU32() => (uint)(NextU64() >> 32);

        // [min, max)
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            uint r = NextU32();
            return min + (int)(r % (uint)(max - min));
        }

        // [min, max)
        public FFloat Range(FFloat min, FFloat max)
        {
            // 取 31 位随机映射到 [0,1) 范围的 raw
            long raw = (long)(NextU32() & 0x7FFFFFFFu);
            // Fix64 的 1.0 = 1 << 32；这里映射到 [0, 1) 用 raw / (1<<31) 不到 1
            // 简化：用 raw * 2 当作 [0, 1) 内的 raw 值
            FFloat t = FFloat.FromRaw(raw * 2);
            return min + (max - min) * t;
        }

        public bool NextBool() => (NextU32() & 1u) == 1u;
    }
}
