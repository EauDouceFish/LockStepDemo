using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Game.Data;
using Lockstep.Math;

namespace Lockstep.Game.Systems
{
    /// <summary>
    /// 物理系统（MUGEN-2D：X 横向、Y 高度，MUGEN 原生约定上为负、下为正）。
    /// 每帧把速度积分进位置；Physics=Air 时按角色重力增加 Y 速度（下落）；落地(Y&gt;0)夹回地面。
    /// v1 不做地面摩擦/走路自动减速（由状态的 VelSet 控制）。
    /// </summary>
    public sealed class MugenPhysicsSystem : ISystem
    {
        public void Tick(World world)
        {
            MugenGameData data = world.GameData as MugenGameData;

            for (int i = 0; i < world.Entities.Count; i++)
            {
                Entity entity = world.Entities[i];
                MugenStateC state = entity.Get<MugenStateC>();
                TransformC transform = entity.Get<TransformC>();
                VelocityC velocity = entity.Get<VelocityC>();
                if (state == null || transform == null || velocity == null)
                {
                    continue;
                }

                FFloat velX = velocity.Vel.X;
                FFloat velY = velocity.Vel.Y;

                if (state.Physics == Physics.Air)
                {
                    velY += Gravity(world, data, entity);
                }

                FFloat posX = transform.Pos.X + velX;
                FFloat posY = transform.Pos.Y + velY;

                // 地面在 Y=0，向上为负；Y>0 表示穿到地下 → 夹回地面并清竖直速度
                if (posY > FFloat.Zero)
                {
                    posY = FFloat.Zero;
                    velY = FFloat.Zero;
                }

                transform.Pos = new FVector3(posX, posY, FFloat.Zero);
                velocity.Vel = new FVector3(velX, velY, FFloat.Zero);
            }
        }

        static FFloat Gravity(World world, MugenGameData data, Entity entity)
        {
            CharacterRefC characterRef = entity.Get<CharacterRefC>();
            if (data != null && characterRef != null
                && data.Characters.TryGetValue(characterRef.CharacterId, out CharacterDef character)
                && character.Const != null)
            {
                return character.Const.Gravity;
            }
            return FFloat.Zero;
        }
    }
}
