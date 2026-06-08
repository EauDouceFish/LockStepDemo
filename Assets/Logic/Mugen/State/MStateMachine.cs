// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go actionRun + bytecode.go StateBlock.Run — 定点版。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
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

            // HitBy/NotHitBy 免疫窗口每帧递减（hitpause 期间时间冻结，不递减）。归零即过滤失效。
            if (!hitpause && c.HitByTime > 0)
            {
                c.HitByTime--;
            }
            if (!hitpause)
            {
                UpdateHitOverrides(c);
            }

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
                UpdateGetHitTimers(c);
                c.Time++;
            }
        }

        // 受击计时逐帧更新（移植 Ikemen char.go:11775-11834，仅非 hitpause 帧）：
        // movetype=H 时 HitShakeTime 递减、fallflag 时 FallTime++；非 H 时清受击残留态；
        // 抖动结束(HitShakeTime<=0)后 HitTime 递减（<0 即 HitOver）；倒地起身计时(5110)递减。
        static void UpdateGetHitTimers(MChar c)
        {
            MGetHitVar ghv = c.Ghv;
            if (c.MoveType == 2)   // MT_H 受击中
            {
                if (ghv.HitShakeTime > 0)
                {
                    ghv.HitShakeTime--;
                }
                if (ghv.Fall)
                {
                    c.FallTime++;
                }
            }
            else
            {
                // 离开受击：清残留（对齐 Ikemen 非 MT_H 分支，避免 HitFall/HitShakeOver 滞留）
                ghv.HitShakeTime = 0;
                ghv.Fall = false;
                ghv.FallCount = 0;
                c.FallTime = 0;
                c.ReceivedHits = 0;   // 连段结束清零（char.go:11809）
                c.GuardCount = 0;     // char.go:11807
            }
            if (ghv.HitShakeTime <= 0 && ghv.HitTime >= 0)
            {
                ghv.HitTime--;
            }
            if (c.StateNo == 5110 && c.Alive)
            {
                if (ghv.DownRecoverTime <= 0 && c.Time == 0)
                {
                    ghv.DownRecoverTime = c.Constants != null ? c.Constants.LiedownTime : 60;
                }
                if (ghv.DownRecoverTime > 0)
                {
                    ghv.DownRecoverTime--;
                }
                if (ghv.DownRecoverTime <= 0)
                {
                    c.QueueTransition(5120, c.PlayerNo);
                }
            }
        }

        static void UpdateHitOverrides(MChar c)
        {
            for (int i = 0; i < c.HitOverrides.Length; i++)
            {
                if (c.HitOverrides[i].Time > 0)
                {
                    MHitOverride value = c.HitOverrides[i];
                    value.Time--;
                    c.HitOverrides[i] = value;
                }
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
                    continue;   // hitpause 期间不执行也不递减 persistent（对齐 Ikemen 在 persistent gate 前返回）
                }
                if (!PersistGate(c, no, i))
                {
                    continue;   // persistent 冷却中：本帧跳过（不求值 trigger）
                }
                if (!ctrl.TriggerPasses(c))
                {
                    continue;   // trigger 不过：返回但不重置冷却（对齐 Ikemen）
                }
                bool changed = ctrl.Run(c);
                ResetPersist(c, no, i, ctrl.Persistent);
                if (changed && c.PendingStateNo >= 0)
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
                MStateDef def = LookupForCurrentOwner(c, c.StateNo, states, commonStates);
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
                        continue;   // hitpause 期间跳过非 ignorehitpause 控制器（不递减 persistent）
                    }
                    if (!PersistGate(c, def.No, i))
                    {
                        continue;   // persistent 冷却中：本帧跳过（在 trigger 求值之前，对齐 Ikemen）
                    }
                    if (!ctrl.TriggerPasses(c))
                    {
                        continue;   // trigger 不过：不重置冷却
                    }
                    bool ran = ctrl.Run(c);
                    if (ran && c.PendingStateNo >= 0)
                    {
                        ApplyTransition(c, states, commonStates);   // 切状态：不 reset(对齐 Ikemen 在 return true 后才 reset)，
                        changed = true;                             // 新状态计数器已由 ApplyTransition 清零
                        break;      // 切状态后从新状态头部重入
                    }
                    ResetPersist(c, def.No, i, ctrl.Persistent);    // 未切状态：执行后重置冷却(对齐 Ikemen StateBlock.Run 末尾)
                }
                if (!changed)
                {
                    return;
                }
                reentry++;
            }
        }

        // ───────── persistent（移植 Ikemen StateBlock.Run + ctrlsps 倒计时模型）─────────
        // 语义：每帧到达该控制器时先递减其计数器，计数器 >0 则本帧跳过（不求值 trigger）；
        // 计数器 <=0 时求值 trigger，trigger 过则执行并把计数器重置为 persistent 值。
        // 故 persistent=N 是"执行后冷却 N 帧"（按在状态内的帧计，与 trigger 真值无关），非"第 N 次 trigger 真"。
        // persistent 取值对齐 Ikemen 编译期归一：1 或 >128 → 1（每帧）；<=0 → 锁定哨兵（进状态仅一次）；其余=N。
        const int PersistLock = int.MaxValue;   // 对应 Ikemen 的 math.MaxInt32：计数器锁定，不再递减→永不再执行（直到重进状态）

        // 计数器按 (状态号, 控制器序号) 复合键存储，避免负状态/当前状态/重入状态间串扰
        // （对齐 Ikemen 每个 StateBytecode 各有独立 ctrlsps 数组）。控制器数 <256，故 *256 编码无碰撞。
        const int PersistKeyStride = 256;

        static int PersistKey(int stateNo, int ctrlIndex)
        {
            return stateNo * PersistKeyStride + ctrlIndex;
        }

        static int ResolvePersist(int raw)
        {
            if (raw == 1 || raw > 128)
            {
                return 1;
            }
            if (raw <= 0)
            {
                return PersistLock;
            }
            return raw;
        }

        // 门控：递减计数器（锁定值不减），返回是否"开放执行"（计数器<=0）。在 trigger 求值之前调用。
        static bool PersistGate(MChar c, int stateNo, int ctrlIndex)
        {
            int key = PersistKey(stateNo, ctrlIndex);
            c.PersistCounters.TryGetValue(key, out int counter);   // 缺省 0（进状态重置后的初值）
            if (counter != PersistLock)
            {
                counter--;
            }
            c.PersistCounters[key] = counter;
            return counter <= 0;
        }

        // 执行后重置：把计数器设为 persistent 值（1 / N / 锁定）。
        static void ResetPersist(MChar c, int stateNo, int ctrlIndex, int rawPersistent)
        {
            c.PersistCounters[PersistKey(stateNo, ctrlIndex)] = ResolvePersist(rawPersistent);
        }

        // 进入状态时重置该状态的 persistent 计数器（对齐 Ikemen changeState 重建 ctrlsps 为全零）。
        // 仅清目标状态范围 [stateNo*256, stateNo*256+256)，负状态计数器保留（它们从不被"进入"）。
        static void ClearStatePersist(MChar c, int stateNo)
        {
            int lo = stateNo * PersistKeyStride;
            int hi = lo + PersistKeyStride;
            List<int> toRemove = null;
            foreach (int key in c.PersistCounters.Keys)
            {
                if (key >= lo && key < hi)
                {
                    if (toRemove == null)
                    {
                        toRemove = new List<int>();
                    }
                    toRemove.Add(key);
                }
            }
            if (toRemove != null)
            {
                for (int k = 0; k < toRemove.Count; k++)
                {
                    c.PersistCounters.Remove(toRemove[k]);
                }
            }
        }

        // 对应 changeStateEx + stateChange：设新状态号、time=0、清 persistent 计数、应用 statedef 头部。
        static void ApplyTransition(MChar c, IReadOnlyDictionary<int, MStateDef> states,
            IReadOnlyDictionary<int, MStateDef> commonStates)
        {
            MStateTransition transition = c.PendingTransition;
            int target = transition.StateNo;
            int oldOwner = c.StatePlayerNo >= 0 ? c.StatePlayerNo : c.PlayerNo;
            int newOwner = transition.OwnerPlayerNo >= 0 ? transition.OwnerPlayerNo : oldOwner;
            if (oldOwner != newOwner) { RescaleStateLocalCoordinates(c, oldOwner, newOwner); }

            c.PendingTransition = MStateTransition.None;
            c.StatePlayerNo = newOwner;
            if (newOwner == c.PlayerNo)
            {
                c.StateOwner = null;
            }
            else if (c.StateOwner != null && c.StateOwner.PlayerNo != newOwner)
            {
                c.StateOwner = null;
            }
            if (transition.AnimNo >= 0)
            {
                c.PlayAnimation(transition.AnimNo, c.PlayerNo, c.PlayerNo);
            }
            if (transition.Ctrl >= 0)
            {
                c.Ctrl = transition.Ctrl != 0;
            }
            c.PrevStateNo = c.StateNo;
            c.PrevStateType = c.StateType;   // 保存上一状态 statetype（prevstatetype trigger）
            c.StateNo = target;
            c.Time = 0;
            ClearStatePersist(c, target);   // 重置进入状态的 persistent 计数器（负状态计数器保留）

            MStateDef def = LookupForCurrentOwner(c, target, states, commonStates);
            if (def == null)
            {
                return;
            }
            def.RunInit(c);   // 应用头部：type/movetype/physics（字面量）+ anim/ctrl/velset/...（表达式求值）
        }

        static void RescaleStateLocalCoordinates(MChar c, int oldOwner, int newOwner)
        {
            int oldWidth = c.LocalCoordWidthFor(oldOwner);
            int newWidth = c.LocalCoordWidthFor(newOwner);
            if (oldWidth == newWidth) { return; }

            FFloat ratio = FFloat.FromInt(newWidth) / FFloat.FromInt(oldWidth);
            c.Pos = c.Pos * ratio;
            c.OldPos = c.Pos;
            c.Vel = c.Vel * ratio;
            c.BindPos = c.BindPos * ratio;
            c.WidthPlayerFront *= ratio;
            c.WidthPlayerBack *= ratio;
            c.WidthEdgeFront *= ratio;
            c.WidthEdgeBack *= ratio;
            c.Ghv.XVel *= ratio;
            c.Ghv.YVel *= ratio;
            c.Ghv.ZVel *= ratio;
            c.Ghv.YAccel *= ratio;
            c.Ghv.FallXVel *= ratio;
            c.Ghv.FallYVel *= ratio;
        }

        static MStateDef LookupForCurrentOwner(MChar c, int no,
            IReadOnlyDictionary<int, MStateDef> fallbackStates,
            IReadOnlyDictionary<int, MStateDef> fallbackCommonStates)
        {
            MCharData data = c.DataFor(c.StatePlayerNo);
            return data != null
                ? Lookup(no, data.States, data.CommonStates)
                : Lookup(no, fallbackStates, fallbackCommonStates);
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
