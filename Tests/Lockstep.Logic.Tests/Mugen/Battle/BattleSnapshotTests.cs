using System.Collections.Generic;
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
    }
}
