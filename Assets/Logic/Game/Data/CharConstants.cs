using Lockstep.Math;

namespace Lockstep.Game.Data
{
    /// <summary>
    /// 角色常量（≈ MUGEN [Data]/[Velocity]/[Movement]）。全 int/FFloat，导入期把 MUGEN
    /// 小数量化成定点。骨架先列常用项，按需扩展。
    /// </summary>
    public sealed class CharConstants
    {
        public int Life;
        public int Power;
        public int Attack;
        public int Defence;

        public FFloat WalkFwdSpeed;
        public FFloat WalkBackSpeed;
        public FVector2 JumpVelocity;     // (横, 高度)
        public FFloat Gravity;            // 每帧高度加速度
    }
}
