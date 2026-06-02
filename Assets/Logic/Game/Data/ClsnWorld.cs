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
        /// <summary>把相对角色原点的 Clsn 框按朝向镜像并平移到世界坐标，返回轴对齐包围盒。</summary>
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

        /// <summary>两个轴对齐包围盒是否重叠（含边界相切）。</summary>
        public static bool Overlap(RectAabb first, RectAabb second)
        {
            return first.MinX <= second.MaxX && first.MaxX >= second.MinX
                && first.MinY <= second.MaxY && first.MaxY >= second.MinY;
        }

        /// <summary>
        /// 攻击框组(attack) 与受击框组(hurt) 是否有任意一对重叠（各自按朝向镜像并平移到世界）。
        /// 任一组为空 → 不重叠。CollisionSystem 用此判定 Clsn1×Clsn2。
        /// </summary>
        public static bool AnyOverlap(
            ClsnBox[] attack, FFloat attackX, FFloat attackY, FFloat attackFacing,
            ClsnBox[] hurt, FFloat hurtX, FFloat hurtY, FFloat hurtFacing)
        {
            if (attack == null || hurt == null)
            {
                return false;
            }
            for (int a = 0; a < attack.Length; a++)
            {
                RectAabb attackBox = ToWorld(attack[a], attackX, attackY, attackFacing);
                for (int h = 0; h < hurt.Length; h++)
                {
                    RectAabb hurtBox = ToWorld(hurt[h], hurtX, hurtY, hurtFacing);
                    if (Overlap(attackBox, hurtBox))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        static FFloat Min(FFloat left, FFloat right)
        {
            return left < right ? left : right;
        }

        static FFloat Max(FFloat left, FFloat right)
        {
            return left > right ? left : right;
        }
    }
}
