using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;
using Lockstep.Tests;
using Lockstep.Tests.Mugen;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// R-ROUND 入场鞠躬 / 胜利姿态：回合 Intro 期角色进入入场态（191，KFM 鞠躬），Fight 期回中立 0，
    /// KO 后胜者进 win pose（195）。无入场/胜利态的角色不受影响（候选存在性保护）。
    /// </summary>
    [TestFixture]
    public sealed class MRoundIntroTests
    {
        // 带入场态 191（anim 190）与胜利态 195 的合成角色。
        const string Cns =
            "[Statedef 0]\ntype=S\nphysics=S\nanim=0\n" +
            "[Statedef 191]\ntype=S\nctrl=0\nanim=190\n" +
            "[Statedef 195]\ntype=S\nctrl=0\nanim=195\n";
        const string LongIntroCns =
            "[Statedef 0]\ntype=S\nphysics=S\nanim=0\n" +
            "[Statedef 191]\ntype=S\nctrl=0\nanim=190\n" +
            "[State 191, hold]\ntype=AssertSpecial\ntrigger1=time<5\nflag=Intro\n" +
            "[State 191, done]\ntype=ChangeState\ntrigger1=time>=5\nvalue=0\n";
        const string RoundStateIntroCns =
            "[Statedef 0]\ntype=S\nphysics=S\nanim=0\n" +
            "[Statedef 191]\ntype=S\nctrl=0\nanim=190\n" +
            "[State 191, hold]\ntype=AssertSpecial\ntrigger1=roundstate = 1 && time < 5\nflag=Intro\n" +
            "[State 191, done]\ntype=ChangeState\ntrigger1=time>=5\nvalue=0\n";
        const string PermanentCustomIntroCns =
            "[Statedef 0]\ntype=S\nphysics=S\nanim=0\n" +
            "[Statedef 191]\ntype=S\nctrl=0\nanim=190\n" +
            "[State 191, custom]\ntype=ChangeState\ntrigger1=1\nvalue=195\n" +
            "[Statedef 195]\ntype=S\nctrl=0\nanim=195\n" +
            "[State 195, hold]\ntype=AssertSpecial\ntrigger1=1\nflag=Intro\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n[Begin Action 190]\n190,0, 0,0, 4\n[Begin Action 195]\n195,0, 0,0, 4\n";

        static readonly List<MInput> NoInput = new List<MInput> { MInput.None, MInput.None };

        static MBattleEngine TwoCharEngine()
        {
            return TwoCharEngine(Cns);
        }

        static MBattleEngine TwoCharEngine(string cns)
        {
            MCharData data = MCharLoader.Load(new[] { cns }, cns, null, Air, null, "Bower");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Chars[0].Id = 1;
            engine.Chars[1].Id = 2;
            engine.Chars[0].Life = engine.Chars[0].LifeMax = 1000;
            engine.Chars[1].Life = engine.Chars[1].LifeMax = 1000;
            engine.LinkPair();
            return engine;
        }

        [Test]
        public void Intro_SendsBothToBowState_ThenFightReturnsToNeutral()
        {
            MBattleEngine engine = TwoCharEngine();
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 3 };

            round.Tick(NoInput);   // 应用开局排队的入场态
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(191), "入场期鞠躬态");
            Assert.That(engine.Chars[1].StateNo, Is.EqualTo(191));

            for (int f = 0; f < 5; f++)
            {
                round.Tick(NoInput);
            }
            Assert.That(round.State, Is.EqualTo(MRoundState.Fight));
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(0), "Fight 后离开鞠躬回中立");
            Assert.That(engine.Chars[1].StateNo, Is.EqualTo(0));
            Assert.IsTrue(engine.Chars[0].Ctrl);
        }

        [Test]
        public void IntroAssert_DelaysFightUntilIntroStateFinishes()
        {
            MBattleEngine engine = TwoCharEngine(LongIntroCns);
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 1 };

            round.Tick(NoInput);   // apply queued intro state
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(191));
            Assert.That(round.State, Is.EqualTo(MRoundState.Intro));

            for (int f = 0; f < 4; f++)
            {
                round.Tick(NoInput);
                Assert.That(round.State, Is.EqualTo(MRoundState.Intro), "AssertSpecial Intro should keep the round out of Fight");
                Assert.IsFalse(engine.Chars[0].Ctrl);
                Assert.IsFalse(engine.Chars[0].KeyCtrl);
            }

            for (int f = 0; f < 4 && round.State != MRoundState.Fight; f++)
            {
                round.Tick(NoInput);
            }

            Assert.That(round.State, Is.EqualTo(MRoundState.Fight));
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(0));
            Assert.IsTrue(engine.Chars[0].Ctrl);
            Assert.IsTrue(engine.Chars[0].KeyCtrl);
        }

        [Test]
        public void RoundStateTriggerIntroAssert_DelaysFightUntilIntroStateFinishes()
        {
            MBattleEngine engine = TwoCharEngine(RoundStateIntroCns);
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 1 };

            Assert.That(engine.Chars[0].RoundState, Is.EqualTo(1));
            round.Tick(NoInput);
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(191));
            Assert.That(engine.Chars[0].RoundState, Is.EqualTo(1));

            for (int f = 0; f < 4; f++)
            {
                round.Tick(NoInput);
                Assert.That(round.State, Is.EqualTo(MRoundState.Intro), "roundstate=1 intro assert should hold the round out of Fight");
                Assert.That(engine.Chars[0].RoundState, Is.EqualTo(1));
                Assert.IsFalse(engine.Chars[0].Ctrl);
                Assert.IsFalse(engine.Chars[0].KeyCtrl);
            }

            for (int f = 0; f < 4 && round.State != MRoundState.Fight; f++)
            {
                round.Tick(NoInput);
            }

            Assert.That(round.State, Is.EqualTo(MRoundState.Fight));
            Assert.That(engine.Chars[0].RoundState, Is.EqualTo(2));
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(0));
            Assert.IsTrue(engine.Chars[0].Ctrl);
            Assert.IsTrue(engine.Chars[0].KeyCtrl);
        }

        [Test]
        public void PermanentCustomIntroAssert_IsCappedAndReturnsToNeutral()
        {
            MBattleEngine engine = TwoCharEngine(PermanentCustomIntroCns);
            MRoundSystem round = new MRoundSystem(engine)
            {
                IntroTime = 1,
                MaxAssertIntroHoldTime = 3,
            };

            bool sawCustomIntro = false;
            for (int f = 0; f < 20 && round.State != MRoundState.Fight; f++)
            {
                round.Tick(NoInput);
                sawCustomIntro |= engine.Chars[0].StateNo == 195;
            }

            Assert.IsTrue(sawCustomIntro, "test must exercise a custom intro state outside the default 190/191 candidates");
            Assert.That(round.State, Is.EqualTo(MRoundState.Fight), "permanent AssertSpecial Intro must not lock the round forever");
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(0));
            Assert.IsTrue(engine.Chars[0].Ctrl);
            Assert.IsTrue(engine.Chars[0].KeyCtrl);
        }

        [Test]
        public void JanosVersusKfm_IntroAssertDoesNotKeepMatchAtSixtySeconds()
        {
            string janosDir = TestAssets.CharDir("Janos");
            string kfmDir = TestAssets.KfmDir();
            if (!Directory.Exists(janosDir) || !Directory.Exists(kfmDir))
            {
                Assert.Ignore("MugenSource Janos/kfm assets are not available.");
            }

            MCharData janos = MugenCharacterPackageTestLoader.Load(janosDir);
            MCharData kfm = MugenCharacterPackageTestLoader.Load(kfmDir);
            MTeamMatch match = new MTeamMatch(
                new List<MCharData> { janos },
                new List<MCharData> { kfm },
                r =>
                {
                    r.IntroTime = 1;
                    r.MaxAssertIntroHoldTime = 10;
                });

            for (int f = 0; f < 60 && match.Round.State != MRoundState.Fight; f++)
            {
                match.Tick(NoInput);
            }

            Assert.That(match.Round.State, Is.EqualTo(MRoundState.Fight));
            for (int f = 0; f < 60; f++)
            {
                match.Tick(NoInput);
            }

            Assert.That(match.Round.TimerSeconds, Is.LessThan(60));
            Assert.IsTrue(match.Engine.Chars[0].Ctrl);
            Assert.IsTrue(match.Engine.Chars[1].Ctrl);
        }

        [Test]
        public void KO_SendsWinnerToWinPose()
        {
            MBattleEngine engine = TwoCharEngine();
            MRoundSystem round = new MRoundSystem(engine)
            {
                IntroTime = 2, OverWaitTime = 2, WinPoseTime = 30,
            };

            for (int f = 0; f < 4; f++) { round.Tick(NoInput); }   // 进入 Fight
            Assert.That(round.State, Is.EqualTo(MRoundState.Fight));

            engine.Chars[1].Life = 0;   // P2 被 KO → P1（index0）胜
            for (int f = 0; f < 6; f++) { round.Tick(NoInput); }

            Assert.That(round.State, Is.EqualTo(MRoundState.Over));
            Assert.That(round.Winner, Is.EqualTo(0));
            Assert.That(round.RoundsWon[0], Is.EqualTo(1));
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(195), "胜者摆 win pose");
        }

        [Test]
        public void TimerSeconds_RoundsUp()
        {
            MBattleEngine engine = TwoCharEngine();
            MRoundSystem round = new MRoundSystem(engine);
            round.RoundTime = 90;   // 90 tick = 1.5s（构造后设置：Timer 在构造时已按默认 RoundTime 缓存，这里直接设 Timer）
            round.Timer = 90;
            Assert.That(round.TimerSeconds, Is.EqualTo(2));
        }
    }
}
