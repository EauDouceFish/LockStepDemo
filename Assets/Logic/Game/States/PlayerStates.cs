using Lockstep.Core;
using Lockstep.Game.Combat;
using Lockstep.Game.Components;
using Lockstep.Input;
using Lockstep.Logging;
using Lockstep.Math;

namespace Lockstep.Game.States
{
    /// <summary>
    /// 元状态枚举。v1 = 6 个，足够支撑基础格斗。
    /// 招式不归元状态管 —— 五招（Jab/Punch/Kick/JumpKick/DiveKick）都进 Attack，
    /// 靠 ActiveMoveC.Id 区分。v2 扩元状态时往下加（Block/Throwing/Thrown/...）。
    /// </summary>
    public enum PlayerStateId : byte
    {
        Idle   = 0,
        Walk   = 1,
        Jump   = 2,
        Attack = 3,
        Hurt   = 4,
        KO     = 5,
        // v2 预留占位（实现时取消 v2Reserved_ 前缀重命名，避免下游 switch 漏 case）
        V2Reserved_Block       = 6,
        V2Reserved_Throwing    = 7,
        V2Reserved_Thrown      = 8,
        V2Reserved_Skill       = 9,
        V2Reserved_KnockedDown = 10,
        V2Reserved_Wakeup      = 11,
    }

    /// <summary>
    /// 状态行为接口。实现类必须是<b>无状态单例</b>：所有运行时态写回 Entity 组件，
    /// 否则 rollback 后状态串扰。
    /// </summary>
    public interface IPlayerState
    {
        PlayerStateId Id { get; }
        void OnEnter(World world, Entity entity);
        void OnTick(World world, Entity entity);
        PlayerStateId? CheckTransition(World world, Entity entity);
    }

    // ═════════════════════ 共用辅助 ═════════════════════

    internal static class CombatHelpers
    {
        public static FrameInput GetInput(World world, Entity entity)
        {
            PlayerTagC tag = entity.Get<PlayerTagC>();
            if (tag == null || world.CurrentInputs == null || tag.PlayerIndex >= world.CurrentInputs.Length)
            {
                return FrameInput.Empty;
            }
            return world.CurrentInputs[tag.PlayerIndex];
        }

        public static IAttackTable GetAttackTable(World world)
        {
            BattleGameData data = world.GameData as BattleGameData;
            if (data == null)
            {
                return null;
            }
            return data.AttackTable;
        }

        public static FFloat Abs(FFloat value)
        {
            if (value < FFloat.Zero)
            {
                return -value;
            }
            return value;
        }

        public static FFloat Clamp(FFloat value, FFloat min, FFloat max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        /// <summary>把按键意图翻译成"想出哪招"。地面 vs 空中分支。</summary>
        public static MoveId ResolveDesiredMove(FrameInput input, bool isAirborne)
        {
            if (isAirborne)
            {
                if (input.IsDown(InputButton.Kick))
                {
                    if (input.MoveY < 0)
                    {
                        return MoveId.DiveKick;
                    }
                    return MoveId.JumpKick;
                }
                return MoveId.None;
            }
            if (input.IsDown(InputButton.LightPunch))
            {
                return MoveId.Jab;
            }
            if (input.IsDown(InputButton.HeavyPunch))
            {
                return MoveId.Punch;
            }
            if (input.IsDown(InputButton.Kick))
            {
                return MoveId.Kick;
            }
            return MoveId.None;
        }
    }

    // ═════════════════════ Idle ═════════════════════

    public sealed class IdleState : IPlayerState
    {
        public PlayerStateId Id
        {
            get { return PlayerStateId.Idle; }
        }

        public void OnEnter(World world, Entity entity)
        {
            VelocityC velocity = entity.Get<VelocityC>();
            if (velocity != null)
            {
                velocity.Vel = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            }
        }

        public void OnTick(World world, Entity entity)
        {
        }

        public PlayerStateId? CheckTransition(World world, Entity entity)
        {
            HealthC health = entity.Get<HealthC>();
            if (health != null && health.HP <= 0)
            {
                return PlayerStateId.KO;
            }
            FrameInput input = CombatHelpers.GetInput(world, entity);
            if (input.IsDown(InputButton.Jump))
            {
                return PlayerStateId.Jump;
            }
            MoveId desired = CombatHelpers.ResolveDesiredMove(input, isAirborne: false);
            if (desired != MoveId.None)
            {
                ActiveMoveC active = entity.Get<ActiveMoveC>();
                active.Id = desired;
                active.HitTargetsBits = 0;
                active.CancelArmed = false;
                return PlayerStateId.Attack;
            }
            if (input.MoveX != 0 || input.MoveY != 0)
            {
                return PlayerStateId.Walk;
            }
            return null;
        }
    }

    // ═════════════════════ Walk ═════════════════════

    public sealed class WalkState : IPlayerState
    {
        public PlayerStateId Id
        {
            get { return PlayerStateId.Walk; }
        }

        public void OnEnter(World world, Entity entity)
        {
        }

        public void OnTick(World world, Entity entity)
        {
            FrameInput input = CombatHelpers.GetInput(world, entity);
            TransformC transform = entity.Get<TransformC>();
            if (transform == null)
            {
                return;
            }

            FFloat step = (world.Config != null) ? world.Config.MoveStepPerFrame : FFloat.One / FFloat.FromInt(5);
            FFloat halfX = (world.Config != null) ? world.Config.MapHalfWidth : FFloat.FromInt(13);
            FFloat halfY = (world.Config != null) ? world.Config.MapHalfHeight : FFloat.FromInt(3);

            FFloat deltaX = FFloat.FromInt(input.MoveX) * step;
            FFloat deltaY = FFloat.FromInt(input.MoveY) * step;
            FFloat nextX = CombatHelpers.Clamp(transform.Pos.X + deltaX, -halfX, halfX);
            FFloat nextY = CombatHelpers.Clamp(transform.Pos.Y + deltaY, -halfY, halfY);
            transform.Pos = new FVector3(nextX, nextY, transform.Pos.Z);

            if (input.MoveX > 0)
            {
                transform.FacingX = FFloat.One;
            }
            else if (input.MoveX < 0)
            {
                transform.FacingX = FFloat.MinusOne;
            }
        }

        public PlayerStateId? CheckTransition(World world, Entity entity)
        {
            HealthC health = entity.Get<HealthC>();
            if (health != null && health.HP <= 0)
            {
                return PlayerStateId.KO;
            }
            FrameInput input = CombatHelpers.GetInput(world, entity);
            if (input.IsDown(InputButton.Jump))
            {
                return PlayerStateId.Jump;
            }
            MoveId desired = CombatHelpers.ResolveDesiredMove(input, isAirborne: false);
            if (desired != MoveId.None)
            {
                ActiveMoveC active = entity.Get<ActiveMoveC>();
                active.Id = desired;
                active.HitTargetsBits = 0;
                active.CancelArmed = false;
                return PlayerStateId.Attack;
            }
            if (input.MoveX == 0 && input.MoveY == 0)
            {
                return PlayerStateId.Idle;
            }
            return null;
        }
    }

    // ═════════════════════ Jump（合并三阶段：startup / air / land） ═════════════════════

    public sealed class JumpState : IPlayerState
    {
        public const int StartupFrames = 3;
        public const int LandFrames = 4;

        // 跳跃物理（毫单位）。后续应迁入 LogicConfigAsset。
        static readonly FFloat s_jumpVz0 = FFloat.FromInt(450) / FFloat.FromInt(1000);    // 0.45/帧
        static readonly FFloat s_jumpVxy = FFloat.FromInt(150) / FFloat.FromInt(1000);    // 0.15/帧
        static readonly FFloat s_gravity = FFloat.FromInt(35) / FFloat.FromInt(1000);     // 0.035/帧²

        public PlayerStateId Id
        {
            get { return PlayerStateId.Jump; }
        }

        public void OnEnter(World world, Entity entity)
        {
            VelocityC velocity = entity.Get<VelocityC>();
            if (velocity != null)
            {
                velocity.Vel = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            }
        }

        public void OnTick(World world, Entity entity)
        {
            StateMachineC sm = entity.Get<StateMachineC>();
            TransformC transform = entity.Get<TransformC>();
            VelocityC velocity = entity.Get<VelocityC>();
            if (sm == null || transform == null || velocity == null)
            {
                return;
            }

            // startup 末帧 = 真正起跳：根据当前 MoveX/Y 给水平速度 + 给 Vz0
            if (sm.FrameInState == StartupFrames - 1)
            {
                FrameInput input = CombatHelpers.GetInput(world, entity);
                FFloat vx = FFloat.FromInt(input.MoveX) * s_jumpVxy;
                FFloat vy = FFloat.FromInt(input.MoveY) * s_jumpVxy;
                velocity.Vel = new FVector3(vx, vy, s_jumpVz0);
                if (input.MoveX > 0)
                {
                    transform.FacingX = FFloat.One;
                }
                else if (input.MoveX < 0)
                {
                    transform.FacingX = FFloat.MinusOne;
                }
                return;
            }

            // 空中阶段（startup 之后）：重力 + 位置积分
            if (sm.FrameInState >= StartupFrames)
            {
                velocity.Vel = new FVector3(velocity.Vel.X, velocity.Vel.Y, velocity.Vel.Z - s_gravity);
                FFloat nextZ = transform.Pos.Z + velocity.Vel.Z;
                if (nextZ < FFloat.Zero)
                {
                    nextZ = FFloat.Zero;
                }
                transform.Pos = new FVector3(
                    transform.Pos.X + velocity.Vel.X,
                    transform.Pos.Y + velocity.Vel.Y,
                    nextZ);
            }
        }

        public PlayerStateId? CheckTransition(World world, Entity entity)
        {
            HealthC health = entity.Get<HealthC>();
            if (health != null && health.HP <= 0)
            {
                return PlayerStateId.KO;
            }
            StateMachineC sm = entity.Get<StateMachineC>();
            TransformC transform = entity.Get<TransformC>();
            if (sm == null || transform == null)
            {
                return null;
            }

            // 空中阶段允许触发空中招（JumpKick / DiveKick）
            if (sm.FrameInState >= StartupFrames)
            {
                FrameInput input = CombatHelpers.GetInput(world, entity);
                MoveId desired = CombatHelpers.ResolveDesiredMove(input, isAirborne: true);
                if (desired != MoveId.None)
                {
                    ActiveMoveC active = entity.Get<ActiveMoveC>();
                    active.Id = desired;
                    active.HitTargetsBits = 0;
                    active.CancelArmed = false;
                    return PlayerStateId.Attack;
                }
                // 落地：Z <= 0 且 startup 已完成
                if (transform.Pos.Z <= FFloat.Zero && sm.FrameInState >= StartupFrames + LandFrames)
                {
                    return PlayerStateId.Idle;
                }
            }
            return null;
        }
    }

    // ═════════════════════ Attack（5 招的通用壳） ═════════════════════

    public sealed class AttackState : IPlayerState
    {
        public PlayerStateId Id
        {
            get { return PlayerStateId.Attack; }
        }

        public void OnEnter(World world, Entity entity)
        {
            ActiveMoveC active = entity.Get<ActiveMoveC>();
            LLog.Log(string.Format("[Frame {0}] Entity {1} ATTACK {2} begin",
                world.Frame, entity.Id, active != null ? active.Id.ToString() : "?"));
        }

        public void OnTick(World world, Entity entity)
        {
            ActiveMoveC active = entity.Get<ActiveMoveC>();
            StateMachineC sm = entity.Get<StateMachineC>();
            IAttackTable table = CombatHelpers.GetAttackTable(world);
            if (active == null || sm == null || table == null)
            {
                return;
            }
            MoveDef move = table.Get(active.Id);
            if (move == null)
            {
                return;
            }

            // 空中招在落地时锁住 Z（防止穿地）
            if (move.Category == MoveCategory.Air)
            {
                ApplyAirPhysics(entity, active, move);
            }

            // active 期间扫击中
            if (sm.FrameInState >= move.StartupFrames && sm.FrameInState < move.StartupFrames + move.ActiveFrames)
            {
                DetectHit(world, entity, active, sm, move);
            }
        }

        public PlayerStateId? CheckTransition(World world, Entity entity)
        {
            HealthC health = entity.Get<HealthC>();
            if (health != null && health.HP <= 0)
            {
                return PlayerStateId.KO;
            }
            StateMachineC sm = entity.Get<StateMachineC>();
            ActiveMoveC active = entity.Get<ActiveMoveC>();
            IAttackTable table = CombatHelpers.GetAttackTable(world);
            if (sm == null || active == null || table == null)
            {
                return null;
            }
            MoveDef move = table.Get(active.Id);
            if (move == null)
            {
                return PlayerStateId.Idle;
            }

            // 命中确认 cancel：active 起开始可 cancel
            if (active.CancelArmed && sm.FrameInState >= move.StartupFrames && move.CancelIntoOnHit.Length > 0)
            {
                MoveId? nextMove = TryReadCancel(entity, move.CancelIntoOnHit);
                if (nextMove.HasValue)
                {
                    // 切换招式上下文，返回 Attack 触发 System 的"自重入"：重跑 OnEnter + FrameInState 归 0
                    active.Id = nextMove.Value;
                    active.HitTargetsBits = 0;
                    active.CancelArmed = false;
                    return PlayerStateId.Attack;
                }
            }

            // 空中招在落地后强制结束
            TransformC transform = entity.Get<TransformC>();
            if (move.Category == MoveCategory.Air && transform != null
                && transform.Pos.Z <= FFloat.Zero && sm.FrameInState >= move.StartupFrames)
            {
                return PlayerStateId.Idle;
            }

            if (sm.FrameInState >= move.TotalFrames)
            {
                return PlayerStateId.Idle;
            }
            return null;
        }

        // ─────── 命中判定 ───────

        static void DetectHit(World world, Entity attacker, ActiveMoveC active, StateMachineC attackerSm, MoveDef move)
        {
            TransformC attackerTransform = attacker.Get<TransformC>();
            if (attackerTransform == null)
            {
                return;
            }

            int activeOffset = attackerSm.FrameInState - move.StartupFrames;
            HitboxFrame box = default;
            bool boxFound = false;
            for (int i = 0; i < move.Hitboxes.Length; i++)
            {
                HitboxFrame candidate = move.Hitboxes[i];
                if (activeOffset >= candidate.StartFrameOffset
                    && activeOffset < candidate.StartFrameOffset + candidate.DurationFrames)
                {
                    box = candidate;
                    boxFound = true;
                    break;
                }
            }
            if (!boxFound)
            {
                return;
            }

            FFloat boxCenterX = attackerTransform.Pos.X + attackerTransform.FacingX * box.CenterX;
            FFloat boxCenterY = attackerTransform.Pos.Y + box.CenterY;
            FFloat boxCenterZ = attackerTransform.Pos.Z + box.CenterZ;

            for (int i = 0; i < world.Entities.Count; i++)
            {
                Entity target = world.Entities[i];
                if (target.Id == attacker.Id)
                {
                    continue;
                }
                ulong mask = 1UL << (target.Id & 63);
                if ((active.HitTargetsBits & mask) != 0)
                {
                    continue;
                }
                HealthC targetHealth = target.Get<HealthC>();
                StateMachineC targetSm = target.Get<StateMachineC>();
                TransformC targetTransform = target.Get<TransformC>();
                IncomingHitC incoming = target.Get<IncomingHitC>();
                if (targetHealth == null || targetSm == null || targetTransform == null || incoming == null)
                {
                    continue;
                }
                if (targetHealth.HP <= 0)
                {
                    continue;
                }
                if (targetSm.Current == PlayerStateId.Hurt || targetSm.Current == PlayerStateId.KO)
                {
                    continue;
                }

                FFloat deltaX = targetTransform.Pos.X - boxCenterX;
                FFloat deltaY = targetTransform.Pos.Y - boxCenterY;
                FFloat deltaZ = targetTransform.Pos.Z - boxCenterZ;
                if (CombatHelpers.Abs(deltaX) > box.HalfX)
                {
                    continue;
                }
                if (CombatHelpers.Abs(deltaY) > box.HalfY)
                {
                    continue;
                }
                if (CombatHelpers.Abs(deltaZ) > box.HalfZ)
                {
                    continue;
                }

                // 命中确认 —— 写受击信号 + 双方 hitstop + 攻方 CancelArmed
                targetHealth.HP -= move.Damage;
                incoming.AttackerEntityId = attacker.Id;
                incoming.Damage = move.Damage;
                incoming.Knockback = new FVector3(
                    attackerTransform.FacingX * move.Knockback.X,
                    move.Knockback.Y,
                    move.Knockback.Z);
                incoming.LaunchAir = move.Knockback.Z > FFloat.Zero;
                targetSm.PendingTransition = (targetHealth.HP <= 0) ? PlayerStateId.KO : PlayerStateId.Hurt;
                attackerSm.HitstopFrames = move.HitstopFrames;
                targetSm.HitstopFrames = move.HitstopFrames;
                active.HitTargetsBits |= mask;
                active.CancelArmed = true;

                LLog.Log(string.Format("[Frame {0}] Entity {1} HIT Entity {2} | move={3} dmg={4} hp={5}/{6}",
                    world.Frame, attacker.Id, target.Id, move.Id, move.Damage, targetHealth.HP, targetHealth.MaxHP));
                if (targetHealth.HP <= 0)
                {
                    LLog.Log(string.Format("[Frame {0}] Entity {1} K.O.", world.Frame, target.Id));
                }
            }
        }

        // ─────── 空中招物理 ───────

        static readonly FFloat s_gravity = FFloat.FromInt(35) / FFloat.FromInt(1000);
        static readonly FFloat s_diveVz = -(FFloat.FromInt(700) / FFloat.FromInt(1000));

        static void ApplyAirPhysics(Entity entity, ActiveMoveC active, MoveDef move)
        {
            TransformC transform = entity.Get<TransformC>();
            VelocityC velocity = entity.Get<VelocityC>();
            StateMachineC sm = entity.Get<StateMachineC>();
            if (transform == null || velocity == null || sm == null)
            {
                return;
            }
            // DiveKick 进入第一帧给一个强负 Vz
            if (active.Id == MoveId.DiveKick && sm.FrameInState == 0)
            {
                velocity.Vel = new FVector3(velocity.Vel.X, velocity.Vel.Y, s_diveVz);
            }
            velocity.Vel = new FVector3(velocity.Vel.X, velocity.Vel.Y, velocity.Vel.Z - s_gravity);
            FFloat nextZ = transform.Pos.Z + velocity.Vel.Z;
            if (nextZ < FFloat.Zero)
            {
                nextZ = FFloat.Zero;
            }
            transform.Pos = new FVector3(
                transform.Pos.X + velocity.Vel.X,
                transform.Pos.Y + velocity.Vel.Y,
                nextZ);
        }

        // ─────── 取消窗口读 buffer ───────

        static MoveId? TryReadCancel(Entity entity, MoveId[] allowed)
        {
            InputBufferC buffer = entity.Get<InputBufferC>();
            if (buffer == null)
            {
                return null;
            }
            for (int i = 0; i < allowed.Length; i++)
            {
                MoveId candidate = allowed[i];
                byte mask = MoveIdToButtonMask(candidate);
                if (mask == 0)
                {
                    continue;
                }
                if (buffer.PressedWithin(mask, frames: 3))
                {
                    return candidate;
                }
            }
            return null;
        }

        static byte MoveIdToButtonMask(MoveId id)
        {
            switch (id)
            {
                case MoveId.Jab:      return (byte)InputButton.LightPunch;
                case MoveId.Punch:    return (byte)InputButton.HeavyPunch;
                case MoveId.Kick:     return (byte)InputButton.Kick;
                case MoveId.JumpKick: return (byte)InputButton.Kick;
                case MoveId.DiveKick: return (byte)InputButton.Kick;
                default:              return 0;
            }
        }
    }

    // ═════════════════════ Hurt ═════════════════════

    public sealed class HurtState : IPlayerState
    {
        public const int GroundHurtFrames = 15;
        public const int AirExtraFrames = 5;       // 空中受击落地后再僵直 5 帧
        static readonly FFloat s_gravity = FFloat.FromInt(35) / FFloat.FromInt(1000);
        static readonly FFloat s_friction = FFloat.FromInt(800) / FFloat.FromInt(1000);  // 每帧 *= 0.8 衰减

        public PlayerStateId Id
        {
            get { return PlayerStateId.Hurt; }
        }

        public void OnEnter(World world, Entity entity)
        {
            IncomingHitC incoming = entity.Get<IncomingHitC>();
            VelocityC velocity = entity.Get<VelocityC>();
            if (incoming != null && velocity != null)
            {
                velocity.Vel = incoming.Knockback;
            }
            LLog.Log(string.Format("[Frame {0}] Entity {1} HURT", world.Frame, entity.Id));
        }

        public void OnTick(World world, Entity entity)
        {
            TransformC transform = entity.Get<TransformC>();
            VelocityC velocity = entity.Get<VelocityC>();
            if (transform == null || velocity == null)
            {
                return;
            }
            // 重力 + 摩擦
            FFloat nextVx = velocity.Vel.X * s_friction;
            FFloat nextVy = velocity.Vel.Y * s_friction;
            FFloat nextVz = velocity.Vel.Z - s_gravity;
            FFloat nextZ = transform.Pos.Z + nextVz;
            if (nextZ < FFloat.Zero)
            {
                nextZ = FFloat.Zero;
                nextVz = FFloat.Zero;
            }
            velocity.Vel = new FVector3(nextVx, nextVy, nextVz);
            transform.Pos = new FVector3(
                transform.Pos.X + nextVx,
                transform.Pos.Y + nextVy,
                nextZ);
        }

        public PlayerStateId? CheckTransition(World world, Entity entity)
        {
            HealthC health = entity.Get<HealthC>();
            if (health != null && health.HP <= 0)
            {
                return PlayerStateId.KO;
            }
            StateMachineC sm = entity.Get<StateMachineC>();
            TransformC transform = entity.Get<TransformC>();
            IncomingHitC incoming = entity.Get<IncomingHitC>();
            if (sm == null || transform == null)
            {
                return null;
            }
            int requiredFrames = GroundHurtFrames;
            if (incoming != null && incoming.LaunchAir)
            {
                requiredFrames += AirExtraFrames;
            }
            bool grounded = transform.Pos.Z <= FFloat.Zero;
            if (sm.FrameInState >= requiredFrames && grounded)
            {
                return PlayerStateId.Idle;
            }
            return null;
        }
    }

    // ═════════════════════ KO ═════════════════════

    public sealed class KoState : IPlayerState
    {
        public PlayerStateId Id
        {
            get { return PlayerStateId.KO; }
        }

        public void OnEnter(World world, Entity entity)
        {
            VelocityC velocity = entity.Get<VelocityC>();
            if (velocity != null)
            {
                velocity.Vel = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            }
        }

        public void OnTick(World world, Entity entity)
        {
        }

        public PlayerStateId? CheckTransition(World world, Entity entity)
        {
            return null;
        }
    }
}
