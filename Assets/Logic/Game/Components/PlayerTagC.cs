using Lockstep.Core;

namespace Lockstep.Game.Components
{
    public class PlayerTagC : IComponent
    {
        public int PlayerIndex;

        public IComponent Clone() => new PlayerTagC { PlayerIndex = PlayerIndex };

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(PlayerIndex);
        }

        public override string ToString() => $"Tag[{PlayerIndex}]";
    }
}
