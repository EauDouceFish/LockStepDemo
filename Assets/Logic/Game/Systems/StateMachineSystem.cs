using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Game.States;

namespace Lockstep.Game.Systems
{
    /// <summary>
    /// 状态机推进系统。每帧对每个有 StateMachineC 的实体：
    ///   1. 若 HitstopFrames > 0：递减并跳过本帧（命中冻结）
    ///   2. 若 PendingTransition 有值：强切（跨实体触发的入口）
    ///   3. 调当前状态的 CheckTransition：若返回新状态则切换
    ///   4. 调当前状态的 OnTick
    ///   5. ++FrameInState
    /// </summary>
    public sealed class StateMachineSystem : ISystem
    {
        static readonly Dictionary<PlayerStateId, IPlayerState> s_states = BuildStateRegistry();

        static Dictionary<PlayerStateId, IPlayerState> BuildStateRegistry()
        {
            Dictionary<PlayerStateId, IPlayerState> registry = new Dictionary<PlayerStateId, IPlayerState>();
            registry[PlayerStateId.Idle]   = new IdleState();
            registry[PlayerStateId.Walk]   = new WalkState();
            registry[PlayerStateId.Jump]   = new JumpState();
            registry[PlayerStateId.Attack] = new AttackState();
            registry[PlayerStateId.Hurt]   = new HurtState();
            registry[PlayerStateId.KO]     = new KoState();
            return registry;
        }

        public void Tick(World world)
        {
            for (int i = 0; i < world.Entities.Count; i++)
            {
                Entity entity = world.Entities[i];
                StateMachineC sm = entity.Get<StateMachineC>();
                if (sm == null)
                {
                    continue;
                }

                if (sm.HitstopFrames > 0)
                {
                    sm.HitstopFrames--;
                    continue;
                }

                if (sm.PendingTransition.HasValue)
                {
                    ChangeState(world, entity, sm, sm.PendingTransition.Value);
                    sm.PendingTransition = null;
                }

                IPlayerState current = s_states[sm.Current];
                PlayerStateId? next = current.CheckTransition(world, entity);
                if (next.HasValue)
                {
                    // 允许"自重入"（next == current）：用于 Attack→Attack cancel 链。
                    // 各状态自觉只在"想换状态/重启状态"时返回值。
                    ChangeState(world, entity, sm, next.Value);
                }

                s_states[sm.Current].OnTick(world, entity);
                sm.FrameInState++;
            }
        }

        static void ChangeState(World world, Entity entity, StateMachineC sm, PlayerStateId next)
        {
            sm.Current = next;
            sm.FrameInState = 0;
            s_states[sm.Current].OnEnter(world, entity);
        }
    }
}
