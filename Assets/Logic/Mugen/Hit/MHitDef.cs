// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go (HitDef struct, 取驱动离散逻辑的核心字段) + Clsn 重叠几何。
// Adapted to fixed-point. 完整 130 字段中表现/音效/projectile/cornerpush 等留后续；连续量(击退速度)容差对账。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Core;
using Lockstep.Math;

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
        public MReaction AnimType = MReaction.Light;
        public MHitType GroundType = MHitType.High;
        public bool Fall;
        public int P1StateNo = -1;  // 攻方命中后切状态（-1 不切）
        public int P2StateNo = -1;  // 守方切状态（-1=默认 5000）
        public int NumHits = 1;

        public MHitDef Clone()
        {
            return (MHitDef)MemberwiseClone();
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddBool(Active); hash.AddInt32(Attr);
            hash.AddBool(HitHigh); hash.AddBool(HitLow); hash.AddBool(HitAir); hash.AddBool(HitDown);
            hash.AddBool(GuardHigh); hash.AddBool(GuardLow); hash.AddBool(GuardAir);
            hash.AddInt32(GuardHitTime); hash.AddInt32(GuardCtrlTime); hash.AddFixed(GuardVelX);
            hash.AddInt32(HitDamage); hash.AddInt32(GuardDamage);
            hash.AddInt32(P1PauseTime); hash.AddInt32(P2PauseTime);
            hash.AddInt32(GroundHitTime); hash.AddInt32(AirHitTime); hash.AddInt32(GroundSlideTime);
            hash.AddFixed(GroundVelX); hash.AddFixed(GroundVelY); hash.AddFixed(AirVelX); hash.AddFixed(AirVelY);
            hash.AddInt32((int)AnimType); hash.AddInt32((int)GroundType);
            hash.AddBool(Fall); hash.AddInt32(P1StateNo); hash.AddInt32(P2StateNo); hash.AddInt32(NumHits);
        }
    }
}
