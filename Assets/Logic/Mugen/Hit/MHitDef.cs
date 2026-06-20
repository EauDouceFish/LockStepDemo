// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go (HitDef struct, 取驱动离散逻辑的核心字段) + Clsn 重叠几何。
// Adapted to fixed-point. 完整 130 字段中表现/音效/projectile/cornerpush 等留后续；连续量(击退速度)容差对账。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;

namespace Lockstep.Mugen.Hit
{
    /// <summary>受击动画反应类型（对应 Ikemen Reaction）。</summary>
    public enum MReaction { Light = 0, Medium = 1, Hard = 2, Back = 3, Up = 4, DiagUp = 5 }

    /// <summary>命中类型（对应 Ikemen HitType）。</summary>
    public enum MHitType { None = 0, High = 1, Low = 2, Trip = 3 }

    /// <summary>攻击类别位标志（对应 Ikemen AttackType AT_*）。N/S/H 强度 × A/T/P 类型。</summary>
    [System.Flags]
    public enum MAttackType
    {
        None = 0,
        NA = 1 << 0, NT = 1 << 1, NP = 1 << 2,   // Normal: Attack/Throw/Projectile
        SA = 1 << 3, ST = 1 << 4, SP = 1 << 5,   // Special
        HA = 1 << 6, HT = 1 << 7, HP = 1 << 8,   // Hyper
        AA = NA | SA | HA,
        AT = NT | ST | HT,
        AP = NP | SP | HP,
        All = AA | AT | AP,
        Hyper = HA | HT | HP,   // 超必杀类（对齐 Ikemen AT_AH，能量获取/给予默认值分支用）
    }

    /// <summary>2D 轴对齐包围盒（横 X × 高度 Y）。</summary>
    public readonly struct MAabb
    {
        public readonly FFloat MinX, MinY, MaxX, MaxY;
        public MAabb(FFloat minX, FFloat minY, FFloat maxX, FFloat maxY)
        {
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        }
    }

    /// <summary>角色局部 Clsn 框（相对原点）。</summary>
    public readonly struct MClsnBox
    {
        public readonly FFloat X1, Y1, X2, Y2;
        public MClsnBox(FFloat x1, FFloat y1, FFloat x2, FFloat y2)
        {
            X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
        }
    }

    /// <summary>Clsn 几何：局部框按朝向镜像+平移到世界，AABB 重叠判定。</summary>
    public static class MClsn
    {
        public static MAabb ToWorld(MClsnBox box, FFloat originX, FFloat originY, FFloat facing)
        {
            FFloat x1 = box.X1 * facing + originX;
            FFloat x2 = box.X2 * facing + originX;
            FFloat y1 = box.Y1 + originY;
            FFloat y2 = box.Y2 + originY;
            return new MAabb(Min(x1, x2), Min(y1, y2), Max(x1, x2), Max(y1, y2));
        }

        public static bool Overlap(MAabb a, MAabb b)
        {
            return a.MinX <= b.MaxX && a.MaxX >= b.MinX && a.MinY <= b.MaxY && a.MaxY >= b.MinY;
        }

        public static bool AnyOverlap(MClsnBox[] attack, FFloat ax, FFloat ay, FFloat af,
            MClsnBox[] hurt, FFloat hx, FFloat hy, FFloat hf)
        {
            if (attack == null || hurt == null)
            {
                return false;
            }
            for (int i = 0; i < attack.Length; i++)
            {
                MAabb atk = ToWorld(attack[i], ax, ay, af);
                for (int j = 0; j < hurt.Length; j++)
                {
                    if (Overlap(atk, ToWorld(hurt[j], hx, hy, hf)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        static FFloat Min(FFloat a, FFloat b) => a < b ? a : b;
        static FFloat Max(FFloat a, FFloat b) => a > b ? a : b;
    }

    /// <summary>HitDef 核心字段（定点）。攻方激活后挂在 MChar.HitDef，命中检测/结算读取。</summary>
    public sealed class MHitDef
    {
        public bool Active;

        // attr 攻击类别（HitBy/NotHitBy 过滤 + 守招判定用）
        public int Attr = (int)MAttackType.NA;
        public int Id;              // HitDef id: target(id) / Target* id filters select by this hit relationship id.

        // hitflag：哪些受击姿态能被命中（H 站立/L 蹲/A 空中/D 倒地）
        public bool HitHigh = true;
        public bool HitLow = true;
        public bool HitAir = true;
        public bool HitDown;

        // guardflag：哪些姿态可防御本攻击（高/低/空），守招判定用
        public bool GuardHigh = true;
        public bool GuardLow = true;
        public bool GuardAir = true;

        // 伤害
        public int HitDamage;
        public int GuardDamage;
        public BytecodeExp HitDamageExpr;
        public BytecodeExp GuardDamageExpr;
        public BytecodeExp FallDamageExpr;

        // 时间（帧）
        public int P1PauseTime;     // 攻方 hitstop
        public int P2PauseTime;     // 守方 hitshake(冻结)
        public int GroundHitTime;   // 守方地面硬直
        public int AirHitTime;      // 守方空中硬直
        public int GroundSlideTime; // 守方滑行

        // 守招相关
        public int GuardHitTime;
        public int GuardCtrlTime;
        public FFloat GuardVelX;

        // 击退速度（连续量，容差对账）
        public FFloat GroundVelX, GroundVelY;
        public FFloat AirVelX, AirVelY;

        // 反应/状态
        public MReaction AnimType = MReaction.Light;       // animtype（地面反应类型）
        public MReaction AirAnimType = MReaction.Up;       // air.animtype（默认随 animtype，解析时回填）
        public MReaction FallAnimType = MReaction.Up;      // fall.animtype
        public MHitType GroundType = MHitType.High;        // ground.type
        public MHitType AirType = MHitType.High;           // air.type
        public bool Fall;
        public int P1StateNo = -1;  // 攻方命中后切状态（-1 不切）
        public int P2StateNo = -1;  // 守方切状态（-1=默认 5000）
        public bool P2GetP1State = true;  // 守方进 p2stateno 时是否用攻方状态表（投技自定义状态，默认 true）
        public int NumHits = 1;
        // hitonce：命中后是否立即停用本 HitDef（只命中一个目标）。-1=未设(解析时按投技→1/否则0 决议，char.go:848)。
        public int HitOnce = -1;

        // 击飞 fall 分支（连续量容差；解析归 R-HITDEF，此处给 MUGEN 默认值）
        public FFloat YAccel = FFloat.FromInt(35) / FFloat.FromInt(100);   // yaccel 默认 .35
        public FFloat FallXVel = FFloat.FromInt(-45) / FFloat.FromInt(10); // fall.xvelocity 显式值；未写时命中结算沿用当前 X 速度
        public bool FallXVelSet;                         // Ikemen 用 NaN 表示未写；定点侧用显式标志。
        public FFloat FallYVel = FFloat.FromInt(-45) / FFloat.FromInt(10); // fall.yvelocity 默认 -4.5
        public bool FallRecover = true;     // fall.recover 默认 1
        public int FallRecoverTime = 4;     // fall.recovertime 默认 4
        public int FallDamage;              // fall.damage：受击方落地时受到的伤害（默认 0，char.go:9109）
        public int AirJuggle;               // air.juggle：弹幕/Ikemen 扩展用空中连段点消耗，默认 0（char.go:997）。

        // 击打倒地对手（受击方 statetype=L）分支（char.go:10859-10871）
        public FFloat DownVelX, DownVelY;   // down.velocity（默认随 air.velocity，char.go:892-894）
        public int DownHitTime = 20;        // down.hittime（默认 20，char.go:724）
        public bool DownBounce;             // down.bounce（默认 0；false 时倒地命中不反弹）

        // KO 阻止（kill=0：本次伤害不会致死，最多打到剩 1 血；对齐 char.go:8453 computeDamage）
        public bool Kill = true;        // kill 默认 1
        public bool GuardKill = true;   // guard.kill 默认 1
        public bool FallKill = true;    // fall.kill 默认 1（落地 fall.damage 是否可致死，归后续 HitFallDamage）

        // 强制站立/下蹲反应（forcestand：有 Y 击退速度时默认开，char.go:911）
        public bool ForceStand;

        // 能量获取/给予（命中→攻方/守方加能量；默认按 lifetopowermul 常量，char.go:931-961）
        public int HitGetPower;     // 命中时攻方 +power（默认 0.7×damage，超必杀 0）
        public int HitGivePower;    // 命中时守方 +power（默认 0.6×damage）
        public int GuardGetPower;   // 被防时攻方 +power（默认 hitgetpower×0.5）
        public int GuardGivePower;  // 被防时守方 +power（默认 hitgivepower×0.5）

        public MHitDef Clone()
        {
            return (MHitDef)MemberwiseClone();
        }

        public void ResolveDynamicValues(MChar character)
        {
            if (HitDamageExpr != null)
            {
                HitDamage = HitDamageExpr.Run(character).ToI();
            }
            if (GuardDamageExpr != null)
            {
                GuardDamage = GuardDamageExpr.Run(character).ToI();
            }
            if (FallDamageExpr != null)
            {
                FallDamage = FallDamageExpr.Run(character).ToI();
            }
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddBool(Active); hash.AddInt32(Attr); hash.AddInt32(Id);
            hash.AddBool(HitHigh); hash.AddBool(HitLow); hash.AddBool(HitAir); hash.AddBool(HitDown);
            hash.AddBool(GuardHigh); hash.AddBool(GuardLow); hash.AddBool(GuardAir);
            hash.AddInt32(GuardHitTime); hash.AddInt32(GuardCtrlTime); hash.AddFixed(GuardVelX);
            hash.AddInt32(HitDamage); hash.AddInt32(GuardDamage);
            hash.AddInt32(P1PauseTime); hash.AddInt32(P2PauseTime);
            hash.AddInt32(GroundHitTime); hash.AddInt32(AirHitTime); hash.AddInt32(GroundSlideTime);
            hash.AddFixed(GroundVelX); hash.AddFixed(GroundVelY); hash.AddFixed(AirVelX); hash.AddFixed(AirVelY);
            hash.AddInt32((int)AnimType); hash.AddInt32((int)AirAnimType); hash.AddInt32((int)FallAnimType);
            hash.AddInt32((int)GroundType); hash.AddInt32((int)AirType);
            hash.AddBool(Fall); hash.AddInt32(P1StateNo); hash.AddInt32(P2StateNo); hash.AddBool(P2GetP1State); hash.AddInt32(NumHits); hash.AddInt32(HitOnce);
            hash.AddFixed(YAccel); hash.AddFixed(FallXVel); hash.AddBool(FallXVelSet); hash.AddFixed(FallYVel);
            hash.AddBool(FallRecover); hash.AddInt32(FallRecoverTime); hash.AddInt32(FallDamage); hash.AddInt32(AirJuggle);
            hash.AddFixed(DownVelX); hash.AddFixed(DownVelY); hash.AddInt32(DownHitTime); hash.AddBool(DownBounce);
            hash.AddBool(Kill); hash.AddBool(GuardKill); hash.AddBool(FallKill); hash.AddBool(ForceStand);
            hash.AddInt32(HitGetPower); hash.AddInt32(HitGivePower);
            hash.AddInt32(GuardGetPower); hash.AddInt32(GuardGivePower);
        }
    }
}
