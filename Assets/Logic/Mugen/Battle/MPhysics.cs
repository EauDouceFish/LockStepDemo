// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go posUpdate()(位置积分 + 物理类型摩擦/重力) + gravity()。
// Adapted to fixed-point. 落地/地面夹取不在此（MUGEN 由公共状态 common1 的 trigger 检测 pos/vel 处理）。
// localcoord 缩放因子在 v1 单坐标(320)下为 1，故略。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
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
        const int StateAir = 4;
        static readonly FFloat MinimumPushHalfWidth = FFloat.FromInt(5);

        // Ikemen reference: src/char.go:9549 posUpdate plus src/char.go:9455 gravity for per-frame position/velocity integration.
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
        // Ikemen reference: src/char.go posUpdate ground physics applies stand/crouch friction and snaps small velocities to zero.
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

        // Ikemen reference: src/char.go CharList.pushDetection. This is the current 2D subset:
        // use Width/player size on X and SizeHeight on Y, then split overlap deterministically.
        public static void ResolvePlayerPush(IReadOnlyList<MChar> chars, MStage stage)
        {
            if (chars == null)
            {
                return;
            }

            for (int i = 0; i < chars.Count; i++)
            {
                MChar a = chars[i];
                if (!CanPush(a))
                {
                    continue;
                }
                for (int j = i + 1; j < chars.Count; j++)
                {
                    MChar b = chars[j];
                    if (!CanPush(b) || !PushTeamsInteract(a, b) || !VerticalOverlaps(a, b))
                    {
                        continue;
                    }

                    FFloat aLeft;
                    FFloat aRight;
                    FFloat bLeft;
                    FFloat bRight;
                    PushBoundsX(a, out aLeft, out aRight);
                    PushBoundsX(b, out bLeft, out bRight);
                    FFloat overlap = Min(aRight, bRight) - Max(aLeft, bLeft);
                    if (overlap <= FFloat.Zero)
                    {
                        continue;
                    }

                    SeparatePair(a, b, overlap);
                    ClampAfterPush(a, stage);
                    ClampAfterPush(b, stage);
                }
            }
        }

        static bool CanPush(MChar c)
        {
            return c != null && !c.PauseBool && !c.PosFreeze && !c.Destroyed && c.PlayerPushEnabled;
        }

        static bool PushTeamsInteract(MChar a, MChar b)
        {
            bool sameTeam = TeamOf(a) == TeamOf(b);
            if (sameTeam)
            {
                return a.PushAffectTeam <= 0 || b.PushAffectTeam <= 0;
            }
            return a.PushAffectTeam >= 0 && b.PushAffectTeam >= 0;
        }

        static int TeamOf(MChar c)
        {
            return c.Root != null ? c.Root.Id : c.Id;
        }

        static bool VerticalOverlaps(MChar a, MChar b)
        {
            FFloat aTop;
            FFloat aBottom;
            FFloat bTop;
            FFloat bBottom;
            PushBoundsY(a, out aTop, out aBottom);
            PushBoundsY(b, out bTop, out bBottom);
            return Min(aBottom, bBottom) - Max(aTop, bTop) > FFloat.Zero;
        }

        static void PushBoundsX(MChar c, out FFloat left, out FFloat right)
        {
            FFloat localLeft;
            FFloat localRight;
            PushLocalBoundsX(c, out localLeft, out localRight);
            if (c.Facing.Raw < 0)
            {
                FFloat mirroredLeft = -localRight;
                localRight = -localLeft;
                localLeft = mirroredLeft;
            }
            left = c.Pos.X + localLeft;
            right = c.Pos.X + localRight;
        }

        static void PushLocalBoundsX(MChar c, out FFloat left, out FFloat right)
        {
            left = -WidthBack(c);
            right = WidthFront(c);
            if (left > right)
            {
                FFloat t = left;
                left = right;
                right = t;
            }
            if (left > -MinimumPushHalfWidth) { left = -MinimumPushHalfWidth; }
            if (right < MinimumPushHalfWidth) { right = MinimumPushHalfWidth; }
        }

        static void PushBoundsY(MChar c, out FFloat top, out FFloat bottom)
        {
            FFloat height = c.Constants != null ? c.Constants.SizeHeight : FFloat.FromInt(60);
            top = c.Pos.Y - height;
            bottom = c.Pos.Y;
        }

        static FFloat WidthFront(MChar c)
        {
            if (c.WidthPlayerFrontSet)
            {
                return c.WidthPlayerFront;
            }
            if (c.Constants == null)
            {
                return FFloat.Zero;
            }
            return c.StateType == StateAir ? c.Constants.SizeAirFront : c.Constants.SizeGroundFront;
        }

        static FFloat WidthBack(MChar c)
        {
            if (c.WidthPlayerBackSet)
            {
                return c.WidthPlayerBack;
            }
            if (c.Constants == null)
            {
                return FFloat.Zero;
            }
            return c.StateType == StateAir ? c.Constants.SizeAirBack : c.Constants.SizeGroundBack;
        }

        static void SeparatePair(MChar a, MChar b, FFloat overlap)
        {
            bool aIsLeft = a.Pos.X < b.Pos.X || (a.Pos.X == b.Pos.X && a.Id <= b.Id);
            FFloat aMove;
            FFloat bMove;
            if (a.PushPriority > b.PushPriority)
            {
                aMove = FFloat.Zero;
                bMove = overlap;
            }
            else if (a.PushPriority < b.PushPriority)
            {
                aMove = overlap;
                bMove = FFloat.Zero;
            }
            else
            {
                aMove = overlap / FFloat.FromInt(2);
                bMove = overlap - aMove;
            }

            if (aIsLeft)
            {
                a.Pos = new FVector3(a.Pos.X - aMove, a.Pos.Y, a.Pos.Z);
                b.Pos = new FVector3(b.Pos.X + bMove, b.Pos.Y, b.Pos.Z);
            }
            else
            {
                a.Pos = new FVector3(a.Pos.X + aMove, a.Pos.Y, a.Pos.Z);
                b.Pos = new FVector3(b.Pos.X - bMove, b.Pos.Y, b.Pos.Z);
            }
        }

        static void ClampAfterPush(MChar c, MStage stage)
        {
            if (stage == null || !stage.BoundsEnabled || !c.ScreenBoundStageBound)
            {
                return;
            }
            FFloat clampedX = c.Pos.X;
            if (stage.ClampX(ref clampedX))
            {
                c.Pos = new FVector3(clampedX, c.Pos.Y, c.Pos.Z);
            }
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
