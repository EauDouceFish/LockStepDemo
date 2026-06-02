using Lockstep.Core;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 搓招用输入历史：逐帧记录"朝向相对方向(InputSymbol 0-8)"+"按钮 bitmask"，供 CommandSystem 做序列匹配。
    /// 与 InputBufferC(仅按钮、6 帧、用于 cancel 容错)不同：这里存方向且更长(覆盖整条指令的时间窗)。
    /// 方向已在写入时按 FacingX 转成相对(Fwd/Back)，故匹配逻辑与朝向无关。
    /// 环形缓冲：Cursor 指向下一个写入位；最近一帧在 (Cursor-1)。
    /// </summary>
    public sealed class CommandInputC : IComponent
    {
        public const int Capacity = 60;

        public byte[] Dir;       // 每帧方向（InputSymbol 值 0-8）
        public byte[] Btn;       // 每帧按钮 bitmask（InputButton）
        public int Cursor;

        public CommandInputC()
        {
            Dir = new byte[Capacity];
            Btn = new byte[Capacity];
            Cursor = 0;
        }

        public void Push(byte direction, byte buttons)
        {
            Dir[Cursor] = direction;
            Btn[Cursor] = buttons;
            Cursor = (Cursor + 1) % Capacity;
        }

        /// <summary>取最近 frames 帧的方向（按时间从旧到新写入 outDir/outBtn，返回实际帧数）。</summary>
        public int ReadRecent(int frames, byte[] outDir, byte[] outBtn)
        {
            int count = frames > Capacity ? Capacity : frames;
            for (int offset = 0; offset < count; offset++)
            {
                // 最新在 Cursor-1；按时间正序填充：最旧在 outDir[0]
                int index = (Cursor - count + offset + Capacity) % Capacity;
                outDir[offset] = Dir[index];
                outBtn[offset] = Btn[index];
            }
            return count;
        }

        public IComponent Clone()
        {
            CommandInputC clone = new CommandInputC { Cursor = Cursor };
            for (int i = 0; i < Capacity; i++)
            {
                clone.Dir[i] = Dir[i];
                clone.Btn[i] = Btn[i];
            }
            return clone;
        }

        public void WriteHash(ref Hash64 hash)
        {
            for (int i = 0; i < Capacity; i++)
            {
                hash.AddInt32(Dir[i]);
                hash.AddInt32(Btn[i]);
            }
            hash.AddInt32(Cursor);
        }
    }
}
