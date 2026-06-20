using System;
using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// 车轮战 / turns 模式：1v1 同屏，每名玩家一队（3 名）轮流出场，被 KO 换下一员，整队打光判负。
    /// 胜方角色留场（索引不前进），负方索引前进。平局双方各损一员。
    /// </summary>
    [TestFixture]
    public sealed class MTeamMatchTests
    {
        const string Cns = "[Statedef 0]\ntype=S\nphysics=S\nanim=0\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n";
        static readonly List<MInput> NoInput = new List<MInput> { MInput.None, MInput.None };

        static MCharData NewData() => MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "Team");

        static List<MCharData> Roster(int n)
        {
            List<MCharData> list = new List<MCharData>();
            for (int i = 0; i < n; i++) { list.Add(NewData()); }
            return list;
        }

        // 小回合计时，快速跑完每场。
        static readonly Action<MRoundSystem> Fast = r =>
        {
            r.IntroTime = 1; r.OverWaitTime = 1; r.OverHitTime = 1; r.WinPoseTime = 1;
        };

        // 跑到当前场分出胜负（每个战斗帧 KO 掉指定要输的玩家的当前角色）。
        static void RunBout(MTeamMatch m, int loser)
        {
            int bout = m.BoutNo;
            int guard = 0;
            while (m.BoutNo == bout && !m.MatchOver && guard++ < 1000)
            {
                if (m.Round.State == MRoundState.Fight)
                {
                    m.Engine.Chars[loser].Life = 0;
                }
                m.Tick(NoInput);
            }
        }

        static void RunBoutWithWinnerLife(MTeamMatch m, int winner, int winnerLife)
        {
            int loser = 1 - winner;
            int bout = m.BoutNo;
            int guard = 0;
            while (m.BoutNo == bout && !m.MatchOver && guard++ < 1000)
            {
                if (m.Round.State == MRoundState.Fight)
                {
                    m.Engine.Chars[winner].Life = winnerLife;
                    m.Engine.Chars[loser].Life = 0;
                }
                m.Tick(NoInput);
            }
        }

        [Test]
        public void Player1RosterWipedOut_Player0WinsMatch()
        {
            MTeamMatch m = new MTeamMatch(Roster(3), Roster(3), Fast);
            RunBout(m, loser: 1);   // bout1: P1 char#0 阵亡
            Assert.IsFalse(m.MatchOver);
            Assert.That(m.ActiveIndex(1), Is.EqualTo(1), "负方换下一员");
            Assert.That(m.ActiveIndex(0), Is.EqualTo(0), "胜方留场");

            RunBout(m, loser: 1);   // bout2
            RunBout(m, loser: 1);   // bout3: P1 整队打光
            Assert.IsTrue(m.MatchOver, "P1 三员全灭 → 整场结束");
            Assert.That(m.MatchWinner, Is.EqualTo(0));
            Assert.That(m.ActiveIndex(1), Is.EqualTo(3), "P1 队耗尽");
            Assert.That(m.ActiveIndex(0), Is.EqualTo(0), "P0 始终是首发");
        }

        [Test]
        public void WinnerKeepsFighter_LoserCyclesThrough()
        {
            MTeamMatch m = new MTeamMatch(Roster(3), Roster(3), Fast);
            Assert.That(m.BoutNo, Is.EqualTo(1));
            RunBout(m, loser: 1);
            Assert.That(m.BoutNo, Is.EqualTo(2), "未结束则进入下一场");
            Assert.That(m.Remaining(0), Is.EqualTo(3), "胜方仍满编");
            Assert.That(m.Remaining(1), Is.EqualTo(2), "负方少一员");
        }

        [Test]
        public void WinnerKeepsLifeAndHealsTenPercentOfPreviousOpponentStartLife()
        {
            MTeamMatch m = new MTeamMatch(Roster(3), Roster(3), Fast);

            RunBoutWithWinnerLife(m, winner: 0, winnerLife: 300);

            Assert.That(m.BoutNo, Is.EqualTo(2));
            Assert.That(m.ActiveIndex(0), Is.EqualTo(0));
            Assert.That(m.ActiveIndex(1), Is.EqualTo(1));
            Assert.That(m.Engine.Chars[0].Life, Is.EqualTo(400), "300 + 1000 * 10%");
            Assert.That(m.Engine.Chars[1].Life, Is.EqualTo(1000), "新登场角色满血");
        }

        [Test]
        public void WinnerHealUsesPreviousOpponentCurrentBoutStartLife()
        {
            MTeamMatch m = new MTeamMatch(Roster(3), Roster(3), Fast);
            RunBoutWithWinnerLife(m, winner: 0, winnerLife: 300);

            RunBoutWithWinnerLife(m, winner: 1, winnerLife: 250);

            Assert.That(m.BoutNo, Is.EqualTo(3));
            Assert.That(m.ActiveIndex(0), Is.EqualTo(1));
            Assert.That(m.ActiveIndex(1), Is.EqualTo(1));
            Assert.That(m.Engine.Chars[1].Life, Is.EqualTo(290), "250 + 400 * 10%");
        }

        [Test]
        public void MixedOutcomes_CorrectMatchWinner()
        {
            MTeamMatch m = new MTeamMatch(Roster(2), Roster(2), Fast);
            RunBout(m, loser: 0);   // P0 失一员（P1 胜）
            RunBout(m, loser: 1);   // P1 失一员
            // 现各剩 1 员。下一场谁输谁出局。
            RunBout(m, loser: 0);   // P0 第二员阵亡 → P0 整队光
            Assert.IsTrue(m.MatchOver);
            Assert.That(m.MatchWinner, Is.EqualTo(1), "P0 先打光 → P1 胜");
        }

        [Test]
        public void Draw_BothLoseAFighter()
        {
            MTeamMatch m = new MTeamMatch(Roster(3), Roster(3), Fast);
            // 双 KO 平局
            int bout = m.BoutNo;
            int guard = 0;
            while (m.BoutNo == bout && !m.MatchOver && guard++ < 1000)
            {
                if (m.Round.State == MRoundState.Fight)
                {
                    m.Engine.Chars[0].Life = 0;
                    m.Engine.Chars[1].Life = 0;
                }
                m.Tick(NoInput);
            }
            Assert.That(m.ActiveIndex(0), Is.EqualTo(1), "平局 P0 也损一员");
            Assert.That(m.ActiveIndex(1), Is.EqualTo(1), "平局 P1 也损一员");
        }

        [Test]
        public void UnevenRosters_SmallerTeamCanStillWin()
        {
            MTeamMatch m = new MTeamMatch(Roster(1), Roster(3), Fast);
            // 单人队 P0 连胜 3 场打光 P1（车轮战以一敌三）。
            RunBout(m, loser: 1);
            RunBout(m, loser: 1);
            RunBout(m, loser: 1);
            Assert.IsTrue(m.MatchOver);
            Assert.That(m.MatchWinner, Is.EqualTo(0), "单人队全胜 → 胜");
            Assert.That(m.BoutNo, Is.EqualTo(3));
        }

        [Test]
        public void EmptyRoster_Throws()
        {
            Assert.Throws<ArgumentException>(() => new MTeamMatch(new List<MCharData>(), Roster(3)));
        }

        [Test]
        public void SnapshotRestore_RestoresCurrentBoutRoundAndEngineState()
        {
            MTeamMatch m = new MTeamMatch(Roster(2), Roster(2), Fast);
            for (int i = 0; i < 5; i++)
            {
                m.Tick(NoInput);
            }

            ulong expectedHash = m.ComputeHash();
            int expectedBout = m.BoutNo;
            int expectedP0 = m.ActiveIndex(0);
            int expectedP1 = m.ActiveIndex(1);
            MTeamMatchSnapshot snapshot = m.Snapshot();

            m.Engine.Chars[0].Life = 123;
            m.Round.Timer = 7;
            m.Tick(NoInput);
            Assert.That(m.ComputeHash(), Is.Not.EqualTo(expectedHash));

            m.Restore(snapshot);

            Assert.That(m.ComputeHash(), Is.EqualTo(expectedHash));
            Assert.That(m.BoutNo, Is.EqualTo(expectedBout));
            Assert.That(m.ActiveIndex(0), Is.EqualTo(expectedP0));
            Assert.That(m.ActiveIndex(1), Is.EqualTo(expectedP1));
        }

        [Test]
        public void SnapshotRestore_DoesNotFireBoutStartedEvent()
        {
            MTeamMatch m = new MTeamMatch(Roster(2), Roster(2), Fast);
            MTeamMatchSnapshot snapshot = m.Snapshot();
            int started = 0;
            m.OnBoutStarted += () => started++;

            m.Restore(snapshot);

            Assert.That(started, Is.EqualTo(0));
        }
    }
}
