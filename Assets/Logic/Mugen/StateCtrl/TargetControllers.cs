// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (TargetFacing/TargetLifeAdd/TargetState/TargetVelSet/TargetVelAdd/
//         TargetPowerAdd/TargetDrop StateController, roughly bytecode.go:9382-9771)
//         + src/char.go target* helpers (roughly char.go:8161-8399).
// Adapted to fixed-point (FFloat) for deterministic lockstep/rollback. See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    /// <summary>Common target selector: id &lt; 0 selects all, index &gt;= 0 selects one from the filtered list.</summary>
    public abstract class TargetControllerBase : MStateController
    {
        public BytecodeExp Id;
        public BytecodeExp Index;

        // Ikemen reference: src/char.go target selection helpers used by Target* StateControllers.
        protected List<MChar> SelectTargets(MChar character)
        {
            int targetId = Id != null ? Id.Run(character).ToI() : -1;
            int targetIndex = Index != null ? Index.Run(character).ToI() : -1;
            return character.SelectTargetsByHitId(targetId, targetIndex);
        }

        protected static int CurrentStateOwnerPlayerNo(MChar character)
        {
            return character.StatePlayerNo >= 0 ? character.StatePlayerNo : character.PlayerNo;
        }

        protected static MChar CurrentStateOwner(MChar character, int ownerPlayerNo)
        {
            if (ownerPlayerNo == character.PlayerNo)
            {
                return character;
            }
            if (character.StateOwner != null && character.StateOwner.PlayerNo == ownerPlayerNo)
            {
                return character.StateOwner;
            }
            return null;
        }
    }

    /// <summary>TargetState: request a state change on selected targets.</summary>
    public sealed class TargetStateController : TargetControllerBase
    {
        public BytecodeExp Value;

        // Ikemen reference: src/bytecode.go TargetState StateController queues target state change.
        public override bool Run(MChar character)
        {
            if (Value == null)
            {
                return false;
            }

            int stateNo = Value.Run(character).ToI();
            if (stateNo < 0)
            {
                return false;
            }

            List<MChar> targets = SelectTargets(character);
            int ownerPlayerNo = CurrentStateOwnerPlayerNo(character);
            MChar owner = CurrentStateOwner(character, ownerPlayerNo);
            for (int index = 0; index < targets.Count; index++)
            {
                MChar target = targets[index];
                target.Ctrl = false;
                target.StateOwner = ownerPlayerNo == target.PlayerNo ? null : owner;
                target.QueueTransition(stateNo, ownerPlayerNo);
            }
            return false;
        }
    }

    /// <summary>TargetLifeAdd: add to selected target life with MUGEN kill clamping.</summary>
    public sealed class TargetLifeAddController : TargetControllerBase
    {
        public BytecodeExp Absolute;
        public BytecodeExp Kill;
        public BytecodeExp Dizzy;
        public BytecodeExp RedLife;
        public BytecodeExp Value;

        // Ikemen reference: src/bytecode.go TargetLifeAdd StateController applies kill-clamped life delta.
        public override bool Run(MChar character)
        {
            if (Value == null)
            {
                return false;
            }

            int amount = Value.Run(character).ToI();
            bool kill = Kill == null || Kill.Run(character).ToB();
            List<MChar> targets = SelectTargets(character);
            for (int index = 0; index < targets.Count; index++)
            {
                MChar target = targets[index];
                int life = target.Life + amount;
                if (!kill && life <= 0)
                {
                    life = 1;
                }
                target.Life = Clamp(life, 0, target.LifeMax);
            }
            return false;
        }

        // Ikemen reference: src/bytecode.go TargetLifeAdd clamps target life after applying value.
        static int Clamp(int value, int low, int high)
        {
            return value < low ? low : (value > high ? high : value);
        }
    }

    /// <summary>TargetPowerAdd: add to selected target power and clamp to [0, PowerMax].</summary>
    public sealed class TargetPowerAddController : TargetControllerBase
    {
        public BytecodeExp Value;

        // Ikemen reference: src/bytecode.go TargetPowerAdd StateController applies clamped target power delta.
        public override bool Run(MChar character)
        {
            if (Value == null)
            {
                return false;
            }

            int amount = Value.Run(character).ToI();
            List<MChar> targets = SelectTargets(character);
            for (int index = 0; index < targets.Count; index++)
            {
                MChar target = targets[index];
                target.Power = Clamp(target.Power + amount, 0, target.PowerMax);
            }
            return false;
        }

        // Ikemen reference: src/bytecode.go TargetPowerAdd clamps target power to legal range.
        static int Clamp(int value, int low, int high)
        {
            return value < low ? low : (value > high ? high : value);
        }
    }

    /// <summary>TargetVelSet: set selected velocity axes. X is converted through the target-facing convention.</summary>
    public sealed class TargetVelSetController : TargetControllerBase
    {
        public BytecodeExp X;
        public BytecodeExp Y;
        public BytecodeExp Z;

        // Ikemen reference: src/bytecode.go TargetVelSet StateController sets selected target velocity.
        public override bool Run(MChar character)
        {
            List<MChar> targets = SelectTargets(character);
            for (int index = 0; index < targets.Count; index++)
            {
                MChar target = targets[index];
                FFloat velocityX = X != null ? X.Run(character).ToF() * target.Facing : target.Vel.X;
                FFloat velocityY = Y != null ? Y.Run(character).ToF() : target.Vel.Y;
                FFloat velocityZ = Z != null ? Z.Run(character).ToF() : target.Vel.Z;
                target.Vel = new FVector3(velocityX, velocityY, velocityZ);
            }
            return false;
        }
    }

    /// <summary>TargetVelAdd: add selected velocity axes. X is converted through the target-facing convention.</summary>
    public sealed class TargetVelAddController : TargetControllerBase
    {
        public BytecodeExp X;
        public BytecodeExp Y;
        public BytecodeExp Z;

        // Ikemen reference: src/bytecode.go TargetVelAdd StateController adds selected target velocity.
        public override bool Run(MChar character)
        {
            List<MChar> targets = SelectTargets(character);
            for (int index = 0; index < targets.Count; index++)
            {
                MChar target = targets[index];
                FFloat velocityX = target.Vel.X + (X != null ? X.Run(character).ToF() * target.Facing : FFloat.Zero);
                FFloat velocityY = target.Vel.Y + (Y != null ? Y.Run(character).ToF() : FFloat.Zero);
                FFloat velocityZ = target.Vel.Z + (Z != null ? Z.Run(character).ToF() : FFloat.Zero);
                target.Vel = new FVector3(velocityX, velocityY, velocityZ);
            }
            return false;
        }
    }

    /// <summary>TargetFacing: value &gt; 0 copies attacker facing; value &lt; 0 flips it.</summary>
    public sealed class TargetFacingController : TargetControllerBase
    {
        public BytecodeExp Value;

        // Ikemen reference: src/bytecode.go TargetFacing StateController sets selected target facing.
        public override bool Run(MChar character)
        {
            if (Value == null)
            {
                return false;
            }

            int value = Value.Run(character).ToI();
            if (value == 0)
            {
                return false;
            }

            FFloat facing = value < 0 ? -character.Facing : character.Facing;
            List<MChar> targets = SelectTargets(character);
            for (int index = 0; index < targets.Count; index++)
            {
                targets[index].Facing = facing;
            }
            return false;
        }
    }

    /// <summary>TargetDrop: trims the local target list by exclude ID; default drops all targets.</summary>
    public sealed class TargetDropController : MStateController
    {
        public BytecodeExp ExcludeId;
        public BytecodeExp KeepOne;

        // Ikemen reference: src/bytecode.go TargetDrop StateController prunes the character target list.
        public override bool Run(MChar character)
        {
            int excludeId = ExcludeId != null ? ExcludeId.Run(character).ToI() : -1;
            bool keepOne = KeepOne == null || KeepOne.Run(character).ToB();
            List<MChar> kept = new List<MChar>();
            List<MTargetRef> keptRefs = new List<MTargetRef>();

            if (excludeId >= 0)
            {
                for (int index = 0; index < character.Targets.Count; index++)
                {
                    MChar target = character.Targets[index];
                    bool keep = target != null && target.Id == excludeId;
                    if (!keep && character.TargetRefs.Count > index)
                    {
                        keep = character.TargetRefs[index].HitDefId == excludeId;
                    }
                    if (keep)
                    {
                        kept.Add(target);
                        if (character.TargetRefs.Count > index)
                        {
                            keptRefs.Add(character.TargetRefs[index]);
                        }
                        else
                        {
                            keptRefs.Add(new MTargetRef { Target = target, HitDefId = target != null ? target.Id : -1 });
                        }
                        if (keepOne)
                        {
                            break;
                        }
                    }
                }
            }

            character.Targets = kept;
            character.TargetRefs = keptRefs;
            return false;
        }
    }
}
