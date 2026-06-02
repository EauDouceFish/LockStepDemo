using Lockstep.Core;
using Lockstep.Game.Anim;
using Lockstep.Game.Components;
using Lockstep.Game.Data;

namespace Lockstep.Game.Systems
{
    /// <summary>
    /// 动画系统：按 AnimC.AnimNo 取角色动画，用已测的 AnimAdvance 推进一帧。
    /// 逻辑帧驱动 → 两端确定一致；表现层只读 AnimC 渲染。
    /// </summary>
    public sealed class MugenAnimSystem : ISystem
    {
        public void Tick(World world)
        {
            MugenGameData data = world.GameData as MugenGameData;
            if (data == null)
            {
                return;
            }

            for (int i = 0; i < world.Entities.Count; i++)
            {
                Entity entity = world.Entities[i];
                AnimC anim = entity.Get<AnimC>();
                CharacterRefC characterRef = entity.Get<CharacterRefC>();
                if (anim == null || characterRef == null)
                {
                    continue;
                }
                if (!data.Characters.TryGetValue(characterRef.CharacterId, out CharacterDef character))
                {
                    continue;
                }
                if (character.Anims == null || !character.Anims.TryGetValue(anim.AnimNo, out AnimData animData))
                {
                    continue;
                }

                int frameIndex = anim.FrameIndex;
                int elemTime = anim.ElemTime;
                int animTime = anim.AnimTime;
                AnimAdvance.Step(animData, ref frameIndex, ref elemTime, ref animTime);
                anim.FrameIndex = frameIndex;
                anim.ElemTime = elemTime;
                anim.AnimTime = animTime;
            }
        }
    }
}
