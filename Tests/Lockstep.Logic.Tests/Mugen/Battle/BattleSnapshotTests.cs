using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Hit;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class BattleSnapshotTests
    {
        static FFloat Fixed(int value)
        {
            return FFloat.FromInt(value);
        }

        static ulong HashOf(MEntityWorld world)
        {
            Hash64 hash = Hash64.Create();
            world.WriteHash(ref hash);
            return hash.Value;
        }

        static MBattleEngine EngineWithLinkedEntities()
        {
            MBattleEngine engine = new MBattleEngine();
            MCharData data = new MCharData();
            MChar p1 = new MChar { Id = 1, Life = 1000, Pos = new FVector3(Fixed(10), FFloat.Zero, FFloat.Zero) };
            MChar p2 = new MChar { Id = 2, Life = 900, Pos = new FVector3(Fixed(60), FFloat.Zero, FFloat.Zero) };
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            p1.Targets.Add(p2);
            p2.BindTo(p1, 3, new FVector3(Fixed(4), FFloat.Zero, FFloat.Zero), 1);

            p1.RequestHelper(0, 7, FFloat.Zero, FFloat.Zero, 1, false);
            engine.Tick(new List<MInput> { MInput.None, MInput.None });

            engine.World.Projectiles.Add(new MProjectile
            {
                Id = 1001,
                OwnerId = p1.Id,
                Owner = p1,
                ProjId = 30,
                HitDef = new MHitDef { Active = true, HitDamage = 123 },
                Pos = new FVector3(Fixed(20), FFloat.Zero, FFloat.Zero),
            });
            engine.World.Explods.Add(new MExplod { Id = 1002, OwnerId = p1.Id, ExplodId = 40, Pos = new FVector3(Fixed(3), FFloat.Zero, FFloat.Zero) });
            return engine;
        }

        [Test]
        public void SnapshotRestore_RestoresHashAfterMutation()
        {
            MBattleEngine engine = EngineWithLinkedEntities();
            engine.Tick(new List<MInput> { MInput.None, MInput.None });
            MBattleEngineSnapshot snapshot = engine.Snapshot();
            ulong expectedHash = engine.ComputeHash();

            engine.Chars[0].Life = 111;
            engine.Chars[0].Targets.Clear();
            engine.World.Projectiles[0].HitDef.HitDamage = 999;
            engine.World.Explods.Clear();
            engine.Tick(new List<MInput> { MInput.Right, MInput.None });

            engine.Restore(snapshot);

            Assert.That(engine.ComputeHash(), Is.EqualTo(expectedHash));
            Assert.That(engine.FrameNo, Is.EqualTo(snapshot.FrameNo));
        }

        [Test]
        public void SnapshotRestore_RelinksObjectGraphToRestoredInstances()
        {
            MBattleEngine engine = EngineWithLinkedEntities();
            MBattleEngineSnapshot snapshot = engine.Snapshot();

            engine.Restore(snapshot);

            MChar p1 = engine.Chars[0];
            MChar p2 = engine.Chars[1];
            MChar helper = engine.Helpers[0];
            MProjectile projectile = engine.World.Projectiles[0];

            Assert.Multiple(() =>
            {
                Assert.That(ReferenceEquals(p1.P2, p2), Is.True);
                Assert.That(ReferenceEquals(p2.P2, p1), Is.True);
                Assert.That(ReferenceEquals(helper.Parent, p1), Is.True);
                Assert.That(ReferenceEquals(helper.Root, p1), Is.True);
                Assert.That(ReferenceEquals(helper.P2, p2), Is.True);
                Assert.That(ReferenceEquals(p1.Targets[0], p2), Is.True);
                Assert.That(ReferenceEquals(p2.BindTarget, p1), Is.True);
                Assert.That(ReferenceEquals(projectile.Owner, p1), Is.True);
                Assert.That(ReferenceEquals(p1.Rng, engine.Random), Is.True);
                Assert.That(ReferenceEquals(helper.World, engine.World), Is.True);
            });
        }

        [Test]
        public void SnapshotRestore_RestoresRecentDeterministicFields()
        {
            MBattleEngine engine = EngineWithLinkedEntities();
            MChar p1 = engine.Chars[0];
            p1.PendingTransition = new MStateTransition
            {
                Active = true,
                StateNo = 220,
                OwnerPlayerNo = p1.PlayerNo,
                AnimNo = 5,
                Ctrl = 1,
                InitPending = true,
                ReturningToSelf = true,
            };
            p1.Ghv.GuardCtrlTimeLeft = 6;
            p1.WidthEdgeBackSet = true;
            p1.BindTime = -1;
            engine.World.Explods[0].BindTime = -1;
            engine.World.Explods[0].IgnoreHitPause = false;
            engine.Stage.SetSymmetric(150);
            engine.NoDamage = true;
            engine.EnableDemoAutoTurnFallback = false;
            ulong expectedHash = engine.ComputeHash();
            MBattleEngineSnapshot snapshot = engine.Snapshot();

            p1.PendingTransition = MStateTransition.None;
            p1.Ghv.GuardCtrlTimeLeft = 0;
            p1.WidthEdgeBackSet = false;
            p1.BindTime = 0;
            engine.World.Explods[0].BindTime = 3;
            engine.World.Explods[0].IgnoreHitPause = true;
            engine.Stage.SetSymmetric(300);
            engine.NoDamage = false;
            engine.EnableDemoAutoTurnFallback = true;

            engine.Restore(snapshot);

            Assert.That(engine.ComputeHash(), Is.EqualTo(expectedHash));
            Assert.That(engine.Chars[0].PendingTransition.InitPending, Is.True);
            Assert.That(engine.Chars[0].PendingTransition.ReturningToSelf, Is.True);
            Assert.That(engine.Chars[0].Ghv.GuardCtrlTimeLeft, Is.EqualTo(6));
            Assert.That(engine.Chars[0].WidthEdgeBackSet, Is.True);
            Assert.That(engine.Chars[0].BindTime, Is.EqualTo(-1));
            Assert.That(engine.World.Explods[0].BindTime, Is.EqualTo(-1));
            Assert.That(engine.World.Explods[0].IgnoreHitPause, Is.False);
            Assert.That(engine.Stage.BoundsEnabled, Is.True);
            Assert.That(engine.Stage.BoundLeft.Raw, Is.EqualTo(FFloat.FromInt(-150).Raw));
            Assert.That(engine.Stage.BoundRight.Raw, Is.EqualTo(FFloat.FromInt(150).Raw));
            Assert.That(engine.NoDamage, Is.True);
            Assert.That(engine.EnableDemoAutoTurnFallback, Is.False);
        }

        [Test]
        public void ComputeHash_IncludesEngineStageAndControlFields()
        {
            MBattleEngine baseline = EngineWithLinkedEntities();
            MBattleEngine changedStage = EngineWithLinkedEntities();
            MBattleEngine changedNoDamage = EngineWithLinkedEntities();
            MBattleEngine changedTurnFallback = EngineWithLinkedEntities();

            changedStage.Stage.SetSymmetric(160);
            changedNoDamage.NoDamage = true;
            changedTurnFallback.EnableDemoAutoTurnFallback = false;

            ulong baselineHash = baseline.ComputeHash();

            Assert.That(changedStage.ComputeHash(), Is.Not.EqualTo(baselineHash));
            Assert.That(changedNoDamage.ComputeHash(), Is.Not.EqualTo(baselineHash));
            Assert.That(changedTurnFallback.ComputeHash(), Is.Not.EqualTo(baselineHash));
        }

        [Test]
        public void SnapshotRestore_ReplayMatchesStraightRunHash()
        {
            MBattleEngine straight = EngineWithLinkedEntities();
            MBattleEngine replay = EngineWithLinkedEntities();
            List<MInput> none = new List<MInput> { MInput.None, MInput.None };
            List<MInput> move = new List<MInput> { MInput.Right, MInput.Left };

            straight.Tick(none);
            replay.Tick(none);
            MBattleEngineSnapshot snapshot = replay.Snapshot();

            straight.Tick(move);
            straight.Tick(none);

            replay.Tick(move);
            replay.Chars[0].Life = 321;
            replay.Restore(snapshot);
            replay.Tick(move);
            replay.Tick(none);

            Assert.That(replay.ComputeHash(), Is.EqualTo(straight.ComputeHash()));
        }

        [Test]
        public void EntityWorldClone_DeepCopiesQueuedProjectileHitDef()
        {
            MChar owner = new MChar { Id = 7 };
            MEntityWorld world = new MEntityWorld();
            world.Projectiles.Add(new MProjectile
            {
                Id = 100,
                OwnerId = owner.Id,
                Owner = owner,
                ProjId = 2,
                HitDef = new MHitDef { Active = true, HitDamage = 6 },
            });
            world.RequestProjectile(new MProjectileRequest
            {
                Owner = owner,
                ProjId = 3,
                HitDef = new MHitDef { Active = true, HitDamage = 12 },
            });
            ulong originalHash = HashOf(world);

            MEntityWorld clone = world.Clone();
            clone.Projectiles[0].HitDef.HitDamage = 33;
            clone.ProjSpawnQueue[0].HitDef.HitDamage = 99;

            Assert.That(world.Projectiles[0].HitDef.HitDamage, Is.EqualTo(6));
            Assert.That(world.ProjSpawnQueue[0].HitDef.HitDamage, Is.EqualTo(12));
            Assert.That(HashOf(world), Is.EqualTo(originalHash));
            Assert.That(HashOf(clone), Is.Not.EqualTo(originalHash));
        }
    }
}
