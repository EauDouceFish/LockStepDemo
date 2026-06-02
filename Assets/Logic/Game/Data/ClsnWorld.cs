using Lockstep.Math;

namespace Lockstep.Game.Data
{
    /// <summary>逻辑层用的 2D 轴对齐包围盒（横 X × 高度 Y）。</summary>
    public readonly struct RectAabb
    {
        public readonly FFloat MinX;
        public readonly FFloat MinY;
        public readonly FFloat MaxX;
        public readonly FFloat MaxY;

        public RectAabb(FFloat minX, FFloat minY, FFloat maxX, FFloat maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }
    }

    /// <summary>
    /// Clsn 框 → 世界 AABB 的纯计算（无 Unity、无 float）。CollisionSystem 与 Gizmo 共用。
    /// box 坐标相对角色原点；facing=+1 朝右、-1 朝左（镜像 X）。MUGEN 原生坐标（高度上为负），
    /// 但 AABB 只关心 min/max，符号无所谓。
    /// </summary>
    public static class ClsnWorld
    {
        public static RectAabb ToWorld(ClsnBox box, FFloat originX, FFloat originY, FFloat facing)
        {
            FFloat x1 = box.X1 * facing + originX;
            FFloat x2 = box.X2 * facing + originX;
            FFloat y1 = box.Y1 + originY;
            FFloat y2 = box.Y2 + originY;

            return new RectAabb(
                Min(x1, x2),
                Min(y1, y2),
                Max(x1, x2),
                Max(y1, y2));
        }

        public static bool Overlap(RectAabb a, RectAabb b)
        {
            return a.MinX <= b.MaxX && a.MaxX >= b.MinX
                && a.MinY <= b.MaxY && a.MaxY >= b.MinY;
        }

        static FFloat Min(FFloat a, FFloat b)
        {
            return a < b ? a : b;
        }

        static FFloat Max(FFloat a, FFloat b)
        {
            return a > b ? a : b;
        }
    }
}
