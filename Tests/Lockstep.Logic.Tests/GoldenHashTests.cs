using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Game.Components;

namespace Lockstep.Tests
{
    /// <summary>
    /// T0.6：第一个黄金哈希测试。锁住 "World + 新组件" 的哈希契约 ——
    /// 任何无意改动（改字段、改 WriteHash 顺序）都会让 MatchesGolden 变红。
    /// 注：此时还没有数据驱动 System，Tick 仅推进 Frame；真正"逻辑跑 N 帧"的黄金测试 Phase 2 起补。
    /// </summary>
    [TestFixture]
    public sealed class GoldenHashTests
    {
        // 由首次运行实际值填入（见提交说明）。
        const ulong Golden10Frames = 15945274352023983870UL;

        static World BuildWorld()
        {
            World world = new World();
            world.Init(777);
            Entity entity = world.CreateEntity();
            entity.Add(new AnimC { AnimNo = 0 });
            entity.Add(new VarsC());
            entity.Add(new CharacterRefC { CharacterId = 0 });
            return world;
        }

        static ulong RunTenFrames()
        {
            World world = BuildWorld();
            for (int i = 0; i < 10; i++)
            {
                world.Tick();
            }
            return world.ComputeHash();
        }

        [Test]
        public void TenFrames_IsDeterministic()
        {
            Assert.That(RunTenFrames(), Is.EqualTo(RunTenFrames()), "同输入必须同结果");
        }

        [Test]
        public void TenFrames_MatchesGolden()
        {
            Assert.That(RunTenFrames(), Is.EqualTo(Golden10Frames));
        }
    }
}
