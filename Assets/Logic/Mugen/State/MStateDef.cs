// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (StateBytecode/StateBlock/StateController) — 定点 MVP 版。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;

namespace Lockstep.Mugen.State
{
    /// <summary>
    /// 一个状态（对应 MUGEN [Statedef N]）。头部(type/movetype/physics/ctrl/anim) + 控制器列表。
    /// MVP：StateType/MoveType/Physics 用 int 原始码；Ctrl/Anim 用 -1 表示"不改"。
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
    /// 状态控制器基类（对应 Ikemen StateController 接口）。Trigger 过则 Run；
    /// Run 返回 true 表示触发了 ChangeState（停止本状态后续控制器）。
    /// 具体控制器（VelSet/PosAdd/HitDef...）在 M5 移植；M4 先有 ChangeState。
    /// </summary>
    public abstract class MStateController
    {
        public BytecodeExp Trigger;   // null = 恒为真

        public bool TriggerPasses(MChar c)
        {
            return Trigger == null || Trigger.Run(c).ToB();
        }

        public abstract bool Run(MChar c);
    }

    /// <summary>ChangeState 控制器（type = ChangeState）：切到 value 指定的状态号。</summary>
    public sealed class ChangeStateController : MStateController
    {
        public BytecodeExp Value;     // 目标状态号表达式
        public int Ctrl = -1;         // 可选：切换时设 ctrl
        public int Anim = -1;         // 可选：切换时设 anim（-1 不改）

        public override bool Run(MChar c)
        {
            c.PendingStateNo = Value.Run(c).ToI();
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
