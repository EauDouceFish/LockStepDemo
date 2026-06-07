using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class BattleTraceTests
    {
        [Test]
        public void Recorder_CapturesV1CoreSectionsAndCanonicalCoordinates()
        {
            MBattleEngine engine = new MBattleEngine();
            MChar player = new MChar
            {
                Id = 10,
                StateNo = 20,
                PrevStateNo = 10,
                AnimNo = 21,
                AnimElemNo = 2,
                Pos = new FVector3(FFloat.FromInt(1), FFloat.FromInt(2), FFloat.Zero),
                Vel = new FVector3(FFloat.FromInt(3), FFloat.FromInt(4), FFloat.Zero),
                Life = 900,
                Power = 500,
            };
            MCharData data = new MCharData();
            data.Definition.LocalCoordWidth = 640;
            data.Definition.LocalCoordHeight = 480;
            engine.Add(player, data);
            engine.LinkPair();

            MChar helper = new MChar { Id = 1000, IsHelper = true, HelperType = 7, Parent = player, Root = player };
            engine.Helpers.Add(helper);
            engine.World.Projectiles.Add(new MProjectile
            {
                Id = 1001, OwnerId = player.Id, Owner = player, ProjId = 30,
                Pos = new FVector3(FFloat.FromInt(5), FFloat.Zero, FFloat.Zero),
            });
            engine.World.Explods.Add(new MExplod { Id = 1002, OwnerId = player.Id, ExplodId = 40 });
            engine.World.Events.BeginFrame(0);
            engine.World.Events.Sounds.Add(new MSoundEvent
            {
                Type = MSoundEventType.Play, OwnerId = player.Id, Group = 1, Number = 2, Channel = 3,
            });

            MBattleTraceRecorder recorder = new MBattleTraceRecorder();
            MBattleTraceFrame frame = recorder.CapturePostTick(
                engine, 0, new[] { MInput.Right | MInput.A });

            Assert.Multiple(() =>
            {
                Assert.That(frame.Type, Is.EqualTo("frame"));
                Assert.That(frame.Phase, Is.EqualTo("post-combat-tick"));
                Assert.That(frame.Inputs[0].Bits, Is.EqualTo((int)(MInput.Right | MInput.A)));
                Assert.That(frame.Global.RngSeed, Is.EqualTo(engine.Random.Seed));
                Assert.That(frame.Global.NextEntityId, Is.EqualTo(engine.World.NextEntityId));
                Assert.That(frame.Players[0].Key, Is.EqualTo("p0"));
                Assert.That(frame.Players[0].Transform.LocalCoord, Is.EqualTo(new[] { 640, 480 }));
                Assert.That(frame.Players[0].Transform.UnsupportedNumericSpace, Is.True);
                Assert.That(frame.Players[0].Transform.PositionLocalQ20[0], Is.EqualTo(1L << 20));
                Assert.That(frame.Players[0].Transform.PositionWorldQ20[0], Is.EqualTo(1L << 19));
                Assert.That(frame.Helpers[0].Key, Is.EqualTo("p0/h0"));
                Assert.That(frame.Projectiles[0].Key, Is.EqualTo("p0/proj0"));
                Assert.That(frame.Explods[0].Key, Is.EqualTo("p0/explod0"));
                Assert.That(frame.Events[0].Key, Is.EqualTo("sound/p0/play/3/0"));
                Assert.That(frame.Hashes.Native, Is.Not.Empty);
            });
        }

        [Test]
        public void Recorder_StableEntityKeysSurviveRuntimeListReordering()
        {
            MBattleEngine engine = new MBattleEngine();
            MChar player = new MChar { Id = 1 };
            engine.Add(player, new MCharData());
            engine.LinkPair();
            MChar first = new MChar { Id = 1000, IsHelper = true, Parent = player, Root = player };
            MChar second = new MChar { Id = 1001, IsHelper = true, Parent = player, Root = player };
            engine.Helpers.Add(first);
            engine.Helpers.Add(second);

            MBattleTraceRecorder recorder = new MBattleTraceRecorder();
            MBattleTraceFrame before = recorder.CapturePostTick(engine, 0, new[] { MInput.None });
            engine.Helpers.Reverse();
            MBattleTraceFrame after = recorder.CapturePostTick(engine, 1, new[] { MInput.None });

            Assert.Multiple(() =>
            {
                Assert.That(before.Helpers[0].Key, Is.EqualTo("p0/h0"));
                Assert.That(before.Helpers[1].Key, Is.EqualTo("p0/h1"));
                Assert.That(after.Helpers[0].Key, Is.EqualTo("p0/h0"));
                Assert.That(after.Helpers[1].Key, Is.EqualTo("p0/h1"));
            });
        }

        [Test]
        public void Comparer_JoinsFramesAndRecordsByStableKeyInsteadOfListIndex()
        {
            MBattleTraceFrame expected = RepresentativeFrame(7);
            MBattleTraceFrame actual = RepresentativeFrame(7);
            actual.Inputs.Reverse();
            actual.Players.Reverse();
            actual.Helpers.Reverse();
            actual.Projectiles.Reverse();
            actual.Explods.Reverse();
            actual.Events.Reverse();

            List<MTraceDifference> differences = MBattleTraceComparer.Compare(
                new[] { expected }, new[] { actual }, MTraceTolerance.Exact());

            Assert.That(differences, Is.Empty);
        }

        [Test]
        public void Comparer_ReportsHeaderScenarioInputGlobalEntitiesAndEndFields()
        {
            MBattleTrace expected = RepresentativeTrace();
            MBattleTrace actual = RepresentativeTrace();
            actual.Header.Producer.Commit = "different";
            actual.Header.Scenario.InputSha256 = "other-input";
            actual.Header.ContentSha256["p1_def"] = "other-content";
            actual.Frames[0].Inputs[0].Bits++;
            actual.Frames[0].Global.RngSeed++;
            actual.Frames[0].Players[0].State.No++;
            actual.Frames[0].Helpers[0].Animation.Element++;
            actual.Frames[0].Projectiles[0].HitCount++;
            actual.Frames[0].Explods[0].RemoveTime++;
            actual.Frames[0].Events[0].Priority++;
            actual.End.Completed = false;

            List<MTraceDifference> differences = MBattleTraceComparer.Compare(
                expected, actual, MTraceTolerance.Exact());

            AssertPaths(differences,
                "trace.Header.Producer.Commit",
                "trace.Header.Scenario.InputSha256",
                "trace.Header.ContentSha256[p1_def]",
                "trace.Frames[step:3].Inputs[slot:0].Bits",
                "trace.Frames[step:3].Global.RngSeed",
                "trace.Frames[step:3].Players[p0].State.No",
                "trace.Frames[step:3].Helpers[p0/h0].Animation.Element",
                "trace.Frames[step:3].Projectiles[p0/proj0].HitCount",
                "trace.Frames[step:3].Explods[p0/explod0].RemoveTime",
                "trace.Frames[step:3].Events[event/0].Priority",
                "trace.End.Completed");
        }

        [Test]
        public void Comparer_ReportsMissingStableEntityKeyAsLifecycleDifference()
        {
            MBattleTraceFrame expected = RepresentativeFrame(5);
            MBattleTraceFrame actual = RepresentativeFrame(5);
            actual.Helpers.RemoveAt(0);

            List<MTraceDifference> differences = MBattleTraceComparer.Compare(
                new[] { expected }, new[] { actual }, MTraceTolerance.Exact());

            Assert.That(differences.Count, Is.EqualTo(1));
            Assert.That(differences[0].Path, Is.EqualTo("trace.Frames[step:5].Helpers[p0/h0]"));
            Assert.That(differences[0].Category, Is.EqualTo("entity-lifecycle"));
        }

        [Test]
        public void FieldTolerances_AreAppliedByContinuousQuantityType()
        {
            MBattleTraceFrame expected = RepresentativeFrame(1);
            MBattleTraceFrame actual = RepresentativeFrame(1);
            actual.Players[0].Transform.PositionWorldQ20[0] += 1024;
            actual.Players[0].Transform.VelocityWorldQ20[0] += 256;
            actual.Projectiles[0].AccelerationWorldQ20[0] += 256;
            actual.Explods[0].ScaleXQ20 += 16;

            Assert.That(MBattleTraceComparer.Compare(new[] { expected }, new[] { actual }), Is.Empty);

            actual.Players[0].Transform.PositionWorldQ20[0]++;
            actual.Players[0].Transform.VelocityWorldQ20[0]++;
            actual.Projectiles[0].AccelerationWorldQ20[0]++;
            actual.Explods[0].ScaleXQ20++;
            List<MTraceDifference> differences = MBattleTraceComparer.Compare(new[] { expected }, new[] { actual });

            AssertPaths(differences,
                "trace.Frames[step:1].Players[p0].Transform.PositionWorldQ20[0]",
                "trace.Frames[step:1].Players[p0].Transform.VelocityWorldQ20[0]",
                "trace.Frames[step:1].Projectiles[p0/proj0].AccelerationWorldQ20[0]",
                "trace.Frames[step:1].Explods[p0/explod0].ScaleXQ20");
        }

        [Test]
        public void ToQ20_UsesRoundToEvenForPositiveAndNegativeTies()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MBattleTraceRecorder.ToQ20(FFloat.FromInt(1)), Is.EqualTo(1L << 20));
                Assert.That(MBattleTraceRecorder.ToQ20(FFloat.FromRaw(2048)), Is.EqualTo(0));
                Assert.That(MBattleTraceRecorder.ToQ20(FFloat.FromRaw(6144)), Is.EqualTo(2));
                Assert.That(MBattleTraceRecorder.ToQ20(FFloat.FromRaw(-2048)), Is.EqualTo(0));
                Assert.That(MBattleTraceRecorder.ToQ20(FFloat.FromRaw(-6144)), Is.EqualTo(-2));
            });
        }

        static MBattleTrace RepresentativeTrace()
        {
            MBattleTrace trace = new MBattleTrace
            {
                Header = MBattleTraceRecorder.CreateHeader(new MTraceScenario
                {
                    Id = "scenario", InputSha256 = "input", Seed = 7, MaxSteps = 60,
                    StartPolicy = "natural_match",
                }, "commit"),
                End = new MBattleTraceEnd { Steps = 1, Completed = true, Reason = "complete", SemanticSha256 = "hash" },
            };
            trace.Header.ContentSha256["p1_def"] = "content";
            trace.Frames.Add(RepresentativeFrame(3));
            return trace;
        }

        static MBattleTraceFrame RepresentativeFrame(int step)
        {
            MBattleTraceFrame frame = new MBattleTraceFrame
            {
                Step = step,
                Global = new MTraceGlobal { NativeFrameNo = step + 1, RngSeed = 7, RoundState = 2, NextEntityId = 1004 },
                Hashes = new MTraceHashes { Native = "native", Semantic = "semantic", Global = "global", Entities = "entities" },
            };
            frame.Inputs.Add(new MTraceInput { Slot = 0, Bits = 1 });
            frame.Inputs.Add(new MTraceInput { Slot = 1, Bits = 2 });
            frame.Players.Add(Entity("p0", "player"));
            frame.Players.Add(Entity("p1", "player"));
            frame.Helpers.Add(Entity("p0/h0", "helper"));
            frame.Helpers.Add(Entity("p1/h0", "helper"));
            frame.Projectiles.Add(new MProjectileTrace
            {
                Key = "p0/proj0", Owner = "p0", CreationOrdinal = 0, ProjectileId = 10,
                PositionWorldQ20 = new long[] { 1, 2, 3 }, VelocityWorldQ20 = new long[] { 4, 5, 6 },
                AccelerationWorldQ20 = new long[] { 7, 8, 9 }, FacingQ20 = 1L << 20,
            });
            frame.Projectiles.Add(new MProjectileTrace { Key = "p1/proj0", Owner = "p1" });
            frame.Explods.Add(new MExplodTrace
            {
                Key = "p0/explod0", Owner = "p0", CreationOrdinal = 0, ExplodId = 20,
                ScaleXQ20 = 1L << 20, ScaleYQ20 = 1L << 20,
            });
            frame.Explods.Add(new MExplodTrace { Key = "p1/explod0", Owner = "p1" });
            frame.Events.Add(new MEventTrace { Key = "event/0", Owner = "p0", Action = "play", Priority = 1 });
            frame.Events.Add(new MEventTrace { Key = "event/1", Owner = "p1", Action = "stop" });
            return frame;
        }

        static MEntityTrace Entity(string key, string kind)
        {
            MEntityTrace entity = new MEntityTrace
            {
                Key = key,
                Kind = kind,
                Owner = key.Substring(0, 2),
                State = new MTraceState { No = 10, Previous = 9, Time = 3, Type = 1, MoveType = 2, Physics = 1, Ctrl = true },
                Animation = new MTraceAnimation
                {
                    No = 20, Element = 1, AnimationOwner = key.Substring(0, 2), SpriteOwner = key.Substring(0, 2),
                },
                Transform = new MTraceTransform
                {
                    PositionWorldQ20 = new long[] { 100, 200, 0 },
                    VelocityWorldQ20 = new long[] { 10, 20, 0 },
                },
                Vitals = new MTraceVitals { Life = 1000, LifeMax = 1000, PowerMax = 3000 },
                Combat = new MTraceCombat { AttackMultiplierQ20 = 1L << 20, DefenseMultiplierQ20 = 1L << 20 },
            };
            entity.IntVars[0] = 1;
            entity.FloatVarsQ20[0] = 2;
            entity.PersistentCounters[0] = 3;
            entity.HitOverrides.Add(new MTraceHitOverride { Slot = 0 });
            entity.JugglePoints.Add(new MTraceJugglePoint { Attacker = "p1", Points = 4 });
            return entity;
        }

        static void AssertPaths(IReadOnlyList<MTraceDifference> differences, params string[] paths)
        {
            List<string> actual = new List<string>();
            for (int i = 0; i < differences.Count; i++) { actual.Add(differences[i].Path); }
            Assert.That(actual, Is.EquivalentTo(paths));
        }
    }
}
