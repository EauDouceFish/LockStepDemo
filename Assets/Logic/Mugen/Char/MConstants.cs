// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go (CharData/CharSize/CharVelocity/CharMovement struct + init 默认值)。
// 角色常量（[Data]/[Size]/[Velocity]/[Movement] 段），由 MugenConstParser 解析填充，
// const(...) 触发器经 OC_const_ + MConstId 读取。常量在加载后不可变，故不参与回滚快照/哈希
// （MChar 仅持引用、浅拷共享）。字段为真实角色用到的子集，对齐 Ikemen 默认值；定点化。
// 注：Ikemen size/velocity const 带 localcoord 缩放((320/localcoord)/localscl)，v1 单坐标(320)下因子=1，故略。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Math;
using Lockstep.Mugen.Expr;

namespace Lockstep.Mugen.Char
{
    /// <summary>角色不可变常量集（const(...) 的取值来源）。默认值对齐 Ikemen *.init()。</summary>
    public sealed class MConstants
    {
        // [Data]（整数）
        public int Life = 1000;
        public int Power = 3000;
        public int Attack = 100;
        public int Defence = 100;
        public int FallDefenceUp = 50;
        public int LiedownTime = 60;
        public int Airjuggle = 15;

        // [Size]（定点；MUGEN 原生键 ground.front/back、air.front/back、height、head/mid.pos）
        public FFloat SizeGroundBack = FFloat.FromInt(16);
        public FFloat SizeGroundFront = FFloat.FromInt(16);
        public FFloat SizeAirBack = FFloat.FromInt(12);
        public FFloat SizeAirFront = FFloat.FromInt(12);
        public FFloat SizeHeight = FFloat.FromInt(60);
        public FFloat HeadPosX = FFloat.FromInt(-5);
        public FFloat HeadPosY = FFloat.FromInt(-90);
        public FFloat MidPosX = FFloat.FromInt(-5);
        public FFloat MidPosY = FFloat.FromInt(-60);

        // [Velocity]（定点；默认 0，角色基本都显式给值）
        public FFloat WalkFwd;
        public FFloat WalkBack;
        public FFloat RunFwdX;
        public FFloat RunFwdY;
        public FFloat RunBackX;
        public FFloat RunBackY;
        public FFloat JumpNeuX;
        public FFloat JumpY;          // = jump.neu 的 y 分量（对齐 Ikemen velocity.jump.y）
        public FFloat JumpBack;
        public FFloat JumpFwd;
        public FFloat RunjumpFwdX;
        public FFloat RunjumpBackX;
        public FFloat RunjumpBackY;
        public FFloat AirjumpNeuX;
        public FFloat AirjumpY;       // = airjump.neu 的 y 分量
        public FFloat AirjumpBack;
        public FFloat AirjumpFwd;

        // [Movement]
        public FFloat Yaccel;
        public FFloat StandFriction;
        public FFloat CrouchFriction;
        public int AirjumpNum;
        public FFloat AirjumpHeight = FFloat.FromInt(35);

        /// <summary>按字段 id 取常量值（整数字段返回 Int，定点字段返回 Float）。未知 id 返回 0。</summary>
        public BytecodeValue Read(MConstId id)
        {
            switch (id)
            {
                case MConstId.DataLife: return BytecodeValue.Int(Life);
                case MConstId.DataPower: return BytecodeValue.Int(Power);
                case MConstId.DataAttack: return BytecodeValue.Int(Attack);
                case MConstId.DataDefence: return BytecodeValue.Int(Defence);
                case MConstId.DataFallDefenceUp: return BytecodeValue.Int(FallDefenceUp);
                case MConstId.DataLiedownTime: return BytecodeValue.Int(LiedownTime);
                case MConstId.DataAirjuggle: return BytecodeValue.Int(Airjuggle);

                case MConstId.SizeGroundBack: return BytecodeValue.Float(SizeGroundBack);
                case MConstId.SizeGroundFront: return BytecodeValue.Float(SizeGroundFront);
                case MConstId.SizeAirBack: return BytecodeValue.Float(SizeAirBack);
                case MConstId.SizeAirFront: return BytecodeValue.Float(SizeAirFront);
                case MConstId.SizeHeight: return BytecodeValue.Float(SizeHeight);
                case MConstId.SizeHeadPosX: return BytecodeValue.Float(HeadPosX);
                case MConstId.SizeHeadPosY: return BytecodeValue.Float(HeadPosY);
                case MConstId.SizeMidPosX: return BytecodeValue.Float(MidPosX);
                case MConstId.SizeMidPosY: return BytecodeValue.Float(MidPosY);

                case MConstId.VelWalkFwd: return BytecodeValue.Float(WalkFwd);
                case MConstId.VelWalkBack: return BytecodeValue.Float(WalkBack);
                case MConstId.VelRunFwdX: return BytecodeValue.Float(RunFwdX);
                case MConstId.VelRunFwdY: return BytecodeValue.Float(RunFwdY);
                case MConstId.VelRunBackX: return BytecodeValue.Float(RunBackX);
                case MConstId.VelRunBackY: return BytecodeValue.Float(RunBackY);
                case MConstId.VelJumpNeuX: return BytecodeValue.Float(JumpNeuX);
                case MConstId.VelJumpY: return BytecodeValue.Float(JumpY);
                case MConstId.VelJumpBack: return BytecodeValue.Float(JumpBack);
                case MConstId.VelJumpFwd: return BytecodeValue.Float(JumpFwd);
                case MConstId.VelRunjumpFwdX: return BytecodeValue.Float(RunjumpFwdX);
                case MConstId.VelRunjumpBackX: return BytecodeValue.Float(RunjumpBackX);
                case MConstId.VelRunjumpBackY: return BytecodeValue.Float(RunjumpBackY);
                case MConstId.VelAirjumpNeuX: return BytecodeValue.Float(AirjumpNeuX);
                case MConstId.VelAirjumpY: return BytecodeValue.Float(AirjumpY);
                case MConstId.VelAirjumpBack: return BytecodeValue.Float(AirjumpBack);
                case MConstId.VelAirjumpFwd: return BytecodeValue.Float(AirjumpFwd);

                case MConstId.MoveYaccel: return BytecodeValue.Float(Yaccel);
                case MConstId.MoveStandFriction: return BytecodeValue.Float(StandFriction);
                case MConstId.MoveCrouchFriction: return BytecodeValue.Float(CrouchFriction);
                case MConstId.MoveAirjumpNum: return BytecodeValue.Int(AirjumpNum);
                case MConstId.MoveAirjumpHeight: return BytecodeValue.Float(AirjumpHeight);

                default: return BytecodeValue.Int(0);
            }
        }
    }
}
