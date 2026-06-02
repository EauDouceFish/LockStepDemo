using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Game.Data;
using Lockstep.Game.Expr;
using Lockstep.Math;

namespace Lockstep.Game.Systems
{
    /// <summary>
    /// 数据驱动状态机（MUGEN 式）。每帧对每个实体：跑当前 StateDef 的控制器（按 trigger 求值），
    /// ChangeState 立即切换并在同帧重入新状态（有界次数防死循环），最后 Time++。
    /// 无静态可变状态；表达式上下文复用一份。
    /// </summary>
    public sealed class MugenStateMachineSystem : ISystem
    {
        const int MaxStateChangesPerFrame = 16;

        readonly WorldEvalContext _context = new WorldEvalContext();

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
                MugenStateC state = entity.Get<MugenStateC>();
                CharacterRefC characterRef = entity.Get<CharacterRefC>();
                if (state == null || characterRef == null)
                {
                    continue;
                }
                if (!data.Characters.TryGetValue(characterRef.CharacterId, out CharacterDef character))
                {
                    continue;
                }

                if (state.Hitstop > 0)
                {
                    state.Hitstop--;
                    continue;
                }

                _context.Bind(world, entity);

                // 跨实体强制切换（如受击时由命中系统写入）优先应用
                if (state.PendingStateNo >= 0)
                {
                    ApplyTransition(entity, state, character);
                }

                int guard = 0;
                while (guard < MaxStateChangesPerFrame)
                {
                    guard++;
                    if (!character.States.TryGetValue(state.StateNo, out StateDef stateDef))
                    {
                        break;
                    }
                    bool changed = RunControllers(stateDef, entity, state);
                    if (changed && state.PendingStateNo >= 0)
                    {
                        ApplyTransition(entity, state, character);
                        continue;
                    }
                    break;
                }

                state.Time++;
            }
        }

        bool RunControllers(StateDef stateDef, Entity entity, MugenStateC state)
        {
            if (stateDef.Controllers == null)
            {
                return false;
            }
            for (int i = 0; i < stateDef.Controllers.Length; i++)
            {
                StateController controller = stateDef.Controllers[i];
                bool pass = controller.Trigger == null || controller.Trigger.EvalBool(_context);
                if (!pass)
                {
                    continue;
                }
                bool changed = StateControllerExecutor.Execute(controller, entity, _context);
                if (changed)
                {
                    return true;
                }
            }
            return false;
        }

        static void ApplyTransition(Entity entity, MugenStateC state, CharacterDef character)
        {
            int target = state.PendingStateNo;
            state.PendingStateNo = -1;
            state.PrevStateNo = state.StateNo;
            state.StateNo = target;
            state.Time = 0;

            if (!character.States.TryGetValue(target, out StateDef stateDef))
            {
                return;
            }

            if (stateDef.StateType != StateType.Unchanged)
            {
                state.StateType = stateDef.StateType;
            }
            if (stateDef.MoveType != MoveType.Unchanged)
            {
                state.MoveType = stateDef.MoveType;
            }
            if (stateDef.Physics != Physics.Unchanged)
            {
                state.Physics = stateDef.Physics;
            }
            if (stateDef.Ctrl.HasValue)
            {
                state.Ctrl = stateDef.Ctrl.Value;
            }
            if (stateDef.Anim.HasValue)
            {
                AnimC anim = entity.Get<AnimC>();
                if (anim != null)
                {
                    anim.AnimNo = stateDef.Anim.Value;
                    anim.FrameIndex = 0;
                    anim.ElemTime = 0;
                    anim.AnimTime = 0;
                }
            }
            if (stateDef.VelSet.HasValue)
            {
                VelocityC velocity = entity.Get<VelocityC>();
                if (velocity != null)
                {
                    velocity.Vel = new FVector3(stateDef.VelSet.Value.X, stateDef.VelSet.Value.Y, FFloat.Zero);
                }
            }
        }
    }
}
