using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Game.Data;
using Lockstep.Math;

namespace Lockstep.Game.Systems
{
    /// <summary>
    /// 命中结算：消费 CollisionSystem 写下的 PendingHitC，对受击方扣血 / 给硬直(hitstop) / 击退 /
    /// 切受击态(MoveType=BeingHit + 切 MUGEN 受击状态 5000)，并给攻击方 hitstop。须在 CollisionSystem 之后、
    /// StateMachineSystem 之前跑。击退方向按攻击方朝向镜像。无静态可变状态。
    /// </summary>
    public sealed class HitSystem : ISystem
    {
        public const int GetHitStateNo = 5000;   // MUGEN 标准受击状态号

        public void Tick(World world)
        {
            for (int i = 0; i < world.Entities.Count; i++)
            {
                Entity defender = world.Entities[i];
                PendingHitC pending = defender.Get<PendingHitC>();
                if (pending == null || !pending.HasHit || pending.Hit == null)
                {
                    continue;
                }
                HitDef hit = pending.Hit;

                HealthC health = defender.Get<HealthC>();
                if (health != null)
                {
                    health.HP -= hit.Damage;
                    if (health.HP < 0)
                    {
                        health.HP = 0;
                    }
                }

                FFloat attackerFacing = FFloat.One;
                if (pending.AttackerEntityIndex >= 0 && pending.AttackerEntityIndex < world.Entities.Count)
                {
                    Entity attacker = world.Entities[pending.AttackerEntityIndex];
                    TransformC attackerTransform = attacker.Get<TransformC>();
                    if (attackerTransform != null)
                    {
                        attackerFacing = attackerTransform.FacingX;
                    }
                    MugenStateC attackerState = attacker.Get<MugenStateC>();
                    if (attackerState != null)
                    {
                        attackerState.Hitstop = hit.PauseTimeAttacker;
                    }
                }

                MugenStateC defenderState = defender.Get<MugenStateC>();
                if (defenderState != null)
                {
                    defenderState.Hitstop = hit.PauseTimeDefender;
                    defenderState.MoveType = MoveType.BeingHit;
                    defenderState.PendingStateNo = GetHitStateNo;
                }

                VelocityC velocity = defender.Get<VelocityC>();
                if (velocity != null)
                {
                    velocity.Vel = new FVector3(
                        hit.GroundVelocity.X * attackerFacing,
                        hit.GroundVelocity.Y,
                        FFloat.Zero);
                }

                pending.HasHit = false;   // 已结算
            }
        }
    }
}
