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
    /// 命令 runtime 直接读取本类；<see cref="MCommandBuffer"/> 仅保留给历史 API 和调试工具。
    /// </summary>
    public sealed class MInputBuffer
    {
        // 当前帧计数（>0 持续按住帧数；<0 持续松开帧数）。对齐 Ikemen Bb/Db/Fb/Ub/Lb/Rb/Nb。
        public int Bb, Db, Fb, Ub, Lb, Rb, Nb;
        // 按钮计数（MInput 含 A/B/C/X/Y/Z/S）。
        public int Ab, Bbtn, Cb, Xb, Yb, Zb, Sb;
        // 上一帧计数（previous，供 Bp/Fp… 边沿差分用；当前硬编码键只用 ==1 判定，但保留以对齐 Ikemen）。
        public int Bp, Dp, Fp, Up, Lp, Rp, Np;
        public int Ap, Bbtnp, Cp, Xp, Yp, Zp, Sp;

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
            Ap = Ab; Bbtnp = Bbtn; Cp = Cb; Xp = Xb; Yp = Yb; Zp = Zb; Sp = Sb;

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

        /// <summary>返回 Ikemen Command.Step 使用的键状态：1=本帧边沿，>0=持续帧数。</summary>
        internal int State(MCommandKey key)
        {
            if (key.IsButton)
            {
                int current = ButtonCurrent(key.Bits);
                int previous = ButtonPrevious(key.Bits);
                return key.Release && (current < 0 || previous > 0) ? -current : key.Release ? 0 : current;
            }
            if (key.IsNeutral)
            {
                return key.Release ? -Nb : Nb;
            }

            if (key.Dollar)
            {
                return DollarState(key);
            }

            int strict = StrictDirectionState(key, previous: false);
            if (!key.Release)
            {
                return strict;
            }
            return DirectionReleaseEligible(key) ? -strict : 0;
        }

        /// <summary>返回蓄力帧数；release 键读取释放前一帧的持续时间。</summary>
        internal int StateCharge(MCommandKey key)
        {
            if (key.IsButton)
            {
                return key.Release ? ButtonPrevious(key.Bits) : ButtonCurrent(key.Bits);
            }
            if (key.IsNeutral)
            {
                return key.Release ? Np : Nb;
            }
            if (key.Dollar)
            {
                return DirectionComponentMinimum(key, key.Release);
            }
            int strict = StrictDirectionState(key, key.Release);
            return strict > 0 ? strict : 0;
        }

        int DollarState(MCommandKey key)
        {
            if (key.Release)
            {
                if (!DirectionReleaseEligible(key)) return 0;
                int current = DirectionComponentMinimum(key, previous: false);
                return current < 0 ? -current : MinimumRelevantDirectionAge(key);
            }
            if (DirectionHeld(key, previous: false))
            {
                return MinimumRelevantDirectionAge(key);
            }
            return 0;
        }

        int StrictDirectionState(MCommandKey key, bool previous)
        {
            int u = previous ? Up : Ub;
            int d = previous ? Dp : Db;
            int l = previous ? Lp : Lb;
            int r = previous ? Rp : Rb;
            int b = previous ? Bp : Bb;
            int f = previous ? Fp : Fb;

            if (key.IsBack && (key.Bits & MInput.Up) != 0) return Min(-Max(d, f), Min(u, b));
            if (key.IsFwd && (key.Bits & MInput.Up) != 0) return Min(-Max(d, b), Min(u, f));
            if (key.IsBack && (key.Bits & MInput.Down) != 0) return Min(-Max(u, f), Min(d, b));
            if (key.IsFwd && (key.Bits & MInput.Down) != 0) return Min(-Max(u, b), Min(d, f));
            if ((key.Bits & (MInput.Up | MInput.Left)) == (MInput.Up | MInput.Left)) return Min(-Max(d, r), Min(u, l));
            if ((key.Bits & (MInput.Up | MInput.Right)) == (MInput.Up | MInput.Right)) return Min(-Max(d, l), Min(u, r));
            if ((key.Bits & (MInput.Down | MInput.Left)) == (MInput.Down | MInput.Left)) return Min(-Max(u, r), Min(d, l));
            if ((key.Bits & (MInput.Down | MInput.Right)) == (MInput.Down | MInput.Right)) return Min(-Max(u, l), Min(d, r));
            if (key.IsBack) return Min(-Max(d, Max(u, f)), b);
            if (key.IsFwd) return Min(-Max(d, Max(u, b)), f);
            if (key.Bits == MInput.Up) return Min(-Max(b, Max(d, f)), u);
            if (key.Bits == MInput.Down) return Min(-Max(b, Max(u, f)), d);
            if (key.Bits == MInput.Left) return Min(-Max(d, Max(u, r)), l);
            if (key.Bits == MInput.Right) return Min(-Max(d, Max(u, l)), r);
            return 0;
        }

        bool DirectionReleaseEligible(MCommandKey key)
        {
            if (key.IsBack && !Released(Bb, Bp)) return false;
            if (key.IsFwd && !Released(Fb, Fp)) return false;
            if ((key.Bits & MInput.Up) != 0 && !Released(Ub, Up)) return false;
            if ((key.Bits & MInput.Down) != 0 && !Released(Db, Dp)) return false;
            if ((key.Bits & MInput.Left) != 0 && !Released(Lb, Lp)) return false;
            if ((key.Bits & MInput.Right) != 0 && !Released(Rb, Rp)) return false;
            return true;
        }

        bool DirectionHeld(MCommandKey key, bool previous)
        {
            int u = previous ? Up : Ub;
            int d = previous ? Dp : Db;
            int l = previous ? Lp : Lb;
            int r = previous ? Rp : Rb;
            int b = previous ? Bp : Bb;
            int f = previous ? Fp : Fb;
            if (key.IsBack && b <= 0) return false;
            if (key.IsFwd && f <= 0) return false;
            if ((key.Bits & MInput.Up) != 0 && u <= 0) return false;
            if ((key.Bits & MInput.Down) != 0 && d <= 0) return false;
            if ((key.Bits & MInput.Left) != 0 && l <= 0) return false;
            if ((key.Bits & MInput.Right) != 0 && r <= 0) return false;
            return key.IsBack || key.IsFwd || (key.Bits & MInput.DirMask) != 0;
        }

        int DirectionComponentMinimum(MCommandKey key, bool previous)
        {
            int result = int.MaxValue;
            if (key.IsBack) result = Min(result, previous ? Bp : Bb);
            if (key.IsFwd) result = Min(result, previous ? Fp : Fb);
            if ((key.Bits & MInput.Up) != 0) result = Min(result, previous ? Up : Ub);
            if ((key.Bits & MInput.Down) != 0) result = Min(result, previous ? Dp : Db);
            if ((key.Bits & MInput.Left) != 0) result = Min(result, previous ? Lp : Lb);
            if ((key.Bits & MInput.Right) != 0) result = Min(result, previous ? Rp : Rb);
            return result == int.MaxValue ? 0 : result;
        }

        int MinimumRelevantDirectionAge(MCommandKey key)
        {
            if ((key.Bits & (MInput.Left | MInput.Right)) != 0)
            {
                return Min(System.Math.Abs(Ub), Min(System.Math.Abs(Db), Min(System.Math.Abs(Lb), System.Math.Abs(Rb))));
            }
            return Min(System.Math.Abs(Ub), Min(System.Math.Abs(Db), Min(System.Math.Abs(Bb), System.Math.Abs(Fb))));
        }

        int ButtonCurrent(MInput bit)
        {
            if (bit == MInput.A) return Ab;
            if (bit == MInput.B) return Bbtn;
            if (bit == MInput.C) return Cb;
            if (bit == MInput.X) return Xb;
            if (bit == MInput.Y) return Yb;
            if (bit == MInput.Z) return Zb;
            if (bit == MInput.S) return Sb;
            return 0;
        }

        int ButtonPrevious(MInput bit)
        {
            if (bit == MInput.A) return Ap;
            if (bit == MInput.B) return Bbtnp;
            if (bit == MInput.C) return Cp;
            if (bit == MInput.X) return Xp;
            if (bit == MInput.Y) return Yp;
            if (bit == MInput.Z) return Zp;
            if (bit == MInput.S) return Sp;
            return 0;
        }

        static bool Released(int current, int previous) => current < 0 || previous > 0;
        static int Min(int a, int b) => a < b ? a : b;
        static int Max(int a, int b) => a > b ? a : b;

        public MInputBuffer Clone()
        {
            return new MInputBuffer
            {
                Bb = Bb, Db = Db, Fb = Fb, Ub = Ub, Lb = Lb, Rb = Rb, Nb = Nb,
                Ab = Ab, Bbtn = Bbtn, Cb = Cb, Xb = Xb, Yb = Yb, Zb = Zb, Sb = Sb,
                Bp = Bp, Dp = Dp, Fp = Fp, Up = Up, Lp = Lp, Rp = Rp, Np = Np,
                Ap = Ap, Bbtnp = Bbtnp, Cp = Cp, Xp = Xp, Yp = Yp, Zp = Zp, Sp = Sp,
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
            hash.AddInt32(Ap); hash.AddInt32(Bbtnp); hash.AddInt32(Cp); hash.AddInt32(Xp);
            hash.AddInt32(Yp); hash.AddInt32(Zp); hash.AddInt32(Sp);
        }
    }
}
