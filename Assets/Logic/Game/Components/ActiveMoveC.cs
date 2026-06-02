using Lockstep.Core;
using Lockstep.Game.Combat;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 当前正在执行的招式上下文。仅 Attack 状态期间有意义。
    ///
    /// 字段全部值类型（含 ulong bitmask 代替 List）—— IComponent.Clone() 浅拷即可，rollback 零成本。
    /// </summary>
    public sealed class ActiveMoveC : IComponent
    {
        public MoveId Id;
        public ulong HitTargetsBits;   // 已命中过的 entityId bitmask（仅支持 entityId 0-63；v1 双人足够）
        public bool CancelArmed;        // 命中确认后才置 true，未命中不允许 cancel

        public IComponent Clone()
        {
            return new ActiveMoveC
            {
                Id = Id,
                HitTargetsBits = HitTargetsBits,
                CancelArmed = CancelArmed,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32((int)Id);
            hash.AddUInt64(HitTargetsBits);
            hash.AddBool(CancelArmed);
        }

        public override string ToString()
        {
            return string.Format("Mv[{0},h{1:X},c{2}]", Id, HitTargetsBits, CancelArmed ? 1 : 0);
        }
    }
}
