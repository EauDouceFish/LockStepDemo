using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Battle.Debugging;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Logic.Tests.Mugen.Battle.Debugging
{
    /// <summary>
    /// 战斗调试修改器命令面：改血/上帝/跳过场/计时/强制胜/暂停单步。
    /// 这是开发调试工具，不进哈希路径；测试只验证命令对运行态的可见效果正确。
    /// </summary>
    [TestFixture]
    public sealed class MBattleDebugControllerTests
    {
        const string Cns = "[Statedef 0]\ntype = S\nphysics = S\nanim = 0\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n";
        static readonly List<MInput> NoInput = new List<MInput> { MInput.None, MInput.None };

        static (MBattleEngine, MRoundSystem, MBattleDebugController) Build()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "Dummy");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Chars[0].Id = 1;
            engine.Chars[1].Id = 2;
            engine.LinkPair();
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 2 };
            return (engine, round, new MBattleDebugController(engine, round));
        }

        [Test]
        public void SetHp_ClampsToLifeMax()
        {
            (MBattleEngine engine, _, MBattleDebugController dbg) = Build();
            dbg.Execute("sethp 1 250");
            Assert.That(engine.Chars[1].Life, Is.EqualTo(250));
            dbg.Execute("sethp 1 999999");
            Assert.That(engine.Chars[1].Life, Is.EqualTo(engine.Chars[1].LifeMax), "超上限夹取");
        }

        [Test]
        public void SetHpPercent_Works()
        {
            (MBattleEngine engine, _, MBattleDebugController dbg) = Build();
            dbg.Execute("sethpp 0 30");
            Assert.That(engine.Chars[0].Life, Is.EqualTo(engine.Chars[0].LifeMax * 30 / 100));
        }

        [Test]
        public void GodMode_RestoresLifeOnPostTick()
        {
            (MBattleEngine engine, _, MBattleDebugController dbg) = Build();
            dbg.Execute("god 0 on");
            engine.Chars[0].Life = 1;
            dbg.PostTick();
            Assert.That(engine.Chars[0].Life, Is.EqualTo(engine.Chars[0].LifeMax), "上帝模式回满");
            dbg.Execute("god 0 off");
            engine.Chars[0].Life = 1;
            dbg.PostTick();
            Assert.That(engine.Chars[0].Life, Is.EqualTo(1), "关闭后不再回满");
        }

        [Test]
        public void SkipIntro_EntersFightAndGrantsCtrl()
        {
            (MBattleEngine engine, MRoundSystem round, MBattleDebugController dbg) = Build();
            Assert.That(round.State, Is.EqualTo(MRoundState.Intro));
            dbg.Execute("skipintro");
            Assert.That(round.State, Is.EqualTo(MRoundState.Fight));
            Assert.IsTrue(engine.Chars[0].Ctrl);
            Assert.IsTrue(engine.Chars[1].Ctrl);
        }

        [Test]
        public void ForceWin_KosOpponent_RoundResolvesToWinner()
        {
            (MBattleEngine engine, MRoundSystem round, MBattleDebugController dbg) = Build();
            dbg.Execute("win 0");
            Assert.That(engine.Chars[1].Life, Is.EqualTo(0));
            round.Tick(NoInput);   // Fight 期一帧 → CheckRoundEnd 判 P1 胜
            Assert.That(round.Winner, Is.EqualTo(0));
            Assert.That(round.State, Is.EqualTo(MRoundState.PreOver));
        }

        [Test]
        public void Timer_SetsRoundTimerSeconds()
        {
            (_, MRoundSystem round, MBattleDebugController dbg) = Build();
            dbg.Execute("timer 5");
            Assert.That(round.Timer, Is.EqualTo(5 * MRoundSystem.TicksPerSecond));
        }

        [Test]
        public void PauseAndStep_GateTicks()
        {
            (_, _, MBattleDebugController dbg) = Build();
            Assert.IsTrue(dbg.ShouldTick(), "默认放行");
            dbg.Execute("pause");
            Assert.IsFalse(dbg.ShouldTick(), "暂停后不放行");
            dbg.Execute("step 2");
            Assert.IsTrue(dbg.ShouldTick(), "单步放行第 1 帧");
            Assert.IsTrue(dbg.ShouldTick(), "单步放行第 2 帧");
            Assert.IsFalse(dbg.ShouldTick(), "用尽后回到暂停");
            dbg.Execute("resume");
            Assert.IsTrue(dbg.ShouldTick(), "恢复后放行");
        }

        [Test]
        public void SetState_QueuesTransition()
        {
            (MBattleEngine engine, _, MBattleDebugController dbg) = Build();
            string result = dbg.Execute("setstate 1 5000");
            Assert.That(result, Does.Contain("5000"));
        }

        [Test]
        public void StateJson_ContainsRoundAndChars()
        {
            (_, _, MBattleDebugController dbg) = Build();
            string json = dbg.Execute("state");
            Assert.That(json, Does.Contain("\"round\""));
            Assert.That(json, Does.Contain("\"chars\""));
            Assert.That(json, Does.Contain("\"life\""));
        }

        [Test]
        public void UnknownCommand_ReturnsError()
        {
            (_, _, MBattleDebugController dbg) = Build();
            Assert.That(dbg.Execute("frobnicate 9"), Does.StartWith("ERR"));
        }
    }
}
