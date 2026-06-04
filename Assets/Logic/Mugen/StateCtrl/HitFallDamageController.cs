// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go (hitFallDamage 9109-9114) + bytecode.go (hitFallDamage StateController)。
// 受击方落地时结算 fall.damage（common1 击飞落地状态调用）。定点化。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    /// <summary>
    /// HitFallDamage：受击态(movetype H)下应用 GetHitVar.FallDamage（按 fall.kill 判定可否致死），应用后清零。
    /// 对齐 char.go:9109——`if moveType==MT_H { lifeAdd(-fall_damage, fall_kill, false); fall_damage=0 }`。
    /// </summary>
    public sealed class HitFallDamageController : MStateController
    {
        public override bool Run(MChar character)
        {
            if (character.MoveType == 2)   // MT_H 受击中
            {
                MHitSystem.ApplySelfDamage(character, character.Ghv.FallDamage, character.Ghv.FallKill);
                character.Ghv.FallDamage = 0;
            }
            return false;
        }
    }
}
