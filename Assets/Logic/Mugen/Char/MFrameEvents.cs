using System.Collections.Generic;
using Lockstep.Math;

namespace Lockstep.Mugen.Char
{
    public enum MSoundEventType
    {
        Play,
        Stop,
        SetPan,
    }

    /// <summary>Deterministic audio command emitted by simulation and consumed by the presentation adapter.</summary>
    public sealed class MSoundEvent
    {
        public MSoundEventType Type;
        public int OwnerId;
        public bool CommonBank;
        public int Group;
        public int Number;
        public int Channel = -1;
        public int VolumeScale = 100;
        public FFloat Pan;
        public bool AbsolutePan;
        public FFloat Frequency = FFloat.One;
        public int LoopCount;
        public int Priority;
        public int LoopStart;
        public int LoopEnd;
        public int StartPosition;
        public bool LowPriority;
        public bool StopOnGetHit;
        public bool StopOnChangeState;
    }

    /// <summary>Transient outputs for one simulation frame. These are intentionally excluded from rollback hashes.</summary>
    public sealed class MFrameEvents
    {
        public int Frame;
        public readonly List<MSoundEvent> Sounds = new List<MSoundEvent>();

        public void BeginFrame(int frame)
        {
            Frame = frame;
            Sounds.Clear();
        }
    }
}
