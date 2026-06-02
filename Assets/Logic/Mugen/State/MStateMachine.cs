// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go changeStateEx + bytecode.go StateBlock.Run — 定点 MVP 版。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Mugen.Char;

namespace Lockstep.Mugen.State
{
    /// <summary>
    /// 状态机运行：每帧跑当前状态的控制器（trigger 过则执行），ChangeState 立即切换并同帧从头重入
    /// （有界次数防死循环，对应 Ikemen changeStateEx 的 for c.stchtmp 循环）。最后 Time++。
    /// hitstop>0 时冻结（不跑状态、不 Time++），对应打击感停顿。无静态可变状态。
    /// </summary>
    public sealed class MStateMachine
    {
        const int MaxReentry = 16;   // 对应 Ikemen MaxLoop

        public void RunFrame(MChar c, IReadOnlyDictionary<int, MStateDef> states)
        {
            if (c.Hitstop > 0)
            {
                c.Hitstop--;
                return;
            }

            // 跨实体强制切换（命中系统等写入 PendingStateNo）优先应用
            if (c.PendingStateNo >= 0)
            {
                ApplyTransition(c, states);
            }

            RunCurrentState(c, states);
            c.Time++;
        }

        void RunCurrentState(MChar c, IReadOnlyDictionary<int, MStateDef> states)
        {
            int reentry = 0;
            while (reentry < MaxReentry)
            {
                if (!states.TryGetValue(c.StateNo, out MStateDef def))
                {
                    return;
                }
                bool changed = false;
                for (int i = 0; i < def.Controllers.Count; i++)
                {
                    MStateController ctrl = def.Controllers[i];
                    if (!ctrl.TriggerPasses(c))
                    {
                        continue;
                    }
                    if (ctrl.Run(c) && c.PendingStateNo >= 0)
                    {
                        ApplyTransition(c, states);
                        changed = true;
                        break;   // 切状态后停止本状态后续控制器，从新状态头部重入
                    }
                }
                if (!changed)
                {
                    return;
                }
                reentry++;
            }
        }

        // 对应 changeStateEx + stateChange：设新状态号、time=0、应用 statedef 头部
        static void ApplyTransition(MChar c, IReadOnlyDictionary<int, MStateDef> states)
        {
            int target = c.PendingStateNo;
            c.PendingStateNo = -1;
            c.PrevStateNo = c.StateNo;
            c.StateNo = target;
            c.Time = 0;

            if (!states.TryGetValue(target, out MStateDef def))
            {
                return;
            }
            if (def.StateType >= 0) { c.StateType = def.StateType; }
            if (def.MoveType >= 0) { c.MoveType = def.MoveType; }
            if (def.Physics >= 0) { c.Physics = def.Physics; }
            if (def.Ctrl >= 0) { c.Ctrl = def.Ctrl != 0; }
            if (def.Anim >= 0) { c.AnimNo = def.Anim; }
        }
    }
}
