using Lockstep.Core;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 滚动按键 buffer：解决"按键早一帧"容错 + 输入序列识别（v2 用于↓↘→+P 这类指令）。
    /// 每帧 InputBufferSystem 把当前帧 Buttons 写入 Cursor 位置并递增。
    /// 6 帧 = 200ms @ 30Hz，是格斗游戏行业默认 buffer 容量。
    /// </summary>
    public sealed class InputBufferC : IComponent
    {
        public const int Capacity = 6;

        public byte[] Buffer;
        public byte Cursor;

        public InputBufferC()
        {
            Buffer = new byte[Capacity];
            Cursor = 0;
        }

        /// <summary>
        /// 查询最近 frames 帧内，是否有任意一帧按下了 buttonMask 中的任一按键。
        /// frames 上限被钳到 Capacity。
        /// </summary>
        public bool PressedWithin(byte buttonMask, int frames)
        {
            int look = frames;
            if (look > Capacity)
            {
                look = Capacity;
            }
            for (int offset = 1; offset <= look; offset++)
            {
                int index = (Cursor - offset + Capacity) % Capacity;
                if ((Buffer[index] & buttonMask) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        public IComponent Clone()
        {
            byte[] copy = new byte[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                copy[i] = Buffer[i];
            }
            return new InputBufferC
            {
                Buffer = copy,
                Cursor = Cursor,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            for (int index = 0; index < Capacity; index++)
            {
                hash.AddInt32(Buffer[index]);
            }
            hash.AddInt32(Cursor);
        }

        public override string ToString()
        {
            return string.Format("IB[c{0}]", Cursor);
        }
    }
}
