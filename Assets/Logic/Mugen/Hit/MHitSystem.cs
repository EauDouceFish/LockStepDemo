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
            // 守招 chip 伤害（含攻防倍率；guard.kill=0 时不致死，char.go:10587/10589）
            int dealt = ComputeDamage(defender.Life, hd.GuardDamage, hd.GuardKill,
                attacker.AttackDamageMul(), defender.ComputeFinalDefense());
            defender.Life = ClampLife(defender.Life - dealt, defender.LifeMax);

            // 能量结算（被防：攻方 +guardgetpower、守方 +guardgivepower）
            attacker.Power = AddPower(attacker.Power, hd.GuardGetPower, attacker.PowerMax);
            defender.Power = AddPower(defender.Power, hd.GuardGivePower, defender.PowerMax);

            defender.Facing = -attacker.Facing;
            defender.Vel = new FVector3(hd.GuardVelX, FFloat.Zero, FFloat.Zero);

            attacker.Hitstop = hd.P1PauseTime;
            defender.Hitstop = hd.P2PauseTime;

            MGetHitVar ghv = defender.Ghv;
            ghv.Damage = dealt;
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

        // 命中结算（移植 Ikemen char.go getHitVarSet + 受击状态路由 char.go:12195-12259）。
        static void ApplyHit(MChar attacker, MChar defender, MHitDef hd)
        {
            int stateType = defender.StateType;
            bool isAir = stateType == 4;

            // 守方先进受击态 H（受击当帧成立）：使 DefenceMulSet onHit 的 customDefense 对本次命中即生效
            // （对齐 char.go finalDefense 在 movetype==MT_H 下取 customDefense）。
            defender.MoveType = 2;

            // 伤害（含攻防倍率；kill=0 不致死保底 1 血；对齐 char.go:8433 computeDamage + :12252 蹲被击致死改站立倒下）
            int dealt = ComputeDamage(defender.Life, hd.HitDamage, hd.Kill,
                attacker.AttackDamageMul(), defender.ComputeFinalDefense());
            defender.Life = ClampLife(defender.Life - dealt, defender.LifeMax);

            // 能量结算（命中：攻方 +getpower、守方 +givepower；char.go:931-961 + powerAdd）
            attacker.Power = AddPower(attacker.Power, hd.HitGetPower, attacker.PowerMax);
            defender.Power = AddPower(defender.Power, hd.HitGivePower, defender.PowerMax);

            // 守方转向面对攻方 + 击退速度（连续量；vx/vy 与 ghv.xvel/yvel 同惯例，物理积分时乘 facing）
            defender.Facing = -attacker.Facing;
            FFloat vx = isAir ? hd.AirVelX : hd.GroundVelX;
            FFloat vy = isAir ? hd.AirVelY : hd.GroundVelY;
            defender.Vel = new FVector3(vx, vy, FFloat.Zero);

            // hitstop：仅攻方冻结。守方不进 hitpause（对齐 Ikemen char.go:12202 getter.hitPauseTime=0），
            // 其"受击抖动冻结"由 ghv.HitShakeTime 驱动（逐帧递减，见 MStateMachine.UpdateGetHitTimers）。
            attacker.Hitstop = hd.P1PauseTime;
            defender.Hitstop = 0;

            // GetHitVar 填值（char.go:10790-10924）
            MGetHitVar ghv = defender.Ghv;
            ghv.Damage = dealt;
            ghv.Kill = hd.Kill;
            ghv.ForceStand = hd.ForceStand;
            ghv.XVel = vx;
            ghv.YVel = vy;
            ghv.SlideTime = hd.GroundSlideTime;
            ghv.HitShakeTime = hd.P2PauseTime < 0 ? 0 : hd.P2PauseTime;
            ghv.HitTime = isAir ? hd.AirHitTime : hd.GroundHitTime;
            if (ghv.HitTime < 0)
            {
                ghv.HitTime = 0;
            }
            ghv.CtrlTime = isAir ? hd.AirHitTime : hd.GroundHitTime;
            ghv.GroundType = (int)hd.GroundType;
            ghv.AirType = (int)hd.AirType;
            ghv.AttrType = isAir ? ghv.AirType : ghv.GroundType;
            ghv.GroundAnimType = (int)hd.AnimType;
            ghv.AirAnimType = (int)hd.AirAnimType;
            ghv.FallAnimType = (int)hd.FallAnimType;
            ghv.YAccel = hd.YAccel;
            ghv.FallXVel = hd.FallXVel;
            ghv.FallYVel = hd.FallYVel;
            ghv.FallRecover = hd.FallRecover;
            ghv.FallRecoverTime = hd.FallRecoverTime;
            ghv.Fall = hd.Fall || isAir;
            ghv.Guarded = false;
            ghv.HitCount++;
            defender.FallTime = 0;
            // 实际受击动画类型（须在 yvel/animtype 之后派生，char.go:10924 gethitAnimtype）
            ghv.AnimType = defender.GetHitAnimType();

            // 守方进入受击状态 + movetype H + 失控
            defender.PendingStateNo = ResolveGetHitState(defender, hd, ghv);
            defender.PendingIsSelf = true;
            defender.Ctrl = false;        // MoveType=2(H) 已在伤害计算前设置

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

        // 受击状态号路由（移植 Ikemen char.go:12195-12259）：
        // p2stateno 优先（命中进自定义状态）；否则倒地→5080、摔绊→5070、按 statetype S/C/A→5000/5010/5020
        // （蹲被击致死则改进 5000 站立倒地，char.go:12252）。
        static int ResolveGetHitState(MChar defender, MHitDef hd, MGetHitVar ghv)
        {
            if (hd.P2StateNo >= 0)
            {
                return hd.P2StateNo;
            }
            bool downed = defender.StateType == 8 && defender.Pos.Y == FFloat.Zero;   // ST_L 倒地
            if (downed)
            {
                return 5080;
            }
            if (ghv.AttrType == (int)MHitType.Trip)
            {
                return 5070;
            }
            // forcestand：蹲被击改判站立反应（char.go:12241 changeStateType(ST_S)）
            if (ghv.ForceStand && defender.StateType == 2)
            {
                defender.StateType = 1;
            }
            switch (defender.StateType)
            {
                case 1:   // ST_S 立
                    return 5000;
                case 2:   // ST_C 蹲
                    return defender.Life <= 0 ? 5000 : 5010;   // 蹲被击致死→站立倒下
                default:  // ST_A 空中
                    return 5020;
            }
        }

        // 计算实际造成的伤害（移植 char.go:8433 computeDamage）：
        // damage ×= atkMul / finalDefense（攻防倍率，char.go:8440）→ 至少 1（char.go:8443）→
        // 不超过剩余血量（bounds，char.go:8447）→ kill=0 保底剩 1（char.go:8453）→ 四舍五入取整（math.Round）。
        // 默认 atkMul=finalDefense=1 时退化为纯整数，与既有行为一致。
        static int ComputeDamage(int life, int damage, bool kill, FFloat atkMul, FFloat finalDefense)
        {
            if (damage == 0)
            {
                return 0;
            }
            FFloat dealt = FFloat.FromInt(damage) * atkMul / finalDefense;
            // 极高防/极低攻仍至少 1 点（仅对正伤害）
            if (dealt > FFloat.Zero && dealt < FFloat.One)
            {
                dealt = FFloat.One;
            }
            FFloat lifeF = FFloat.FromInt(life);
            if (dealt > lifeF)
            {
                dealt = lifeF;   // 伤害不超过剩余血量
            }
            if (!kill && dealt >= lifeF && life > 0)
            {
                dealt = FFloat.FromInt(life - 1);   // 不可致死：保底剩 1 血
            }
            // 四舍五入（伤害非负，floor(x+0.5) 即 Round half away from zero）
            return (dealt + FFloat.Half).ToInt();
        }

        static int ClampLife(int life, int lifeMax)
        {
            if (life < 0)
            {
                return 0;
            }
            return life > lifeMax ? lifeMax : life;
        }

        // 能量增减（powerAdd）：累加后夹到 [0, PowerMax]。
        static int AddPower(int current, int add, int powerMax)
        {
            long value = (long)current + add;
            if (value < 0)
            {
                value = 0;
            }
            if (value > powerMax)
            {
                value = powerMax;
            }
            return (int)value;
        }
    }
}
