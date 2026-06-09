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

    public enum MVisualEventType
    {
        PalFX,
        AllPalFX,
        BGPalFX,
        AfterImage,
        AfterImageTime,
        EnvShake,
        FallEnvShake,
        MakeDust,
        GameMakeAnim,
        EnvColor,
        RemapPal,
        Text,
        ModifyText,
        RemoveText,
        DisplayToClipboard,
        AppendToClipboard,
        ClearClipboard,
        ForceFeedback,
    }

    /// <summary>Deterministic visual/presentation command emitted by simulation for the Unity adapter and trace.</summary>
    public sealed class MVisualEvent
    {
        public MVisualEventType Type;
        public int OwnerId;
        public int Time;
        public int Id = -1;
        public int Index = -1;
        public int Value0;
        public int Value1;
        public int Value2;
        public int Value3;
        public FFloat X;
        public FFloat Y;
        public FFloat Z;
        public FFloat ScaleX = FFloat.One;
        public FFloat ScaleY = FFloat.One;
        public bool Under;
    }

    /// <summary>Transient outputs for one simulation frame. These are intentionally excluded from rollback hashes.</summary>
    public sealed class MFrameEvents
    {
        public int Frame;
        public readonly List<MSoundEvent> Sounds = new List<MSoundEvent>();
        public readonly List<MVisualEvent> Visuals = new List<MVisualEvent>();

        public void BeginFrame(int frame)
        {
            Frame = frame;
            Sounds.Clear();
            Visuals.Clear();
        }
    }
}
