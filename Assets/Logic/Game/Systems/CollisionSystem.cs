using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Game.Data;

namespace Lockstep.Game.Systems
{
    /// <summary>
    /// 命中检测：对每个"攻击方"（HitDefStateC.Active 且当前帧有 Clsn1），与每个"受击方"（当前帧有 Clsn2）
    /// 做 Clsn1×Clsn2 世界重叠判定（按各自 facing 镜像）。命中则写受击方 PendingHitC 并在攻击方
    /// HitTargetsBits 标记该目标，**防一招对同一目标多次命中**。HitSystem(T3.4) 随后结算。
    /// 每帧开头清空上帧 PendingHit。无静态可变状态。
    /// </summary>
    public sealed class CollisionSystem : ISystem
    {
        public void Tick(World world)
        {
            MugenGameData data = world.GameData as MugenGameData;
            if (data == null)
            {
                return;
            }

            // 清空上一帧的待结算命中（HitSystem 同帧消费，未消费的也不应跨帧残留）
            for (int i = 0; i < world.Entities.Count; i++)
            {
                PendingHitC pending = world.Entities[i].Get<PendingHitC>();
                if (pending != null)
                {
                    pending.HasHit = false;
                }
            }

            for (int attackerIndex = 0; attackerIndex < world.Entities.Count; attackerIndex++)
            {
                Entity attacker = world.Entities[attackerIndex];
                HitDefStateC hitDef = attacker.Get<HitDefStateC>();
                if (hitDef == null || !hitDef.Active)
                {
                    continue;
                }
                AnimFrame attackFrame = CurrentFrame(world, data, attacker);
                if (attackFrame == null || attackFrame.Clsn1 == null || attackFrame.Clsn1.Length == 0)
                {
                    continue;
                }
                TransformC attackTransform = attacker.Get<TransformC>();
                if (attackTransform == null)
                {
                    continue;
                }

                for (int defenderIndex = 0; defenderIndex < world.Entities.Count; defenderIndex++)
                {
                    if (defenderIndex == attackerIndex)
                    {
                        continue;
                    }
                    if ((hitDef.HitTargetsBits & (1UL << defenderIndex)) != 0)
                    {
                        continue;   // 本招已打过该目标
                    }
                    Entity defender = world.Entities[defenderIndex];
                    PendingHitC defenderPending = defender.Get<PendingHitC>();
                    TransformC defenderTransform = defender.Get<TransformC>();
                    if (defenderPending == null || defenderTransform == null)
                    {
                        continue;
                    }
                    AnimFrame defendFrame = CurrentFrame(world, data, defender);
                    if (defendFrame == null || defendFrame.Clsn2 == null || defendFrame.Clsn2.Length == 0)
                    {
                        continue;
                    }

                    bool overlap = ClsnWorld.AnyOverlap(
                        attackFrame.Clsn1, attackTransform.Pos.X, attackTransform.Pos.Y, attackTransform.FacingX,
                        defendFrame.Clsn2, defenderTransform.Pos.X, defenderTransform.Pos.Y, defenderTransform.FacingX);
                    if (!overlap)
                    {
                        continue;
                    }

                    defenderPending.HasHit = true;
                    defenderPending.AttackerEntityIndex = attackerIndex;
                    defenderPending.Hit = hitDef.Current;
                    hitDef.HitTargetsBits |= 1UL << defenderIndex;
                }
            }
        }

        static AnimFrame CurrentFrame(World world, MugenGameData data, Entity entity)
        {
            CharacterRefC characterRef = entity.Get<CharacterRefC>();
            AnimC anim = entity.Get<AnimC>();
            if (characterRef == null || anim == null)
            {
                return null;
            }
            if (!data.Characters.TryGetValue(characterRef.CharacterId, out CharacterDef character))
            {
                return null;
            }
            if (character.Anims == null || !character.Anims.TryGetValue(anim.AnimNo, out AnimData animData))
            {
                return null;
            }
            if (animData.Frames == null || anim.FrameIndex < 0 || anim.FrameIndex >= animData.Frames.Length)
            {
                return null;
            }
            return animData.Frames[anim.FrameIndex];
        }
    }
}
