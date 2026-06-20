using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle
{
    public enum MMoveTestStatus
    {
        Passed,
        UnreachablePrerequisite,
        CommandFailure,
        TransitionFailure,
        RuntimeFailure,
        DeterministicFailure,
    }

    public sealed class MMovePrerequisiteProfile
    {
        public string Name = "standing";
        public int ActorStateNo = 0;
        public int ActorStateType = 1;
        public int ActorMoveType = 1;
        public bool ActorCtrl = true;
        public int ActorPower = -1;
        public int Distance = 60;
        public int ActorY;
        public int TargetLife = -1;
        public bool TargetGuarding;

        // Project-specific: curated test prerequisites that approximate common Ikemen/MUGEN move-entry situations.
        public static List<MMovePrerequisiteProfile> DefaultSet()
        {
            return new List<MMovePrerequisiteProfile>
            {
                new MMovePrerequisiteProfile { Name = "standing-close", Distance = 35, ActorPower = -1 },
                new MMovePrerequisiteProfile { Name = "standing-mid", Distance = 80, ActorPower = -1 },
                new MMovePrerequisiteProfile
                {
                    Name = "standing-fatal-ready", Distance = 70, ActorPower = -1, TargetLife = 100,
                },
                new MMovePrerequisiteProfile
                {
                    Name = "crouching-close", ActorStateNo = 11, ActorStateType = 2,
                    Distance = 35, ActorPower = -1,
                },
                new MMovePrerequisiteProfile
                {
                    Name = "airborne-close", ActorStateNo = 50, ActorStateType = 4,
                    ActorMoveType = 1, ActorY = -70, Distance = 35, ActorPower = -1,
                },
                new MMovePrerequisiteProfile
                {
                    Name = "guard-target-close", Distance = 35, ActorPower = -1, TargetGuarding = true,
                },
            };
        }
    }

    public sealed class MMoveTestResult
    {
        public MMoveTestStatus Status;
        public string ProfileName = "";
        public bool UsedSnapshot;
        public bool Deterministic;
        public bool CommandMatched;
        public bool StateEntered;
        public int TargetStateNo;
        public int FinalStateNo;
        public int FramesRun;
        public ulong FirstHash;
        public ulong ReplayHash;
    }

    public static class MMoveTestRunner
    {
        // Project-specific: command reachability test harness around the C# Ikemen-style battle engine.
        public static MMoveTestResult Run(
            MCharData data,
            IReadOnlyList<MCommandDef> commands,
            int targetStateNo,
            IReadOnlyList<MMovePrerequisiteProfile> profiles = null,
            int warmupFrames = 5,
            int tailFrames = 45)
        {
            if (data == null || commands == null || commands.Count == 0)
            {
                return new MMoveTestResult
                {
                    Status = MMoveTestStatus.UnreachablePrerequisite,
                    TargetStateNo = targetStateNo,
                };
            }

            IReadOnlyList<MMovePrerequisiteProfile> effectiveProfiles =
                profiles != null && profiles.Count > 0 ? profiles : MMovePrerequisiteProfile.DefaultSet();
            MMoveTestResult best = null;
            for (int i = 0; i < effectiveProfiles.Count; i++)
            {
                MMoveTestResult result = RunProfile(data, commands, targetStateNo, effectiveProfiles[i],
                    warmupFrames, tailFrames);
                if (result.Status == MMoveTestStatus.Passed)
                {
                    return result;
                }
                if (best == null || Rank(result.Status) < Rank(best.Status))
                {
                    best = result;
                }
            }
            return best ?? new MMoveTestResult
            {
                Status = MMoveTestStatus.UnreachablePrerequisite,
                TargetStateNo = targetStateNo,
            };
        }

        // Project-specific: runs one prerequisite profile twice to verify deterministic rollback behavior.
        static MMoveTestResult RunProfile(
            MCharData data,
            IReadOnlyList<MCommandDef> commands,
            int targetStateNo,
            MMovePrerequisiteProfile profile,
            int warmupFrames,
            int tailFrames)
        {
            MBattleEngine engine = CreateEngine(data);
            for (int i = 0; i < warmupFrames; i++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
            }
            ApplyProfile(engine, profile);
            MBattleEngineSnapshot snapshot = engine.Snapshot();

            MMoveAttempt first = RunAttempt(engine, commands, targetStateNo, tailFrames);
            ulong firstHash = engine.ComputeHash();
            engine.Restore(snapshot);
            MMoveAttempt replay = RunAttempt(engine, commands, targetStateNo, tailFrames);
            ulong replayHash = engine.ComputeHash();

            MMoveTestStatus status = Classify(first, firstHash == replayHash);
            return new MMoveTestResult
            {
                Status = status,
                ProfileName = profile.Name,
                UsedSnapshot = true,
                Deterministic = firstHash == replayHash,
                CommandMatched = first.CommandMatched,
                StateEntered = first.StateEntered,
                TargetStateNo = targetStateNo,
                FinalStateNo = first.FinalStateNo,
                FramesRun = first.FramesRun,
                FirstHash = firstHash,
                ReplayHash = replayHash,
            };
        }

        // Project-specific: feeds synthesized command input and observes state entry in the C# harness.
        static MMoveAttempt RunAttempt(
            MBattleEngine engine,
            IReadOnlyList<MCommandDef> commands,
            int targetStateNo,
            int tailFrames)
        {
            MChar actor = engine.Chars[0];
            List<MInput> sequence = MCommandInputSynthesizer.BuildCombinedSequence(commands, actor.Facing.Raw >= 0);
            MMoveAttempt result = new MMoveAttempt();
            for (int frame = 0; frame < sequence.Count; frame++)
            {
                engine.Tick(new[] { sequence[frame], MInput.None });
                result.FramesRun++;
                if (AnyCommandActive(actor, commands))
                {
                    result.CommandMatched = true;
                }
                if (actor.StateNo == targetStateNo)
                {
                    result.StateEntered = true;
                    result.FinalStateNo = actor.StateNo;
                    return result;
                }
            }
            for (int tail = 0; tail < tailFrames; tail++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
                result.FramesRun++;
                if (AnyCommandActive(actor, commands))
                {
                    result.CommandMatched = true;
                }
                if (actor.StateNo == targetStateNo)
                {
                    result.StateEntered = true;
                    break;
                }
            }
            result.FinalStateNo = actor.StateNo;
            return result;
        }

        // Ikemen reference: src/input.go command buffer matching; C# checks active command names exposed by MCommandList.
        static bool AnyCommandActive(MChar actor, IReadOnlyList<MCommandDef> commands)
        {
            if (actor.CommandList == null)
            {
                return false;
            }
            for (int i = 0; i < commands.Count; i++)
            {
                if (actor.CommandList.IsActive(commands[i].Name))
                {
                    return true;
                }
            }
            return false;
        }

        // Project-specific: classifies harness failures into command, transition, and determinism buckets.
        static MMoveTestStatus Classify(MMoveAttempt attempt, bool deterministic)
        {
            if (!attempt.CommandMatched)
            {
                return MMoveTestStatus.CommandFailure;
            }
            if (!attempt.StateEntered)
            {
                return MMoveTestStatus.TransitionFailure;
            }
            return deterministic ? MMoveTestStatus.Passed : MMoveTestStatus.DeterministicFailure;
        }

        // Project-specific: sets C# actor/target state to emulate Ikemen preconditions before command execution.
        static void ApplyProfile(MBattleEngine engine, MMovePrerequisiteProfile profile)
        {
            MChar actor = engine.Chars[0];
            MChar target = engine.Chars[1];
            int distance = profile.Distance <= 0 ? 60 : profile.Distance;
            actor.Pos = new FVector3(FFloat.FromInt(-distance / 2), FFloat.FromInt(profile.ActorY), FFloat.Zero);
            actor.OldPos = actor.Pos;
            target.Pos = new FVector3(FFloat.FromInt(distance / 2), FFloat.Zero, FFloat.Zero);
            target.OldPos = target.Pos;
            actor.Facing = FFloat.One;
            target.Facing = -FFloat.One;
            actor.StateNo = profile.ActorStateNo;
            actor.StateType = profile.ActorStateType;
            actor.MoveType = profile.ActorMoveType;
            actor.Ctrl = profile.ActorCtrl;
            actor.KeyCtrl = true;
            actor.Time = 0;
            actor.Power = profile.ActorPower < 0 ? actor.PowerMax : profile.ActorPower;
            if (profile.TargetLife >= 0)
            {
                target.Life = profile.TargetLife;
            }
            target.Guarding = profile.TargetGuarding;
            target.Ctrl = true;
            target.KeyCtrl = false;
            engine.LinkPair();
        }

        // Project-specific: creates a two-player C# battle fixture with Ikemen-style P1/P2 facing and links.
        static MBattleEngine CreateEngine(MCharData data)
        {
            MBattleEngine engine = new MBattleEngine();
            MChar left = MCharLoader.SpawnChar(data, 1);
            MChar right = MCharLoader.SpawnChar(data, 2);
            left.Pos = new FVector3(FFloat.FromInt(-30), FFloat.Zero, FFloat.Zero);
            right.Pos = new FVector3(FFloat.FromInt(30), FFloat.Zero, FFloat.Zero);
            right.Facing = -FFloat.One;
            engine.Add(left, data);
            engine.Add(right, data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        // Project-specific: orders harness outcomes so the most useful failure is reported.
        static int Rank(MMoveTestStatus status)
        {
            switch (status)
            {
                case MMoveTestStatus.Passed: return 0;
                case MMoveTestStatus.DeterministicFailure: return 1;
                case MMoveTestStatus.TransitionFailure: return 2;
                case MMoveTestStatus.CommandFailure: return 3;
                case MMoveTestStatus.RuntimeFailure: return 4;
                default: return 5;
            }
        }

        sealed class MMoveAttempt
        {
            public bool CommandMatched;
            public bool StateEntered;
            public int FinalStateNo;
            public int FramesRun;
        }
    }
}
