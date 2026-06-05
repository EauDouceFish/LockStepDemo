using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// Pause/SuperPause 按 Ikemen 模型（全局 sys.pausetime/supertime + 每角色 movetime + pauseBool/acttmp，
    /// 移植 system.go:2562 + char.go:8920/8941/11421/11524）。控制器经 SetSuperPause 写 buffer，下一帧 Step 生效；
    /// 施暂停方在 movetime 帧内可动、之后冻结；被暂停方全程冻结；命中不结算；计时确定性递减。
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
        public void SuperPause_BufferAppliesNextFrame_FreezesNonPauser_PauserActs()
        {
            MBattleEngine engine = TwoChars();
            engine.Tick(NoInput());   // 暖机
            MChar pauser = engine.Chars[0];
            MChar other = engine.Chars[1];
            pauser.SetSuperPause(pausetime: 6, movetime: 6, unhittable: false);   // 写 buffer，本帧未生效
            int otherTimeBefore = other.Time;
            int pauserTimeBefore = pauser.Time;

            engine.Tick(NoInput());   // Step 应用 buffer → SuperTime=6 生效；pauser movetime>0 可动、other 冻结
            engine.Tick(NoInput());

            Assert.That(other.Time, Is.EqualTo(otherTimeBefore), "被暂停方全程冻结（Time 不变）");
            Assert.That(pauser.Time, Is.GreaterThan(pauserTimeBefore), "施暂停方在 movetime 窗口内继续");
        }

        [Test]
        public void SuperPause_ExpiresAfterDuration_NonPauserResumes()
        {
            MBattleEngine engine = TwoChars();
            engine.Tick(NoInput());
            MChar other = engine.Chars[1];
            engine.Chars[0].SetSuperPause(pausetime: 4, movetime: 4, unhittable: false);
            for (int f = 0; f < 10; f++) { engine.Tick(NoInput()); }   // 跑满整个暂停
            int otherFrozen = other.Time;
            engine.Tick(NoInput());
            Assert.That(other.Time, Is.GreaterThan(otherFrozen), "暂停结束后被暂停方恢复推进");
            Assert.That(engine.PauseState.SuperTime, Is.EqualTo(0), "SuperTime 归零");
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
            engine.Tick(NoInput());
            Assert.That(c.Pos.X.Raw, Is.Not.EqualTo(xBefore.Raw), "次帧恢复位置积分");
        }

        [Test]
        public void SuperPause_IsDeterministic()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = TwoChars();
                engine.Tick(NoInput());
                engine.Chars[0].SetSuperPause(pausetime: 5, movetime: 5, unhittable: false);
                for (int f = 0; f < 10; f++) { engine.Tick(NoInput()); }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()));
        }
    }
}
