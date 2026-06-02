using Lockstep.Core;
using Lockstep.Game.Data;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 本帧待结算的命中（受击方持有）。CollisionSystem 检出 Clsn1×Clsn2 重叠后写入，
    /// HitSystem(T3.4) 读取并结算伤害/硬直/击退后清掉。Hit 指向攻击方的静态 HitDef（不入哈希身份，
    /// 与 HitDefStateC.Current 同理）；运行态(HasHit/攻击方下标)入哈希。
    /// </summary>
    public sealed class PendingHitC : IComponent
    {
        public bool HasHit;
        public int AttackerEntityIndex;
        public HitDef Hit;

        public IComponent Clone()
        {
            return new PendingHitC
            {
                HasHit = HasHit,
                AttackerEntityIndex = AttackerEntityIndex,
                Hit = Hit,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddBool(HasHit);
            hash.AddInt32(AttackerEntityIndex);
        }
    }
}
