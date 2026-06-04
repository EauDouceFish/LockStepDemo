// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/input.go InputBuffer（updateInputTime 的有符号帧计数 + B/F 按朝向推导）。
// Adapted to fixed-point lockstep. 仅逻辑相关方向/按钮缓冲，供引擎硬编码基础动作（actionPrepare）读边沿。
// See Docs/动作系统_Ikemen移植.md.
using Lockstep.Core;

namespace Lockstep.Mugen.Command
{
    /// <summary>
    /// 输入边沿缓冲（移植 Ikemen InputBuffer）：每个方向/按钮一个有符号帧计数器——
    /// 按住第 N 帧 = +N、松开第 N 帧 = -N。引擎硬编码基础动作读 `Fb>0`(本帧持前进)、`Ub==1`(本帧刚按上) 等。
    /// 方向 B/F（后/前）由 L/R 按朝向推导；U/D、B/F 各做 SOCD 对消（同轴双向同按 → 中立）。
    /// 与命令匹配用的 <see cref="MCommandBuffer"/>（存历史帧位）互补：本类只存"持续多久"的边沿计数。
    /// </summary>
    public sealed class MInputBuffer
    {
        // 当前帧计数（>0 持续按住帧数；<0 持续松开帧数）。对齐 Ikemen Bb/Db/Fb/Ub/Lb/Rb/Nb。
        public int Bb, Db, Fb, Ub, Lb, Rb, Nb;
        // 按钮计数（MInput 含 A/B/C/X/Y/Z/S）。
        public int Ab, Bbtn, Cb, Xb, Yb, Zb, Sb;
        // 上一帧计数（previous，供 Bp/Fp… 边沿差分用；当前硬编码键只用 ==1 判定，但保留以对齐 Ikemen）。
        public int Bp, Dp, Fp, Up, Lp, Rp, Np;

        /// <summary>
        /// 推进一帧。input=本帧原始输入位；facingRight=角色是否面向右（决定 B/F 与 L/R 的对应）。
        /// 对齐 Ikemen：facing 右时 B=Left、F=Right；facing 左时 B=Right、F=Left。
        /// </summary>
        public void Update(MInput input, bool facingRight)
        {
            bool up = (input & MInput.Up) != 0;
            bool down = (input & MInput.Down) != 0;
            bool left = (input & MInput.Left) != 0;
            bool right = (input & MInput.Right) != 0;

            // SOCD 对消：同轴双向同时按下 → 视为都未按（中立）。对齐 Ikemen SocdResolution 的中立解。
            if (up && down) { up = false; down = false; }
            if (left && right) { left = false; right = false; }

            // 后/前由 左/右 按朝向推导。
            bool back = facingRight ? left : right;
            bool fwd = facingRight ? right : left;

            // 保存上一帧（在更新前）。
            Up = Ub; Dp = Db; Lp = Lb; Rp = Rb; Bp = Bb; Fp = Fb; Np = Nb;

            Step(up, ref Ub);
            Step(down, ref Db);
            Step(left, ref Lb);
            Step(right, ref Rb);
            Step(back, ref Bb);
            Step(fwd, ref Fb);

            bool noDir = !(up || down || left || right || back || fwd);
            Step(noDir, ref Nb);

            Step((input & MInput.A) != 0, ref Ab);
            Step((input & MInput.B) != 0, ref Bbtn);
            Step((input & MInput.C) != 0, ref Cb);
            Step((input & MInput.X) != 0, ref Xb);
            Step((input & MInput.Y) != 0, ref Yb);
            Step((input & MInput.Z) != 0, ref Zb);
            Step((input & MInput.S) != 0, ref Sb);
        }

        // 计数推进（移植 Ikemen updateInputTime 内 update 闭包）：状态翻转则重置为 ±1，否则同向 ±1。
        static void Step(bool held, ref int buffer)
        {
            if (held != (buffer > 0))
            {
                buffer = held ? 1 : -1;
                return;
            }
            buffer += held ? 1 : -1;
        }

        public MInputBuffer Clone()
        {
            return new MInputBuffer
            {
                Bb = Bb, Db = Db, Fb = Fb, Ub = Ub, Lb = Lb, Rb = Rb, Nb = Nb,
                Ab = Ab, Bbtn = Bbtn, Cb = Cb, Xb = Xb, Yb = Yb, Zb = Zb, Sb = Sb,
                Bp = Bp, Dp = Dp, Fp = Fp, Up = Up, Lp = Lp, Rp = Rp, Np = Np,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(Bb); hash.AddInt32(Db); hash.AddInt32(Fb); hash.AddInt32(Ub);
            hash.AddInt32(Lb); hash.AddInt32(Rb); hash.AddInt32(Nb);
            hash.AddInt32(Ab); hash.AddInt32(Bbtn); hash.AddInt32(Cb); hash.AddInt32(Xb);
            hash.AddInt32(Yb); hash.AddInt32(Zb); hash.AddInt32(Sb);
            hash.AddInt32(Bp); hash.AddInt32(Dp); hash.AddInt32(Fp); hash.AddInt32(Up);
            hash.AddInt32(Lp); hash.AddInt32(Rp); hash.AddInt32(Np);
        }
    }
}
