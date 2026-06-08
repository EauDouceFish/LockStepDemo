using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle
{
    public interface IMTraceKeyed
    {
        string TraceKey { get; }
    }

    public sealed class MBattleTrace
    {
        public MBattleTraceHeader Header = new MBattleTraceHeader();
        public readonly List<MBattleTraceFrame> Frames = new List<MBattleTraceFrame>();
        public MBattleTraceEnd End = new MBattleTraceEnd();
    }

    public sealed class MBattleTraceHeader
    {
        public string Type = "header";
        public string Schema = "mugen-oracle-trace/v1";
        public string Level = "core";
        public MTraceProducer Producer = new MTraceProducer();
        public MTraceScenario Scenario = new MTraceScenario();
        public readonly SortedDictionary<string, string> ContentSha256 =
            new SortedDictionary<string, string>(StringComparer.Ordinal);
        public MTraceNumeric Numeric = new MTraceNumeric();
    }

    public sealed class MTraceProducer
    {
        public string Engine = "lockstep-csharp";
        public string Commit = "";
        public string Patch = "oracle-v1";
    }

    public sealed class MTraceScenario
    {
        public string Id = "";
        public string InputSha256 = "";
        public int Seed = 1;
        public int MaxSteps;
        public string StartPolicy = "natural_match";
    }

    public sealed class MTraceNumeric
    {
        public string CanonicalSpace = "world320";
        public string Quantization = "q20-round-even";
    }

    public sealed class MBattleTraceEnd
    {
        public string Type = "end";
        public int Steps;
        public bool Completed;
        public string Reason = "";
        public string SemanticSha256 = "";
    }

    public sealed class MBattleTraceFrame : IMTraceKeyed
    {
        public string Type = "frame";
        public int Step;
        public string Phase = "post-combat-tick";
        public readonly List<MTraceInput> Inputs = new List<MTraceInput>();
        public MTraceGlobal Global = new MTraceGlobal();
        public readonly List<MEntityTrace> Players = new List<MEntityTrace>();
        public readonly List<MEntityTrace> Helpers = new List<MEntityTrace>();
        public readonly List<MProjectileTrace> Projectiles = new List<MProjectileTrace>();
        public readonly List<MExplodTrace> Explods = new List<MExplodTrace>();
        public readonly List<MEventTrace> Events = new List<MEventTrace>();
        public MTraceHashes Hashes = new MTraceHashes();

        public string TraceKey => "step:" + Step.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class MTraceInput : IMTraceKeyed
    {
        public int Slot;
        public int Bits;
        public int[] Analog = new int[6];

        public string TraceKey => "slot:" + Slot.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class MTraceGlobal
    {
        public int NativeFrameNo;
        public int RngSeed;
        public int Pause;
        public int SuperPause;
        public int PauseBuffer;
        public int SuperPauseBuffer;
        public string PauseOwner = null;
        public string SuperPauseOwner = null;
        public int RoundState;
        public int NextEntityId;
    }

    public sealed class MEntityTrace : IMTraceKeyed
    {
        public string Key = "";
        public string Kind = "player";
        public string Owner = "";
        public int CreationOrdinal;
        public int HelperId;
        public MTraceState State = new MTraceState();
        public MTraceAnimation Animation = new MTraceAnimation();
        public MTraceTransform Transform = new MTraceTransform();
        public MTraceVitals Vitals = new MTraceVitals();
        public MTraceCombat Combat = new MTraceCombat();
        public MTraceGetHit GetHit = new MTraceGetHit();
        public MTracePause Pause = new MTracePause();
        public MTraceBind Bind = new MTraceBind();
        public MTraceLinks Links = new MTraceLinks();
        public readonly SortedDictionary<int, int> IntVars = new SortedDictionary<int, int>();
        public readonly SortedDictionary<int, long> FloatVarsQ20 = new SortedDictionary<int, long>();
        public readonly SortedDictionary<int, int> PersistentCounters = new SortedDictionary<int, int>();
        public readonly List<MTraceHitOverride> HitOverrides = new List<MTraceHitOverride>();
        public readonly List<MTraceJugglePoint> JugglePoints = new List<MTraceJugglePoint>();
        public MTraceHashes Hashes = new MTraceHashes();

        public string TraceKey => Key;
    }

    public sealed class MTraceState
    {
        public int No;
        public int Previous;
        public int Time;
        public int Type;
        public int PreviousType;
        public int MoveType;
        public int Physics;
        public bool Ctrl;
        public int PendingNo;
        public bool PendingIsSelf;
    }

    public sealed class MTraceAnimation
    {
        public int No;
        public int Previous;
        public int RunningNo;
        public int Element;
        public int ElementIndex;
        public int ElementTime;
        public int CurrentTime;
        public int Remaining;
        public bool LoopEnd;
        public string AnimationOwner = "";
        public string SpriteOwner = "";
    }

    public sealed class MTraceTransform
    {
        public int[] LocalCoord = new[] { 320, 240 };
        public bool UnsupportedNumericSpace;
        public long[] PositionLocalQ20 = new long[3];
        public long[] PositionWorldQ20 = new long[3];
        public long[] PreviousPositionWorldQ20 = new long[3];
        public long[] VelocityLocalQ20 = new long[3];
        public long[] VelocityWorldQ20 = new long[3];
        public int Facing;
    }

    public sealed class MTraceVitals
    {
        public int Life;
        public int LifeMax;
        public int Power;
        public int PowerMax;
        public int Juggle;
        public int FallTime;
        public int PalNo;
    }

    public sealed class MTraceCombat
    {
        public int Hitstop;
        public int PendingLifeDamage;
        public int HitCount;
        public int UniqueHitCount;
        public int GuardCount;
        public int ReceivedHits;
        public int MoveContact;
        public int MoveHit;
        public int MoveGuarded;
        public int MoveReversed;
        public int MoveContactTime;
        public bool CounterHit;
        public bool Guarding;
        public int HitByAttribute;
        public int HitByTime;
        public bool HitByIsNot;
        public int ProjectileContactId;
        public int ProjectileContactType;
        public int ProjectileContactTime;
        public long AttackMultiplierQ20;
        public long DefenseMultiplierQ20;
        public long SuperDefenseMultiplierQ20;
        public long FallDefenseMultiplierQ20;
        public bool DefenseMultiplierDelayed;
    }

    public sealed class MTraceGetHit
    {
        public long VelocityXQ20;
        public long VelocityYQ20;
        public long VelocityZQ20;
        public int HitShakeTime;
        public int HitTime;
        public int SlideTime;
        public int CtrlTime;
        public int Damage;
        public int HitCount;
        public int FallCount;
        public int AnimType;
        public int AttributeType;
        public int GroundType;
        public int AirType;
        public int GroundAnimType;
        public int AirAnimType;
        public int FallAnimType;
        public bool Fall;
        public bool Guarded;
        public bool Up;
        public bool ForceStand;
        public bool Kill;
        public long YAccelerationQ20;
        public long FallVelocityXQ20;
        public long FallVelocityYQ20;
        public bool FallRecover;
        public int FallRecoverTime;
        public int DownRecoverTime;
        public int FallDamage;
        public bool FallKill;
    }

    public sealed class MTracePause
    {
        public int PauseMoveTime;
        public int SuperMoveTime;
        public bool Frozen;
        public int ActionTemporary;
        public int UnhittableTime;
        public bool PositionFrozen;
    }

    public sealed class MTraceBind
    {
        public string Target = null;
        public int Time;
        public long[] PositionWorldQ20 = new long[3];
        public int Facing;
    }

    public sealed class MTraceLinks
    {
        public string P2 = null;
        public string Root = null;
        public string Parent = null;
        public string Partner = null;
        public string StateOwner = null;
        public readonly SortedDictionary<string, bool> Targets =
            new SortedDictionary<string, bool>(StringComparer.Ordinal);
    }

    public sealed class MTraceHitOverride : IMTraceKeyed
    {
        public int Slot;
        public int Attribute;
        public int StateNo;
        public int Time;
        public bool ForceAir;
        public bool KeepState;

        public string TraceKey => "slot:" + Slot.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class MTraceJugglePoint : IMTraceKeyed
    {
        public string Attacker = "";
        public int Points;

        public string TraceKey => Attacker;
    }

    public sealed class MProjectileTrace : IMTraceKeyed
    {
        public string Key = "";
        public string Owner = "";
        public int CreationOrdinal;
        public int ProjectileId;
        public int AnimationNo;
        public int RemoveTime;
        public int Time;
        public bool Removed;
        public int HitCount;
        public int ContactCount;
        public bool HitDone;
        public long[] PositionWorldQ20 = new long[3];
        public long[] VelocityWorldQ20 = new long[3];
        public long[] AccelerationWorldQ20 = new long[3];
        public long FacingQ20;

        public string TraceKey => Key;
    }

    public sealed class MExplodTrace : IMTraceKeyed
    {
        public string Key = "";
        public string Owner = "";
        public int CreationOrdinal;
        public int ExplodId;
        public int AnimationNo;
        public long[] PositionWorldQ20 = new long[3];
        public long[] VelocityWorldQ20 = new long[3];
        public long[] AccelerationWorldQ20 = new long[3];
        public long ScaleXQ20;
        public long ScaleYQ20;
        public int Facing;
        public int VerticalFacing;
        public int BindTime;
        public int RemoveTime;
        public int SpritePriority;
        public bool OwnPalette;
        public bool RemoveOnGetHit;
        public bool RemoveOnChangeState;

        public string TraceKey => Key;
    }

    public sealed class MEventTrace : IMTraceKeyed
    {
        public string Key = "";
        public string Kind = "sound";
        public string Owner = "";
        public string Action = "";
        public bool CommonBank;
        public int Group;
        public int Number;
        public int Channel;
        public int VolumeScale;
        public long PanQ20;
        public bool AbsolutePan;
        public long FrequencyQ20;
        public int LoopCount;
        public int Priority;
        public int LoopStart;
        public int LoopEnd;
        public int StartPosition;
        public bool LowPriority;
        public bool StopOnGetHit;
        public bool StopOnChangeState;

        public string TraceKey => Key;
    }

    public sealed class MTraceHashes
    {
        public string Native = "";
        public string Semantic = "";
        public string Global = "";
        public string Entities = "";
        public string State = "";
        public string Variables = "";
        public string Commands = "";
    }

    /// <summary>
    /// Stateful recorder: creation ordinals survive list reordering and removal, while raw runtime ids stay out
    /// of the cross-engine schema. One recorder instance must be used for the full trace.
    /// </summary>
    public sealed class MBattleTraceRecorder
    {
        readonly Dictionary<MChar, string> _entityKeys = new Dictionary<MChar, string>();
        readonly Dictionary<MProjectile, string> _projectileKeys = new Dictionary<MProjectile, string>();
        readonly Dictionary<MExplod, string> _explodKeys = new Dictionary<MExplod, string>();
        readonly Dictionary<string, int> _nextOrdinal = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Dictionary<int, string> _runtimeIdKeys = new Dictionary<int, string>();

        // Project-specific: emits C# oracle-trace metadata; Ikemen counterpart is the external headless trace producer.
        public static MBattleTraceHeader CreateHeader(MTraceScenario scenario, string commit = "")
        {
            return new MBattleTraceHeader
            {
                Producer = new MTraceProducer { Engine = "lockstep-csharp", Commit = commit, Patch = "oracle-v1" },
                Scenario = scenario ?? new MTraceScenario(),
            };
        }

        // Project-specific: captures post-tick C# state for Ikemen diffing; fields mirror src/char.go Char/System runtime state.
        public MBattleTraceFrame CapturePostTick(
            MBattleEngine engine,
            int step,
            IReadOnlyList<MInput> consumedInputs)
        {
            if (engine == null) { throw new ArgumentNullException(nameof(engine)); }
            PrepareEntityKeys(engine);

            MBattleTraceFrame frame = new MBattleTraceFrame
            {
                Step = step,
                Global = CaptureGlobal(engine),
                Hashes = new MTraceHashes { Native = engine.ComputeHash().ToString("x16", CultureInfo.InvariantCulture) },
            };

            int inputCount = System.Math.Max(engine.Chars.Count, consumedInputs != null ? consumedInputs.Count : 0);
            for (int i = 0; i < inputCount; i++)
            {
                MInput input = consumedInputs != null && i < consumedInputs.Count ? consumedInputs[i] : MInput.None;
                frame.Inputs.Add(new MTraceInput { Slot = i, Bits = (int)input });
            }

            for (int i = 0; i < engine.Chars.Count; i++)
            {
                MCharData data = i < engine.Data.Count ? engine.Data[i] : null;
                frame.Players.Add(CaptureEntity(engine.Chars[i], data, false, i));
            }
            for (int i = 0; i < engine.Helpers.Count; i++)
            {
                MChar helper = engine.Helpers[i];
                frame.Helpers.Add(CaptureEntity(helper, DataForOwner(engine, helper), true, CreationOrdinal(_entityKeys[helper])));
            }
            for (int i = 0; i < engine.World.Projectiles.Count; i++)
            {
                frame.Projectiles.Add(CaptureProjectile(engine.World.Projectiles[i], DataForOwner(engine, engine.World.Projectiles[i].Owner)));
            }
            for (int i = 0; i < engine.World.Explods.Count; i++)
            {
                frame.Explods.Add(CaptureExplod(engine.World.Explods[i], DataForOwnerId(engine, engine.World.Explods[i].OwnerId)));
            }
            CaptureEvents(engine, frame.Events);

            Sort(frame.Players);
            Sort(frame.Helpers);
            Sort(frame.Projectiles);
            Sort(frame.Explods);
            Sort(frame.Events);
            return frame;
        }

        // Project-specific: records global System-like state (pause/superpause/rng/roundstate) for Ikemen trace comparison.
        MTraceGlobal CaptureGlobal(MBattleEngine engine)
        {
            MPauseState pause = engine.PauseState;
            return new MTraceGlobal
            {
                NativeFrameNo = engine.FrameNo,
                RngSeed = engine.Random.Seed,
                Pause = pause.PauseTime,
                SuperPause = pause.SuperTime,
                PauseBuffer = pause.PauseTimeBuffer,
                SuperPauseBuffer = pause.SuperTimeBuffer,
                PauseOwner = KeyByRuntimeId(pause.PausePlayerNo),
                SuperPauseOwner = KeyByRuntimeId(pause.SuperPlayerNo),
                RoundState = engine.Chars.Count > 0 ? engine.Chars[0].RoundState : 0,
                NextEntityId = engine.World.NextEntityId,
            };
        }

        // Project-specific: assigns stable trace keys for players/helpers/projectiles/explods independent of raw runtime ids.
        void PrepareEntityKeys(MBattleEngine engine)
        {
            _runtimeIdKeys.Clear();
            for (int i = 0; i < engine.Chars.Count; i++)
            {
                MChar player = engine.Chars[i];
                string key = "p" + i.ToString(CultureInfo.InvariantCulture);
                _entityKeys[player] = key;
                _runtimeIdKeys[player.Id] = key;
            }
            for (int i = 0; i < engine.Helpers.Count; i++)
            {
                MChar helper = engine.Helpers[i];
                if (!_entityKeys.ContainsKey(helper))
                {
                    string owner = RootPlayerKey(helper);
                    int ordinal = AllocateOrdinal(owner + "/h");
                    _entityKeys[helper] = owner + "/h" + ordinal.ToString(CultureInfo.InvariantCulture);
                }
                _runtimeIdKeys[helper.Id] = _entityKeys[helper];
            }
            for (int i = 0; i < engine.World.Projectiles.Count; i++)
            {
                MProjectile projectile = engine.World.Projectiles[i];
                if (!_projectileKeys.ContainsKey(projectile))
                {
                    string owner = RootPlayerKey(projectile.Owner);
                    int ordinal = AllocateOrdinal(owner + "/proj");
                    _projectileKeys[projectile] = owner + "/proj" + ordinal.ToString(CultureInfo.InvariantCulture);
                }
            }
            for (int i = 0; i < engine.World.Explods.Count; i++)
            {
                MExplod explod = engine.World.Explods[i];
                if (!_explodKeys.ContainsKey(explod))
                {
                    string owner = RootPlayerKeyById(explod.OwnerId);
                    int ordinal = AllocateOrdinal(owner + "/explod");
                    _explodKeys[explod] = owner + "/explod" + ordinal.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        // Project-specific: per-owner creation ordinal used by the trace schema to compare against Ikemen entity ordering.
        int AllocateOrdinal(string counterKey)
        {
            int value;
            _nextOrdinal.TryGetValue(counterKey, out value);
            _nextOrdinal[counterKey] = value + 1;
            return value;
        }

        // Project-specific: reduces helper/custom-state ownership to the root player key used by the trace schema.
        string RootPlayerKey(MChar character)
        {
            if (character == null) { return "unowned"; }
            MChar root = character.Root ?? character.Parent ?? character;
            string key;
            if (_entityKeys.TryGetValue(root, out key))
            {
                int slash = key.IndexOf('/');
                return slash >= 0 ? key.Substring(0, slash) : key;
            }
            return RootPlayerKeyById(root.Id);
        }

        // Project-specific: runtime-id to root-player-key lookup for events that only store owner ids.
        string RootPlayerKeyById(int runtimeId)
        {
            string key;
            if (_runtimeIdKeys.TryGetValue(runtimeId, out key))
            {
                int slash = key.IndexOf('/');
                return slash >= 0 ? key.Substring(0, slash) : key;
            }
            return "unowned";
        }

        string KeyOf(MChar character)
        {
            if (character == null) { return null; }
            string key;
            return _entityKeys.TryGetValue(character, out key) ? key : null;
        }

        MEntityTrace CaptureEntity(MChar character, MCharData data, bool helper, int creationOrdinal)
        {
            int width = data != null && data.Definition != null ? data.Definition.LocalCoordWidth : 320;
            int height = data != null && data.Definition != null ? data.Definition.LocalCoordHeight : 240;
            string key = _entityKeys[character];
            MEntityTrace trace = new MEntityTrace
            {
                Key = key,
                Kind = helper ? "helper" : "player",
                Owner = RootPlayerKey(character),
                CreationOrdinal = creationOrdinal,
                HelperId = helper ? character.HelperType : 0,
                State = new MTraceState
                {
                    No = character.StateNo,
                    Previous = character.PrevStateNo,
                    Time = character.Time,
                    Type = character.StateType,
                    PreviousType = character.PrevStateType,
                    MoveType = character.MoveType,
                    Physics = character.Physics,
                    Ctrl = character.Ctrl,
                    PendingNo = character.PendingStateNo,
                    PendingIsSelf = character.PendingIsSelf,
                },
                Animation = new MTraceAnimation
                {
                    No = character.AnimNo,
                    Previous = character.PrevAnimNo,
                    RunningNo = character.AnimRunningNo,
                    Element = character.AnimElemNo,
                    ElementIndex = character.AnimElem,
                    ElementTime = character.AnimElemTime,
                    CurrentTime = character.AnimCurTime,
                    Remaining = character.AnimTime,
                    LoopEnd = character.AnimLoopEnd,
                    AnimationOwner = KeyOf(character.StateOwner ?? character),
                    SpriteOwner = key,
                },
                Transform = CaptureTransform(character, width, height),
                Vitals = new MTraceVitals
                {
                    Life = character.Life,
                    LifeMax = character.LifeMax,
                    Power = character.Power,
                    PowerMax = character.PowerMax,
                    Juggle = character.Juggle,
                    FallTime = character.FallTime,
                    PalNo = character.PalNo,
                },
                Combat = CaptureCombat(character),
                GetHit = CaptureGetHit(character.Ghv),
                Pause = new MTracePause
                {
                    PauseMoveTime = character.PauseMovetime,
                    SuperMoveTime = character.SuperMovetime,
                    Frozen = character.PauseBool,
                    ActionTemporary = character.Acttmp,
                    UnhittableTime = character.UnhittableTime,
                    PositionFrozen = character.PosFreeze,
                },
                Bind = new MTraceBind
                {
                    Target = KeyOf(character.BindTarget),
                    Time = character.BindTime,
                    PositionWorldQ20 = WorldVector(character.BindPos, width),
                    Facing = character.BindFacing,
                },
                Links = new MTraceLinks
                {
                    P2 = KeyOf(character.P2),
                    Root = KeyOf(character.Root),
                    Parent = KeyOf(character.Parent),
                    Partner = KeyOf(character.Partner),
                    StateOwner = KeyOf(character.StateOwner),
                },
            };

            foreach (KeyValuePair<int, int> pair in character.IntVars) { trace.IntVars[pair.Key] = pair.Value; }
            foreach (KeyValuePair<int, FFloat> pair in character.FloatVars) { trace.FloatVarsQ20[pair.Key] = ToQ20(pair.Value); }
            foreach (KeyValuePair<int, int> pair in character.PersistCounters) { trace.PersistentCounters[pair.Key] = pair.Value; }
            for (int i = 0; i < character.Targets.Count; i++)
            {
                string target = KeyOf(character.Targets[i]);
                if (target != null) { trace.Links.Targets[target] = true; }
            }
            for (int i = 0; i < character.HitOverrides.Length; i++)
            {
                MHitOverride value = character.HitOverrides[i];
                trace.HitOverrides.Add(new MTraceHitOverride
                {
                    Slot = i, Attribute = value.Attr, StateNo = value.StateNo, Time = value.Time,
                    ForceAir = value.ForceAir, KeepState = value.KeepState,
                });
            }
            for (int i = 0; i < character.Ghv.JugglePoints.Count; i++)
            {
                MGetHitVar.MJugglePoint value = character.Ghv.JugglePoints[i];
                string attacker = KeyByRuntimeId(value.PlayerId) ?? "runtime:" + value.PlayerId.ToString(CultureInfo.InvariantCulture);
                trace.JugglePoints.Add(new MTraceJugglePoint { Attacker = attacker, Points = value.Points });
            }
            Sort(trace.HitOverrides);
            Sort(trace.JugglePoints);
            return trace;
        }

        string KeyByRuntimeId(int id)
        {
            string key;
            return _runtimeIdKeys.TryGetValue(id, out key) ? key : null;
        }

        static MTraceTransform CaptureTransform(MChar character, int width, int height)
        {
            return new MTraceTransform
            {
                LocalCoord = new[] { width, height },
                UnsupportedNumericSpace = width != 320 || height != 240,
                PositionLocalQ20 = LocalVector(character.Pos),
                PositionWorldQ20 = WorldVector(character.Pos, width),
                PreviousPositionWorldQ20 = WorldVector(character.OldPos, width),
                VelocityLocalQ20 = LocalVector(character.Vel),
                VelocityWorldQ20 = WorldVector(character.Vel, width),
                Facing = character.Facing.Raw >= 0 ? 1 : -1,
            };
        }

        static MTraceCombat CaptureCombat(MChar character)
        {
            return new MTraceCombat
            {
                Hitstop = character.Hitstop,
                PendingLifeDamage = character.PendingLifeDamage,
                HitCount = character.HitCount,
                UniqueHitCount = character.UniqHitCount,
                GuardCount = character.GuardCount,
                ReceivedHits = character.ReceivedHits,
                MoveContact = character.MoveContact,
                MoveHit = character.MoveHit,
                MoveGuarded = character.MoveGuarded,
                MoveReversed = character.MoveReversed,
                MoveContactTime = character.MoveContactTime,
                CounterHit = character.CounterHit,
                Guarding = character.Guarding,
                HitByAttribute = character.HitByAttr,
                HitByTime = character.HitByTime,
                HitByIsNot = character.HitByIsNot,
                ProjectileContactId = character.ProjectileContactId,
                ProjectileContactType = character.ProjectileContactType,
                ProjectileContactTime = character.ProjectileContactTime,
                AttackMultiplierQ20 = ToQ20(character.AttackMul),
                DefenseMultiplierQ20 = ToQ20(character.CustomDefense),
                SuperDefenseMultiplierQ20 = ToQ20(character.SuperDefenseMul),
                FallDefenseMultiplierQ20 = ToQ20(character.FallDefenseMul),
                DefenseMultiplierDelayed = character.DefenseMulDelay,
            };
        }

        static MTraceGetHit CaptureGetHit(MGetHitVar value)
        {
            return new MTraceGetHit
            {
                VelocityXQ20 = ToQ20(value.XVel), VelocityYQ20 = ToQ20(value.YVel), VelocityZQ20 = ToQ20(value.ZVel),
                HitShakeTime = value.HitShakeTime, HitTime = value.HitTime, SlideTime = value.SlideTime,
                CtrlTime = value.CtrlTime, Damage = value.Damage, HitCount = value.HitCount, FallCount = value.FallCount,
                AnimType = value.AnimType, AttributeType = value.AttrType, GroundType = value.GroundType,
                AirType = value.AirType, GroundAnimType = value.GroundAnimType, AirAnimType = value.AirAnimType,
                FallAnimType = value.FallAnimType, Fall = value.Fall, Guarded = value.Guarded, Up = value.Up,
                ForceStand = value.ForceStand, Kill = value.Kill, YAccelerationQ20 = ToQ20(value.YAccel),
                FallVelocityXQ20 = ToQ20(value.FallXVel), FallVelocityYQ20 = ToQ20(value.FallYVel),
                FallRecover = value.FallRecover, FallRecoverTime = value.FallRecoverTime,
                DownRecoverTime = value.DownRecoverTime, FallDamage = value.FallDamage, FallKill = value.FallKill,
            };
        }

        MProjectileTrace CaptureProjectile(MProjectile value, MCharData data)
        {
            int width = data != null && data.Definition != null ? data.Definition.LocalCoordWidth : 320;
            string key = _projectileKeys[value];
            return new MProjectileTrace
            {
                Key = key,
                Owner = KeyOf(value.Owner) ?? RootPlayerKey(value.Owner),
                CreationOrdinal = CreationOrdinal(key),
                ProjectileId = value.ProjId,
                AnimationNo = value.AnimNo,
                RemoveTime = value.RemoveTime,
                Time = value.Time,
                Removed = value.Removed,
                HitCount = value.HitCount,
                ContactCount = value.ContactCount,
                HitDone = value.HitDone,
                PositionWorldQ20 = WorldVector(value.Pos, width),
                VelocityWorldQ20 = WorldVector(value.Vel, width),
                AccelerationWorldQ20 = WorldVector(value.Accel, width),
                FacingQ20 = ToQ20(value.Facing),
            };
        }

        MExplodTrace CaptureExplod(MExplod value, MCharData data)
        {
            int width = data != null && data.Definition != null ? data.Definition.LocalCoordWidth : 320;
            string key = _explodKeys[value];
            return new MExplodTrace
            {
                Key = key,
                Owner = KeyByRuntimeId(value.OwnerId) ?? RootPlayerKeyById(value.OwnerId),
                CreationOrdinal = CreationOrdinal(key),
                ExplodId = value.ExplodId,
                AnimationNo = value.AnimNo,
                PositionWorldQ20 = WorldVector(value.Pos, width),
                VelocityWorldQ20 = WorldVector(value.Vel, width),
                AccelerationWorldQ20 = WorldVector(value.Accel, width),
                ScaleXQ20 = ToQ20(value.ScaleX),
                ScaleYQ20 = ToQ20(value.ScaleY),
                Facing = value.Facing,
                VerticalFacing = value.VFacing,
                BindTime = value.BindTime,
                RemoveTime = value.RemoveTime,
                SpritePriority = value.SprPriority,
                OwnPalette = value.OwnPal,
                RemoveOnGetHit = value.RemoveOnGetHit,
                RemoveOnChangeState = value.RemoveOnChangeState,
            };
        }

        void CaptureEvents(MBattleEngine engine, List<MEventTrace> destination)
        {
            Dictionary<string, int> occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < engine.World.Events.Sounds.Count; i++)
            {
                MSoundEvent value = engine.World.Events.Sounds[i];
                string owner = KeyByRuntimeId(value.OwnerId) ?? RootPlayerKeyById(value.OwnerId);
                string prefix = "sound/" + owner + "/" + value.Type.ToString().ToLowerInvariant() + "/" +
                    value.Channel.ToString(CultureInfo.InvariantCulture);
                int ordinal;
                occurrences.TryGetValue(prefix, out ordinal);
                occurrences[prefix] = ordinal + 1;
                destination.Add(new MEventTrace
                {
                    Key = prefix + "/" + ordinal.ToString(CultureInfo.InvariantCulture),
                    Owner = owner,
                    Action = value.Type.ToString().ToLowerInvariant(),
                    CommonBank = value.CommonBank,
                    Group = value.Group,
                    Number = value.Number,
                    Channel = value.Channel,
                    VolumeScale = value.VolumeScale,
                    PanQ20 = ToQ20(value.Pan),
                    AbsolutePan = value.AbsolutePan,
                    FrequencyQ20 = ToQ20(value.Frequency),
                    LoopCount = value.LoopCount,
                    Priority = value.Priority,
                    LoopStart = value.LoopStart,
                    LoopEnd = value.LoopEnd,
                    StartPosition = value.StartPosition,
                    LowPriority = value.LowPriority,
                    StopOnGetHit = value.StopOnGetHit,
                    StopOnChangeState = value.StopOnChangeState,
                });
            }
            for (int i = 0; i < engine.World.Events.Visuals.Count; i++)
            {
                MVisualEvent value = engine.World.Events.Visuals[i];
                string owner = KeyByRuntimeId(value.OwnerId) ?? RootPlayerKeyById(value.OwnerId);
                string prefix = "visual/" + owner + "/" + value.Type.ToString().ToLowerInvariant();
                int ordinal;
                occurrences.TryGetValue(prefix, out ordinal);
                occurrences[prefix] = ordinal + 1;
                destination.Add(new MEventTrace
                {
                    Key = prefix + "/" + ordinal.ToString(CultureInfo.InvariantCulture),
                    Kind = "visual",
                    Owner = owner,
                    Action = value.Type.ToString().ToLowerInvariant(),
                    Group = value.Id,
                    Number = value.Index,
                    Channel = value.Value0,
                    VolumeScale = value.Time,
                    Priority = value.Value1,
                    LoopStart = value.Value2,
                    LoopEnd = value.Value3,
                    PanQ20 = ToQ20(value.X),
                    FrequencyQ20 = ToQ20(value.Y),
                    StartPosition = ToQ20(value.Z) > int.MaxValue ? int.MaxValue : (int)ToQ20(value.Z),
                    CommonBank = value.Under,
                });
            }
        }

        static MCharData DataForOwner(MBattleEngine engine, MChar character)
        {
            if (character == null) { return null; }
            MChar root = character.Root ?? character.Parent ?? character;
            for (int i = 0; i < engine.Chars.Count; i++)
            {
                if (ReferenceEquals(engine.Chars[i], root) || ReferenceEquals(engine.Chars[i], character))
                {
                    return i < engine.Data.Count ? engine.Data[i] : null;
                }
            }
            return null;
        }

        static MCharData DataForOwnerId(MBattleEngine engine, int ownerId)
        {
            for (int i = 0; i < engine.Chars.Count; i++)
            {
                if (engine.Chars[i].Id == ownerId) { return i < engine.Data.Count ? engine.Data[i] : null; }
            }
            for (int i = 0; i < engine.Helpers.Count; i++)
            {
                if (engine.Helpers[i].Id == ownerId) { return DataForOwner(engine, engine.Helpers[i]); }
            }
            return null;
        }

        static int CreationOrdinal(string key)
        {
            int end = key.Length - 1;
            while (end >= 0 && char.IsDigit(key[end])) { end--; }
            int result;
            return int.TryParse(key.Substring(end + 1), NumberStyles.None, CultureInfo.InvariantCulture, out result)
                ? result
                : 0;
        }

        static long[] LocalVector(FVector3 value)
        {
            return new[] { ToQ20(value.X), ToQ20(value.Y), ToQ20(value.Z) };
        }

        static long[] WorldVector(FVector3 value, int localCoordWidth)
        {
            long[] local = LocalVector(value);
            if (localCoordWidth <= 0 || localCoordWidth == 320) { return local; }
            return new[]
            {
                ScaleRoundEven(local[0], 320, localCoordWidth),
                ScaleRoundEven(local[1], 320, localCoordWidth),
                ScaleRoundEven(local[2], 320, localCoordWidth),
            };
        }

        public static long ToQ20(FFloat value)
        {
            const long divisor = 1L << 12;
            long quotient = value.Raw / divisor;
            long remainder = value.Raw % divisor;
            long absoluteRemainder = remainder < 0 ? -remainder : remainder;
            long half = divisor >> 1;
            if (absoluteRemainder > half || (absoluteRemainder == half && (quotient & 1L) != 0))
            {
                quotient += value.Raw < 0 ? -1 : 1;
            }
            return quotient;
        }

        static long ScaleRoundEven(long value, int numerator, int denominator)
        {
            long scaled = value * numerator;
            long quotient = scaled / denominator;
            long remainder = scaled % denominator;
            long absoluteRemainder = remainder < 0 ? -remainder : remainder;
            long half = denominator / 2;
            bool isTie = denominator % 2 == 0 && absoluteRemainder == half;
            if (absoluteRemainder > half || (isTie && (quotient & 1L) != 0))
            {
                quotient += scaled < 0 ? -1 : 1;
            }
            return quotient;
        }

        static void Sort<T>(List<T> values) where T : IMTraceKeyed
        {
            values.Sort((left, right) => string.CompareOrdinal(left.TraceKey, right.TraceKey));
        }
    }

    public sealed class MTraceTolerance
    {
        public long PositionQ20 = 1024;
        public long VelocityQ20 = 256;
        public long AccelerationQ20 = 256;
        public long ScaleQ20 = 16;
        public long OtherQ20;

        public static MTraceTolerance Exact()
        {
            return new MTraceTolerance
            {
                PositionQ20 = 0,
                VelocityQ20 = 0,
                AccelerationQ20 = 0,
                ScaleQ20 = 0,
                OtherQ20 = 0,
            };
        }
    }

    public sealed class MTraceDifference
    {
        public int Frame;
        public string Category;
        public string Path;
        public string Expected;
        public string Actual;

        public override string ToString()
        {
            return "frame " + Frame + " [" + Category + "] " + Path + ": expected " + Expected +
                ", actual " + Actual;
        }
    }

    /// <summary>
    /// Schema-driven comparer. Public DTO fields are discovered recursively, so adding a field cannot silently
    /// escape comparison. Record lists are joined by their stable key rather than by list position.
    /// </summary>
    public static class MBattleTraceComparer
    {
        public static List<MTraceDifference> Compare(
            MBattleTrace expected,
            MBattleTrace actual,
            MTraceTolerance tolerance = null)
        {
            List<MTraceDifference> differences = new List<MTraceDifference>();
            MTraceTolerance effective = tolerance ?? new MTraceTolerance();
            CompareValue("trace.Header", expected != null ? expected.Header : null,
                actual != null ? actual.Header : null, -1, effective, differences);
            CompareKeyedList("trace.Frames", expected != null ? expected.Frames : null,
                actual != null ? actual.Frames : null, -1, effective, differences);
            CompareValue("trace.End", expected != null ? expected.End : null,
                actual != null ? actual.End : null, -1, effective, differences);
            return differences;
        }

        public static List<MTraceDifference> Compare(
            IReadOnlyList<MBattleTraceFrame> expected,
            IReadOnlyList<MBattleTraceFrame> actual,
            MTraceTolerance tolerance = null)
        {
            List<MTraceDifference> differences = new List<MTraceDifference>();
            CompareKeyedList("trace.Frames", expected, actual, -1,
                tolerance ?? new MTraceTolerance(), differences);
            return differences;
        }

        static void CompareValue(string path, object expected, object actual, int frame,
            MTraceTolerance tolerance, List<MTraceDifference> differences)
        {
            if (ReferenceEquals(expected, actual)) { return; }
            if (expected == null || actual == null)
            {
                Add(frame, path, expected, actual, differences);
                return;
            }

            Type expectedType = expected.GetType();
            Type actualType = actual.GetType();
            if (expectedType != actualType)
            {
                Add(frame, path + ".type", expectedType.FullName, actualType.FullName, differences);
                return;
            }
            if (IsScalar(expectedType))
            {
                if (expectedType == typeof(long) && path.IndexOf("Q20", StringComparison.Ordinal) >= 0)
                {
                    CompareQ20(path, (long)expected, (long)actual, frame, tolerance, differences);
                }
                else if (!expected.Equals(actual))
                {
                    Add(frame, path, expected, actual, differences);
                }
                return;
            }
            if (expected is IDictionary)
            {
                CompareDictionary(path, (IDictionary)expected, (IDictionary)actual, frame, tolerance, differences);
                return;
            }
            if (expected is IList)
            {
                IList left = (IList)expected;
                IList right = (IList)actual;
                if (ContainsKeyedRecords(left) || ContainsKeyedRecords(right))
                {
                    CompareKeyedList(path, left, right, frame, tolerance, differences);
                }
                else
                {
                    ComparePositionalValues(path, left, right, frame, tolerance, differences);
                }
                return;
            }

            FieldInfo[] fields = expectedType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, (left, right) => string.CompareOrdinal(left.Name, right.Name));
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                CompareValue(path + "." + field.Name, field.GetValue(expected), field.GetValue(actual), frame,
                    tolerance, differences);
            }
        }

        static bool IsScalar(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
        }

        static bool ContainsKeyedRecords(IList values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] != null) { return values[i] is IMTraceKeyed; }
            }
            return false;
        }

        static void CompareKeyedList(string path, object expectedValue, object actualValue, int frame,
            MTraceTolerance tolerance, List<MTraceDifference> differences)
        {
            IList expected = ToList(expectedValue);
            IList actual = ToList(actualValue);
            if (expected == null || actual == null)
            {
                Add(frame, path, expected == null ? null : "present", actual == null ? null : "present", differences);
                return;
            }
            SortedDictionary<string, object> left = IndexByKey(path, expected, frame, differences);
            SortedDictionary<string, object> right = IndexByKey(path, actual, frame, differences);
            SortedDictionary<string, bool> keys = new SortedDictionary<string, bool>(StringComparer.Ordinal);
            foreach (string key in left.Keys) { keys[key] = true; }
            foreach (string key in right.Keys) { keys[key] = true; }
            foreach (string key in keys.Keys)
            {
                object leftValue;
                object rightValue;
                bool hasLeft = left.TryGetValue(key, out leftValue);
                bool hasRight = right.TryGetValue(key, out rightValue);
                int itemFrame = frame;
                MBattleTraceFrame leftFrame = leftValue as MBattleTraceFrame;
                MBattleTraceFrame rightFrame = rightValue as MBattleTraceFrame;
                if (leftFrame != null) { itemFrame = leftFrame.Step; }
                else if (rightFrame != null) { itemFrame = rightFrame.Step; }
                string itemPath = path + "[" + key + "]";
                if (!hasLeft || !hasRight)
                {
                    Add(itemFrame, itemPath, hasLeft ? "present" : "missing", hasRight ? "present" : "missing", differences);
                }
                else
                {
                    CompareValue(itemPath, leftValue, rightValue, itemFrame, tolerance, differences);
                }
            }
        }

        static IList ToList(object value)
        {
            if (value == null) { return null; }
            IList list = value as IList;
            if (list != null) { return list; }
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null) { return null; }
            ArrayList copy = new ArrayList();
            foreach (object item in enumerable) { copy.Add(item); }
            return copy;
        }

        static SortedDictionary<string, object> IndexByKey(string path, IList values, int frame,
            List<MTraceDifference> differences)
        {
            SortedDictionary<string, object> result = new SortedDictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                IMTraceKeyed keyed = values[i] as IMTraceKeyed;
                string key = keyed != null ? keyed.TraceKey : null;
                if (string.IsNullOrEmpty(key))
                {
                    Add(frame, path + ".key", "non-empty stable key", key ?? "null", differences);
                    continue;
                }
                if (result.ContainsKey(key))
                {
                    Add(frame, path + "[" + key + "]", "unique", "duplicate", differences);
                    continue;
                }
                result[key] = values[i];
            }
            return result;
        }

        static void ComparePositionalValues(string path, IList expected, IList actual, int frame,
            MTraceTolerance tolerance, List<MTraceDifference> differences)
        {
            if (expected.Count != actual.Count)
            {
                Add(frame, path + ".Count", expected.Count, actual.Count, differences);
            }
            int count = System.Math.Min(expected.Count, actual.Count);
            for (int i = 0; i < count; i++)
            {
                CompareValue(path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                    expected[i], actual[i], frame, tolerance, differences);
            }
        }

        static void CompareDictionary(string path, IDictionary expected, IDictionary actual, int frame,
            MTraceTolerance tolerance, List<MTraceDifference> differences)
        {
            SortedDictionary<string, object> left = DictionaryByTextKey(expected);
            SortedDictionary<string, object> right = DictionaryByTextKey(actual);
            SortedDictionary<string, bool> keys = new SortedDictionary<string, bool>(StringComparer.Ordinal);
            foreach (string key in left.Keys) { keys[key] = true; }
            foreach (string key in right.Keys) { keys[key] = true; }
            foreach (string key in keys.Keys)
            {
                object leftValue;
                object rightValue;
                bool hasLeft = left.TryGetValue(key, out leftValue);
                bool hasRight = right.TryGetValue(key, out rightValue);
                string itemPath = path + "[" + key + "]";
                if (!hasLeft || !hasRight)
                {
                    Add(frame, itemPath, hasLeft ? "present" : "missing", hasRight ? "present" : "missing", differences);
                }
                else
                {
                    CompareValue(itemPath, leftValue, rightValue, frame, tolerance, differences);
                }
            }
        }

        static SortedDictionary<string, object> DictionaryByTextKey(IDictionary values)
        {
            SortedDictionary<string, object> result = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in values)
            {
                string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                result[key] = entry.Value;
            }
            return result;
        }

        static void CompareQ20(string path, long expected, long actual, int frame,
            MTraceTolerance tolerance, List<MTraceDifference> differences)
        {
            long allowed = ToleranceFor(path, tolerance);
            decimal delta = System.Math.Abs((decimal)expected - actual);
            if (delta > System.Math.Max(0L, allowed))
            {
                Add(frame, path, expected, actual, differences);
            }
        }

        static long ToleranceFor(string path, MTraceTolerance tolerance)
        {
            if (path.IndexOf("Acceleration", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("YAcceleration", StringComparison.Ordinal) >= 0)
            {
                return tolerance.AccelerationQ20;
            }
            if (path.IndexOf("Velocity", StringComparison.Ordinal) >= 0)
            {
                return tolerance.VelocityQ20;
            }
            if (path.IndexOf("Position", StringComparison.Ordinal) >= 0)
            {
                return tolerance.PositionQ20;
            }
            if (path.IndexOf("Scale", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("Multiplier", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("FacingQ20", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("FrequencyQ20", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("PanQ20", StringComparison.Ordinal) >= 0)
            {
                return tolerance.ScaleQ20;
            }
            return tolerance.OtherQ20;
        }

        static void Add(int frame, string path, object expected, object actual,
            List<MTraceDifference> differences)
        {
            differences.Add(new MTraceDifference
            {
                Frame = frame,
                Category = CategoryFor(path),
                Path = path,
                Expected = Format(expected),
                Actual = Format(actual),
            });
        }

        static string CategoryFor(string path)
        {
            if (path.IndexOf(".Inputs", StringComparison.Ordinal) >= 0) { return "input"; }
            if (path.IndexOf(".Global.Rng", StringComparison.Ordinal) >= 0) { return "rng"; }
            if (path.IndexOf(".Players[", StringComparison.Ordinal) >= 0 ||
                path.IndexOf(".Helpers[", StringComparison.Ordinal) >= 0 ||
                path.IndexOf(".Projectiles[", StringComparison.Ordinal) >= 0 ||
                path.IndexOf(".Explods[", StringComparison.Ordinal) >= 0)
            {
                if (path.EndsWith("]", StringComparison.Ordinal) || path.EndsWith(".Key", StringComparison.Ordinal) ||
                    path.IndexOf("CreationOrdinal", StringComparison.Ordinal) >= 0)
                {
                    return "entity-lifecycle";
                }
            }
            if (path.IndexOf("AnimationOwner", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("SpriteOwner", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("StateOwner", StringComparison.Ordinal) >= 0 ||
                path.EndsWith(".Owner", StringComparison.Ordinal))
            {
                return "resource-owner";
            }
            if (path.IndexOf(".State", StringComparison.Ordinal) >= 0) { return "state-machine"; }
            if (path.IndexOf(".Animation", StringComparison.Ordinal) >= 0) { return "animation"; }
            if (path.IndexOf("Transform", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("Position", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("Velocity", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("Acceleration", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("Scale", StringComparison.Ordinal) >= 0)
            {
                return "physics-numeric";
            }
            if (path.IndexOf("Combat", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("GetHit", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("Vitals", StringComparison.Ordinal) >= 0)
            {
                return "hit-resolution";
            }
            return "trace-schema";
        }

        static string Format(object value)
        {
            return value == null ? "null" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
