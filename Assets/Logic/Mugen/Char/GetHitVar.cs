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
        public int AnimType;         // 受击动画类型（light/medium/hard/back/...）原始码
        public int AttrType;         // 命中属性类型（S/C/A）原始码
        public bool Fall;            // 是否击倒/浮空
        public bool Guarded;         // 是否被防御
        public bool Up;              // 是否处于击倒上升段（fall.recover 判定用）

        public MGetHitVar Clone()
        {
            return new MGetHitVar
            {
                XVel = XVel, YVel = YVel, ZVel = ZVel,
                HitShakeTime = HitShakeTime, HitTime = HitTime, SlideTime = SlideTime,
                CtrlTime = CtrlTime, Damage = Damage, HitCount = HitCount, FallCount = FallCount,
                AnimType = AnimType, AttrType = AttrType,
                Fall = Fall, Guarded = Guarded, Up = Up,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddFixed(XVel); hash.AddFixed(YVel); hash.AddFixed(ZVel);
            hash.AddInt32(HitShakeTime); hash.AddInt32(HitTime); hash.AddInt32(SlideTime);
            hash.AddInt32(CtrlTime); hash.AddInt32(Damage); hash.AddInt32(HitCount); hash.AddInt32(FallCount);
            hash.AddInt32(AnimType); hash.AddInt32(AttrType);
            hash.AddBool(Fall); hash.AddBool(Guarded); hash.AddBool(Up);
        }
    }
}
