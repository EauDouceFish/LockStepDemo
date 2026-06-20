// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go bindToParent/bindToRoot/bindToTarget/targetBind/changeAnim2.
// Adapted to fixed-point lockstep. See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    public sealed class BindToParentController : MStateController
    {
        public BytecodeExp Time;
        public BytecodeExp Facing;
        public BytecodeExp[] Position;

        // Ikemen reference: src/bytecode.go BindToParent StateController binds helper to parent.
        public override bool Run(MChar character)
        {
            Bind(character, character.Parent);
            return false;
        }

        // Ikemen reference: src/bytecode.go bindToParent shares bind time/facing/pos resolution.
        void Bind(MChar character, MChar target)
        {
            if (target == null)
            {
                return;
            }
            int time = Time != null ? Time.Run(character).ToI() : 1;
            int bindFacing = 0;
            if (Facing != null)
            {
                int value = Facing.Run(character).ToI();
                if (value < 0) { bindFacing = -1; }
                else if (value > 0) { bindFacing = 1; }
            }
            character.BindTo(target, time, ReadPosition(character), bindFacing);
        }

        // Ikemen reference: src/bytecode.go bindToParent pos parameter maps to bind offset vector.
        FVector3 ReadPosition(MChar character)
        {
            FFloat x = FFloat.Zero;
            FFloat y = FFloat.Zero;
            FFloat z = FFloat.Zero;
            if (Position != null && Position.Length > 0)
            {
                x = Position[0].Run(character).ToF();
                if (Position.Length > 1)
                {
                    y = Position[1].Run(character).ToF();
                    if (Position.Length > 2)
                    {
                        z = Position[2].Run(character).ToF();
                    }
                }
            }
            return new FVector3(x, y, z);
        }
    }

    public sealed class BindToRootController : MStateController
    {
        public BytecodeExp Time;
        public BytecodeExp Facing;
        public BytecodeExp[] Position;

        // Ikemen reference: src/bytecode.go BindToRoot StateController binds helper to root or self.
        public override bool Run(MChar character)
        {
            MChar target = character.Root ?? character;
            int time = Time != null ? Time.Run(character).ToI() : 1;
            int bindFacing = 0;
            if (Facing != null)
            {
                int value = Facing.Run(character).ToI();
                if (value < 0) { bindFacing = -1; }
                else if (value > 0) { bindFacing = 1; }
            }
            character.BindTo(target, time, ReadPosition(character), bindFacing);
            return false;
        }

        // Ikemen reference: src/bytecode.go bindToRoot pos parameter maps to bind offset vector.
        FVector3 ReadPosition(MChar character)
        {
            FFloat x = FFloat.Zero;
            FFloat y = FFloat.Zero;
            FFloat z = FFloat.Zero;
            if (Position != null && Position.Length > 0)
            {
                x = Position[0].Run(character).ToF();
                if (Position.Length > 1)
                {
                    y = Position[1].Run(character).ToF();
                    if (Position.Length > 2)
                    {
                        z = Position[2].Run(character).ToF();
                    }
                }
            }
            return new FVector3(x, y, z);
        }
    }

    public sealed class TargetBindController : TargetControllerBase
    {
        public BytecodeExp Time;
        public BytecodeExp[] Position;

        // Ikemen reference: src/bytecode.go TargetBind StateController binds selected targets to self.
        public override bool Run(MChar character)
        {
            int time = Time != null ? Time.Run(character).ToI() : 1;
            FVector3 offset = ReadPosition(character);
            List<MChar> targets = SelectTargets(character);
            for (int index = 0; index < targets.Count; index++)
            {
                targets[index].BindTo(character, time, offset, 0);
            }
            return false;
        }

        // Ikemen reference: src/bytecode.go targetBind pos parameter maps to target bind offset.
        FVector3 ReadPosition(MChar character)
        {
            FFloat x = FFloat.Zero;
            FFloat y = FFloat.Zero;
            FFloat z = FFloat.Zero;
            if (Position != null && Position.Length > 0)
            {
                x = Position[0].Run(character).ToF();
                if (Position.Length > 1)
                {
                    y = Position[1].Run(character).ToF();
                    if (Position.Length > 2)
                    {
                        z = Position[2].Run(character).ToF();
                    }
                }
            }
            return new FVector3(x, y, z);
        }
    }

    public sealed class BindToTargetController : TargetControllerBase
    {
        public BytecodeExp Time;
        public BytecodeExp[] Position;
        public BytecodeExp PosZ;

        // Ikemen reference: src/bytecode.go BindToTarget StateController binds self to selected target.
        public override bool Run(MChar character)
        {
            List<MChar> targets = SelectTargets(character);
            if (targets.Count == 0)
            {
                return false;
            }
            int time = Time != null ? Time.Run(character).ToI() : 1;
            FVector3 offset = ReadPosition(character);
            character.BindTo(targets[0], time, offset, 0);
            return false;
        }

        // Ikemen reference: src/bytecode.go bindToTarget pos/posz parameters map to bind offset.
        FVector3 ReadPosition(MChar character)
        {
            FFloat x = FFloat.Zero;
            FFloat y = FFloat.Zero;
            FFloat z = FFloat.Zero;
            if (Position != null && Position.Length > 0)
            {
                x = Position[0].Run(character).ToF();
                if (Position.Length > 1)
                {
                    y = Position[1].Run(character).ToF();
                }
            }
            if (PosZ != null)
            {
                z = PosZ.Run(character).ToF();
            }
            return new FVector3(x, y, z);
        }
    }

    public sealed class ChangeAnim2Controller : MStateController
    {
        public BytecodeExp Value;
        public BytecodeExp Elem;
        public BytecodeExp ElemTime;

        // Ikemen reference: src/bytecode.go ChangeAnim2 StateController plays state-owner animation.
        public override bool Run(MChar character)
        {
            if (Value == null)
            {
                return false;
            }
            int animNo = Value.Run(character).ToI();
            int elem = Elem != null ? Elem.Run(character).ToI() - 1 : 0;
            int elemTime = ElemTime != null ? ElemTime.Run(character).ToI() : 0;

            // Standalone characters used outside MBattleEngine have no registered player identity.
            // Preserve the legacy StateOwner table fallback while registered matches use playerNo ownership.
            if (character.StatePlayerNo < 0 && character.StateOwner != null && character.StateOwner.PlayerNo < 0)
            {
                IReadOnlyDictionary<int, MAnimData> table = character.StateOwner.AnimTable;
                if (table != null && !table.ContainsKey(animNo))
                {
                    return false;
                }
                character.AnimTable = table;
                MAnimSystem.PlayAt(character, animNo, table, elem, elemTime);
                return false;
            }

            character.PlayAnimation(animNo, character.StatePlayerNo, character.PlayerNo, elem, elemTime);
            return false;
        }
    }
}
