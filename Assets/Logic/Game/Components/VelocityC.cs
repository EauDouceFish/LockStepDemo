using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Game.Components
{
    public class VelocityC : IComponent
    {
        public FVector3 Vel;

        public IComponent Clone() => new VelocityC { Vel = Vel };

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddFixed(Vel);
        }

        public override string ToString()
            => $"V[{Vel.X.Raw},{Vel.Y.Raw},{Vel.Z.Raw}]";
    }
}
