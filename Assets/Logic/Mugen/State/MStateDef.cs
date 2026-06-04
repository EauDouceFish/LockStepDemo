// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (StateBytecode/StateBlock/StateController) — 定点版。
// 不照搬 Ikemen 的嵌套 StateBlock 树，而是移植其触发/persistent/ignorehitpause 行为到扁平控制器模型。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Math;
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
    /// 一个状态（对应 MUGEN [Statedef N]）。头部 + 控制器列表。
    /// type/movetype/physics 是字母枚举（字面量 int 码：S=1/C=2/A=4/L=8/N=16；MoveType I=1/H=2/A=4；-1=不改）。
    /// 其余头部参数（anim/ctrl/velset/facep2/juggle/poweradd）在 MUGEN 里**可为表达式**（如 anim=40+var(11)），
    /// 故存编译后的 <see cref="BytecodeExp"/>，进入状态时用角色上下文求值——对齐 Ikemen StateBytecode.init + stateDef.Run。
    /// </summary>
    public sealed class MStateDef
    {
        public int No;
        public int StateType = -1;   // 字母枚举，-1 = 进入时不改
        public int MoveType = -1;
        public int Physics = -1;
        // 头部表达式（null = 该参数未写，不改）。进入状态时由 RunInit 求值。
        public BytecodeExp Anim;        // 求值为 int；-1 表示不改（对齐 Ikemen "anim = -1"）
        public BytecodeExp Ctrl;        // 求值为 bool
        public BytecodeExp VelSetX;     // velset 第 1 分量
        public BytecodeExp VelSetY;     // velset 第 2 分量（可缺）
        public BytecodeExp VelSetZ;     // velset 第 3 分量（可缺）
        public BytecodeExp Facep2;      // 求值为 bool：true 则转向面对 P2
        public BytecodeExp Juggle;      // 求值为 int
        public BytecodeExp PowerAdd;    // 求值为 int
        public List<MStateController> Controllers = new List<MStateController>();

        /// <summary>
        /// 进入本状态时应用头部（对齐 Ikemen StateBytecode.init + stateDef.Run）：
        /// 先 type/movetype/physics（字面量），再按表达式求值 anim/ctrl/velset/facep2/juggle/poweradd。
        /// **anim 等表达式（如 40+var(11)）必须在此用角色上下文求值**，否则跳跃/落地/走路状态的动画号取不到。
        /// </summary>
        public void RunInit(MChar c)
        {
            if (StateType >= 0) { c.StateType = StateType; }
            if (MoveType >= 0) { c.MoveType = MoveType; }
            if (Physics >= 0) { c.Physics = Physics; }

            if (Facep2 != null && Facep2.Run(c).ToB() && c.P2 != null)
            {
                // 转向面对 P2（对齐 stateDef_facep2）。
                c.Facing = c.P2.Pos.X.Raw >= c.Pos.X.Raw ? FFloat.One : -FFloat.One;
            }
            if (Juggle != null)
            {
                c.Juggle = Juggle.Run(c).ToI();
            }
            if (VelSetX != null)
            {
                FFloat vx = VelSetX.Run(c).ToF();
                FFloat vy = VelSetY != null ? VelSetY.Run(c).ToF() : c.Vel.Y;
                FFloat vz = VelSetZ != null ? VelSetZ.Run(c).ToF() : c.Vel.Z;
                c.Vel = new FVector3(vx, vy, vz);
            }
            if (Anim != null)
            {
                int animNo = Anim.Run(c).ToI();
                // -1=不改；目标动画不存在则不切（对齐 changeAnimEx，避免进状态即冻结到无效动画）。
                if (animNo != -1 && c.CanChangeAnimTo(animNo))
                {
                    c.PrevAnimNo = c.AnimNo;
                    c.AnimNo = animNo;   // 动画重置由 MAnimSystem 按 AnimRunningNo 变化处理（M8）
                }
            }
            if (Ctrl != null)
            {
                c.Ctrl = Ctrl.Run(c).ToB();
            }
            if (PowerAdd != null)
            {
                int p = c.Power + PowerAdd.Run(c).ToI();
                c.Power = p < 0 ? 0 : (p > c.PowerMax ? c.PowerMax : p);
            }
        }
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
            if (Anim >= 0 && c.CanChangeAnimTo(Anim))
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
            if (Anim >= 0 && c.CanChangeAnimTo(Anim))
            {
                c.AnimNo = Anim;
            }
            return true;
        }
    }
}
