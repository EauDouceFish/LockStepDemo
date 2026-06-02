using Lockstep.Math;

namespace Lockstep.Game.Data
{
    /// <summary>
    /// 碰撞矩形。MUGEN Clsn1=攻击框 / Clsn2=受击框。
    /// 坐标为 (横向 X, 高度 Y) 相对角色原点（MUGEN 原生 2D 约定，上为负）。
    /// facing 镜像在运行时做；纵深厚度（v2 火影模式）由引擎另给，不在此结构。
    /// </summary>
    public readonly struct ClsnBox
    {
        public readonly FFloat X1;
        public readonly FFloat Y1;
        public readonly FFloat X2;
        public readonly FFloat Y2;

        public ClsnBox(FFloat x1, FFloat y1, FFloat x2, FFloat y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }
    }
}
