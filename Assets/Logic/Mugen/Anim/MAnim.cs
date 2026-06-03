// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/anim.go (Animation/AnimFrame 数据 + totaltime/looptime/prelooptime 计算)。
// Adapted to fixed-point. 引擎自有动画数据（Clsn 用 MClsnBox，与命中系统一致，避免运行期转换）。
// 加载（AIR→MAnimData）见 MAnimImport；推进逻辑见 MAnimSystem。See Docs/移植方案_Ikemen.md.
using Lockstep.Mugen.Hit;

namespace Lockstep.Mugen.Anim
{
    /// <summary>动画一帧（≈ AIR 一行 + 其上的 Clsn 块）。静态数据，加载期产出，运行期只读。</summary>
    public sealed class MAnimFrame
    {
        public int SpriteGroup;     // → SFF group
        public int SpriteImage;     // → SFF image
        public Lockstep.Math.FFloat OffX;   // 绘制偏移（表现层用；逻辑只用 Clsn）
        public Lockstep.Math.FFloat OffY;
        public int Time;            // 持续 tick；-1 = 永久帧（对齐 Ikemen AnimFrame.Time）
        public int Flip;            // 翻转位（0 无 / 1 横 / 2 纵），表现层用
        public MClsnBox[] Clsn1;    // 攻击框，可为 null
        public MClsnBox[] Clsn2;    // 受击框，可为 null
    }

    /// <summary>一段动画（≈ AIR [Begin Action N]）。加载后调用 <see cref="ComputePacing"/> 预算节拍量。</summary>
    public sealed class MAnimData
    {
        public int No;
        public MAnimFrame[] Frames;
        public int LoopStart;       // 循环起始元素索引（AIR LoopStart）

        // 节拍量（移植 Ikemen anim.go ReadAnimation 末尾计算，供 AnimTime/循环回绕用）。
        public int TotalTime;       // 全部帧时长之和；末帧 Time=-1（永久）则为 -1
        public int LoopTime;        // loopstart 起到末尾的时长之和
        public int PreLoopTime;     // loopstart 之前的时长之和

        /// <summary>预算 TotalTime/LoopTime/PreLoopTime（移植 Ikemen anim.go 行 337-363）。加载后调用一次。</summary>
        public void ComputePacing()
        {
            TotalTime = 0;
            LoopTime = 0;
            PreLoopTime = 0;
            if (Frames == null || Frames.Length == 0)
            {
                return;
            }
            if (LoopStart < 0 || LoopStart >= Frames.Length)
            {
                LoopStart = 0;
            }
            if (Frames[Frames.Length - 1].Time == -1)
            {
                TotalTime = -1;
                return;
            }
            int tmp = 0;
            for (int i = 0; i < Frames.Length; i++)
            {
                int t = Frames[i].Time;
                if (t == -1)
                {
                    TotalTime = 0;
                    LoopTime = -tmp;
                    PreLoopTime = 0;
                }
                TotalTime += t;
                if (i < LoopStart)
                {
                    PreLoopTime += t;
                    tmp += t;
                }
                else
                {
                    LoopTime += t;
                }
            }
            if (TotalTime == -1)
            {
                PreLoopTime = 0;
            }
        }
    }
}
