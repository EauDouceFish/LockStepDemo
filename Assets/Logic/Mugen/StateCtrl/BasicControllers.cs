// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (velSet/velAdd/posSet/posAdd/changeAnim/ctrlSet/varSet/varAdd/stateTypeSet 等 StateController)
//         + src/char.go (addX/addY/setPosX facing 语义)。
// Adapted to fixed-point. 朝向语义照搬 Ikemen：PosAdd X 乘 facing、PosSet 绝对、VelSet 存原始值(facing 积分时应用)。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Math;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    /// <summary>Null：无操作（对应 MUGEN [State ...] type = Null，常配 trigger 占位）。</summary>
    public sealed class NullController : MStateController
    {
        public override bool Run(MChar c)
        {
            return false;
        }
    }

    /// <summary>VelSet：vel = (x, y)。不乘 facing（移动积分时 pos += vel*facing 再应用朝向）。null 轴不改。</summary>
    public sealed class VelSetController : MStateController
    {
        public BytecodeExp X;
        public BytecodeExp Y;

        public override bool Run(MChar c)
        {
            FFloat vx = X != null ? X.Run(c).ToF() : c.Vel.X;
            FFloat vy = Y != null ? Y.Run(c).ToF() : c.Vel.Y;
            c.Vel = new FVector3(vx, vy, c.Vel.Z);
            return false;
        }
    }

    /// <summary>VelAdd：vel += (x, y)。同 VelSet 不乘 facing。</summary>
    public sealed class VelAddController : MStateController
    {
        public BytecodeExp X;
        public BytecodeExp Y;

        public override bool Run(MChar c)
        {
            FFloat vx = c.Vel.X + (X != null ? X.Run(c).ToF() : FFloat.Zero);
            FFloat vy = c.Vel.Y + (Y != null ? Y.Run(c).ToF() : FFloat.Zero);
            c.Vel = new FVector3(vx, vy, c.Vel.Z);
            return false;
        }
    }

    /// <summary>PosSet：pos = (x, y)。绝对坐标（不乘 facing，对齐 Ikemen setPosX）。null 轴不改。</summary>
    public sealed class PosSetController : MStateController
    {
        public BytecodeExp X;
        public BytecodeExp Y;

        public override bool Run(MChar c)
        {
            FFloat px = X != null ? X.Run(c).ToF() : c.Pos.X;
            FFloat py = Y != null ? Y.Run(c).ToF() : c.Pos.Y;
            c.Pos = new FVector3(px, py, c.Pos.Z);
            return false;
        }
    }

    /// <summary>PosAdd：pos.x += x*facing、pos.y += y（对齐 Ikemen addX/addY：X 朝向相对、Y 绝对）。</summary>
    public sealed class PosAddController : MStateController
    {
        public BytecodeExp X;
        public BytecodeExp Y;

        public override bool Run(MChar c)
        {
            FFloat px = c.Pos.X + (X != null ? X.Run(c).ToF() * c.Facing : FFloat.Zero);
            FFloat py = c.Pos.Y + (Y != null ? Y.Run(c).ToF() : FFloat.Zero);
            c.Pos = new FVector3(px, py, c.Pos.Z);
            return false;
        }
    }

    /// <summary>ChangeAnim：设当前动画号（elem 起始帧归 Anim 系统 M8）。</summary>
    public sealed class ChangeAnimController : MStateController
    {
        public BytecodeExp Value;

        public override bool Run(MChar c)
        {
            if (Value != null)
            {
                int animNo = Value.Run(c).ToI();
                // 目标动画不存在则不切（对齐 Ikemen changeAnimEx：a==nil → 保留当前动画，避免冻结）。
                if (c.CanChangeAnimTo(animNo))
                {
                    MAnimSystem.Play(c, animNo, c.AnimTable);
                }
            }
            return false;
        }
    }

    /// <summary>CtrlSet：设角色控制权 ctrl（value 求值为 bool）。</summary>
    public sealed class CtrlSetController : MStateController
    {
        public BytecodeExp Value;

        public override bool Run(MChar c)
        {
            if (Value != null)
            {
                c.Ctrl = Value.Run(c).ToB();
            }
            return false;
        }
    }

    /// <summary>VarSet：var(index)=value 或 fvar(index)=value（IsFloat 区分整型/定点变量）。</summary>
    public sealed class VarSetController : MStateController
    {
        public int Index;
        public bool IsFloat;
        public BytecodeExp Value;

        public override bool Run(MChar c)
        {
            if (Value == null)
            {
                return false;
            }
            if (IsFloat)
            {
                c.FloatVars[Index] = Value.Run(c).ToF();
            }
            else
            {
                c.IntVars[Index] = Value.Run(c).ToI();
            }
            return false;
        }
    }

    /// <summary>VarAdd：var(index)+=value 或 fvar(index)+=value（未设的变量按 0 起算）。</summary>
    public sealed class VarAddController : MStateController
    {
        public int Index;
        public bool IsFloat;
        public BytecodeExp Value;

        public override bool Run(MChar c)
        {
            if (Value == null)
            {
                return false;
            }
            if (IsFloat)
            {
                c.FloatVars.TryGetValue(Index, out FFloat cur);
                c.FloatVars[Index] = cur + Value.Run(c).ToF();
            }
            else
            {
                c.IntVars.TryGetValue(Index, out int cur);
                c.IntVars[Index] = cur + Value.Run(c).ToI();
            }
            return false;
        }
    }

    /// <summary>
    /// HitBy / NotHitBy：设置受击属性免疫窗口（对应 Ikemen hitby StateController）。
    /// HitBy：仅 Attr 匹配的攻击能命中；NotHitBy(IsNot=true)：仅不匹配的能命中。Time 帧后失效（由状态机每帧递减）。
    /// Attr/Time/IsNot 已在 CnsParser 解析为常量（攻击类别 bitmask），运行期直接写入 MChar。
    /// </summary>
    public sealed class HitByController : MStateController
    {
        public int Attr;
        public int Time;
        public bool IsNot;

        public override bool Run(MChar c)
        {
            c.HitByAttr = Attr;
            c.HitByTime = Time;
            c.HitByIsNot = IsNot;
            return false;
        }
    }

    /// <summary>
    /// StateTypeSet：状态中途改 statetype/movetype/physics/ctrl（-1/null = 不改）。
    /// 码值对齐 M2/M4：StateType S=1/C=2/A=4/L=8/N=16；MoveType I=1/H=2/A=4。
    /// </summary>
    public sealed class StateTypeSetController : MStateController
    {
        public int StateType = -1;
        public int MoveType = -1;
        public int Physics = -1;
        public BytecodeExp CtrlExpr;

        public override bool Run(MChar c)
        {
            if (StateType >= 0) { c.StateType = StateType; }
            if (MoveType >= 0) { c.MoveType = MoveType; }
            if (Physics >= 0) { c.Physics = Physics; }
            if (CtrlExpr != null) { c.Ctrl = CtrlExpr.Run(c).ToB(); }
            return false;
        }
    }
}
