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

        public override bool Run(MChar character)
        {
            Bind(character, character.Parent);
            return false;
        }

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

        public override bool Run(MChar character)
        {
            if (Value == null)
            {
                return false;
            }
            int animNo = Value.Run(character).ToI();
            IReadOnlyDictionary<int, MAnimData> sourceTable = character.StateOwner != null
                ? character.StateOwner.AnimTable : character.AnimTable;
            if (sourceTable != null && !sourceTable.ContainsKey(animNo))
            {
                return false;
            }
            character.PrevAnimNo = character.AnimNo;
            character.AnimNo = animNo;
            if (sourceTable != null)
            {
                character.AnimTable = sourceTable;
            }
            if (Elem != null)
            {
                character.AnimElem = System.Math.Max(0, Elem.Run(character).ToI() - 1);
                character.AnimElemNo = character.AnimElem + 1;
            }
            if (ElemTime != null)
            {
                character.AnimElemTime = ElemTime.Run(character).ToI();
            }
            return false;
        }
    }
}
