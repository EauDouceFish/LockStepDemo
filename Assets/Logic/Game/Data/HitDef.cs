using Lockstep.Math;

namespace Lockstep.Game.Data
{
    /// <summary>
    /// 命中定义（MUGEN HitDef controller 的强类型代表 —— 命中是热点，单独建类）。
    /// 数据形状保持完整（避免"形状捷径"烂尾）；运行时处理可简化（见架构设计 §12）。
    /// 表现字段（sparkno/sound 等）归表现层，不入逻辑哈希。
    /// </summary>
    public sealed class HitDef
    {
        // ── attr（MUGEN attr）──
        public StateType AttrStateType;   // 命中对哪种姿势起手生效（S/C/A）
        public AttackClass AttrClass;     // N/S/H
        public AttackKind AttrKind;       // A/T/P

        // ── 伤害 ──
        public int Damage;
        public int GuardDamage;

        // ── 打击感（hitstop / pausetime）──
        public int PauseTimeAttacker;
        public int PauseTimeDefender;

        // ── 击退（定点）──
        public FVector2 GroundVelocity;
        public FVector2 AirVelocity;

        // ── 硬直 ──
        public int GroundHitTime;
        public int GuardHitTime;
    }
}
