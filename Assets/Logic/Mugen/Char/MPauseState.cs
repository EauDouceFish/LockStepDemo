// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/system.go (sys.pausetime/supertime + buffer 应用, action() 2562-2575) +
//         char.go:8920 setPauseTime / 8941 setSuperPauseTime（buffer/playerno 优先级机制）。
// 把 Ikemen 的全局 sys.pausetime/supertime 重新安置为引擎持有、角色共享引用的对象（去全局单例，可入快照/哈希）。
// Adapted to fixed-point lockstep. See Docs/移植方案_Ikemen.md.
using Lockstep.Core;

namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// 全局暂停状态（= Ikemen sys.pausetime/supertime 及其 buffer/playerno）。引擎持单例、各角色共享引用。
    /// 时长递减与 buffer 应用在 <see cref="Step"/>（对齐 system.go action() 开头）；
    /// 控制器经 <see cref="MChar.SetPause"/>/<see cref="MChar.SetSuperPause"/> 写 buffer（对齐 char.go setPauseTime）。
    /// </summary>
    public sealed class MPauseState
    {
        public int PauseTime;          // sys.pausetime
        public int SuperTime;          // sys.supertime
        public int PauseTimeBuffer;    // sys.pausetimebuffer（持 ^pausetime，下一帧 Step 应用）
        public int SuperTimeBuffer;    // sys.supertimebuffer
        public int PausePlayerNo = -1; // sys.pauseplayerno（谁设置了暂停）
        public int SuperPlayerNo = -1; // sys.superplayerno

        /// <summary>SuperPause 优先于 Pause。任一 > 0 即处于暂停。</summary>
        public bool AnyActive => SuperTime > 0 || PauseTime > 0;

        /// <summary>每帧开头推进（移植 system.go:2562-2575）：先递减当前时长，再应用 buffer 的 ^ 标志。</summary>
        public void Step()
        {
            if (SuperTime > 0)
            {
                SuperTime--;
            }
            else if (PauseTime > 0)
            {
                PauseTime--;
            }
            if (SuperTimeBuffer < 0)
            {
                SuperTimeBuffer = ~SuperTimeBuffer;   // ^ 还原为正的 pausetime
                SuperTime = SuperTimeBuffer;
            }
            if (PauseTimeBuffer < 0)
            {
                PauseTimeBuffer = ~PauseTimeBuffer;
                PauseTime = PauseTimeBuffer;
            }
        }

        public MPauseState Clone()
        {
            return new MPauseState
            {
                PauseTime = PauseTime, SuperTime = SuperTime,
                PauseTimeBuffer = PauseTimeBuffer, SuperTimeBuffer = SuperTimeBuffer,
                PausePlayerNo = PausePlayerNo, SuperPlayerNo = SuperPlayerNo,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(PauseTime); hash.AddInt32(SuperTime);
            hash.AddInt32(PauseTimeBuffer); hash.AddInt32(SuperTimeBuffer);
            hash.AddInt32(PausePlayerNo); hash.AddInt32(SuperPlayerNo);
        }
    }
}
