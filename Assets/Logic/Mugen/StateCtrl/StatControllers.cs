// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (lifeAdd/lifeSet/powerAdd/powerSet/turn/assertSpecial) + src/char.go (lifeAdd/lifeSet/setPower).
// Adapted to fixed-point. 取地基核心：clamp/kill 语义照搬；防御缩放(finalDefense)/胜负类型/红血/共享血等高层系统留后续。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    /// <summary>LifeAdd：life += value。kill=false 时结果≤0 夹到 1；clamp 到 [0, LifeMax]。</summary>
    public sealed class LifeAddController : MStateController
    {
        public BytecodeExp Value;
        public bool Kill = true;     // 默认可击杀

        public override bool Run(MChar c)
        {
            if (Value == null)
            {
                return false;
            }
            int newLife = c.Life + Value.Run(c).ToI();
            if (!Kill && newLife <= 0)
            {
                newLife = 1;          // 不可击杀：保命到 1
            }
            c.Life = Clamp(newLife, 0, c.LifeMax);
            return false;
        }

        internal static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }

    /// <summary>LifeSet：life = Clamp(value, 0, LifeMax)。</summary>
    public sealed class LifeSetController : MStateController
    {
        public BytecodeExp Value;

        public override bool Run(MChar c)
        {
            if (Value != null)
            {
                c.Life = LifeAddController.Clamp(Value.Run(c).ToI(), 0, c.LifeMax);
            }
            return false;
        }
    }

    /// <summary>PowerAdd：power = Clamp(power + value, 0, PowerMax)。</summary>
    public sealed class PowerAddController : MStateController
    {
        public BytecodeExp Value;

        public override bool Run(MChar c)
        {
            if (Value != null)
            {
                c.Power = LifeAddController.Clamp(c.Power + Value.Run(c).ToI(), 0, c.PowerMax);
            }
            return false;
        }
    }

    /// <summary>PowerSet：power = Clamp(value, 0, PowerMax)。</summary>
    public sealed class PowerSetController : MStateController
    {
        public BytecodeExp Value;

        public override bool Run(MChar c)
        {
            if (Value != null)
            {
                c.Power = LifeAddController.Clamp(Value.Run(c).ToI(), 0, c.PowerMax);
            }
            return false;
        }
    }

    /// <summary>Turn：翻转朝向（facing *= -1），对应 MUGEN Turn。</summary>
    public sealed class TurnController : MStateController
    {
        public override bool Run(MChar c)
        {
            c.Facing = -c.Facing;
            return false;
        }
    }

    /// <summary>AssertSpecial：本帧断言一个或多个标志（每帧清，须每帧重断言）。</summary>
    public sealed class AssertSpecialController : MStateController
    {
        public int Flags;            // MAssertFlag 位或

        public override bool Run(MChar c)
        {
            c.AssertFlags |= Flags;
            return false;
        }
    }
}
