using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 跨实体受击信号：攻击者命中时填写到受击者身上，受击者在 HurtState.OnEnter 时消费。
    ///
    /// Why：StateMachineC.PendingTransition 只能携带一个枚举，没法附带伤害值/击退方向，
    /// 而 HurtState 进入时必须知道这些 —— 所以拆个专用 Component。
    /// </summary>
    public sealed class IncomingHitC : IComponent
    {
        public int AttackerEntityId;
        public int Damage;
        public FVector3 Knockback;
        public bool LaunchAir;        // 是否打飞到空中（一般 Knockback.Z > 0 时为 true）

        public IComponent Clone()
        {
            return new IncomingHitC
            {
                AttackerEntityId = AttackerEntityId,
                Damage = Damage,
                Knockback = Knockback,
                LaunchAir = LaunchAir,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(AttackerEntityId);
            hash.AddInt32(Damage);
            hash.AddFixed(Knockback);
            hash.AddBool(LaunchAir);
        }

        public override string ToString()
        {
            return string.Format("InHit[from={0},dmg={1},kb={2}]", AttackerEntityId, Damage, Knockback);
        }
    }
}
