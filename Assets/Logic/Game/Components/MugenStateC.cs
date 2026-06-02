using Lockstep.Core;
using Lockstep.Game.Data;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 数据驱动状态机的运行态（MUGEN 式，int 状态号）。与旧 StateMachineC(枚举) 并行存在，
    /// 供新引擎使用；旧栈接入场景时再切除。Time 即 MUGEN 的 Time（进入当前状态后的帧数）。
    /// </summary>
    public sealed class MugenStateC : IComponent
    {
        public int StateNo;
        public int PrevStateNo;
        public int Time;
        public bool Ctrl;
        public StateType StateType;
        public MoveType MoveType;
        public Physics Physics;
        public int Hitstop;
        public int PendingStateNo = -1;   // >=0 表示有待应用的状态切换（控制器或跨实体触发）

        public IComponent Clone()
        {
            return new MugenStateC
            {
                StateNo = StateNo,
                PrevStateNo = PrevStateNo,
                Time = Time,
                Ctrl = Ctrl,
                StateType = StateType,
                MoveType = MoveType,
                Physics = Physics,
                Hitstop = Hitstop,
                PendingStateNo = PendingStateNo,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(StateNo);
            hash.AddInt32(PrevStateNo);
            hash.AddInt32(Time);
            hash.AddBool(Ctrl);
            hash.AddInt32((int)StateType);
            hash.AddInt32((int)MoveType);
            hash.AddInt32((int)Physics);
            hash.AddInt32(Hitstop);
            hash.AddInt32(PendingStateNo);
        }

        public override string ToString()
        {
            return string.Format("MState[no{0} t{1} ctrl{2} stop{3} pend{4}]",
                StateNo, Time, Ctrl ? 1 : 0, Hitstop, PendingStateNo);
        }
    }
}
