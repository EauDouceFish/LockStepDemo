using System;

namespace Lockstep.Math
{
    // 2D 横版战斗里 v1 几乎用不到。预留给镜头/特效坐标抽象。
    [Serializable]
    public readonly struct FVector3 : IEquatable<FVector3>
    {
        public readonly FFloat X;
        public readonly FFloat Y;
        public readonly FFloat Z;

        public FVector3(FFloat x, FFloat y, FFloat z) { X = x; Y = y; Z = z; }
        public FVector3(FVector2 xy, FFloat z) { X = xy.X; Y = xy.Y; Z = z; }

        public static readonly FVector3 Zero    = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
        public static readonly FVector3 One     = new FVector3(FFloat.One,  FFloat.One,  FFloat.One);
        public static readonly FVector3 Right   = new FVector3(FFloat.One,  FFloat.Zero, FFloat.Zero);
        public static readonly FVector3 Up      = new FVector3(FFloat.Zero, FFloat.One,  FFloat.Zero);
        public static readonly FVector3 Forward = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.One);

        public static FVector3 operator +(FVector3 a, FVector3 b) => new FVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static FVector3 operator -(FVector3 a, FVector3 b) => new FVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static FVector3 operator -(FVector3 a) => new FVector3(-a.X, -a.Y, -a.Z);
        public static FVector3 operator *(FVector3 a, FFloat s) => new FVector3(a.X * s, a.Y * s, a.Z * s);
        public static FVector3 operator /(FVector3 a, FFloat s) => new FVector3(a.X / s, a.Y / s, a.Z / s);
        public static bool operator ==(FVector3 a, FVector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        public static bool operator !=(FVector3 a, FVector3 b) => !(a == b);

        public FFloat SqrMagnitude => X * X + Y * Y + Z * Z;
        public FFloat Magnitude => FMath.Sqrt(SqrMagnitude);
        public FVector3 Normalized { get { var m = Magnitude; return m == FFloat.Zero ? Zero : this / m; } }
        public FVector2 XY => new FVector2(X, Y);

        public static FFloat Dot(FVector3 a, FVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public bool Equals(FVector3 o) => X == o.X && Y == o.Y && Z == o.Z;
        public override bool Equals(object o) => o is FVector3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
