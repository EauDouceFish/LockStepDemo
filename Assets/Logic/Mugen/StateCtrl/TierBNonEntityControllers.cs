// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (Pause/SuperPause/PosFreeze/Width/PlayerPush/ScreenBound/AttackDist,
//         HitOverride/MoveHitReset/ReversalDef, VarRandom/VarRangeSet/RemapPal,
//         Trans/SprPriority/Offset/Angle*/AfterImage*/PalFX*/EnvColor,
//         PlaySnd/StopSnd/SndPan, Explod/ModifyExplod/RemoveExplod/MakeDust/GameMakeAnim/
//         EnvShake/FallEnvShake/ForceFeedback/DisplayToClipboard/VictoryQuote StateController,
//         roughly bytecode.go:5030-11350).
// Adapted to fixed-point (FFloat) for deterministic lockstep/rollback. See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    /// <summary>
    /// Parameter-preserving no-op base for controllers whose observable state belongs to MChar/system fields
    /// that are not present in this worktree yet. It deliberately avoids static side tables because rollback
    /// state must live on MChar and be cloned/hashed there.
    /// </summary>
    public abstract class ParameterOnlyController : MStateController
    {
        public Dictionary<string, string> Parameters = new Dictionary<string, string>();

        public override bool Run(MChar character)
        {
            return false;
        }
    }

    /// <summary>Pause: parser-ready placeholder until PauseTime/PauseMoveTime fields land on MChar.</summary>
    public sealed class PauseController : ParameterOnlyController
    {
        public BytecodeExp Time;
        public BytecodeExp MoveTime;
    }

    /// <summary>SuperPause: applies poweradd now; pause timers/p2defmul need MChar fields from Claude batch.</summary>
    public sealed class SuperPauseController : ParameterOnlyController
    {
        public BytecodeExp Time;
        public BytecodeExp MoveTime;
        public BytecodeExp PowerAdd;
        public BytecodeExp P2DefMul;

        public override bool Run(MChar character)
        {
            if (PowerAdd != null)
            {
                character.Power = Clamp(character.Power + PowerAdd.Run(character).ToI(), 0, character.PowerMax);
            }
            return false;
        }

        static int Clamp(int value, int low, int high)
        {
            return value < low ? low : (value > high ? high : value);
        }
    }

    /// <summary>PosFreeze: requires MChar PosFreeze flag to affect physics; placeholder parses params only.</summary>
    public sealed class PosFreezeController : ParameterOnlyController
    {
        public BytecodeExp Value;
    }

    /// <summary>Width: requires MChar width/edge width fields; placeholder parses params only.</summary>
    public sealed class WidthController : ParameterOnlyController
    {
        public BytecodeExp[] Value;
        public BytecodeExp[] Player;
        public BytecodeExp[] Edge;
    }

    /// <summary>PlayerPush: requires MChar push flags/priority; placeholder parses params only.</summary>
    public sealed class PlayerPushController : ParameterOnlyController
    {
        public BytecodeExp Value;
        public BytecodeExp Priority;
        public BytecodeExp AffectTeam;
    }

    /// <summary>ScreenBound: requires MChar screen/stage/move-camera flags; placeholder parses params only.</summary>
    public sealed class ScreenBoundController : ParameterOnlyController
    {
        public BytecodeExp Value;
        public BytecodeExp[] MoveCamera;
        public BytecodeExp StageBound;
    }

    /// <summary>AttackDist: Ikemen writes HitDef guard distance arrays; MHitDef lacks them in this worktree.</summary>
    public sealed class AttackDistController : ParameterOnlyController
    {
        public BytecodeExp[] XValues;
        public BytecodeExp[] YValues;
        public BytecodeExp[] ZValues;
    }

    /// <summary>HitOverride: requires an eight-slot MChar HitOverride container.</summary>
    public sealed class HitOverrideController : ParameterOnlyController
    {
        public int Attr;
        public BytecodeExp Slot;
        public BytecodeExp StateNo;
        public BytecodeExp Time;
        public BytecodeExp ForceAir;
        public BytecodeExp ForceGuard;
        public BytecodeExp KeepState;
    }

    /// <summary>
    /// MoveHitReset: clears the currently exposed move-contact flags. Ikemen clearMoveHit resets mctime/counterHit
    /// (char.go:4469-4472); those exact fields are not present yet, so this maps to the port's visible trigger flags.
    /// </summary>
    public sealed class MoveHitResetController : MStateController
    {
        public override bool Run(MChar character)
        {
            character.MoveContact = 0;
            character.MoveHit = 0;
            character.MoveGuarded = 0;
            character.MoveReversed = 0;
            return false;
        }
    }

    /// <summary>ReversalDef: requires active reversal definition container and hit detection integration.</summary>
    public sealed class ReversalDefController : ParameterOnlyController
    {
        public int Attr;
    }

    /// <summary>VarRandom: var(v)=Rand(min,max), inclusive bounds, using the shared Ikemen RNG.</summary>
    public sealed class VarRandomController : MStateController
    {
        public BytecodeExp Index;
        public BytecodeExp[] Range;

        public override bool Run(MChar character)
        {
            int index = Index != null ? Index.Run(character).ToI() : 0;
            int minimum = 0;
            int maximum = 1000;
            if (Range != null && Range.Length > 0)
            {
                maximum = Range[0].Run(character).ToI();
                if (Range.Length > 1)
                {
                    minimum = maximum;
                    maximum = Range[1].Run(character).ToI();
                }
            }
            if (maximum < minimum)
            {
                int oldMinimum = minimum;
                minimum = maximum;
                maximum = oldMinimum;
            }
            character.IntVars[index] = character.Rng != null ? character.Rng.Rand(minimum, maximum) : minimum;
            return false;
        }
    }

    /// <summary>VarRangeSet: sets var/fvar entries in [first,last], matching Ikemen's inclusive range.</summary>
    public sealed class VarRangeSetController : MStateController
    {
        public BytecodeExp First;
        public BytecodeExp Last;
        public BytecodeExp Value;
        public BytecodeExp FloatValue;

        public override bool Run(MChar character)
        {
            int first = First != null ? First.Run(character).ToI() : 0;
            int last = Last != null ? Last.Run(character).ToI() : 59;
            if (last < first)
            {
                return false;
            }
            if (Value != null)
            {
                int value = Value.Run(character).ToI();
                for (int index = first; index <= last; index++)
                {
                    character.IntVars[index] = value;
                }
            }
            if (FloatValue != null)
            {
                FFloat value = FloatValue.Run(character).ToF();
                for (int index = first; index <= last; index++)
                {
                    character.FloatVars[index] = value;
                }
            }
            return false;
        }
    }

    /// <summary>RemapPal: requires palette remap state. PalNo alone is not equivalent to Ikemen remapPal.</summary>
    public sealed class RemapPalController : ParameterOnlyController
    {
        public BytecodeExp[] Source;
        public BytecodeExp[] Dest;
    }

    public sealed class TransController : ParameterOnlyController { public BytecodeExp[] Trans; }
    public sealed class SprPriorityController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp LayerNo; }
    public sealed class OffsetController : ParameterOnlyController { public BytecodeExp XOffset; public BytecodeExp YOffset; }
    public sealed class AngleDrawController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp XAngle; public BytecodeExp YAngle; public BytecodeExp[] Scale; }
    public sealed class AngleSetController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp XAngle; public BytecodeExp YAngle; }
    public sealed class AngleAddController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp XAngle; public BytecodeExp YAngle; }
    public sealed class AngleMulController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp XAngle; public BytecodeExp YAngle; }
    public sealed class AfterImageController : ParameterOnlyController { public BytecodeExp Time; }
    public sealed class AfterImageTimeController : ParameterOnlyController { public BytecodeExp Time; }
    public sealed class PalFXController : ParameterOnlyController { public BytecodeExp Time; }
    public sealed class AllPalFXController : ParameterOnlyController { public BytecodeExp Time; }
    public sealed class BGPalFXController : ParameterOnlyController { public BytecodeExp Time; }
    public sealed class EnvColorController : ParameterOnlyController { public BytecodeExp[] Value; public BytecodeExp Time; public BytecodeExp Under; }

    /// <summary>PlaySnd: logic-layer no-op until R-SND connects audio events.</summary>
    public sealed class PlaySndController : ParameterOnlyController { }

    /// <summary>StopSnd: logic-layer no-op until R-SND connects audio events.</summary>
    public sealed class StopSndController : ParameterOnlyController { }

    /// <summary>SndPan: logic-layer no-op until R-SND connects audio events.</summary>
    public sealed class SndPanController : ParameterOnlyController { }

    /// <summary>Explod: logic-layer no-op until R-ENT introduces explod entities.</summary>
    public sealed class ExplodController : ParameterOnlyController { }

    public sealed class ModifyExplodController : ParameterOnlyController { }
    public sealed class RemoveExplodController : ParameterOnlyController { public BytecodeExp Id; public BytecodeExp Index; }
    public sealed class MakeDustController : ParameterOnlyController { }
    public sealed class GameMakeAnimController : ParameterOnlyController { }
    public sealed class EnvShakeController : ParameterOnlyController { }
    public sealed class FallEnvShakeController : ParameterOnlyController { }
    public sealed class ForceFeedbackController : ParameterOnlyController { }
    public sealed class DisplayToClipboardController : ParameterOnlyController { }
    public sealed class VictoryQuoteController : ParameterOnlyController { public BytecodeExp Value; }
}
