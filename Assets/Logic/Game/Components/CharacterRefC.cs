using Lockstep.Core;

namespace Lockstep.Game.Components
{
    /// <summary>该实体使用哪个角色定义（索引到 World.GameData 的角色表）。</summary>
    public sealed class CharacterRefC : IComponent
    {
        public int CharacterId;

        public IComponent Clone()
        {
            return new CharacterRefC { CharacterId = CharacterId };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(CharacterId);
        }
    }
}
