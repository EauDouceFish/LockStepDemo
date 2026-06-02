using Lockstep.Math;

namespace Lockstep.Game.Combat
{
    /// <summary>
    /// 招式 ID。配表索引。
    /// 编号区间约定：1-99 体术，100-199 忍术（v2），200-299 奥义（v2），300+ 投技（v2）。
    /// </summary>
    public enum MoveId : byte
    {
        None     = 0,
        Jab      = 1,
        Punch    = 2,
        Kick     = 3,
        JumpKick = 4,
        DiveKick = 5,
    }

    /// <summary>
    /// 招式大类。决定起手位置（地面 / 空中）与命中允许范围。
    /// Throw / Skill 为 v2 预留，v1 不会出现。
    /// </summary>
    public enum MoveCategory : byte
    {
        Ground = 0,
        Air    = 1,
        Throw  = 2,
        Skill  = 3,
    }

    /// <summary>
    /// 攻击盒的一帧。一招的 active 期间可由多个 HitboxFrame 组合，实现多段命中盒切换。
    /// Center* 相对于攻击者 facing 镜像：CenterX > 0 表示位于攻击者前方。
    /// Half* 是 AABB 半长，配 XY 平面 + Z 高度三轴。
    /// </summary>
    public struct HitboxFrame
    {
        public int StartFrameOffset;   // 距 active 起始的帧数
        public int DurationFrames;
        public FFloat CenterX;
        public FFloat CenterY;
        public FFloat CenterZ;
        public FFloat HalfX;
        public FFloat HalfY;
        public FFloat HalfZ;
    }

    /// <summary>
    /// 一招的完整定义。字段全 readonly，运行时不可变 —— 由 IAttackTable 持有静态数据。
    /// State 类只读，所有运行时态（已命中谁、是否 CancelArmed）放在 ActiveMoveC 组件。
    /// </summary>
    public sealed class MoveDef
    {
        public readonly MoveId Id;
        public readonly MoveCategory Category;

        // ─────── 帧数据 ───────
        public readonly int StartupFrames;
        public readonly int ActiveFrames;
        public readonly int RecoveryFrames;
        public readonly int InvincibilityFrames;   // 起手无敌帧数（v1 = 0，留口）

        // ─────── 命中 ───────
        public readonly int Damage;                 // v1 用 int；后续若做百分比伤害再改 FFloat
        public readonly FVector3 Knockback;         // X 朝攻方 facing 推开；Z > 0 表示浮空
        public readonly HitboxFrame[] Hitboxes;
        public readonly int HitstopFrames;          // 命中冻结帧数（打击感来源）

        // ─────── 取消 ───────
        public readonly MoveId[] CancelIntoOnHit;       // 命中确认后允许 cancel 的招
        public readonly MoveId[] CancelIntoOnWhiff;     // 空挥也可 cancel（v1 通常空）
        public readonly MoveId[] CancelIntoOnStartup;   // Kara cancel 预留口子（v1 不开）

        public int TotalFrames
        {
            get { return StartupFrames + ActiveFrames + RecoveryFrames; }
        }

        public MoveDef(
            MoveId id,
            MoveCategory category,
            int startupFrames,
            int activeFrames,
            int recoveryFrames,
            int invincibilityFrames,
            int damage,
            FVector3 knockback,
            HitboxFrame[] hitboxes,
            int hitstopFrames,
            MoveId[] cancelIntoOnHit,
            MoveId[] cancelIntoOnWhiff,
            MoveId[] cancelIntoOnStartup)
        {
            Id = id;
            Category = category;
            StartupFrames = startupFrames;
            ActiveFrames = activeFrames;
            RecoveryFrames = recoveryFrames;
            InvincibilityFrames = invincibilityFrames;
            Damage = damage;
            Knockback = knockback;
            Hitboxes = hitboxes;
            HitstopFrames = hitstopFrames;
            CancelIntoOnHit = cancelIntoOnHit;
            CancelIntoOnWhiff = cancelIntoOnWhiff;
            CancelIntoOnStartup = cancelIntoOnStartup;
        }
    }
}
