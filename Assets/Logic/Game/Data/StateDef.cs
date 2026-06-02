using Lockstep.Math;

namespace Lockstep.Game.Data
{
    /// <summary>
    /// 状态模板（≈ MUGEN [Statedef N]）。入场参数（Anim/Ctrl/VelSet...）用可空类型表示
    /// "未指定则不改"。Controllers 顺序敏感，每帧按序求值触发。
    /// </summary>
    public sealed class StateDef
    {
        public int Id;
        public StateType StateType;
        public MoveType MoveType;
        public Physics Physics;

        public int? Anim;
        public bool? Ctrl;
        public FVector2? VelSet;
        public int? PowerAdd;
        public int? Juggle;

        public StateController[] Controllers;
    }
}
