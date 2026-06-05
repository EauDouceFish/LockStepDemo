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
using Lockstep.Mugen.Hit;
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

    /// <summary>Shared PalFX parameter bag (bytecode.go palFX_time..palFX_hue).</summary>
    public sealed class PalFXParamSet
    {
        public BytecodeExp Time;
        public BytecodeExp Color;
        public BytecodeExp[] Add;
        public BytecodeExp[] Mul;
        public BytecodeExp[] SinAdd;
        public BytecodeExp[] SinMul;
        public BytecodeExp[] SinColor;
        public BytecodeExp[] SinHue;
        public BytecodeExp InvertAll;
        public BytecodeExp InvertBlend;
        public BytecodeExp Hue;
    }

    /// <summary>Shared AfterImage parameter bag (bytecode.go afterImage_time..afterImage_ignorehitpause).</summary>
    public sealed class AfterImageParamSet
    {
        public BytecodeExp Time;
        public BytecodeExp[] Trans;
        public BytecodeExp Length;
        public BytecodeExp TimeGap;
        public BytecodeExp FrameGap;
        public BytecodeExp PalColor;
        public BytecodeExp PalHue;
        public BytecodeExp PalInvertAll;
        public BytecodeExp PalInvertBlend;
        public BytecodeExp[] PalBright;
        public BytecodeExp[] PalContrast;
        public BytecodeExp[] PalPostBright;
        public BytecodeExp[] PalAdd;
        public BytecodeExp[] PalMul;
        public BytecodeExp IgnoreHitPause;
    }

    /// <summary>Explod interpolation parameter bag.</summary>
    public sealed class ExplodInterpolationParamSet
    {
        public BytecodeExp Time;
        public BytecodeExp AnimElem;
        public BytecodeExp[] Position;
        public BytecodeExp[] Scale;
        public BytecodeExp Angle;
        public BytecodeExp[] Alpha;
        public BytecodeExp FocalLength;
        public BytecodeExp XShear;
        public BytecodeExp[] PalFXMul;
        public BytecodeExp[] PalFXAdd;
        public BytecodeExp PalFXColor;
        public BytecodeExp PalFXHue;
    }

    /// <summary>Pause: parser-ready placeholder until PauseTime/PauseMoveTime fields land on MChar.</summary>
    public sealed class PauseController : ParameterOnlyController
    {
        public BytecodeExp Time;
        public BytecodeExp MoveTime;
        public BytecodeExp PauseBg;
        public BytecodeExp EndCmdBufTime;
    }

    /// <summary>SuperPause: applies poweradd now; pause timers/p2defmul need MChar fields from Claude batch.</summary>
    public sealed class SuperPauseController : ParameterOnlyController
    {
        public BytecodeExp Time;
        public BytecodeExp MoveTime;
        public BytecodeExp PauseBg;
        public BytecodeExp EndCmdBufTime;
        public BytecodeExp Darken;
        public BytecodeExp Brightness;
        public BytecodeExp[] Anim;
        public BytecodeExp[] Position;
        public BytecodeExp PowerAdd;
        public BytecodeExp P2DefMul;
        public BytecodeExp Unhittable;
        public BytecodeExp[] Sound;

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
        public BytecodeExp GuardFlag;
        public BytecodeExp GuardFlagNot;
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
        public int GuardFlag;
        public int GuardFlagNot;
        public MHitDef Template;
        public PalFXParamSet PalFX = new PalFXParamSet();
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

    public sealed class TransController : ParameterOnlyController { public BytecodeExp[] Trans; public string TransText; }
    public sealed class SprPriorityController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp LayerNo; }
    public sealed class OffsetController : ParameterOnlyController { public BytecodeExp XOffset; public BytecodeExp YOffset; }
    public sealed class AngleDrawController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp XAngle; public BytecodeExp YAngle; public BytecodeExp[] Scale; }
    public sealed class AngleSetController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp XAngle; public BytecodeExp YAngle; }
    public sealed class AngleAddController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp XAngle; public BytecodeExp YAngle; }
    public sealed class AngleMulController : ParameterOnlyController { public BytecodeExp Value; public BytecodeExp XAngle; public BytecodeExp YAngle; }
    public sealed class AfterImageController : ParameterOnlyController { public AfterImageParamSet AfterImage = new AfterImageParamSet(); }
    public sealed class AfterImageTimeController : ParameterOnlyController { public BytecodeExp Time; }
    public sealed class PalFXController : ParameterOnlyController { public PalFXParamSet PalFX = new PalFXParamSet(); }
    public sealed class AllPalFXController : ParameterOnlyController { public PalFXParamSet PalFX = new PalFXParamSet(); }
    public sealed class BGPalFXController : ParameterOnlyController { public BytecodeExp Id; public BytecodeExp Index; public PalFXParamSet PalFX = new PalFXParamSet(); }
    public sealed class EnvColorController : ParameterOnlyController { public BytecodeExp[] Value; public BytecodeExp Time; public BytecodeExp Under; }

    /// <summary>PlaySnd: logic-layer no-op until R-SND connects audio events.</summary>
    public sealed class PlaySndController : ParameterOnlyController
    {
        public BytecodeExp[] Value;
        public BytecodeExp Channel;
        public BytecodeExp LowPriority;
        public BytecodeExp Pan;
        public BytecodeExp AbsPan;
        public BytecodeExp Volume;
        public BytecodeExp VolumeScale;
        public BytecodeExp FreqMul;
        public BytecodeExp Loop;
        public BytecodeExp Priority;
        public BytecodeExp LoopStart;
        public BytecodeExp LoopEnd;
        public BytecodeExp StartPosition;
        public BytecodeExp LoopCount;
        public BytecodeExp StopOnGetHit;
        public BytecodeExp StopOnChangeState;
    }

    /// <summary>StopSnd: logic-layer no-op until R-SND connects audio events.</summary>
    public sealed class StopSndController : ParameterOnlyController { public BytecodeExp Channel; }

    /// <summary>SndPan: logic-layer no-op until R-SND connects audio events.</summary>
    public sealed class SndPanController : ParameterOnlyController { public BytecodeExp Channel; public BytecodeExp Pan; public BytecodeExp AbsPan; }

    /// <summary>Explod: logic-layer no-op until R-ENT introduces explod entities.</summary>
    public class ExplodController : ParameterOnlyController
    {
        public BytecodeExp[] Anim;
        public BytecodeExp OwnPal;
        public BytecodeExp[] RemapPal;
        public BytecodeExp Id;
        public BytecodeExp Facing;
        public BytecodeExp VFacing;
        public BytecodeExp[] Position;
        public BytecodeExp[] Random;
        public BytecodeExp PosType;
        public BytecodeExp[] Velocity;
        public BytecodeExp[] Friction;
        public BytecodeExp[] Accel;
        public BytecodeExp[] Scale;
        public BytecodeExp BindTime;
        public BytecodeExp RemoveTime;
        public BytecodeExp SuperMove;
        public BytecodeExp SuperMoveTime;
        public BytecodeExp PauseMoveTime;
        public BytecodeExp SprPriority;
        public BytecodeExp LayerNo;
        public BytecodeExp Under;
        public BytecodeExp OnTop;
        public BytecodeExp[] Shadow;
        public BytecodeExp RemoveOnGetHit;
        public BytecodeExp RemoveOnChangeState;
        public BytecodeExp HideWithBars;
        public BytecodeExp[] Trans;
        public BytecodeExp AnimElem;
        public BytecodeExp AnimElemTime;
        public BytecodeExp AnimFreeze;
        public BytecodeExp Angle;
        public BytecodeExp YAngle;
        public BytecodeExp XAngle;
        public BytecodeExp XShear;
        public BytecodeExp Projection;
        public BytecodeExp FocalLength;
        public BytecodeExp ExplodIgnoreHitPause;
        public BytecodeExp BindId;
        public BytecodeExp Space;
        public BytecodeExp[] Window;
        public ExplodInterpolationParamSet Interpolation = new ExplodInterpolationParamSet();
        public BytecodeExp AnimPlayerNo;
        public BytecodeExp SpritePlayerNo;
        public BytecodeExp SyncParams;
        public BytecodeExp SyncLayer;
        public BytecodeExp SyncId;
        public BytecodeExp Shader;
        public BytecodeExp[] ShaderParam;
        public AfterImageParamSet AfterImage = new AfterImageParamSet();
        public PalFXParamSet PalFX = new PalFXParamSet();
    }

    public sealed class ModifyExplodController : ExplodController { public BytecodeExp Index; }
    public sealed class RemoveExplodController : ParameterOnlyController { public BytecodeExp Id; public BytecodeExp Index; }
    public sealed class MakeDustController : ParameterOnlyController { public BytecodeExp Spacing; public BytecodeExp[] Position; public BytecodeExp[] Position2; }
    public sealed class GameMakeAnimController : ParameterOnlyController { public BytecodeExp[] Value; public BytecodeExp[] Position; public BytecodeExp Under; }
    public sealed class EnvShakeController : ParameterOnlyController
    {
        public BytecodeExp Time;
        public BytecodeExp Amplitude;
        public BytecodeExp Frequency;
        public BytecodeExp Multiplier;
        public BytecodeExp Phase;
        public BytecodeExp Direction;
    }
    public sealed class FallEnvShakeController : ParameterOnlyController { }
    public sealed class ForceFeedbackController : ParameterOnlyController
    {
        public BytecodeExp Time;
        public BytecodeExp Waveform;
        public BytecodeExp Intensity;
    }
    public class DisplayToClipboardController : ParameterOnlyController { public BytecodeExp[] Params; public BytecodeExp Text; }
    public sealed class AppendToClipboardController : DisplayToClipboardController { }
    public sealed class ClearClipboardController : ParameterOnlyController { }
    public sealed class VictoryQuoteController : ParameterOnlyController { public BytecodeExp Value; }

    /// <summary>Text controller parameter capture. Logic-layer no-op until text entities exist.</summary>
    public class TextController : ParameterOnlyController
    {
        public BytecodeExp Removetime;
        public BytecodeExp LayerNo;
        public BytecodeExp[] Params;
        public BytecodeExp[] Font;
        public BytecodeExp[] LocalCoord;
        public BytecodeExp Bank;
        public BytecodeExp Align;
        public BytecodeExp[] TextSpacing;
        public BytecodeExp TextDelay;
        public BytecodeExp Text;
        public BytecodeExp[] Position;
        public BytecodeExp[] Velocity;
        public BytecodeExp[] MaxDist;
        public BytecodeExp[] Friction;
        public BytecodeExp[] Accel;
        public BytecodeExp Angle;
        public BytecodeExp XAngle;
        public BytecodeExp YAngle;
        public BytecodeExp Projection;
        public BytecodeExp FocalLength;
        public BytecodeExp[] Scale;
        public BytecodeExp[] Color;
        public BytecodeExp XShear;
        public BytecodeExp HideWithBars;
        public BytecodeExp Id;
        public PalFXParamSet PalFX = new PalFXParamSet();
    }

    public sealed class ModifyTextController : TextController { public BytecodeExp Index; }
    public sealed class RemoveTextController : ParameterOnlyController { public BytecodeExp Id; public BytecodeExp Index; }

    public sealed class TagInController : ParameterOnlyController
    {
        public BytecodeExp StateNo;
        public BytecodeExp PartnerStateNo;
        public BytecodeExp Self;
        public BytecodeExp Partner;
        public BytecodeExp Ctrl;
        public BytecodeExp PartnerCtrl;
        public BytecodeExp Leader;
        public BytecodeExp MemberNo;
    }

    public sealed class TagOutController : ParameterOnlyController
    {
        public BytecodeExp Self;
        public BytecodeExp Partner;
        public BytecodeExp StateNo;
        public BytecodeExp PartnerStateNo;
        public BytecodeExp MemberNo;
    }

    public sealed class ModifyStageVarController : ParameterOnlyController { }
}
