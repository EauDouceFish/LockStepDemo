// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (OC_const_* 子操作码) + src/compiler.go (const(...) 名字映射)。
// 我方把 Ikemen 的 OC_const_<field> 子操作码族压成单字节 id（OC_const_ 后跟此 id），
// 仅覆盖真实角色实际用到的常量子集（data/size/velocity/movement）。完整字段族增量补。
// See Docs/移植方案_Ikemen.md.
namespace Lockstep.Mugen.Expr
{
    /// <summary>
    /// const(...) 常量字段 id（OC_const_ 操作码后携带 1 字节本枚举值）。
    /// 值类型见 <c>MConstants.Read</c>：data.* / movement.airjump.num 为整数，其余为定点。
    /// </summary>
    public enum MConstId : byte
    {
        Unknown = 0,

        // [Data]
        DataLife, DataPower, DataAttack, DataDefence, DataFallDefenceUp, DataLiedownTime, DataAirjuggle,

        // [Size]
        SizeGroundBack, SizeGroundFront, SizeAirBack, SizeAirFront, SizeHeight,
        SizeHeadPosX, SizeHeadPosY, SizeMidPosX, SizeMidPosY,

        // [Velocity]
        VelWalkFwd, VelWalkBack,
        VelRunFwdX, VelRunFwdY, VelRunBackX, VelRunBackY,
        VelJumpNeuX, VelJumpY, VelJumpBack, VelJumpFwd,
        VelRunjumpFwdX, VelRunjumpBackX, VelRunjumpBackY,
        VelAirjumpNeuX, VelAirjumpY, VelAirjumpBack, VelAirjumpFwd,

        // [Movement]
        MoveYaccel, MoveStandFriction, MoveCrouchFriction, MoveAirjumpNum, MoveAirjumpHeight,
    }
}
