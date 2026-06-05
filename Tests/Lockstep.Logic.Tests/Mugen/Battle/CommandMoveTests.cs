using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
using Lockstep.Tests;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// 回归：搓招/出招端到端。.cmd 不仅含 [Command] 定义，还含 [Statedef -1] 命令解释器
    /// （command="x" → ChangeState 出招）。修复前 MCharLoader 只解析命令定义、不解析 .cmd 的
    /// 状态块 → States 缺 -1 → 命令能匹配但无人消费 → 角色完全不出招。本测试守住该链路。
    /// </summary>
    [TestFixture]
    public sealed class CommandMoveTests
    {
        static MBattleEngine LoadKfm()
        {
            string kfmDir = TestAssets.KfmDir();
            string common = TestAssets.Common1Cns();
            string cns = Path.Combine(kfmDir, "kfm.cns");
            string cmd = Path.Combine(kfmDir, "kfm.cmd");
            string air = Path.Combine(kfmDir, "kfm.air");
            if (!File.Exists(cns) || !File.Exists(air) || !File.Exists(common) || !File.Exists(cmd))
            {
                Assert.Ignore("KFM/common1 素材缺失，跳过。");
            }
            MCharData data = MCharLoader.Load(
                new[] { File.ReadAllText(cns) }, File.ReadAllText(cns),
                File.ReadAllText(common), File.ReadAllText(air), File.ReadAllText(cmd), "kfm");
            MChar kfm = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            MBattleEngine engine = new MBattleEngine();
            engine.Add(kfm, data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        [Test]
        public void Loader_ParsesCmdStatedefMinus1_AsCommandInterpreter()
        {
            MBattleEngine engine = LoadKfm();
            Assert.That(engine.Data[0].States.ContainsKey(-1), Is.True,
                ".cmd 的 [Statedef -1] 命令解释器必须并入 States");
            Assert.That(engine.Data[0].Commands.Count, Is.GreaterThan(0), "命令定义也应解析");
        }

        // 在动画已建立 AnimTime 的真实帧按键（对齐人类操作；回合刚开始第 0 帧 AnimTime 尚未建立，
        // 属另一时序边缘，单列追踪，不在本回归范围）。
        static bool PressAfterWarmup(MBattleEngine engine, MInput button, int targetState, int frames = 12)
        {
            MChar c = engine.Chars[0];
            List<MInput> none = new List<MInput> { MInput.None };
            for (int f = 0; f < 5; f++) { engine.Tick(none); }   // 暖机：让站立动画建立 AnimTime
            for (int f = 0; f < frames; f++)
            {
                engine.Tick(f == 0 ? new List<MInput> { button } : none);   // 仅首帧按下（边沿）
                if (c.StateNo == targetState) { return true; }
            }
            return false;
        }

        [Test]
        public void PressKick_A_EntersLightKick230()
        {
            MBattleEngine engine = LoadKfm();
            Assert.That(PressAfterWarmup(engine, MInput.A, 230), Is.True, "按 A → 站立轻脚态 230");
        }

        [Test]
        public void PressPunch_X_EntersLightPunch200()
        {
            MBattleEngine engine = LoadKfm();
            Assert.That(PressAfterWarmup(engine, MInput.X, 200), Is.True, "按 X → 站立轻拳态 200");
        }

        [Test]
        public void PressPunch_Y_EntersStrongPunch210()
        {
            // 强拳 command="y" → 210（kfm.cmd 站立强拳）
            MBattleEngine engine = LoadKfm();
            Assert.That(PressAfterWarmup(engine, MInput.Y, 210), Is.True, "按 Y → 站立强拳态 210");
        }
    }
}
