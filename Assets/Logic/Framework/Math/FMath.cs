using FixMath.NET;

namespace Lockstep.Math
{
    public static class FMath
    {
        public static FFloat Sqrt(FFloat x)   => new FFloat(Fix64.Sqrt(x.V));
        public static FFloat Sin(FFloat x)    => new FFloat(Fix64.Sin(x.V));
        public static FFloat Cos(FFloat x)    => new FFloat(Fix64.Cos(x.V));
        public static FFloat Tan(FFloat x)    => new FFloat(Fix64.Tan(x.V));
        public static FFloat Atan2(FFloat y, FFloat x) => new FFloat(Fix64.Atan2(y.V, x.V));
        public static FFloat Abs(FFloat x)    => new FFloat(Fix64.Abs(x.V));
        public static int Sign(FFloat x)      => Fix64.Sign(x.V);
        public static FFloat Floor(FFloat x)  => new FFloat(Fix64.Floor(x.V));
        public static FFloat Ceil(FFloat x)   => new FFloat(Fix64.Ceiling(x.V));
        public static FFloat Round(FFloat x)  => new FFloat(Fix64.Round(x.V));

        public static FFloat Min(FFloat a, FFloat b) => a < b ? a : b;
        public static FFloat Max(FFloat a, FFloat b) => a > b ? a : b;
        public static int    Min(int a, int b)       => a < b ? a : b;
        public static int    Max(int a, int b)       => a > b ? a : b;

        public static FFloat Clamp(FFloat v, FFloat lo, FFloat hi)
            => v < lo ? lo : v > hi ? hi : v;
        public static int Clamp(int v, int lo, int hi)
            => v < lo ? lo : v > hi ? hi : v;

        public static FFloat Lerp(FFloat a, FFloat b, FFloat t) => a + (b - a) * Clamp(t, FFloat.Zero, FFloat.One);
    }
}
