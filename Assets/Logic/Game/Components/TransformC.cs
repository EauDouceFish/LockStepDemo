using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Game.Components
{
    public class TransformC : IComponent
    {
        public FVector3 Pos;
        public FFloat FacingX = FFloat.One;

        public IComponent Clone() => new TransformC { Pos = Pos, FacingX = FacingX };

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddFixed(Pos);
            hash.AddFixed(FacingX);
        }

        public override string ToString()
            => $"T[{Pos.X.Raw},{Pos.Y.Raw},{Pos.Z.Raw},f{FacingX.Raw}]";
    }
}
