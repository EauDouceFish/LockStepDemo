using System;

namespace Lockstep.Math
{
    // 2D 横版战斗的主力向量。逻辑层 90% 用它，FVector3 仅在少数 3D 抽象处使用。
    [Serializable]
    public readonly struct FVector2 : IEquatable<FVector2>
    {
        public readonly FFloat X;
        public readonly FFloat Y;

        public FVector2(FFloat x, FFloat y) { X = x; Y = y; }
        public FVector2(int x, int y) { X = FFloat.FromInt(x); Y = FFloat.FromInt(y); }

        public static readonly FVector2 Zero  = new FVector2(FFloat.Zero, FFloat.Zero);
        public static readonly FVector2 One   = new FVector2(FFloat.One,  FFloat.One);
        public static readonly FVector2 Right = new FVector2(FFloat.One,  FFloat.Zero);
        public static readonly FVector2 Up    = new FVector2(FFloat.Zero, FFloat.One);
        public static readonly FVector2 Left  = new FVector2(FFloat.MinusOne, FFloat.Zero);
        public static readonly FVector2 Down  = new FVector2(FFloat.Zero, FFloat.MinusOne);

        public static FVector2 operator +(FVector2 a, FVector2 b) => new FVector2(a.X + b.X, a.Y + b.Y);
        public static FVector2 operator -(FVector2 a, FVector2 b) => new FVector2(a.X - b.X, a.Y - b.Y);
        public static FVector2 operator -(FVector2 a) => new FVector2(-a.X, -a.Y);
        public static FVector2 operator *(FVector2 a, FFloat s) => new FVector2(a.X * s, a.Y * s);
        public static FVector2 operator *(FFloat s, FVector2 a) => new FVector2(a.X * s, a.Y * s);
        public static FVector2 operator /(FVector2 a, FFloat s) => new FVector2(a.X / s, a.Y / s);
        public static bool operator ==(FVector2 a, FVector2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(FVector2 a, FVector2 b) => !(a == b);

        public FFloat SqrMagnitude => X * X + Y * Y;
        public FFloat Magnitude => FMath.Sqrt(SqrMagnitude);
        public FVector2 Normalized
        {
            get
            {
                var m = Magnitude;
                return m == FFloat.Zero ? Zero : this / m;
            }
        }

        public static FFloat Dot(FVector2 a, FVector2 b) => a.X * b.X + a.Y * b.Y;
        public static FFloat Cross(FVector2 a, FVector2 b) => a.X * b.Y - a.Y * b.X;
        public static FFloat Distance(FVector2 a, FVector2 b) => (a - b).Magnitude;
        public static FFloat SqrDistance(FVector2 a, FVector2 b) => (a - b).SqrMagnitude;
        public static FVector2 Lerp(FVector2 a, FVector2 b, FFloat t) => a + (b - a) * t;

        public bool Equals(FVector2 o) => X == o.X && Y == o.Y;
        public override bool Equals(object o) => o is FVector2 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";
    }
}
