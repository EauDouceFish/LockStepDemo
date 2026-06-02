using Lockstep.Core;

namespace Lockstep.Game.Components
{
    public class HealthC : IComponent
    {
        public int HP = 100;
        public int MaxHP = 100;

        public IComponent Clone() => new HealthC { HP = HP, MaxHP = MaxHP };

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(HP);
            hash.AddInt32(MaxHP);
        }

        public override string ToString() => $"HP[{HP}/{MaxHP}]";
    }
}
