using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// R-ROUND：回合状态机 intro→fight→KO/timeover→over→下一回合/结算。
    /// 离散对照 RoundState 序列、winner id、回合记分、整场结束。oracle：手算 Ikemen system.go roundState/stepRoundState 逻辑。
    /// </summary>
    [TestFixture]
    public sealed class MRoundSystemTests
    {
        // 最小可推进的合成角色：站立状态 0（VelSet 0，避免移动），有受击/物理但不互相命中（除非测试构造）。
        const string Cns = "[Statedef 0]\ntype = S\nphysics = S\nanim = 0\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n";

        static MBattleEngine TwoCharEngine()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "Dummy");
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

        static readonly List<MInput> NoInput = new List<MInput> { MInput.None, MInput.None };

        [Test]
        public void Intro_GrantsNoCtrl_ThenFightGrantsCtrl()
        {
            MBattleEngine engine = TwoCharEngine();
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 3 };
            Assert.That(round.State, Is.EqualTo(MRoundState.Intro));

            round.Tick(NoInput);   // intro f0
            Assert.IsFalse(engine.Chars[0].Ctrl, "入场期无控制权");

            for (int f = 0; f < 5; f++)
            {
                round.Tick(NoInput);
            }
            Assert.That(round.State, Is.EqualTo(MRoundState.Fight), "满 IntroTime 进入战斗");
            Assert.IsTrue(engine.Chars[0].Ctrl, "战斗期授予控制权");
            Assert.IsTrue(engine.Chars[1].Ctrl);
        }

        [Test]
        public void KO_TransitionsThroughPreOverToOver_AndScores()
        {
            MBattleEngine engine = TwoCharEngine();
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 1, OverWaitTime = 2, WinPoseTime = 2 };
            List<MRoundState> seq = new List<MRoundState>();
            void Step()
            {
                round.Tick(NoInput);
                if (seq.Count == 0 || seq[seq.Count - 1] != round.State)
                {
                    seq.Add(round.State);
                }
            }

            // 进入战斗
            Step(); Step();
            Assert.That(round.State, Is.EqualTo(MRoundState.Fight));

            // P2 被 KO（直接置血，模拟受击致死）
            engine.Chars[1].Life = 0;
            Step();   // 检测到 KO → PreOver
            Assert.That(round.State, Is.EqualTo(MRoundState.PreOver));
            Assert.That(round.Winner, Is.EqualTo(0), "P1 胜（P2 被 KO）");

            for (int f = 0; f < 10; f++)
            {
                Step();
                if (round.State == MRoundState.Over)
                {
                    break;
                }
            }
            Assert.That(round.RoundsWon[0], Is.EqualTo(1), "进 Over 记 P1 一胜");
            Assert.That(seq, Is.EqualTo(new[] { MRoundState.Intro, MRoundState.Fight, MRoundState.PreOver, MRoundState.Over }),
                "回合状态序列 intro→fight→preover→over");
        }

        [Test]
        public void TimeOver_HigherLifeWins()
        {
            MBattleEngine engine = TwoCharEngine();
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 1, RoundTime = 3, OverWaitTime = 1, WinPoseTime = 1 };
            round.Tick(NoInput); round.Tick(NoInput);   // 进战斗
            Assert.That(round.State, Is.EqualTo(MRoundState.Fight));
            engine.Chars[0].Life = 500;
            engine.Chars[1].Life = 800;   // P2 血高
            int decidedWinner = -2;
            for (int f = 0; f < 6; f++)
            {
                round.Tick(NoInput);
                if (round.State == MRoundState.PreOver && decidedWinner == -2)
                {
                    decidedWinner = round.Winner;   // 在进入下一回合复位前捕获
                }
            }
            Assert.That(decidedWinner, Is.EqualTo(1), "超时血量高者(P2)胜");
        }

        [Test]
        public void DoubleKO_IsDraw()
        {
            MBattleEngine engine = TwoCharEngine();
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 1 };
            round.Tick(NoInput); round.Tick(NoInput);
            engine.Chars[0].Life = 0;
            engine.Chars[1].Life = 0;
            round.Tick(NoInput);
            Assert.That(round.State, Is.EqualTo(MRoundState.PreOver));
            Assert.That(round.Winner, Is.EqualTo(-1), "双 KO 同血 = 平局");
        }

        [Test]
        public void FullMatch_BestOfThree_EndsWhenOneSideWinsTwo()
        {
            MBattleEngine engine = TwoCharEngine();
            MRoundSystem round = new MRoundSystem(engine)
            {
                IntroTime = 1, OverWaitTime = 1, WinPoseTime = 1, RoundsToWin = 2,
            };
            int guard = 0;
            while (!round.MatchOver && guard++ < 2000)
            {
                // 每进入战斗就立刻 KO 掉 P2，让 P1 连胜
                if (round.State == MRoundState.Fight)
                {
                    engine.Chars[1].Life = 0;
                }
                round.Tick(NoInput);
            }
            Assert.IsTrue(round.MatchOver, "整场应结束");
            Assert.That(round.MatchWinner, Is.EqualTo(0), "P1 两胜拿下整场");
            Assert.That(round.RoundsWon[0], Is.EqualTo(2));
            Assert.That(round.RoundNo, Is.EqualTo(2), "打到第 2 回合即 2:0 结束");
        }

        [Test]
        public void NextRound_ResetsCombatantsToFullLife()
        {
            MBattleEngine engine = TwoCharEngine();
            MRoundSystem round = new MRoundSystem(engine)
            {
                IntroTime = 1, OverWaitTime = 1, WinPoseTime = 1, RoundsToWin = 3,
            };
            // 跑完第 1 回合（P1 胜）
            int guard = 0;
            while (round.RoundNo == 1 && guard++ < 500)
            {
                if (round.State == MRoundState.Fight)
                {
                    engine.Chars[1].Life = 0;
                }
                round.Tick(NoInput);
            }
            Assert.That(round.RoundNo, Is.EqualTo(2), "进入第 2 回合");
            Assert.That(engine.Chars[1].Life, Is.EqualTo(engine.Chars[1].LifeMax), "新回合 P2 满血复位");
            Assert.That(engine.Chars[0].Life, Is.EqualTo(engine.Chars[0].LifeMax), "新回合 P1 满血复位");
        }

        [Test]
        public void Deterministic_SameScriptSameHash()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = TwoCharEngine();
                MRoundSystem round = new MRoundSystem(engine) { IntroTime = 2, OverWaitTime = 2, WinPoseTime = 2 };
                for (int f = 0; f < 30; f++)
                {
                    if (f == 10)
                    {
                        engine.Chars[1].Life = 0;
                    }
                    round.Tick(NoInput);
                }
                Lockstep.Core.Hash64 h = new Lockstep.Core.Hash64();
                h.AddInt32((int)round.State);
                round.WriteHash(ref h);
                h.AddInt32((int)(engine.ComputeHash() & 0x7fffffff));
                return h.Value;
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()), "回合状态机 + 引擎逐帧确定性一致");
        }
    }
}
