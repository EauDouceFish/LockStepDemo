// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go state controllers:
// velSet/velAdd/posSet/posAdd/changeAnim/ctrlSet/varSet/varAdd/stateTypeSet.
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    public sealed class NullController : MStateController
    {
        public override bool Run(MChar c)
        {
            return false;
        }
    }

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

    public sealed class ChangeAnimController : MStateController
    {
        public BytecodeExp Value;
        public BytecodeExp Elem;
        public BytecodeExp ElemTime;

        public override bool Run(MChar c)
        {
            if (Value == null)
            {
                return false;
            }

            int animNo = Value.Run(c).ToI();
            int elem = Elem != null ? Elem.Run(c).ToI() - 1 : 0;
            int elemTime = ElemTime != null ? ElemTime.Run(c).ToI() : 0;
            // Ikemen reference: src/bytecode.go ChangeAnim calls changeAnimEx(value, elem, elemtime).
            c.PlayAnimation(animNo, c.PlayerNo, c.PlayerNo, elem, elemTime);
            return false;
        }
    }

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
