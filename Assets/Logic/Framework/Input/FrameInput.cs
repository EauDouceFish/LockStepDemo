using System;

namespace Lockstep.Input
{
    /// <summary>
    /// 按键 bitmask。byte 占 1 字节，最多承载 8 个键位，足够 v1 + v2 投技 / 防御 / 技能扩展。
    /// </summary>
    [Flags]
    public enum InputButton : byte
    {
        None       = 0,
        LightPunch = 1 << 0,   // Jab
        HeavyPunch = 1 << 1,   // Punch
        Kick       = 1 << 2,   // Kick / 空中变形：JumpKick / DiveKick
        Jump       = 1 << 3,
        // v2 预留：Block = 1 << 4, Throw = 1 << 5, Skill1 = 1 << 6, Skill2 = 1 << 7
    }

    /// <summary>
    /// 一玩家一帧的输入快照。结构体值类型，按值传递，rollback 友好。
    /// MoveX / MoveY 均为 -1 / 0 / 1，Buttons 为 InputButton bitmask。
    /// </summary>
    public struct FrameInput
    {
        public sbyte MoveX;
        public sbyte MoveY;
        public byte Buttons;

        public bool IsDown(InputButton button)
        {
            return (Buttons & (byte)button) != 0;
        }

        public static FrameInput Empty
        {
            get { return default; }
        }
    }
}
