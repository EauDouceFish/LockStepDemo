// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go posUpdate()(位置积分 + 物理类型摩擦/重力) + gravity()。
// Adapted to fixed-point. 落地/地面夹取不在此（MUGEN 由公共状态 common1 的 trigger 检测 pos/vel 处理）。
// localcoord 缩放因子在 v1 单坐标(320)下为 1，故略。See Docs/移植方案_Ikemen.md.
using Lockstep.Math;
using Lockstep.Mugen.Char;

namespace Lockstep.Mugen.Battle
{
    /// <summary>
    /// 单角色物理推进（移植 Ikemen posUpdate）：先按当前速度积分位置（X 乘朝向），
    /// 再按状态 physics 类型对速度施加摩擦(S/C)或重力(A)，供下一帧使用。N 型不施加。
    /// </summary>
    public static class MPhysics
    {
        // physics 码（对齐 MugenCodes：S=1/C=2/A=4/N=16）
        const int PhysicsStand = 1;
        const int PhysicsCrouch = 2;
        const int PhysicsAir = 4;

        public static void Step(MChar c)
        {
            // 位置积分：pos = oldPos + vel*facing(x) + vel(y)（对齐 Ikemen setPosX/Y）。
            c.OldPos = c.Pos;
            FFloat px = c.Pos.X + c.Vel.X * c.Facing;
            FFloat py = c.Pos.Y + c.Vel.Y;
            c.Pos = new FVector3(px, py, c.Pos.Z);

            if (c.Constants == null)
            {
                return;
            }

            // 按状态 physics 类型施加摩擦/重力（对齐 Ikemen posUpdate 末尾 switch ss.physics）。
            switch (c.Physics)
            {
                case PhysicsStand:
                    ApplyGroundFriction(c, c.Constants.StandFriction, c.Constants.StandFrictionThreshold);
                    break;
                case PhysicsCrouch:
                    ApplyGroundFriction(c, c.Constants.CrouchFriction, c.Constants.CrouchFrictionThreshold);
                    break;
                case PhysicsAir:
                    // 重力：vel.y += yaccel（MUGEN 下为正，落地由公共状态检测）。
                    c.Vel = new FVector3(c.Vel.X, c.Vel.Y + c.Constants.Yaccel, c.Vel.Z);
                    break;
            }
        }

        // 地面摩擦：vel.x *= friction，绝对值低于阈值则归零（对齐 Ikemen getStandFriction + snap）。
        static void ApplyGroundFriction(MChar c, FFloat friction, FFloat threshold)
        {
            FFloat vx = c.Vel.X * friction;
            FFloat absVx = vx < FFloat.Zero ? -vx : vx;
            if (absVx < threshold)
            {
                vx = FFloat.Zero;
            }
            c.Vel = new FVector3(vx, c.Vel.Y, c.Vel.Z);
        }
    }
}
