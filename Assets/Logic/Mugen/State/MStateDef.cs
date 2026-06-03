// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (StateBytecode/StateBlock/StateController) — 定点版。
// 不照搬 Ikemen 的嵌套 StateBlock 树，而是移植其触发/persistent/ignorehitpause 行为到扁平控制器模型。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;

namespace Lockstep.Mugen.State
{
    /// <summary>
    /// 控制器触发条件集（对应 MUGEN triggerall + trigger1..n）：
    /// triggerall 全部 AND；trigger1..n 每组内部 AND、组间 OR；最终 = triggerall &amp;&amp; (组1 || 组2 || ...)。
    /// </summary>
    public sealed class MTriggerSet
    {
        public List<BytecodeExp> TriggerAll = new List<BytecodeExp>();
        public List<List<BytecodeExp>> Groups = new List<List<BytecodeExp>>();   // trigger1, trigger2, ...

        public bool IsEmpty => TriggerAll.Count == 0 && Groups.Count == 0;

        public bool Passes(MChar c)
        {
            for (int k = 0; k < TriggerAll.Count; k++)
            {
                if (!TriggerAll[k].Run(c).ToB())
                {
                    return false;
                }
            }
            if (Groups.Count == 0)
            {
                return true;   // 仅 triggerall（或全空）：由 triggerall 决定
            }
            for (int g = 0; g < Groups.Count; g++)
            {
                List<BytecodeExp> group = Groups[g];
                bool allPass = true;
                for (int k = 0; k < group.Count; k++)
                {
                    if (!group[k].Run(c).ToB())
                    {
                        allPass = false;
                        break;
                    }
                }
                if (allPass)
                {
                    return true;   // 任一组全过即触发（组间 OR）
                }
            }
            return false;
        }

        /// <summary>便捷：用单个表达式作为 trigger1（测试/简单控制器常用）。</summary>
        public static MTriggerSet Single(BytecodeExp trigger)
        {
            MTriggerSet set = new MTriggerSet();
            if (trigger != null)
            {
                set.Groups.Add(new List<BytecodeExp> { trigger });
            }
            return set;
        }
    }

    /// <summary>
    /// 一个状态（对应 MUGEN [Statedef N]）。头部(type/movetype/physics/ctrl/anim) + 控制器列表。
    /// StateType/MoveType/Physics 用 int 原始码(S=1/C=2/A=4/L=8/N=16；MoveType I=1/H=2/A=4)；Ctrl/Anim -1=不改。
    /// </summary>
    public sealed class MStateDef
    {
        public int No;
        public int StateType = -1;   // -1 = 进入时不改
        public int MoveType = -1;
        public int Physics = -1;
        public int Ctrl = -1;        // -1 不改 / 0 / 1
        public int Anim = -1;        // -1 不改
        public List<MStateController> Controllers = new List<MStateController>();
    }

    /// <summary>
    /// 状态控制器基类（对应 Ikemen StateController）。触发=triggerall &amp;&amp; OR(trigger组)；
    /// persistent: 1=每帧、0=进状态后仅一次、N=每 N 次触发帧；ignorehitpause: hitpause 期间是否仍执行。
    /// Run 返回 true 表示触发了 ChangeState/SelfState（停止本状态后续控制器）。
    /// </summary>
    public abstract class MStateController
    {
        public MTriggerSet Triggers;     // null/空 = 恒真
        public int Persistent = 1;
        public bool IgnoreHitPause;

        public bool TriggerPasses(MChar c)
        {
            return Triggers == null || Triggers.IsEmpty || Triggers.Passes(c);
        }

        public abstract bool Run(MChar c);
    }

    /// <summary>ChangeState 控制器：切到 value 指定的状态号（用当前角色状态表）。</summary>
    public sealed class ChangeStateController : MStateController
    {
        public BytecodeExp Value;     // 目标状态号表达式
        public int Ctrl = -1;         // 可选：切换时设 ctrl
        public int Anim = -1;         // 可选：切换时设 anim（-1 不改）

        public override bool Run(MChar c)
        {
            c.PendingStateNo = Value.Run(c).ToI();
            c.PendingIsSelf = false;
            if (Ctrl >= 0)
            {
                c.Ctrl = Ctrl != 0;
            }
            if (Anim >= 0)
            {
                c.AnimNo = Anim;
            }
            return true;
        }
    }

    /// <summary>
    /// SelfState 控制器：切到自身状态表的状态号（对应 Ikemen SelfState）。
    /// 与 ChangeState 的区别在于当角色处于他人自定义状态时，强制回到自身状态表；单角色下行为一致，但置 PendingIsSelf。
    /// </summary>
    public sealed class SelfStateController : MStateController
    {
        public BytecodeExp Value;
        public int Ctrl = -1;
        public int Anim = -1;

        public override bool Run(MChar c)
        {
            c.PendingStateNo = Value.Run(c).ToI();
            c.PendingIsSelf = true;
            if (Ctrl >= 0)
            {
                c.Ctrl = Ctrl != 0;
            }
            if (Anim >= 0)
            {
                c.AnimNo = Anim;
            }
            return true;
        }
    }
}
