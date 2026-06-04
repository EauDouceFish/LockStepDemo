// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (velMul, hitVelSet, hitFallSet, hitFallVel, gravity, hitAdd StateController)
//         + src/char.go (hitAdd, hitFallSet, hitFallVel, gravity).
// Adapted to fixed-point (FFloat) for deterministic lockstep/rollback. See Docs/移植方案_Ikemen.md.
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    /// <summary>HitVelSet: copy selected gethit velocities back to current velocity.</summary>
    public sealed class HitVelSetController : MStateController
    {
        public BytecodeExp X;
        public BytecodeExp Y;
        public BytecodeExp Z;

        public override bool Run(MChar character)
        {
            FFloat velocityX = character.Vel.X;
            FFloat velocityY = character.Vel.Y;
            FFloat velocityZ = character.Vel.Z;

            if (X != null && X.Run(character).ToB())
            {
                // Local convention stores raw X velocity; MPhysics applies facing during integration.
                velocityX = character.Ghv.XVel;
            }
            if (Y != null && Y.Run(character).ToB())
            {
                velocityY = character.Ghv.YVel;
            }
            if (Z != null && Z.Run(character).ToB())
            {
                velocityZ = character.Ghv.ZVel;
            }

            character.Vel = new FVector3(velocityX, velocityY, velocityZ);
            return false;
        }
    }

    /// <summary>VelMul: multiply existing velocity components by optional x/y/z factors.</summary>
    public sealed class VelMulController : MStateController
    {
        public BytecodeExp X;
        public BytecodeExp Y;
        public BytecodeExp Z;

        public override bool Run(MChar character)
        {
            FFloat velocityX = X != null ? character.Vel.X * X.Run(character).ToF() : character.Vel.X;
            FFloat velocityY = Y != null ? character.Vel.Y * Y.Run(character).ToF() : character.Vel.Y;
            FFloat velocityZ = Z != null ? character.Vel.Z * Z.Run(character).ToF() : character.Vel.Z;
            character.Vel = new FVector3(velocityX, velocityY, velocityZ);
            return false;
        }
    }

    /// <summary>HitAdd: add combo hit counts to the attacker and the most recent target.</summary>
    public sealed class HitAddController : MStateController
    {
        public BytecodeExp Value;

        public override bool Run(MChar character)
        {
            if (Value == null)
            {
                return false;
            }

            int amount = Value.Run(character).ToI();
            if (amount == 0)
            {
                return false;
            }

            character.HitCount += amount;
            character.UniqHitCount += amount;

            if (character.Targets.Count > 0)
            {
                MChar target = character.Targets[character.Targets.Count - 1];
                if (target != null)
                {
                    target.Ghv.HitCount += amount;
                }
            }

            return false;
        }
    }

    /// <summary>HitFallSet: update the current gethit fall flag and fall velocity proxy fields.</summary>
    public sealed class HitFallSetController : MStateController
    {
        public BytecodeExp Value;
        public BytecodeExp XVelocity;
        public BytecodeExp YVelocity;
        public BytecodeExp ZVelocity;

        public override bool Run(MChar character)
        {
            if (Value != null)
            {
                character.Ghv.Fall = Value.Run(character).ToB();
            }
            if (XVelocity != null)
            {
                character.Ghv.XVel = XVelocity.Run(character).ToF();
            }
            if (YVelocity != null)
            {
                character.Ghv.YVel = YVelocity.Run(character).ToF();
            }
            if (ZVelocity != null)
            {
                character.Ghv.ZVel = ZVelocity.Run(character).ToF();
            }
            return false;
        }
    }

    /// <summary>HitFallVel: apply gethit fall velocity proxy fields while the character is in movetype H.</summary>
    public sealed class HitFallVelController : MStateController
    {
        const int MoveTypeHit = 2;

        public override bool Run(MChar character)
        {
            if (character.MoveType == MoveTypeHit)
            {
                character.Vel = new FVector3(character.Ghv.XVel, character.Ghv.YVel, character.Ghv.ZVel);
            }
            return false;
        }
    }

    /// <summary>Gravity: add character yaccel to vertical velocity.</summary>
    public sealed class GravityController : MStateController
    {
        public override bool Run(MChar character)
        {
            FFloat yaccel = character.Constants != null ? character.Constants.Yaccel : FFloat.Zero;
            character.Vel = new FVector3(character.Vel.X, character.Vel.Y + yaccel, character.Vel.Z);
            return false;
        }
    }
}
