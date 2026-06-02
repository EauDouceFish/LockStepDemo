using System;

namespace Lockstep.Math
{
    // 2D AABB 盒（hitbox / hurtbox 用），中心 + 半尺寸表示。
    // 严禁用 UnityEngine.Bounds —— 它内部是 float。
    [Serializable]
    public readonly struct FAABB2D : IEquatable<FAABB2D>
    {
        public readonly FVector2 Center;
        public readonly FVector2 HalfSize;

        public FAABB2D(FVector2 center, FVector2 halfSize) { Center = center; HalfSize = halfSize; }

        public FFloat MinX => Center.X - HalfSize.X;
        public FFloat MaxX => Center.X + HalfSize.X;
        public FFloat MinY => Center.Y - HalfSize.Y;
        public FFloat MaxY => Center.Y + HalfSize.Y;

        public FAABB2D Translate(FVector2 offset) => new FAABB2D(Center + offset, HalfSize);

        // 朝向翻转（角色面左 / 面右）—— hitbox 配置默认朝右，朝左时镜像
        public FAABB2D Mirror(int facing)
            => facing >= 0 ? this : new FAABB2D(new FVector2(-Center.X, Center.Y), HalfSize);

        public static bool Overlaps(FAABB2D a, FAABB2D b)
        {
            return a.MaxX > b.MinX && a.MinX < b.MaxX
                && a.MaxY > b.MinY && a.MinY < b.MaxY;
        }

        public bool Contains(FVector2 p)
            => p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;

        public bool Equals(FAABB2D o) => Center == o.Center && HalfSize == o.HalfSize;
        public override bool Equals(object o) => o is FAABB2D b && Equals(b);
        public override int GetHashCode() => HashCode.Combine(Center, HalfSize);
        public static bool operator ==(FAABB2D a, FAABB2D b) => a.Equals(b);
        public static bool operator !=(FAABB2D a, FAABB2D b) => !a.Equals(b);
    }
}
