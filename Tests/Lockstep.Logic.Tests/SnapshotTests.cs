using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Math;

namespace Lockstep.Tests
{
    /// <summary>T0.5：新增组件的快照/还原契约验收（含数组字段深拷）。</summary>
    [TestFixture]
    public sealed class SnapshotTests
    {
        static World BuildWorld()
        {
            World world = new World();
            world.Init(12345);
            Entity entity = world.CreateEntity();
            entity.Add(new AnimC { AnimNo = 200, FrameIndex = 2, ElemTime = 1, AnimTime = 5 });

            VarsC vars = new VarsC();
            vars.Var[3] = 7;
            vars.FVar[1] = FFloat.FromInt(3) / FFloat.Two;
            entity.Add(vars);

            entity.Add(new CommandStateC { Active = new bool[] { true, false, true } });
            entity.Add(new HitDefStateC { Active = true, HitTargetsBits = 0b101UL });
            entity.Add(new CharacterRefC { CharacterId = 1 });
            return world;
        }

        [Test]
        public void SnapshotRestore_RoundTrip_RestoresHash()
        {
            World world = BuildWorld();
            ulong before = world.ComputeHash();
            WorldSnapshot snap = world.Snapshot();

            world.Entities[0].Get<AnimC>().FrameIndex = 99;
            world.Entities[0].Get<VarsC>().Var[3] = 999;
            Assert.That(world.ComputeHash(), Is.Not.EqualTo(before), "改了状态哈希应变");

            world.Restore(snap);
            Assert.That(world.ComputeHash(), Is.EqualTo(before), "还原后哈希应复原");
        }

        [Test]
        public void ArrayFields_AreDeepCopied_NotAliased()
        {
            World world = BuildWorld();
            ulong before = world.ComputeHash();
            WorldSnapshot snap = world.Snapshot();

            // 改原 world 的数组元素；若 Clone 没深拷，快照会被串、Restore 复原不了
            world.Entities[0].Get<VarsC>().FVar[1] = FFloat.FromInt(100);
            world.Restore(snap);

            Assert.That(world.ComputeHash(), Is.EqualTo(before), "数组字段必须深拷");
        }
    }
}
