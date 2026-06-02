using Lockstep.Math;

namespace Lockstep.Core
{
    /// <summary>
    /// 确定性 64 位滚动哈希（FNV-1a）。用于给 World / 组件状态算"指纹"，跨端比对不同步。
    ///
    /// Why 自己写而不用 object.GetHashCode：GetHashCode 不保证跨运行时 / 跨平台稳定，
    /// 而帧同步要求"同样的逻辑状态在任意机器上得到同样的哈希"。这里只吃整数 raw 值
    /// （定点数的 long raw、int、枚举底层值），并按固定的小端字节序混入，结果完全确定。
    ///
    /// 是 struct：调用链上以 ref 传递，避免每帧装箱 / 分配。
    /// </summary>
    public struct Hash64
    {
        const ulong FnvOffsetBasis = 14695981039346656037UL;
        const ulong FnvPrime = 1099511628211UL;

        ulong _value;

        public ulong Value => _value;

        public static Hash64 Create()
        {
            return new Hash64 { _value = FnvOffsetBasis };
        }

        public void AddUInt64(ulong word)
        {
            // 按小端逐字节混入：不依赖运行平台的内存字节序，保证跨平台一致
            for (int shift = 0; shift < 64; shift += 8)
            {
                _value ^= (word >> shift) & 0xFF;
                _value *= FnvPrime;
            }
        }

        public void AddInt64(long word)
        {
            AddUInt64((ulong)word);
        }

        public void AddInt32(int value)
        {
            AddUInt64((ulong)(uint)value);
        }

        public void AddBool(bool value)
        {
            AddUInt64(value ? 1UL : 0UL);
        }

        public void AddFixed(FFloat value)
        {
            AddInt64(value.Raw);
        }

        public void AddFixed(FVector3 value)
        {
            AddInt64(value.X.Raw);
            AddInt64(value.Y.Raw);
            AddInt64(value.Z.Raw);
        }

        /// <summary>混入字符串（按 UTF-16 码元，跨平台确定）。用于把组件类型名并入哈希。</summary>
        public void AddString(string text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                AddUInt64(text[index]);
            }
        }
    }
}
