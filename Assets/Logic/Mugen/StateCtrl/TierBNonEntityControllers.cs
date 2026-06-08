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

    /// <summary>Pause: 写共享暂停态 buffer（移植 bytecode.go pause.Run，默认 t=0/mt=0）。</summary>
    public sealed class PauseController : ParameterOnlyController
    {
        public BytecodeExp Time;
        public BytecodeExp MoveTime;
        public BytecodeExp PauseBg;
        public BytecodeExp EndCmdBufTime;

        public override bool Run(MChar character)
        {
            int t = Time != null ? Time.Run(character).ToI() : 0;
            int mt = MoveTime != null ? MoveTime.Run(character).ToI() : 0;
            character.SetPause(t, mt);
            return false;
        }
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
            // 写共享暂停态（移植 bytecode.go superPause.Run，默认 t=30/mt=0/unhittable=true）。
            int t = Time != null ? Time.Run(character).ToI() : 30;
            int mt = MoveTime != null ? MoveTime.Run(character).ToI() : 0;
            bool uh = Unhittable == null || Unhittable.Run(character).ToB();
            character.SetSuperPause(t, mt, uh);
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

    /// <summary>PosFreeze: 冻结本帧位置（无参 value 默认 true）。引擎物理相据 MChar.PosFreeze 跳过积分。</summary>
    public sealed class PosFreezeController : ParameterOnlyController
    {
        public BytecodeExp Value;

        public override bool Run(MChar character)
        {
            character.PosFreeze = Value == null || Value.Run(character).ToB();
            return false;
        }
    }

    /// <summary>Width: 设角色推挤宽度(player)与边界宽度(edge)，前/后。value 同时设两者。每帧重新断言。</summary>
    public sealed class WidthController : ParameterOnlyController
    {
        public BytecodeExp[] Value;
        public BytecodeExp[] Player;
        public BytecodeExp[] Edge;

        public override bool Run(MChar character)
        {
            if (Value != null && Value.Length >= 1)
            {
                FFloat front = Value[0].Run(character).ToF();
                FFloat back = Value.Length > 1 ? Value[1].Run(character).ToF() : front;
                character.WidthPlayerFront = front;
                character.WidthPlayerBack = back;
                character.WidthEdgeFront = front;
                character.WidthEdgeBack = back;
            }
            if (Player != null && Player.Length >= 1)
            {
                character.WidthPlayerFront = Player[0].Run(character).ToF();
                character.WidthPlayerBack = Player.Length > 1 ? Player[1].Run(character).ToF() : character.WidthPlayerFront;
            }
            if (Edge != null && Edge.Length >= 1)
            {
                character.WidthEdgeFront = Edge[0].Run(character).ToF();
                character.WidthEdgeBack = Edge.Length > 1 ? Edge[1].Run(character).ToF() : character.WidthEdgeFront;
            }
            return false;
        }
    }

    /// <summary>PlayerPush: 设是否参与角色推挤 + 优先级 + 影响队伍。每帧重新断言。</summary>
    public sealed class PlayerPushController : ParameterOnlyController
    {
        public BytecodeExp Value;
        public BytecodeExp Priority;
        public BytecodeExp AffectTeam;

        public override bool Run(MChar character)
        {
            if (Value != null) { character.PlayerPushEnabled = Value.Run(character).ToB(); }
            if (Priority != null) { character.PushPriority = Priority.Run(character).ToI(); }
            if (AffectTeam != null) { character.PushAffectTeam = AffectTeam.Run(character).ToI(); }
            return false;
        }
    }

    /// <summary>ScreenBound: 设屏幕/舞台边界约束 + 相机跟随。每帧重新断言。</summary>
    public sealed class ScreenBoundController : ParameterOnlyController
    {
        public BytecodeExp Value;
        public BytecodeExp[] MoveCamera;
        public BytecodeExp StageBound;

        public override bool Run(MChar character)
        {
            if (Value != null) { character.ScreenBoundEnabled = Value.Run(character).ToB(); }
            if (MoveCamera != null && MoveCamera.Length >= 1)
            {
                character.ScreenBoundMoveCameraX = MoveCamera[0].Run(character).ToB();
                character.ScreenBoundMoveCameraY = MoveCamera.Length > 1 && MoveCamera[1].Run(character).ToB();
            }
            if (StageBound != null) { character.ScreenBoundStageBound = StageBound.Run(character).ToB(); }
            return false;
        }
    }

    /// <summary>AttackDist: writes the attack guard distance used by enemy inguarddist checks.</summary>
    public sealed class AttackDistController : ParameterOnlyController
    {
        public BytecodeExp[] XValues;
        public BytecodeExp[] YValues;
        public BytecodeExp[] ZValues;

        public override bool Run(MChar character)
        {
            if (XValues != null && XValues.Length > 0)
            {
                character.AttackDistX = XValues[0].Run(character).ToF();
            }
            return false;
        }
    }

    /// <summary>HitOverride: 写 MChar.HitOverrides[slot]（移植 char.go HitOverride，默认 time=1）。命中系统据此改受击态。</summary>
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

        public override bool Run(MChar character)
        {
            int slot = Slot != null ? Slot.Run(character).ToI() : 0;
            if (slot < 0 || slot >= character.HitOverrides.Length)
            {
                return false;
            }
            character.HitOverrides[slot] = new MHitOverride
            {
                Attr = Attr,
                StateNo = StateNo != null ? StateNo.Run(character).ToI() : -1,
                Time = Time != null ? Time.Run(character).ToI() : 1,
                ForceAir = ForceAir != null && ForceAir.Run(character).ToB(),
                ForceGuard = ForceGuard != null && ForceGuard.Run(character).ToB(),
                KeepState = KeepState != null && KeepState.Run(character).ToB(),
            };
            return false;
        }
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

    /// <summary>ReversalDef: installs an active reversal definition consumed by hit detection.</summary>
    public sealed class ReversalDefController : ParameterOnlyController
    {
        public int Attr;
        public int GuardFlag;
        public int GuardFlagNot;
        public MHitDef Template;
        public PalFXParamSet PalFX = new PalFXParamSet();

        public override bool Run(MChar character)
        {
            character.ReversalDef = new MReversalDefRuntime
            {
                Active = true,
                Attr = Attr,
                GuardFlag = GuardFlag,
                GuardFlagNot = GuardFlagNot,
                Template = Template != null ? Template.Clone() : new MHitDef(),
            };
            if (character.ReversalDef.Template != null)
            {
                character.ReversalDef.Template.Active = true;
            }
            return false;
        }
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
    public sealed class AfterImageController : ParameterOnlyController
    {
        public AfterImageParamSet AfterImage = new AfterImageParamSet();

        public override bool Run(MChar character)
        {
            PresentationEvents.EmitAfterImage(character, MVisualEventType.AfterImage, AfterImage);
            return false;
        }
    }

    public sealed class AfterImageTimeController : ParameterOnlyController
    {
        public BytecodeExp Time;

        public override bool Run(MChar character)
        {
            if (character.World != null)
            {
                character.World.Events.Visuals.Add(new MVisualEvent
                {
                    Type = MVisualEventType.AfterImageTime,
                    OwnerId = character.Id,
                    Time = Time != null ? Time.Run(character).ToI() : 1,
                });
            }
            return false;
        }
    }

    public sealed class PalFXController : ParameterOnlyController
    {
        public PalFXParamSet PalFX = new PalFXParamSet();

        public override bool Run(MChar character)
        {
            PresentationEvents.EmitPalFX(character, MVisualEventType.PalFX, PalFX, -1, -1);
            return false;
        }
    }

    public sealed class AllPalFXController : ParameterOnlyController
    {
        public PalFXParamSet PalFX = new PalFXParamSet();

        public override bool Run(MChar character)
        {
            PresentationEvents.EmitPalFX(character, MVisualEventType.AllPalFX, PalFX, -1, -1);
            return false;
        }
    }

    public sealed class BGPalFXController : ParameterOnlyController
    {
        public BytecodeExp Id;
        public BytecodeExp Index;
        public PalFXParamSet PalFX = new PalFXParamSet();

        public override bool Run(MChar character)
        {
            int id = Id != null ? Id.Run(character).ToI() : -1;
            int index = Index != null ? Index.Run(character).ToI() : -1;
            PresentationEvents.EmitPalFX(character, MVisualEventType.BGPalFX, PalFX, id, index);
            return false;
        }
    }
    public sealed class EnvColorController : ParameterOnlyController { public BytecodeExp[] Value; public BytecodeExp Time; public BytecodeExp Under; }

    static class PresentationEvents
    {
        public static void EmitPalFX(MChar character, MVisualEventType type, PalFXParamSet palFX, int id, int index)
        {
            if (character.World == null || palFX == null)
            {
                return;
            }
            character.World.Events.Visuals.Add(new MVisualEvent
            {
                Type = type,
                OwnerId = character.Id,
                Time = palFX.Time != null ? palFX.Time.Run(character).ToI() : 1,
                Id = id,
                Index = index,
                Value0 = palFX.Color != null ? palFX.Color.Run(character).ToI() : 256,
                Value1 = VectorInt(character, palFX.Add, 0),
                Value2 = VectorInt(character, palFX.Add, 1),
                Value3 = VectorInt(character, palFX.Add, 2),
                ScaleX = VectorFixed(character, palFX.Mul, 0, FFloat.One),
                ScaleY = VectorFixed(character, palFX.Mul, 1, FFloat.One),
                X = palFX.Hue != null ? palFX.Hue.Run(character).ToF() : FFloat.Zero,
            });
        }

        public static void EmitAfterImage(MChar character, MVisualEventType type, AfterImageParamSet afterImage)
        {
            if (character.World == null || afterImage == null)
            {
                return;
            }
            character.World.Events.Visuals.Add(new MVisualEvent
            {
                Type = type,
                OwnerId = character.Id,
                Time = afterImage.Time != null ? afterImage.Time.Run(character).ToI() : 1,
                Value0 = afterImage.Length != null ? afterImage.Length.Run(character).ToI() : 20,
                Value1 = afterImage.TimeGap != null ? afterImage.TimeGap.Run(character).ToI() : 1,
                Value2 = afterImage.FrameGap != null ? afterImage.FrameGap.Run(character).ToI() : 4,
                Value3 = afterImage.PalColor != null ? afterImage.PalColor.Run(character).ToI() : 256,
                X = afterImage.PalHue != null ? afterImage.PalHue.Run(character).ToF() : FFloat.Zero,
            });
        }

        public static FFloat VectorValue(MChar character, BytecodeExp[] values, int index)
        {
            return VectorFixed(character, values, index, FFloat.Zero);
        }

        static int VectorInt(MChar character, BytecodeExp[] values, int index)
        {
            return values != null && values.Length > index && values[index] != null
                ? values[index].Run(character).ToI()
                : 0;
        }

        static FFloat VectorFixed(MChar character, BytecodeExp[] values, int index, FFloat fallback)
        {
            return values != null && values.Length > index && values[index] != null
                ? values[index].Run(character).ToF()
                : fallback;
        }
    }

    /// <summary>PlaySnd emits a deterministic presentation event. The logic layer never owns an audio device.</summary>
    public sealed class PlaySndController : ParameterOnlyController
    {
        public bool CommonBank;
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

        public override bool Run(MChar character)
        {
            if (character.World == null || Value == null || Value.Length == 0) { return false; }
            MSoundEvent sound = new MSoundEvent
            {
                Type = MSoundEventType.Play,
                OwnerId = character.Id,
                CommonBank = CommonBank,
                Group = Value[0].Run(character).ToI(),
                Number = Value.Length > 1 ? Value[1].Run(character).ToI() : 0,
                Channel = Channel != null ? Channel.Run(character).ToI() : -1,
                VolumeScale = VolumeScale != null ? VolumeScale.Run(character).ToI() : 100,
                Pan = Pan != null ? Pan.Run(character).ToF() :
                    (AbsPan != null ? AbsPan.Run(character).ToF() : FFloat.Zero),
                AbsolutePan = AbsPan != null,
                Frequency = FreqMul != null ? FreqMul.Run(character).ToF() : FFloat.One,
                LoopCount = LoopCount != null ? LoopCount.Run(character).ToI() :
                    (Loop != null && Loop.Run(character).ToB() ? -1 : 0),
                Priority = Priority != null ? Priority.Run(character).ToI() : 0,
                LoopStart = LoopStart != null ? LoopStart.Run(character).ToI() : 0,
                LoopEnd = LoopEnd != null ? LoopEnd.Run(character).ToI() : 0,
                StartPosition = StartPosition != null ? StartPosition.Run(character).ToI() : 0,
                LowPriority = LowPriority != null && LowPriority.Run(character).ToB(),
                StopOnGetHit = StopOnGetHit != null && StopOnGetHit.Run(character).ToB(),
                StopOnChangeState = StopOnChangeState != null && StopOnChangeState.Run(character).ToB(),
            };
            if (Volume != null)
            {
                sound.VolumeScale += (Volume.Run(character).ToI() * 25) / 128;
            }
            character.World.Events.Sounds.Add(sound);
            return false;
        }
    }

    public sealed class StopSndController : ParameterOnlyController
    {
        public BytecodeExp Channel;
        public override bool Run(MChar character)
        {
            if (character.World != null)
            {
                character.World.Events.Sounds.Add(new MSoundEvent
                {
                    Type = MSoundEventType.Stop,
                    OwnerId = character.Id,
                    Channel = Channel != null ? Channel.Run(character).ToI() : -1,
                });
            }
            return false;
        }
    }

    public sealed class SndPanController : ParameterOnlyController
    {
        public BytecodeExp Channel;
        public BytecodeExp Pan;
        public BytecodeExp AbsPan;
        public override bool Run(MChar character)
        {
            if (character.World != null)
            {
                character.World.Events.Sounds.Add(new MSoundEvent
                {
                    Type = MSoundEventType.SetPan,
                    OwnerId = character.Id,
                    Channel = Channel != null ? Channel.Run(character).ToI() : -1,
                    Pan = Pan != null ? Pan.Run(character).ToF() :
                        (AbsPan != null ? AbsPan.Run(character).ToF() : FFloat.Zero),
                    AbsolutePan = AbsPan != null,
                });
            }
            return false;
        }
    }

    /// <summary>Explod: creates a deterministic logic-layer explod record; rendering is handled later.</summary>
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

        public override bool Run(MChar character)
        {
            if (character.World == null)
            {
                return false;
            }
            MExplod explod = new MExplod
            {
                Id = character.World.AllocId(),
                OwnerId = character.Id,
            };
            ApplyToExplod(character, explod);
            character.World.AddExplod(explod);
            return false;
        }

        protected void ApplyToExplod(MChar character, MExplod explod)
        {
            if (Id != null) { explod.ExplodId = Id.Run(character).ToI(); }
            if (Anim != null && Anim.Length > 0) { explod.AnimNo = Anim[Anim.Length - 1].Run(character).ToI(); }
            if (Position != null && Position.Length > 0)
            {
                FFloat x = Position[0].Run(character).ToF();
                FFloat y = Position.Length > 1 ? Position[1].Run(character).ToF() : explod.Pos.Y;
                FFloat z = Position.Length > 2 ? Position[2].Run(character).ToF() : explod.Pos.Z;
                explod.Pos = new FVector3(x, y, z);
            }
            if (Velocity != null && Velocity.Length > 0)
            {
                FFloat x = Velocity[0].Run(character).ToF();
                FFloat y = Velocity.Length > 1 ? Velocity[1].Run(character).ToF() : explod.Vel.Y;
                FFloat z = Velocity.Length > 2 ? Velocity[2].Run(character).ToF() : explod.Vel.Z;
                explod.Vel = new FVector3(x, y, z);
            }
            if (Accel != null && Accel.Length > 0)
            {
                FFloat x = Accel[0].Run(character).ToF();
                FFloat y = Accel.Length > 1 ? Accel[1].Run(character).ToF() : explod.Accel.Y;
                FFloat z = Accel.Length > 2 ? Accel[2].Run(character).ToF() : explod.Accel.Z;
                explod.Accel = new FVector3(x, y, z);
            }
            if (Scale != null && Scale.Length > 0)
            {
                explod.ScaleX = Scale[0].Run(character).ToF();
                explod.ScaleY = Scale.Length > 1 ? Scale[1].Run(character).ToF() : explod.ScaleX;
            }
            if (Facing != null) { explod.Facing = Facing.Run(character).ToI() < 0 ? -1 : 1; }
            if (VFacing != null) { explod.VFacing = VFacing.Run(character).ToI() < 0 ? -1 : 1; }
            if (BindTime != null) { explod.BindTime = BindTime.Run(character).ToI(); }
            if (RemoveTime != null) { explod.RemoveTime = RemoveTime.Run(character).ToI(); }
            if (SprPriority != null) { explod.SprPriority = SprPriority.Run(character).ToI(); }
            if (OwnPal != null) { explod.OwnPal = OwnPal.Run(character).ToB(); }
            if (RemoveOnGetHit != null) { explod.RemoveOnGetHit = RemoveOnGetHit.Run(character).ToB(); }
            if (RemoveOnChangeState != null) { explod.RemoveOnChangeState = RemoveOnChangeState.Run(character).ToB(); }
        }
    }

    public sealed class ModifyExplodController : ExplodController
    {
        public BytecodeExp Index;

        public override bool Run(MChar character)
        {
            if (character.World == null)
            {
                return false;
            }
            int explodId = Id != null ? Id.Run(character).ToI() : -1;
            int matchIndex = Index != null ? Index.Run(character).ToI() : -1;
            List<MExplod> explods = character.World.FindExplods(explodId, character.Id, matchIndex);
            for (int index = 0; index < explods.Count; index++)
            {
                ApplyToExplod(character, explods[index]);
            }
            return false;
        }
    }

    public sealed class RemoveExplodController : ParameterOnlyController
    {
        public BytecodeExp Id;
        public BytecodeExp Index;

        public override bool Run(MChar character)
        {
            if (character.World == null)
            {
                return false;
            }
            int explodId = Id != null ? Id.Run(character).ToI() : -1;
            int matchIndex = Index != null ? Index.Run(character).ToI() : -1;
            character.World.RemoveExplods(explodId, character.Id, matchIndex);
            return false;
        }
    }
    public sealed class MakeDustController : ParameterOnlyController
    {
        public BytecodeExp Spacing;
        public BytecodeExp[] Position;
        public BytecodeExp[] Position2;

        public override bool Run(MChar character)
        {
            if (character.World != null)
            {
                character.World.Events.Visuals.Add(new MVisualEvent
                {
                    Type = MVisualEventType.MakeDust,
                    OwnerId = character.Id,
                    Value0 = Spacing != null ? Spacing.Run(character).ToI() : 1,
                    X = PresentationEvents.VectorValue(character, Position, 0),
                    Y = PresentationEvents.VectorValue(character, Position, 1),
                    Z = PresentationEvents.VectorValue(character, Position2, 0),
                });
            }
            return false;
        }
    }

    public sealed class GameMakeAnimController : ParameterOnlyController
    {
        public BytecodeExp[] Value;
        public BytecodeExp[] Position;
        public BytecodeExp Under;

        public override bool Run(MChar character)
        {
            if (character.World != null)
            {
                character.World.Events.Visuals.Add(new MVisualEvent
                {
                    Type = MVisualEventType.GameMakeAnim,
                    OwnerId = character.Id,
                    Value0 = Value != null && Value.Length > 0 ? Value[0].Run(character).ToI() : 0,
                    Value1 = Value != null && Value.Length > 1 ? Value[1].Run(character).ToI() : 0,
                    X = PresentationEvents.VectorValue(character, Position, 0),
                    Y = PresentationEvents.VectorValue(character, Position, 1),
                    Under = Under != null && Under.Run(character).ToB(),
                });
            }
            return false;
        }
    }
    public sealed class EnvShakeController : ParameterOnlyController
    {
        public BytecodeExp Time;
        public BytecodeExp Amplitude;
        public BytecodeExp Frequency;
        public BytecodeExp Multiplier;
        public BytecodeExp Phase;
        public BytecodeExp Direction;

        public override bool Run(MChar character)
        {
            if (character.World != null)
            {
                character.World.Events.Visuals.Add(new MVisualEvent
                {
                    Type = MVisualEventType.EnvShake,
                    OwnerId = character.Id,
                    Time = Time != null ? Time.Run(character).ToI() : 0,
                    Value0 = Amplitude != null ? Amplitude.Run(character).ToI() : 0,
                    Value1 = Frequency != null ? Frequency.Run(character).ToI() : 0,
                    Value2 = Multiplier != null ? Multiplier.Run(character).ToI() : 1,
                    Value3 = Phase != null ? Phase.Run(character).ToI() : 0,
                    X = Direction != null ? Direction.Run(character).ToF() : FFloat.Zero,
                });
            }
            return false;
        }
    }
    public sealed class FallEnvShakeController : ParameterOnlyController
    {
        public override bool Run(MChar character)
        {
            if (character.World != null)
            {
                character.World.Events.Visuals.Add(new MVisualEvent
                {
                    Type = MVisualEventType.FallEnvShake,
                    OwnerId = character.Id,
                    Time = character.Ghv != null ? character.Ghv.FallRecoverTime : 0,
                });
            }
            return false;
        }
    }
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
