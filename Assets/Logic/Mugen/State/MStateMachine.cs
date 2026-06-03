// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go actionRun + bytecode.go StateBlock.Run — 定点版。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Mugen.Char;

namespace Lockstep.Mugen.State
{
    /// <summary>
    /// 状态机每帧推进（移植 Ikemen actionRun 主干）：
    /// 1) 外部强制切换(命中等写 PendingStateNo) → 2) 负状态 -3/-2/-1 每帧跑(命令/通用逻辑) →
    /// 3) 应用负状态触发的切换 → 4) 当前状态(trigger 过则执行控制器, ChangeState 同帧重入) → 5) Time++。
    /// hitpause(Hitstop>0)：递减 Hitstop、Time 冻结、仅 IgnoreHitPause 控制器执行。无静态可变状态。
    /// 状态查找支持 common states 回退（角色自身状态覆盖 common）。
    /// </summary>
    public sealed class MStateMachine
    {
        const int MaxReentry = 16;   // 对应 Ikemen MaxLoop

        public void RunFrame(MChar c, IReadOnlyDictionary<int, MStateDef> states)
        {
            RunFrame(c, states, null);
        }

        public void RunFrame(MChar c, IReadOnlyDictionary<int, MStateDef> states,
            IReadOnlyDictionary<int, MStateDef> commonStates)
        {
            bool hitpause = c.Hitstop > 0;
            if (hitpause)
            {
                c.Hitstop--;
            }

            // AssertSpecial 标志每帧清空：须本帧重新断言才保持（对齐 MUGEN 每 tick 清）
            c.AssertFlags = 0;

            // 外部强制切换（命中系统等）优先
            if (c.PendingStateNo >= 0)
            {
                ApplyTransition(c, states, commonStates);
            }

            // 负状态每帧跑：-3(自身状态归属)、-2(通用)、-1(命令)。各自跑控制器，ChangeState 缓冲到 PendingStateNo。
            RunNegativeState(c, -3, states, commonStates, hitpause);
            RunNegativeState(c, -2, states, commonStates, hitpause);
            RunNegativeState(c, -1, states, commonStates, hitpause);
            if (c.PendingStateNo >= 0)
            {
                ApplyTransition(c, states, commonStates);
            }

            RunCurrentState(c, states, commonStates, hitpause);

            if (!hitpause)
            {
                c.Time++;
            }
        }

        // 负状态：跑一遍控制器（不做同帧重入；ChangeState 只缓冲），命中即停。
        void RunNegativeState(MChar c, int no, IReadOnlyDictionary<int, MStateDef> states,
            IReadOnlyDictionary<int, MStateDef> commonStates, bool hitpause)
        {
            MStateDef def = Lookup(no, states, commonStates);
            if (def == null)
            {
                return;
            }
            for (int i = 0; i < def.Controllers.Count; i++)
            {
                MStateController ctrl = def.Controllers[i];
                if (hitpause && !ctrl.IgnoreHitPause)
                {
                    continue;
                }
                if (!ctrl.TriggerPasses(c))
                {
                    continue;
                }
                if (ctrl.Run(c) && c.PendingStateNo >= 0)
                {
                    break;   // 负状态触发切换后停止本负状态后续控制器
                }
            }
        }

        void RunCurrentState(MChar c, IReadOnlyDictionary<int, MStateDef> states,
            IReadOnlyDictionary<int, MStateDef> commonStates, bool hitpause)
        {
            int reentry = 0;
            while (reentry < MaxReentry)
            {
                MStateDef def = Lookup(c.StateNo, states, commonStates);
                if (def == null)
                {
                    return;
                }
                bool changed = false;
                for (int i = 0; i < def.Controllers.Count; i++)
                {
                    MStateController ctrl = def.Controllers[i];
                    if (hitpause && !ctrl.IgnoreHitPause)
                    {
                        continue;   // hitpause 期间跳过非 ignorehitpause 控制器
                    }
                    if (!ctrl.TriggerPasses(c))
                    {
                        continue;
                    }
                    if (!PersistGate(c, i, ctrl.Persistent))
                    {
                        continue;   // persistent 节流：本触发帧不执行
                    }
                    if (ctrl.Run(c) && c.PendingStateNo >= 0)
                    {
                        ApplyTransition(c, states, commonStates);
                        changed = true;
                        break;      // 切状态后从新状态头部重入
                    }
                }
                if (!changed)
                {
                    return;
                }
                reentry++;
            }
        }

        // persistent 门控：1=每帧执行；0=进状态后仅一次；N=每 N 次触发帧。计数仅作用当前状态。
        static bool PersistGate(MChar c, int ctrlIndex, int persistent)
        {
            if (persistent == 1)
            {
                return true;
            }
            c.PersistCounters.TryGetValue(ctrlIndex, out int fired);
            bool run = persistent <= 0 ? fired == 0 : (fired % persistent) == 0;
            c.PersistCounters[ctrlIndex] = fired + 1;   // 触发帧计数（无论是否执行）
            return run;
        }

        // 对应 changeStateEx + stateChange：设新状态号、time=0、清 persistent 计数、应用 statedef 头部。
        static void ApplyTransition(MChar c, IReadOnlyDictionary<int, MStateDef> states,
            IReadOnlyDictionary<int, MStateDef> commonStates)
        {
            int target = c.PendingStateNo;
            c.PendingStateNo = -1;
            c.PendingIsSelf = false;
            c.PrevStateNo = c.StateNo;
            c.StateNo = target;
            c.Time = 0;
            c.PersistCounters.Clear();

            MStateDef def = Lookup(target, states, commonStates);
            if (def == null)
            {
                return;
            }
            if (def.StateType >= 0) { c.StateType = def.StateType; }
            if (def.MoveType >= 0) { c.MoveType = def.MoveType; }
            if (def.Physics >= 0) { c.Physics = def.Physics; }
            if (def.Ctrl >= 0) { c.Ctrl = def.Ctrl != 0; }
            if (def.Anim >= 0) { c.AnimNo = def.Anim; }
        }

        // 状态查找：先查角色自身状态表，未命中再回退 common states（common1.cns 共享状态）。
        static MStateDef Lookup(int no, IReadOnlyDictionary<int, MStateDef> states,
            IReadOnlyDictionary<int, MStateDef> commonStates)
        {
            if (states != null && states.TryGetValue(no, out MStateDef def))
            {
                return def;
            }
            if (commonStates != null && commonStates.TryGetValue(no, out MStateDef common))
            {
                return common;
            }
            return null;
        }
    }
}
