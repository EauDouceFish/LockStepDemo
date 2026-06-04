// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (attackMulSet 10714-10740 / defenceMulSet 10751-10789 StateController)
//         + char.go (finalDefense 12081-12085 / computeDamage atkmul 8440)。
// 攻防倍率控制器：写运行态 AttackMul/CustomDefense（命中伤害公式按之缩放）。定点化。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    /// <summary>
    /// AttackMulSet：设置攻击倍率（命中造成的伤害 ×= AttackMul×attackBase/100）。
    /// Ikemen 有 [4] 分量(damage/redlife/dizzy/guard)，本引擎仅实现伤害分量；value 与 damage 都写伤害倍率。
    /// </summary>
    public sealed class AttackMulSetController : MStateController
    {
        public BytecodeExp Value;    // value：Ikemen 同时设 4 分量，这里等价 damage 分量
        public BytecodeExp Damage;   // damage：仅伤害分量

        public override bool Run(MChar character)
        {
            if (Damage != null)
            {
                character.AttackMul = Damage.Run(character).ToF();
            }
            else if (Value != null)
            {
                character.AttackMul = Value.Run(character).ToF();
            }
            return false;
        }
    }

    /// <summary>
    /// DefenceMulSet：设置防御倍率（受到的伤害按 finalDefense 缩放）。
    /// mulType≠0：customDefense = value；mulType=0：customDefense = 1/value（MUGEN 默认，value 直接当伤害倍率）。
    /// onHit=true：customDefense 仅在受击态(movetype H)生效（DefenseMulDelay）。
    /// MUGEN 角色（无 ikemenversion）默认 onHit=true、mulType=0（对齐 bytecode.go:10762）。
    /// </summary>
    public sealed class DefenceMulSetController : MStateController
    {
        public BytecodeExp Value;
        public BytecodeExp OnHit;
        public BytecodeExp MulType;

        public override bool Run(MChar character)
        {
            // MUGEN 角色默认行为（本引擎吃 MUGEN .cns，无 ikemenversion）
            FFloat value = FFloat.One;
            bool onHit = true;
            int mulType = 0;

            if (Value != null)
            {
                value = Value.Run(character).ToF();
            }
            if (OnHit != null)
            {
                onHit = OnHit.Run(character).ToB();
            }
            if (MulType != null)
            {
                mulType = MulType.Run(character).ToI();
            }

            if (mulType != 0)
            {
                character.CustomDefense = value;
            }
            else if (value != FFloat.Zero)
            {
                character.CustomDefense = FFloat.One / value;   // 防除零
            }
            character.DefenseMulDelay = onHit;
            return false;
        }
    }
}
