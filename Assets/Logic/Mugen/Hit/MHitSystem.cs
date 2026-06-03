// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go (命中检测 + gethit 结算 + GetHitVar 填值)。
// 离散量(命中与否/伤害/受击状态号/连击计数)对账 Ikemen；连续量(击退速度/坐标)容差。
// 含 HitBy/NotHitBy 属性免疫过滤 + guardflag 守招结算(chip 伤害/守方击退/moveguarded，不进受击 5000)。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Math;
using Lockstep.Mugen.Char;

namespace Lockstep.Mugen.Hit
{
    /// <summary>命中系统：检测攻方 Clsn1 × 守方 Clsn2 重叠并结算一次命中。无静态可变状态。</summary>
    public static class MHitSystem
    {
        public const int GetHitStateNo = 5000;   // MUGEN 默认受击状态号

        /// <summary>尝试让 attacker 命中 defender；命中返回 true 并完成结算。</summary>
        public static bool TryHit(MChar attacker, MChar defender)
        {
            MHitDef hd = attacker.HitDef;
            if (hd == null || !hd.Active || attacker == defender)
            {
                return false;
            }
            if (attacker.Targets.Contains(defender))
            {
                return false;   // 同招对同目标只命中一次
            }
            if (!HitFlagMatches(hd, defender.StateType))
            {
                return false;
            }
            // HitBy/NotHitBy 属性免疫：HitBy 仅允许匹配的攻击命中；NotHitBy 仅允许不匹配的命中
            if (defender.HitByTime > 0)
            {
                bool attrMatch = (hd.Attr & defender.HitByAttr) != 0;
                if (defender.HitByIsNot ? attrMatch : !attrMatch)
                {
                    return false;   // 被免疫过滤挡下
                }
            }
            if (!MClsn.AnyOverlap(
                    attacker.Clsn1, attacker.Pos.X, attacker.Pos.Y, attacker.Facing,
                    defender.Clsn2, defender.Pos.X, defender.Pos.Y, defender.Facing))
            {
                return false;
            }
            if (defender.Guarding && GuardFlagAllows(hd, defender.StateType))
            {
                ApplyGuard(attacker, defender, hd);
            }
            else
            {
                ApplyHit(attacker, defender, hd);
            }
            return true;
        }

        static bool GuardFlagAllows(MHitDef hd, int defenderStateType)
        {
            switch (defenderStateType)
            {
                case 1: return hd.GuardHigh;   // S 站立防
                case 2: return hd.GuardLow;    // C 蹲防
                case 4: return hd.GuardAir;    // A 空中防
                default: return hd.GuardHigh;
            }
        }

        // 守招结算：chip 伤害 + 守方击退 + ghv.guarded + 攻方 moveguarded（不进受击 5000，不失 movetype）。
        static void ApplyGuard(MChar attacker, MChar defender, MHitDef hd)
        {
            int newLife = defender.Life - hd.GuardDamage;
            defender.Life = newLife < 0 ? 0 : (newLife > defender.LifeMax ? defender.LifeMax : newLife);

            defender.Facing = -attacker.Facing;
            defender.Vel = new FVector3(hd.GuardVelX, FFloat.Zero, FFloat.Zero);

            attacker.Hitstop = hd.P1PauseTime;
            defender.Hitstop = hd.P2PauseTime;

            MGetHitVar ghv = defender.Ghv;
            ghv.Damage = hd.GuardDamage;
            ghv.HitTime = hd.GuardHitTime;
            ghv.CtrlTime = hd.GuardCtrlTime;
            ghv.HitShakeTime = hd.P2PauseTime;
            ghv.Guarded = true;
            ghv.Fall = false;

            attacker.MoveContact = 1;
            attacker.MoveGuarded = 1;
            attacker.MoveHit = 0;
            attacker.Targets.Add(defender);
        }

        static bool HitFlagMatches(MHitDef hd, int defenderStateType)
        {
            switch (defenderStateType)
            {
                case 1: return hd.HitHigh;   // S 站立
                case 2: return hd.HitLow;    // C 蹲
                case 4: return hd.HitAir;    // A 空中
                case 8: return hd.HitDown;   // L 倒地
                default: return hd.HitHigh;
            }
        }

        static void ApplyHit(MChar attacker, MChar defender, MHitDef hd)
        {
            bool isAir = defender.StateType == 4;

            int newLife = defender.Life - hd.HitDamage;
            defender.Life = newLife < 0 ? 0 : (newLife > defender.LifeMax ? defender.LifeMax : newLife);

            // 守方转向面对攻方 + 击退速度（连续量；facing 在积分时应用）
            defender.Facing = -attacker.Facing;
            FFloat vx = isAir ? hd.AirVelX : hd.GroundVelX;
            FFloat vy = isAir ? hd.AirVelY : hd.GroundVelY;
            defender.Vel = new FVector3(vx, vy, FFloat.Zero);

            // hitstop（双方冻结）
            attacker.Hitstop = hd.P1PauseTime;
            defender.Hitstop = hd.P2PauseTime;

            // GetHitVar 填值
            MGetHitVar ghv = defender.Ghv;
            ghv.Damage = hd.HitDamage;
            ghv.XVel = vx;
            ghv.YVel = vy;
            ghv.HitTime = isAir ? hd.AirHitTime : hd.GroundHitTime;
            ghv.SlideTime = hd.GroundSlideTime;
            ghv.HitShakeTime = hd.P2PauseTime;
            ghv.CtrlTime = isAir ? hd.AirHitTime : hd.GroundHitTime;
            ghv.AnimType = (int)hd.AnimType;
            ghv.AttrType = (int)hd.GroundType;
            ghv.Fall = hd.Fall || isAir;
            ghv.Guarded = false;
            ghv.HitCount++;

            // 守方进入受击状态（默认 5000）+ movetype H + 失控
            defender.PendingStateNo = hd.P2StateNo >= 0 ? hd.P2StateNo : GetHitStateNo;
            defender.PendingIsSelf = true;
            defender.MoveType = 2;        // H 受击
            defender.Ctrl = false;

            // 攻方命中统计 + 目标登记 + 可选切状态
            attacker.MoveContact = 1;
            attacker.MoveHit = 1;
            attacker.HitCount++;
            attacker.UniqHitCount++;
            attacker.Targets.Add(defender);
            if (hd.P1StateNo >= 0)
            {
                attacker.PendingStateNo = hd.P1StateNo;
            }
        }
    }
}
