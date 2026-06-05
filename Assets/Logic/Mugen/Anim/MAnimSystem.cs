// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/anim.go (Animation.Action 推进 + AnimTime/AnimElemNo 取值)。
// Adapted to fixed-point. 动画运行态存于 MChar(便于快照/回滚)，本系统为纯逻辑无状态推进器。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Mugen.Char;

namespace Lockstep.Mugen.Anim
{
    /// <summary>
    /// 动画推进（移植 Ikemen Animation.Action）。每帧对 MChar 当前动画推进一 tick：
    /// 更新 curelem/curelemtime/curtime（存于 MChar 的 AnimElem/AnimElemTime/AnimCurTime），
    /// 派生 trigger 量 AnimElemNo(1-based)/AnimTime(=curtime-totaltime, 惯例≤0)，并填当前帧 Clsn。
    /// 动画号变化（ChangeAnim/statedef anim=）由本系统自检（AnimNo!=AnimRunningNo→重置），
    /// 无需控制器侧介入。无静态可变状态：全部运行态写回 MChar。
    /// </summary>
    public static class MAnimSystem
    {
        /// <summary>推进一 tick。table 为角色动画表（动画号→MAnimData）。无当前动画则安全跳过。</summary>
        public static void Action(MChar c, IReadOnlyDictionary<int, MAnimData> table)
        {
            if (table == null || !table.TryGetValue(c.AnimNo, out MAnimData a) || a == null || a.Frames == null)
            {
                return;
            }
            if (a.Frames.Length == 0)
            {
                c.AnimLoopEnd = true;
                return;
            }

            // 动画号变化 → 重置运行态到新动画头部（对齐 Ikemen ChangeAnim，本 tick 仍照常推进一格）。
            if (c.AnimNo != c.AnimRunningNo)
            {
                Reinit(c, a);
            }

            // ───── 移植 Ikemen Animation.Action（UpdateSprite 为纯表现，逻辑层略） ─────
            if (a.Frames[c.AnimElem].Time <= 0)
            {
                Next(c, a);
            }
            if (c.AnimElem < a.Frames.Length)
            {
                c.AnimElemTime++;
                if (c.AnimElemTime >= a.Frames[c.AnimElem].Time)
                {
                    Next(c, a);
                    if (c.AnimElem >= a.Frames.Length)
                    {
                        c.AnimElem = a.LoopStart;
                    }
                }
            }
            else
            {
                c.AnimElem = a.LoopStart;
            }
            if (a.TotalTime != -1 && c.AnimCurTime >= a.TotalTime)
            {
                c.AnimCurTime = a.TotalTime - a.LoopTime;
            }
            c.AnimCurTime++;
            if (a.TotalTime != -1 && c.AnimCurTime >= a.TotalTime)
            {
                c.AnimLoopEnd = true;
            }

            DeriveAndPopulate(c, a);
        }

        /// <summary>把角色切到指定动画并复位运行态（不推进）。供加载初始化或显式置帧用。</summary>
        public static void Play(MChar c, int animNo, IReadOnlyDictionary<int, MAnimData> table)
        {
            PlayAt(c, animNo, table, 0, 0);
        }

        /// <summary>切到指定动画的指定元素/元素时间，并立即刷新 AnimTime/AnimElem/Clsn（不推进）。</summary>
        public static void PlayAt(MChar c, int animNo, IReadOnlyDictionary<int, MAnimData> table, int elem, int elemTime)
        {
            c.PrevAnimNo = c.AnimNo;
            c.AnimNo = animNo;
            if (table != null && table.TryGetValue(animNo, out MAnimData a) && a != null && a.Frames != null && a.Frames.Length > 0)
            {
                c.AnimRunningNo = animNo;
                c.AnimElem = Clamp(elem, 0, a.Frames.Length - 1);
                c.AnimElemTime = elemTime < 0 ? 0 : elemTime;
                c.AnimCurTime = SumTimeBefore(a, c.AnimElem) + c.AnimElemTime;
                c.AnimLoopEnd = false;
                DeriveAndPopulate(c, a);
            }
            else
            {
                c.AnimRunningNo = animNo;
            }
        }

        static int SumTimeBefore(MAnimData anim, int elem)
        {
            int time = 0;
            for (int i = 0; i < elem && i < anim.Frames.Length; i++)
            {
                int frameTime = anim.Frames[i].Time;
                if (frameTime > 0)
                {
                    time += frameTime;
                }
            }
            return time;
        }

        static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            return value > max ? max : value;
        }

        // 元素前进（移植 Ikemen Action 内 next 闭包）：跳过 0 时长帧；永久动画停在末元素。
        static void Next(MChar c, MAnimData a)
        {
            if (a.TotalTime != -1 || c.AnimElem < a.Frames.Length - 1)
            {
                c.AnimElemTime = 0;
                while (true)
                {
                    c.AnimElem++;
                    if (a.TotalTime == -1 && c.AnimElem == a.Frames.Length - 1
                        || c.AnimElem >= a.Frames.Length
                        || a.Frames[c.AnimElem].Time > 0)
                    {
                        break;
                    }
                }
            }
        }

        static void Reinit(MChar c, MAnimData a)
        {
            c.AnimRunningNo = c.AnimNo;
            c.AnimElem = 0;
            c.AnimElemTime = 0;
            c.AnimCurTime = 0;
            c.AnimLoopEnd = false;
        }

        // 派生 trigger 量 + 填当前帧 Clsn（攻击框 Clsn1 / 受击框 Clsn2）。
        static void DeriveAndPopulate(MChar c, MAnimData a)
        {
            int elem = c.AnimElem;
            if (elem < 0)
            {
                elem = 0;
            }
            else if (elem >= a.Frames.Length)
            {
                elem = a.Frames.Length - 1;
            }
            c.AnimElemNo = elem + 1;                 // 1-based（对齐 Ikemen AnimElemNo 简单情形）
            c.AnimTime = c.AnimCurTime - a.TotalTime;   // 惯例 ≤0（末帧到时为 0）
            MAnimFrame frame = a.Frames[elem];
            if (frame == null)
            {
                c.Clsn1 = null;
                c.Clsn2 = null;
                return;
            }
            c.Clsn1 = frame.Clsn1;
            c.Clsn2 = frame.Clsn2;
        }
    }
}
