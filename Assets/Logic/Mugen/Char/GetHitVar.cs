// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go  (GetHitVar struct) — 定点常用字段子集。
// Adapted to fixed-point (FFloat). gethitvar(...) 触发器经 OC_ex_ + 字段id 读取(已接入, 见 MChar.ReadGetHitVar)；
// 命中系统(MHitSystem)负责填值，本类提供深拷/哈希(回滚)。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// 受击变量（对应 Ikemen GetHitVar）。命中结算时由 HitDef 写入，受击方状态机/表达式读取
    /// （gethitvar(...)）。M3 先落常用字段 + 深拷/哈希（回滚需要），完整字段与 trigger 读取归 M7。
    /// </summary>
    public sealed class MGetHitVar
    {
        // 击退速度
        public FFloat XVel;
        public FFloat YVel;
        public FFloat ZVel;

        // 时间/计数
        public int HitShakeTime;     // 命中抖动（双方冻结）帧
        public int HitTime;          // 受击硬直帧
        public int SlideTime;        // 滑行帧
        public int CtrlTime;         // 防御硬直帧
        public int Damage;           // 本次伤害
        public int HitCount;         // 连击数
        public int FallCount;        // 浮空内被击次数

        // 反应类型 / 标志
        public int AnimType;         // 实际受击动画类型 gethitvar(animtype)（gethitAnimtype() 派生：地/空/fall 三选一）
        public int AttrType;         // gethitvar(type)：命中属性类型（按当前 stateType 取 groundtype/airtype）原始码
        public int GroundType = (int)Hit.MHitType.High;   // gethitvar(groundtype)：HitDef ground.type（1=high,2=low,3=trip）
        public int AirType = (int)Hit.MHitType.High;      // gethitvar(airtype)：HitDef air.type
        public int GroundAnimType;   // HitDef animtype（地面反应类型，gethitAnimtype 源）
        public int AirAnimType;      // HitDef air.animtype
        public int FallAnimType = (int)Hit.MReaction.Up;  // HitDef fall.animtype（默认 Up）
        public bool Fall;            // gethitvar(fall)：是否击倒/浮空（= Ikemen fallflag）
        public bool Guarded;         // 是否被防御
        public bool Up;              // 是否处于击倒上升段（fall.recover 判定用）
        public bool ForceStand;      // HitDef forcestand：蹲被击改判站立反应（char.go:12241）
        public bool Kill = true;     // 本次受击是否可致死（HitDef kill；后续 fall 伤害沿用，char.go:10579）

        // 重力 / 击飞（fall 分支用，common1 5030 读 gethitvar(yaccel)、5050 读 fall.*）
        public FFloat YAccel;        // gethitvar(yaccel)：浮空下落加速度
        public FFloat FallXVel;      // gethitvar(fall.xvel)
        public FFloat FallYVel;      // gethitvar(fall.yvel)：落地反弹后的 Y 速度
        public bool FallRecover = true;  // 是否允许 fall.recover 起身
        public int FallRecoverTime = 4;  // canRecover 所需浮空帧数
        public int DownRecoverTime;  // 倒地起身计时（5110 读，逐帧递减）

        public MGetHitVar Clone()
        {
            return new MGetHitVar
            {
                XVel = XVel, YVel = YVel, ZVel = ZVel,
                HitShakeTime = HitShakeTime, HitTime = HitTime, SlideTime = SlideTime,
                CtrlTime = CtrlTime, Damage = Damage, HitCount = HitCount, FallCount = FallCount,
                AnimType = AnimType, AttrType = AttrType, GroundType = GroundType, AirType = AirType,
                GroundAnimType = GroundAnimType, AirAnimType = AirAnimType, FallAnimType = FallAnimType,
                Fall = Fall, Guarded = Guarded, Up = Up, ForceStand = ForceStand, Kill = Kill,
                YAccel = YAccel, FallXVel = FallXVel, FallYVel = FallYVel,
                FallRecover = FallRecover, FallRecoverTime = FallRecoverTime, DownRecoverTime = DownRecoverTime,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddFixed(XVel); hash.AddFixed(YVel); hash.AddFixed(ZVel);
            hash.AddInt32(HitShakeTime); hash.AddInt32(HitTime); hash.AddInt32(SlideTime);
            hash.AddInt32(CtrlTime); hash.AddInt32(Damage); hash.AddInt32(HitCount); hash.AddInt32(FallCount);
            hash.AddInt32(AnimType); hash.AddInt32(AttrType); hash.AddInt32(GroundType); hash.AddInt32(AirType);
            hash.AddInt32(GroundAnimType); hash.AddInt32(AirAnimType); hash.AddInt32(FallAnimType);
            hash.AddBool(Fall); hash.AddBool(Guarded); hash.AddBool(Up);
            hash.AddBool(ForceStand); hash.AddBool(Kill);
            hash.AddFixed(YAccel); hash.AddFixed(FallXVel); hash.AddFixed(FallYVel);
            hash.AddBool(FallRecover); hash.AddInt32(FallRecoverTime); hash.AddInt32(DownRecoverTime);
        }
    }
}
