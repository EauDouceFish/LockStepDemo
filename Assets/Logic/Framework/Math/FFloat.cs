using System;
using FixMath.NET;

namespace Lockstep.Math
{
    [Serializable]
    public readonly struct FFloat : IEquatable<FFloat>, IComparable<FFloat>
    {
        public readonly Fix64 V;

        public FFloat(Fix64 v) { V = v; }

        public long Raw => V.RawValue;
        public float ToFloat() => (float)V;
        public int ToInt() => (int)V;

        public static FFloat FromRaw(long raw) => new FFloat(Fix64.FromRaw(raw));
        public static FFloat FromInt(int n) => new FFloat((Fix64)n);

        // 仅供配置/编辑器加载，运行时逻辑禁用 float
        public static FFloat FromFloatUnsafe(float f) => new FFloat((Fix64)f);

        public static readonly FFloat Zero = new FFloat(Fix64.Zero);
        public static readonly FFloat One = new FFloat(Fix64.One);
        public static readonly FFloat MinusOne = new FFloat(-Fix64.One);
        public static readonly FFloat Half = new FFloat(Fix64.One / (Fix64)2);
        public static readonly FFloat Two = new FFloat((Fix64)2);
        public static readonly FFloat MaxValue = new FFloat(Fix64.MaxValue);
        public static readonly FFloat MinValue = new FFloat(Fix64.MinValue);
        public static readonly FFloat Epsilon = new FFloat(Fix64.FromRaw(1));
        public static readonly FFloat PI = new FFloat(Fix64.Pi);
        public static readonly FFloat PIOver2 = new FFloat(Fix64.PiOver2);

        public static FFloat operator +(FFloat a, FFloat b) => new FFloat(a.V + b.V);
        public static FFloat operator -(FFloat a, FFloat b) => new FFloat(a.V - b.V);
        public static FFloat operator -(FFloat a) => new FFloat(-a.V);
        public static FFloat operator *(FFloat a, FFloat b) => new FFloat(a.V * b.V);
        public static FFloat operator /(FFloat a, FFloat b) => new FFloat(a.V / b.V);
        public static FFloat operator %(FFloat a, FFloat b) => new FFloat(a.V % b.V);

        public static bool operator <(FFloat a, FFloat b) => a.V < b.V;
        public static bool operator >(FFloat a, FFloat b) => a.V > b.V;
        public static bool operator <=(FFloat a, FFloat b) => a.V <= b.V;
        public static bool operator >=(FFloat a, FFloat b) => a.V >= b.V;
        public static bool operator ==(FFloat a, FFloat b) => a.V == b.V;
        public static bool operator !=(FFloat a, FFloat b) => a.V != b.V;

        public int CompareTo(FFloat other) => V.CompareTo(other.V);
        public bool Equals(FFloat other) => V == other.V;
        public override bool Equals(object obj) => obj is FFloat f && Equals(f);
        public override int GetHashCode() => V.RawValue.GetHashCode();
        public override string ToString() => ((float)V).ToString("F3");
    }
}
