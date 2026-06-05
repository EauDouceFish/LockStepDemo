using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// 引擎对 Pause/SuperPause/PosFreeze 字段的消费（移植 system.go 全局冻结）：暂停期间被冻结角色跳过
    /// 全部处理（Time 不前进、物理不动），仅施暂停方在其 movetime 窗口可动；命中不结算；计时每帧递减。
    /// 字段由控制器写入（Codex），本测试直接置字段以隔离验证引擎消费逻辑。
    /// </summary>
    [TestFixture]
    public sealed class PauseFreezeTests
    {
        const string Cns =
            "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State 0, idle]\ntype = Null\ntrigger1 = 1\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        static MBattleEngine TwoChars()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "Dummy");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 1), data);
            engine.LinkPair();
            return engine;
        }

        static List<MInput> NoInput()
        {
            return new List<MInput> { MInput.None, MInput.None };
        }

        [Test]
        public void NoPause_BothCharsAdvanceTime()
        {
            MBattleEngine engine = TwoChars();
            engine.Tick(NoInput());
            engine.Tick(NoInput());
            Assert.That(engine.Chars[0].Time, Is.GreaterThan(0));
            Assert.That(engine.Chars[1].Time, Is.GreaterThan(0));
        }

        [Test]
        public void SuperPause_FreezesNonPauser_PauserKeepsActing()
        {
            MBattleEngine engine = TwoChars();
            // char0 发动 SuperPause：自身可动 movetime 帧，char1 被冻结。
            engine.Chars[0].SuperPauseTime = 5;
            engine.Chars[0].SuperPauseMoveTime = 5;
            int t1Before = engine.Chars[1].Time;
            engine.Tick(NoInput());
            Assert.That(engine.Chars[1].Time, Is.EqualTo(t1Before), "被暂停方 Time 冻结");
            Assert.That(engine.Chars[0].Time, Is.GreaterThan(t1Before), "施暂停方在 movetime 窗口内继续");
        }

        [Test]
        public void SuperPause_DecrementsAndExpires_ThenBothResume()
        {
            MBattleEngine engine = TwoChars();
            engine.Chars[0].SuperPauseTime = 3;
            engine.Chars[0].SuperPauseMoveTime = 3;
            for (int f = 0; f < 3; f++) { engine.Tick(NoInput()); }
            Assert.That(engine.Chars[0].SuperPauseTime, Is.EqualTo(0), "SuperPauseTime 递减到 0");
            int t1Frozen = engine.Chars[1].Time;
            // 暂停过后 char1 恢复推进。
            engine.Tick(NoInput());
            Assert.That(engine.Chars[1].Time, Is.GreaterThan(t1Frozen), "暂停结束后被暂停方恢复");
        }

        [Test]
        public void PosFreeze_SkipsPhysicsThisFrame_ThenClears()
        {
            MBattleEngine engine = TwoChars();
            MChar c = engine.Chars[0];
            c.Vel = new FVector3(FFloat.FromInt(5), FFloat.Zero, FFloat.Zero);
            c.PosFreeze = true;
            FFloat xBefore = c.Pos.X;
            engine.Tick(NoInput());
            Assert.That(c.Pos.X.Raw, Is.EqualTo(xBefore.Raw), "PosFreeze 帧位置不变");
            Assert.That(c.PosFreeze, Is.False, "PosFreeze 用后清零（需每帧重新断言）");
            // 下一帧不再冻结 → 位置随速度推进（physics=N 不施摩擦/重力，仅积分）。
            engine.Tick(NoInput());
            Assert.That(c.Pos.X.Raw, Is.Not.EqualTo(xBefore.Raw), "次帧恢复位置积分");
        }

        [Test]
        public void SuperPause_IsDeterministic()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = TwoChars();
                engine.Chars[0].SuperPauseTime = 4;
                engine.Chars[0].SuperPauseMoveTime = 4;
                for (int f = 0; f < 8; f++) { engine.Tick(NoInput()); }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()));
        }
    }
}
