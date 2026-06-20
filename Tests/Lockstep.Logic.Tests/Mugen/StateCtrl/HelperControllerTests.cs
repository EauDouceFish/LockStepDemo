using System.Collections.Generic;
using System.IO;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.StateCtrl;
using Lockstep.Tests;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    /// <summary>
    /// R-ENT 切片 2：Helper/DestroySelf 控制器端到端——CNS 文本里的 [State] type=Helper 经状态机执行 → 引擎造 helper 实体；
    /// helper 状态里的 type=DestroySelf → 帧末移除。用合成 CNS 注入 owner 的负状态，驱动 spawn。
    /// </summary>
    [TestFixture]
    public sealed class HelperControllerTests
    {
        // owner 的 state -1（命令解释器位）无条件请求一个 helper（用 var 防重复请求）。
        const string OwnerCns =
            "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State -1, spawn helper once]\ntype = Helper\ntrigger1 = time = 1\nid = 9\nstateno = 1000\npos = 20, 0\n\n" +
            "[Statedef 1000]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State 1000, selfdestroy]\ntype = DestroySelf\ntrigger1 = time >= 3\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        static MBattleEngine OneCharEngine()
        {
            MCharData data = MCharLoader.Load(new[] { OwnerCns }, OwnerCns, null, Air, null, "Dummy");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static List<MInput> NoInput()
        {
            return new List<MInput> { MInput.None };
        }

        [Test]
        public void DestroySelf_Helper_MarksDestroyedAndStopsControllerChain()
        {
            MChar helper = new MChar { IsHelper = true };

            bool stop = new DestroySelfController().Run(helper);

            Assert.IsTrue(stop);
            Assert.IsTrue(helper.Destroyed);
        }

        [Test]
        public void DestroySelf_Player_IsIgnoredAndKeepsControllerChainRunning()
        {
            MChar player = new MChar { IsHelper = false };

            bool stop = new DestroySelfController().Run(player);

            Assert.IsFalse(stop);
            Assert.IsFalse(player.Destroyed);
        }

        [Test]
        public void HelperController_SpawnsHelperFromCns()
        {
            MBattleEngine engine = OneCharEngine();
            // time=1 触发 Helper（state -1 每帧跑）。前几帧造出 helper。
            for (int f = 0; f < 4; f++) { engine.Tick(NoInput()); }
            Assert.That(engine.Helpers.Count, Is.GreaterThanOrEqualTo(1), "Helper 控制器经状态机造出 helper 实体");
            MChar helper = engine.Helpers[0];
            Assert.That(helper.HelperType, Is.EqualTo(9), "id=9");
            Assert.That(helper.StateNo, Is.EqualTo(1000), "helper 进 stateno=1000");
        }

        [Test]
        public void HelperController_SpawnedHelperStartsRunningOnNextFrame()
        {
            MBattleEngine engine = OneCharEngine();

            engine.Tick(NoInput());
            Assert.That(engine.Helpers.Count, Is.EqualTo(0));

            engine.Tick(NoInput());
            Assert.That(engine.Helpers.Count, Is.EqualTo(1));
            MChar helper = engine.Helpers[0];
            Assert.That(helper.StateNo, Is.EqualTo(1000));
            Assert.That(helper.Time, Is.EqualTo(0), "helper is created at frame end and does not run its state machine immediately");

            engine.Tick(NoInput());
            Assert.That(helper.Time, Is.EqualTo(1), "helper starts on the first frame after DrainSpawns");
        }

        [Test]
        public void HelperController_HelperDestroysSelf()
        {
            MBattleEngine engine = OneCharEngine();
            // 跑足够帧：helper 造出后在 state 1000 里 time>=3 触发 DestroySelf。
            int maxHelpers = 0;
            for (int f = 0; f < 20; f++)
            {
                engine.Tick(NoInput());
                if (engine.Helpers.Count > maxHelpers) { maxHelpers = engine.Helpers.Count; }
            }
            Assert.That(maxHelpers, Is.GreaterThanOrEqualTo(1), "曾造出 helper");
            // 注：state -1 每帧 time=1 不再成立（time 单调增），故只造一次；之后 DestroySelf 移除，稳态 0。
            Assert.That(engine.Helpers.Count, Is.EqualTo(0), "helper DestroySelf 后被移除，稳态无残留");
        }
    }
}
