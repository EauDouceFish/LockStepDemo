using Lockstep.Core;
using Lockstep.Game.States;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 状态机数据（每实体一份）。State 类是无状态单例，只读这份数据并写回。
    /// </summary>
    public sealed class StateMachineC : IComponent
    {
        public PlayerStateId Current = PlayerStateId.Idle;
        public int FrameInState;
        public PlayerStateId? PendingTransition;
        public int HitstopFrames;       // > 0 时本帧完全冻结（不 OnTick、不 ++FrameInState、不接受新输入）

        public IComponent Clone()
        {
            return new StateMachineC
            {
                Current = Current,
                FrameInState = FrameInState,
                PendingTransition = PendingTransition,
                HitstopFrames = HitstopFrames,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32((int)Current);
            hash.AddInt32(FrameInState);
            hash.AddBool(PendingTransition.HasValue);
            hash.AddInt32(PendingTransition.HasValue ? (int)PendingTransition.Value : 0);
            hash.AddInt32(HitstopFrames);
        }

        public override string ToString()
        {
            return string.Format("SM[{0},f{1},stop{2},pend={3}]",
                Current, FrameInState, HitstopFrames, PendingTransition);
        }
    }
}
