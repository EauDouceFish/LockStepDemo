using System;

namespace Lockstep.Math
{
    // 2D 横版战斗 v1 用不到完整四元数（朝向用 int Facing = ±1 表达）。
    // 预留壳，等到加 3D 镜头/3D 特效的时候补全。
    [Serializable]
    public readonly struct FQuat : IEquatable<FQuat>
    {
        public readonly FFloat X, Y, Z, W;

        public FQuat(FFloat x, FFloat y, FFloat z, FFloat w) { X = x; Y = y; Z = z; W = w; }

        public static readonly FQuat Identity = new FQuat(FFloat.Zero, FFloat.Zero, FFloat.Zero, FFloat.One);

        // TODO(v2-3D): FromAxisAngle, FromEuler, Slerp, multiplication, etc.

        public bool Equals(FQuat o) => X == o.X && Y == o.Y && Z == o.Z && W == o.W;
        public override bool Equals(object o) => o is FQuat q && Equals(q);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        public static bool operator ==(FQuat a, FQuat b) => a.Equals(b);
        public static bool operator !=(FQuat a, FQuat b) => !a.Equals(b);
    }
}
