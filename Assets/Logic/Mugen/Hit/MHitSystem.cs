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

        static int CurrentStateOwnerPlayerNo(MChar character)
        {
            return character.StatePlayerNo >= 0 ? character.StatePlayerNo : character.PlayerNo;
        }

        static MChar CurrentStateOwner(MChar character, int ownerPlayerNo)
        {
            if (ownerPlayerNo == character.PlayerNo)
            {
                return character;
            }
            if (character.StateOwner != null && character.StateOwner.PlayerNo == ownerPlayerNo)
            {
                return character.StateOwner;
            }
            return null;
        }

        /// <summary>尝试让 attacker 命中 defender；命中返回 true 并完成结算。</summary>
        public static bool TryHit(MChar attacker, MChar defender, bool deferDamage = false)
        {
            MHitDef hd = attacker.HitDef;
            if (hd == null || !hd.Active || attacker == defender)
            {
                return false;
            }
            if (!CanAttack(attacker) || !CanBeHit(defender))
            {
                return false;
            }
            if (attacker.HasTarget(defender))
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
            if (TryReversal(defender, attacker, hd, deferDamage))
            {
                return true;
            }
            MHitOverride hitOverride = FindHitOverride(defender, hd.Attr);
            bool guarded = hitOverride.Active && hitOverride.ForceGuard;
            guarded = guarded || (defender.Guarding && GuardFlagAllows(hd, defender.StateType));
            if (!guarded && !CanJuggle(attacker, defender, hd, false))
            {
                return false;
            }
            if (guarded)
            {
                ApplyGuard(attacker, defender, hd, deferDamage);
            }
            else
            {
                ApplyHit(attacker, defender, hd, deferDamage, false);
            }
            return true;
        }

        static bool TryReversal(MChar defender, MChar attacker, MHitDef incoming, bool deferDamage)
        {
            MReversalDefRuntime reversal = defender.ReversalDef;
            if (reversal == null || !reversal.Active || reversal.Template == null || !reversal.Template.Active)
            {
                return false;
            }
            if ((reversal.Attr & incoming.Attr) == 0)
            {
                return false;
            }

            MHitDef reversalHit = reversal.Template;
            reversalHit.Active = true;
            ApplyHit(defender, attacker, reversalHit, deferDamage, false);
            defender.MoveReversed = 1;
            return true;
        }

        /// <summary>
        /// 弹幕命中（切片 3b）：用弹幕几何(Clsn1/Pos/Facing)+弹幕 HitDef 判重叠，命中走 ApplyHit(proj.Owner,...)。
        /// 攻方侧效果(能量/movehit/targets)归 owner；返回是否命中（命中后弹幕由引擎移除/计数）。
        /// </summary>
        public static bool TryProjectileHit(MProjectile proj, MChar defender, bool deferDamage = false)
        {
            MHitDef hd = proj.HitDef;
            if (hd == null || proj.Owner == defender || proj.Clsn1 == null)
            {
                return false;
            }
            if (!CanBeHit(defender))
            {
                return false;
            }
            if (!HitFlagMatches(hd, defender.StateType))
            {
                return false;
            }
            if (defender.HitByTime > 0)
            {
                bool attrMatch = (hd.Attr & defender.HitByAttr) != 0;
                if (defender.HitByIsNot ? attrMatch : !attrMatch)
                {
                    return false;
                }
            }
            if (!MClsn.AnyOverlap(
                    proj.Clsn1, proj.Pos.X, proj.Pos.Y, proj.Facing,
                    defender.Clsn2, defender.Pos.X, defender.Pos.Y, defender.Facing))
            {
                return false;
            }
            proj.ContactCount++;
            if (proj.Owner != null && TryReversal(defender, proj.Owner, hd, deferDamage))
            {
                proj.Owner.RecordProjectileContact(proj.ProjId, false);
                return true;
            }
            MHitOverride hitOverride = FindHitOverride(defender, hd.Attr);
            bool guarded = hitOverride.Active && hitOverride.ForceGuard;
            guarded = guarded || (defender.Guarding && GuardFlagAllows(hd, defender.StateType));
            if (!guarded && !CanJuggle(proj.Owner, defender, hd, true))
            {
                return false;
            }
            if (proj.Owner != null)
            {
                proj.Owner.RecordProjectileContact(proj.ProjId, guarded);
            }
            if (guarded)
            {
                ApplyGuard(proj.Owner, defender, hd, deferDamage);
            }
            else
            {
                ApplyHit(proj.Owner, defender, hd, deferDamage, true);
                proj.HitCount++;
            }
            return true;
        }

        static bool CanAttack(MChar attacker)
        {
            return attacker != null && !attacker.PauseBool && attacker.Acttmp >= 0;
        }

        static bool CanBeHit(MChar defender)
        {
            return defender != null && defender.UnhittableTime <= 0;
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
        static void ApplyGuard(MChar attacker, MChar defender, MHitDef hd, bool deferDamage)
        {
            // 守招 chip 伤害（含攻防倍率；公式对标 char.go:11074-11078 Damage on guard，computeDamage 本体 char.go:8433；
            // guard.kill 等价 Ikemen getter.ghv.kill 在守招路径，char.go:10579/10587）。
            // 伤害应用时机同 ApplyHit 注释（R-DMG-PIPELINE）：引擎路径累计，直接单测路径默认即时应用。
            int dealt = ComputeDamage(defender.Life, hd.GuardDamage, hd.GuardKill,
                attacker.AttackDamageMul(), defender.ComputeFinalDefense());
            QueueLifeDamage(defender, dealt, deferDamage);

            // 能量结算（被防：攻方 +guardgetpower、守方 +guardgivepower）
            attacker.Power = AddPower(attacker.Power, hd.GuardGetPower, attacker.PowerMax);
            defender.Power = AddPower(defender.Power, hd.GuardGivePower, defender.PowerMax);

            defender.Facing = -attacker.Facing;
            defender.Vel = new FVector3(hd.GuardVelX, FFloat.Zero, FFloat.Zero);

            attacker.Hitstop = hd.P1PauseTime;
            defender.Hitstop = 0;

            MGetHitVar ghv = defender.Ghv;
            ghv.Damage = dealt;
            ghv.HitTime = hd.GuardHitTime < 0 ? 0 : hd.GuardHitTime;
            ghv.CtrlTime = hd.GuardCtrlTime;
            ghv.GuardCtrlTimeLeft = hd.GuardCtrlTime < 0 ? 0 : hd.GuardCtrlTime;
            ghv.HitShakeTime = hd.P2PauseTime < 0 ? 0 : hd.P2PauseTime;
            ghv.Guarded = true;
            ghv.Fall = false;
            defender.MoveType = 2;
            defender.Ctrl = false;
            defender.StateOwner = null;
            defender.QueueTransition(ResolveGuardState(defender), defender.PlayerNo, ctrl: 0);

            attacker.MoveContact = 1;
            attacker.MoveGuarded = 1;
            attacker.MoveHit = 0;
            attacker.MoveReversed = 0;
            attacker.MoveContactTime = 1;
            attacker.GuardCount += hd.NumHits;   // char.go:12191（被防按 numhits 计）
            attacker.AddTarget(defender, hd.Id);
            if (hd.HitOnce > 0)
            {
                hd.Active = false;   // hitonce 在被防（接触）时同样停用（char.go:12171 hitdefContact）
            }
        }

        static int ResolveGuardState(MChar defender)
        {
            switch (defender.StateType)
            {
                case 2: return 152;
                case 4: return 154;
                default: return 150;
            }
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

        static bool CanJuggle(MChar attacker, MChar defender, MHitDef hd, bool projectile)
        {
            if (!NeedsJuggleCheck(defender))
            {
                return true;
            }
            if ((attacker.AssertFlags & (int)MAssertFlag.NoJuggleCheck) != 0)
            {
                return true;
            }
            int cost = projectile ? hd.AirJuggle : attacker.Juggle;
            if (cost <= 0)
            {
                return true;
            }
            return cost <= defender.Ghv.GetJuggle(attacker.Id, AirJuggleMax(attacker));
        }

        static bool NeedsJuggleCheck(MChar defender)
        {
            return defender.StateType == 4 || defender.StateType == 8 || defender.Ghv.Fall;
        }

        static int AirJuggleMax(MChar character)
        {
            return character.Constants != null ? character.Constants.Airjuggle : 15;
        }

        // 命中结算（移植 Ikemen char.go getHitVarSet + 受击状态路由 char.go:12195-12259）。
        //
        // R-DMG-PIPELINE：
        //   Ikemen 是两段式——命中阶段把伤害「累加」到 getter.ghv.damage（char.go:11063/11078，不扣血），
        //   再在防守方自身 update 循环里「一次性应用」(char.go:11743 lifeAdd(-ghv.damage, kill, absolute=true))。
        //   本引擎在 battle 路径传 deferDamage=true，累计 PendingLifeDamage，帧内 hit/projectile 全部结算后统一扣血。
        //   直接调用 MHitSystem 的既有单测默认即时应用，保持旧行为；KO 排序细节后续由 R-ORACLE trace 精确化。
        static void ApplyHit(MChar attacker, MChar defender, MHitDef hd, bool deferDamage, bool projectile)
        {
            int stateType = defender.StateType;
            bool isAir = stateType == 4;
            bool wasFalling = defender.Ghv.Fall;
            MHitOverride hitOverride = FindHitOverride(defender, hd.Attr);
            if (hitOverride.Active && hitOverride.ForceAir)
            {
                stateType = 4;
                isAir = true;
            }

            // 守方先进受击态 H（受击当帧成立）：使 DefenceMulSet onHit 的 customDefense 对本次命中即生效
            // （对齐 char.go finalDefense 在 movetype==MT_H 下取 customDefense）。
            defender.MoveType = 2;

            // 伤害（含攻防倍率；kill=0 不致死保底 1 血；对齐 char.go:8433 computeDamage + :12252 蹲被击致死改站立倒下）
            int dealt = ComputeDamage(defender.Life, hd.HitDamage, hd.Kill,
                attacker.AttackDamageMul(), defender.ComputeFinalDefense());
            QueueLifeDamage(defender, dealt, deferDamage);

            // 能量结算（命中：攻方 +getpower、守方 +givepower；char.go:931-961 + powerAdd）
            attacker.Power = AddPower(attacker.Power, hd.HitGetPower, attacker.PowerMax);
            defender.Power = AddPower(defender.Power, hd.HitGivePower, defender.PowerMax);

            // 守方转向面对攻方 + 击退速度（连续量；vx/vy 与 ghv.xvel/yvel 同惯例，物理积分时乘 facing）
            // 倒地(ST_L)分支：贴地用 down.velocity，浮空倒地用 air.velocity；否则空中→air、地面→ground（char.go:10859-10873）。
            bool downed = stateType == 8;   // ST_L 倒地
            bool downedOnGround = downed && defender.Pos.Y == FFloat.Zero;
            FFloat vx;
            FFloat vy;
            if (downedOnGround)
            {
                vx = hd.DownVelX;
                vy = hd.DownVelY;
            }
            else if (isAir || downed)
            {
                vx = hd.AirVelX;
                vy = hd.AirVelY;
            }
            else
            {
                vx = hd.GroundVelX;
                vy = hd.GroundVelY;
            }
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
            // 硬直/控制时间：倒地用 down.hittime，否则空中→air.hittime、地面→ground.hittime（char.go:10860）。
            int hitTime = downed ? hd.DownHitTime : (isAir ? hd.AirHitTime : hd.GroundHitTime);
            ghv.HitTime = hitTime < 0 ? 0 : hitTime;
            ghv.CtrlTime = hitTime;
            ghv.GroundType = (int)hd.GroundType;
            ghv.AirType = (int)hd.AirType;
            ghv.AttrType = isAir ? ghv.AirType : ghv.GroundType;
            ghv.GroundAnimType = (int)hd.AnimType;
            ghv.AirAnimType = (int)hd.AirAnimType;
            ghv.FallAnimType = (int)hd.FallAnimType;
            ghv.YAccel = hd.YAccel;
            ghv.FallXVel = hd.FallXVelSet ? hd.FallXVel : vx;
            ghv.FallYVel = hd.FallYVel;
            // 倒地贴地命中且 down.bounce=0 且有 Y 速度 → 落地不反弹（fall.yvel 归零，char.go:10867-10870）。
            if (downedOnGround && !hd.DownBounce && vy != FFloat.Zero)
            {
                ghv.FallYVel = FFloat.Zero;
            }
            ghv.FallRecover = hd.FallRecover;
            ghv.FallRecoverTime = hd.FallRecoverTime;
            ghv.FallDamage = hd.FallDamage;   // 落地伤害（HitFallDamage 控制器读取，char.go:10842）
            ghv.FallKill = hd.FallKill;
            ghv.Fall = hd.Fall || isAir;
            UpdateJuggleAfterHit(attacker, defender, hd, projectile, wasFalling || ghv.Fall);
            ghv.Guarded = false;
            ghv.HitCount++;
            defender.FallTime = 0;
            // 实际受击动画类型（须在 yvel/animtype 之后派生，char.go:10924 gethitAnimtype）
            ghv.AnimType = defender.GetHitAnimType();

            // 守方进入受击状态 + movetype H + 失控
            int getHitState = hitOverride.Active && hitOverride.StateNo >= 0
                ? hitOverride.StateNo
                : ResolveGetHitState(defender, hd, ghv);
            // 自定义状态（投技）：p2stateno 且 p2getp1state → 守方跑攻方状态表（StateOwner=attacker）；否则用自身状态表。
            if (hd.P2StateNo >= 0 && hd.P2GetP1State)
            {
                int ownerPlayerNo = CurrentStateOwnerPlayerNo(attacker);
                defender.StateOwner = ownerPlayerNo == defender.PlayerNo
                    ? null
                    : CurrentStateOwner(attacker, ownerPlayerNo);
                defender.QueueTransition(getHitState, ownerPlayerNo);
            }
            else
            {
                defender.StateOwner = null;
                if (!hitOverride.Active || !hitOverride.KeepState)
                {
                    defender.QueueTransition(getHitState, defender.PlayerNo);
                }
            }
            defender.Ctrl = false;        // MoveType=2(H) 已在伤害计算前设置

            // 连段计数（受击方）：numhits 计入 receivedHits（combocount 源，char.go:11170）
            defender.ReceivedHits += hd.NumHits;

            // 攻方命中统计 + 目标登记 + 可选切状态
            attacker.MoveContact = 1;
            attacker.MoveHit = 1;
            attacker.MoveGuarded = 0;
            attacker.MoveReversed = 0;
            attacker.MoveContactTime = 1;
            attacker.HitCount += hd.NumHits;   // char.go:12189（按 numhits 计，非 +1）
            attacker.UniqHitCount++;
            attacker.AddTarget(defender, hd.Id);
            if (hd.HitOnce > 0)
            {
                hd.Active = false;   // hitonce：只命中一个目标，命中即停用本招（char.go:11283/12172）
            }
            if (hd.P1StateNo >= 0)
            {
                attacker.QueueTransition(hd.P1StateNo, attacker.StatePlayerNo);
            }
        }

        static MHitOverride FindHitOverride(MChar defender, int hitAttr)
        {
            if (defender.HitOverrides == null)
            {
                return default;
            }
            for (int i = 0; i < defender.HitOverrides.Length; i++)
            {
                MHitOverride hitOverride = defender.HitOverrides[i];
                if (hitOverride.Active && (hitOverride.Attr & hitAttr) != 0)
                {
                    return hitOverride;
                }
            }
            return default;
        }

        static void UpdateJuggleAfterHit(MChar attacker, MChar defender, MHitDef hd, bool projectile, bool falling)
        {
            if (!falling)
            {
                return;
            }
            if ((attacker.AssertFlags & (int)MAssertFlag.NoJuggleCheck) == 0)
            {
                int cost = projectile ? hd.AirJuggle : attacker.Juggle;
                int remaining = defender.Ghv.GetJuggle(attacker.Id, AirJuggleMax(attacker));
                defender.Ghv.SetJuggle(attacker.Id, remaining - cost);
            }
            if (!projectile)
            {
                attacker.Juggle = 0;
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

        /// <summary>
        /// 自伤（落地 fall.damage 等）：按受方 finalDefense 缩放（无攻方倍率），kill=false 时保底剩 1 血，
        /// 直接扣到 Life（移植 char.go:9111 lifeAdd(-fall_damage, fall_kill, absolute=false)）。
        /// </summary>
        public static void ApplySelfDamage(MChar character, int rawDamage, bool kill)
        {
            if (rawDamage == 0)
            {
                return;
            }
            int dealt = ComputeDamage(character.Life, rawDamage, kill, FFloat.One, character.ComputeFinalDefense());
            character.Life = ClampLife(character.Life - dealt, character.LifeMax);
        }

        static void QueueLifeDamage(MChar defender, int dealt, bool deferDamage)
        {
            if (deferDamage)
            {
                defender.PendingLifeDamage += dealt;
            }
            else
            {
                defender.Life = ClampLife(defender.Life - dealt, defender.LifeMax);
            }
        }

        public static void ApplyPendingDamage(MChar character)
        {
            if (character.PendingLifeDamage == 0)
            {
                return;
            }
            character.Life = ClampLife(character.Life - character.PendingLifeDamage, character.LifeMax);
            character.PendingLifeDamage = 0;
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
