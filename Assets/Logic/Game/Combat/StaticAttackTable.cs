using Lockstep.Math;

namespace Lockstep.Game.Combat
{
    /// <summary>
    /// v1 招式表：5 招写死在 C# 里，便于早期调参。
    /// v2 换 ScriptableObject 时整文件替换为 SoAttackTable，IAttackTable 调用方零改动。
    ///
    /// 单位约定：
    ///   - 时间：30Hz 帧
    ///   - 长度：1 单位 = 角色身位约半个
    ///   - Knockback.X：以攻方 facing 方向为正，应用时再乘攻方 FacingX
    /// </summary>
    public sealed class StaticAttackTable : IAttackTable
    {
        const int TableSize = 16;   // 预留容量，便于 v2 加新招

        static readonly MoveId[] s_emptyMoves = new MoveId[0];
        static readonly FFloat s_milliFactor = FFloat.FromInt(1000);

        readonly MoveDef[] _defs;

        public StaticAttackTable()
        {
            _defs = new MoveDef[TableSize];
            _defs[(int)MoveId.Jab]      = BuildJab();
            _defs[(int)MoveId.Punch]    = BuildPunch();
            _defs[(int)MoveId.Kick]     = BuildKick();
            _defs[(int)MoveId.JumpKick] = BuildJumpKick();
            _defs[(int)MoveId.DiveKick] = BuildDiveKick();
        }

        public MoveDef Get(MoveId id)
        {
            int index = (int)id;
            if (index < 0 || index >= _defs.Length)
            {
                return null;
            }
            return _defs[index];
        }

        public bool TryGet(MoveId id, out MoveDef def)
        {
            def = Get(id);
            return def != null;
        }

        /// <summary>毫单位转 FFloat：Milli(1500) = 1.5。整数运算确保确定性。</summary>
        static FFloat Milli(int millis)
        {
            return FFloat.FromInt(millis) / s_milliFactor;
        }

        // ─────────────── Jab：快速轻拳，命中后可 cancel 到 Punch / Kick ───────────────
        static MoveDef BuildJab()
        {
            HitboxFrame[] hitboxes = new HitboxFrame[1];
            hitboxes[0] = new HitboxFrame
            {
                StartFrameOffset = 0,
                DurationFrames = 3,
                CenterX = Milli(1800),
                CenterY = FFloat.Zero,
                CenterZ = Milli(1000),
                HalfX = Milli(1000),
                HalfY = Milli(800),
                HalfZ = Milli(1000),
            };
            MoveId[] cancelOnHit = new MoveId[2];
            cancelOnHit[0] = MoveId.Punch;
            cancelOnHit[1] = MoveId.Kick;
            return new MoveDef(
                id: MoveId.Jab,
                category: MoveCategory.Ground,
                startupFrames: 2,
                activeFrames: 3,
                recoveryFrames: 6,
                invincibilityFrames: 0,
                damage: 5,
                knockback: new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero),
                hitboxes: hitboxes,
                hitstopFrames: 4,
                cancelIntoOnHit: cancelOnHit,
                cancelIntoOnWhiff: s_emptyMoves,
                cancelIntoOnStartup: s_emptyMoves);
        }

        // ─────────────── Punch：重拳，慢但伤害高，命中后可 cancel 到 Kick ───────────────
        static MoveDef BuildPunch()
        {
            HitboxFrame[] hitboxes = new HitboxFrame[1];
            hitboxes[0] = new HitboxFrame
            {
                StartFrameOffset = 0,
                DurationFrames = 4,
                CenterX = Milli(2300),
                CenterY = FFloat.Zero,
                CenterZ = Milli(1000),
                HalfX = Milli(1200),
                HalfY = Milli(800),
                HalfZ = Milli(1000),
            };
            MoveId[] cancelOnHit = new MoveId[1];
            cancelOnHit[0] = MoveId.Kick;
            return new MoveDef(
                id: MoveId.Punch,
                category: MoveCategory.Ground,
                startupFrames: 5,
                activeFrames: 4,
                recoveryFrames: 12,
                invincibilityFrames: 0,
                damage: 12,
                knockback: new FVector3(Milli(800), FFloat.Zero, FFloat.Zero),
                hitboxes: hitboxes,
                hitstopFrames: 5,
                cancelIntoOnHit: cancelOnHit,
                cancelIntoOnWhiff: s_emptyMoves,
                cancelIntoOnStartup: s_emptyMoves);
        }

        // ─────────────── Kick：长距离重腿，无 cancel ───────────────
        static MoveDef BuildKick()
        {
            HitboxFrame[] hitboxes = new HitboxFrame[1];
            hitboxes[0] = new HitboxFrame
            {
                StartFrameOffset = 0,
                DurationFrames = 4,
                CenterX = Milli(2800),
                CenterY = FFloat.Zero,
                CenterZ = Milli(1000),
                HalfX = Milli(1300),
                HalfY = Milli(800),
                HalfZ = Milli(1000),
            };
            return new MoveDef(
                id: MoveId.Kick,
                category: MoveCategory.Ground,
                startupFrames: 7,
                activeFrames: 4,
                recoveryFrames: 15,
                invincibilityFrames: 0,
                damage: 15,
                knockback: new FVector3(Milli(1500), FFloat.Zero, FFloat.Zero),
                hitboxes: hitboxes,
                hitstopFrames: 6,
                cancelIntoOnHit: s_emptyMoves,
                cancelIntoOnWhiff: s_emptyMoves,
                cancelIntoOnStartup: s_emptyMoves);
        }

        // ─────────────── JumpKick：空中按 K，命中盒持续到落地 ───────────────
        static MoveDef BuildJumpKick()
        {
            HitboxFrame[] hitboxes = new HitboxFrame[1];
            hitboxes[0] = new HitboxFrame
            {
                StartFrameOffset = 0,
                DurationFrames = 30,                // 兜底大值，State 内根据 Z<=0 提前结束
                CenterX = Milli(2300),
                CenterY = FFloat.Zero,
                CenterZ = -Milli(500),               // 偏下，对地面目标更友好
                HalfX = Milli(1200),
                HalfY = Milli(800),
                HalfZ = Milli(1500),
            };
            return new MoveDef(
                id: MoveId.JumpKick,
                category: MoveCategory.Air,
                startupFrames: 2,
                activeFrames: 30,
                recoveryFrames: 0,
                invincibilityFrames: 0,
                damage: 14,
                knockback: new FVector3(Milli(1000), FFloat.Zero, FFloat.Zero),
                hitboxes: hitboxes,
                hitstopFrames: 5,
                cancelIntoOnHit: s_emptyMoves,
                cancelIntoOnWhiff: s_emptyMoves,
                cancelIntoOnStartup: s_emptyMoves);
        }

        // ─────────────── DiveKick：空中按 ↓+K，急速下坠 ───────────────
        static MoveDef BuildDiveKick()
        {
            HitboxFrame[] hitboxes = new HitboxFrame[1];
            hitboxes[0] = new HitboxFrame
            {
                StartFrameOffset = 0,
                DurationFrames = 30,
                CenterX = Milli(2000),
                CenterY = FFloat.Zero,
                CenterZ = -Milli(1000),              // 更偏下
                HalfX = Milli(1200),
                HalfY = Milli(800),
                HalfZ = Milli(1500),
            };
            return new MoveDef(
                id: MoveId.DiveKick,
                category: MoveCategory.Air,
                startupFrames: 3,
                activeFrames: 30,
                recoveryFrames: 10,
                invincibilityFrames: 0,
                damage: 18,
                knockback: new FVector3(Milli(500), FFloat.Zero, FFloat.Zero),
                hitboxes: hitboxes,
                hitstopFrames: 6,
                cancelIntoOnHit: s_emptyMoves,
                cancelIntoOnWhiff: s_emptyMoves,
                cancelIntoOnStartup: s_emptyMoves);
        }
    }
}
