using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// R-ROUND Ikemen 忠实度回归：锁定从 system.go stepRoundState/roundEndDecision 1:1 移植后修正的行为
    /// （这些断言在旧"简化主干"实现下会失败）：
    ///   • 双 KO 恒为平局（不按残余血量选胜方）
    ///   • Over 期送 win=180 / lose=170 / draw=175（旧只送胜者）
    ///   • over_hittime 窗口：分胜负后控制权暂留到进入 win pose 才收（旧立即收）
    ///   • finishType（KO/DKO/TO/TODraw）+ winType（满血 Perfect）
    /// oracle：手算 Ikemen system.go intro 计数器时间线。
    /// </summary>
    [TestFixture]
    public sealed class MRoundIkemenFidelityTests
    {
        // 带胜/负/平姿态（180/170/175）的合成角色。
        const string Cns =
            "[Statedef 0]\ntype=S\nphysics=S\nanim=0\n" +
            "[Statedef 170]\ntype=S\nctrl=0\nanim=170\n" +
            "[Statedef 175]\ntype=S\nctrl=0\nanim=175\n" +
            "[Statedef 180]\ntype=S\nctrl=0\nanim=180\n";
        const string Air =
            "[Begin Action 0]\n0,0, 0,0, 4\n[Begin Action 170]\n170,0, 0,0, 4\n" +
            "[Begin Action 175]\n175,0, 0,0, 4\n[Begin Action 180]\n180,0, 0,0, 4\n";
        const string RecoveryCns =
            "[Statedef 0]\ntype=S\nmovetype=I\nphysics=S\nctrl=1\nanim=0\n" +
            "[Statedef 200]\ntype=S\nmovetype=A\nphysics=S\nctrl=0\nanim=200\n" +
            "[State 200, recover]\ntype=ChangeState\ntrigger1=time>=6\nvalue=0\nctrl=1\n" +
            "[Statedef 180]\ntype=S\nmovetype=I\nphysics=S\nctrl=0\nanim=180\n";
        const string StuckRecoveryCns =
            "[Statedef 0]\ntype=S\nmovetype=I\nphysics=S\nctrl=1\nanim=0\n" +
            "[Statedef 200]\ntype=S\nmovetype=A\nphysics=S\nctrl=0\nanim=200\n" +
            "[Statedef 180]\ntype=S\nmovetype=I\nphysics=S\nctrl=0\nanim=180\n";
        const string RecoveryAir =
            "[Begin Action 0]\n0,0, 0,0, 4\n[Begin Action 180]\n180,0, 0,0, 4\n" +
            "[Begin Action 200]\n200,0, 0,0, 20\n";

        static readonly List<MInput> NoInput = new List<MInput> { MInput.None, MInput.None };

        static (MBattleEngine, MRoundSystem) Build(int introTime = 1, int overWait = 2,
            int overHit = 1, int winPose = 3, int roundTime = 99 * 60)
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "Poser");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Chars[0].Id = 1;
            engine.Chars[1].Id = 2;
            engine.Chars[0].Life = engine.Chars[0].LifeMax = 1000;
            engine.Chars[1].Life = engine.Chars[1].LifeMax = 1000;
            engine.LinkPair();
            MRoundSystem round = new MRoundSystem(engine)
            {
                IntroTime = introTime, OverWaitTime = overWait, OverHitTime = overHit,
                WinPoseTime = winPose, RoundTime = roundTime,
            };
            return (engine, round);
        }

        static (MBattleEngine, MRoundSystem) BuildWithCns(string cns, int overReadyWait = 900)
        {
            MCharData data = MCharLoader.Load(new[] { cns }, cns, null, RecoveryAir, null, "Recovery");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Chars[0].Id = 1;
            engine.Chars[1].Id = 2;
            engine.Chars[0].Life = engine.Chars[0].LifeMax = 1000;
            engine.Chars[1].Life = engine.Chars[1].LifeMax = 1000;
            engine.LinkPair();
            MRoundSystem round = new MRoundSystem(engine)
            {
                IntroTime = 1,
                OverWaitTime = 1,
                OverHitTime = 1,
                WinPoseTime = 4,
                RoundTime = 99 * 60,
                OverReadyWaitTime = overReadyWait,
            };
            return (engine, round);
        }

        static void TickToFight(MRoundSystem round)
        {
            int guard = 0;
            while (round.State != MRoundState.Fight && guard++ < 50)
            {
                round.Tick(NoInput);
            }
            Assert.That(round.State, Is.EqualTo(MRoundState.Fight), "应进入战斗");
        }

        static void TickUntil(MRoundSystem round, MRoundState target, int max = 200)
        {
            int guard = 0;
            while (round.State != target && guard++ < max)
            {
                round.Tick(NoInput);
            }
            Assert.That(round.State, Is.EqualTo(target), "应到达 " + target);
        }

        [Test]
        public void DoubleKO_WithUnequalLife_IsStillDraw()
        {
            (MBattleEngine engine, MRoundSystem round) = Build();
            TickToFight(round);
            // 同帧双双阵亡，但残余血量不同（一个 0、一个被打成负）。Ikemen：双 KO 恒平局。
            engine.Chars[0].Life = 0;
            engine.Chars[1].Life = -50;
            round.Tick(NoInput);
            Assert.That(round.State, Is.EqualTo(MRoundState.PreOver));
            Assert.That(round.Winner, Is.EqualTo(-1), "双 KO 恒为平局，不看残余血量");
            Assert.That(round.FinishType, Is.EqualTo(MFinishType.DoubleKo));
        }

        [Test]
        public void SingleKO_SetsFinishTypeKo_AndWinner()
        {
            (MBattleEngine engine, MRoundSystem round) = Build();
            TickToFight(round);
            engine.Chars[1].Life = 0;   // P2 阵亡 → P1 胜
            round.Tick(NoInput);
            Assert.That(round.Winner, Is.EqualTo(0));
            Assert.That(round.FinishType, Is.EqualTo(MFinishType.Ko));
        }

        [Test]
        public void LaterKO_DuringOverHitTime_UpgradesSingleKOToDoubleKO()
        {
            (MBattleEngine engine, MRoundSystem round) = Build(overHit: 2, overWait: 5);
            TickToFight(round);
            engine.Chars[1].Life = 0;   // First decision: P1 wins by KO.
            round.Tick(NoInput);
            Assert.That(round.Intro, Is.EqualTo(-1));
            Assert.That(round.Winner, Is.EqualTo(0));
            Assert.That(round.FinishType, Is.EqualTo(MFinishType.Ko));

            engine.Chars[0].Life = 0;   // Still inside over_hittime, so this becomes DKO.
            round.Tick(NoInput);
            Assert.That(round.Winner, Is.EqualTo(-1), "over_hittime window can still upgrade a single KO into DKO");
            Assert.That(round.FinishType, Is.EqualTo(MFinishType.DoubleKo));
        }

        [Test]
        public void LaterKO_AfterScorePoint_RollsBackSingleKOPointWhenUpgradedToDoubleKO()
        {
            (MBattleEngine engine, MRoundSystem round) = Build(overHit: 2, overWait: 5, winPose: 8);
            TickToFight(round);
            engine.Chars[1].Life = 0;
            round.Tick(NoInput);
            Assert.That(round.Winner, Is.EqualTo(0));

            engine.Chars[0].PendingLifeDamage = engine.Chars[0].Life;
            round.Tick(NoInput);   // Step reaches score point, then engine applies the final allowed damage.
            Assert.That(round.RoundsWon[0], Is.EqualTo(1), "single KO point has been recorded before the later KO is observed");
            Assert.That(engine.Chars[0].Life, Is.EqualTo(0));

            round.Tick(NoInput);
            Assert.That(round.FinishType, Is.EqualTo(MFinishType.DoubleKo));
            Assert.That(round.Winner, Is.EqualTo(-1));
            Assert.That(round.RoundsWon[0], Is.EqualTo(0), "upgrading to DKO must revoke the stale single-KO point");
            Assert.That(round.RoundsWon[1], Is.EqualTo(0));
        }

        [Test]
        public void TimeOver_AliveLoser_GoesToLosePose170_WinnerTo180()
        {
            (MBattleEngine engine, MRoundSystem round) = Build(roundTime: 4);
            TickToFight(round);
            engine.Chars[0].Life = 300;   // 双方都活着，P2 血高
            engine.Chars[1].Life = 800;
            TickUntil(round, MRoundState.Over);
            Assert.That(round.FinishType, Is.EqualTo(MFinishType.TimeOver));
            Assert.That(round.Winner, Is.EqualTo(1));
            Assert.That(engine.Chars[1].StateNo, Is.EqualTo(180), "胜者 → win pose 180");
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(170), "活着的败者 → lose pose 170");
        }

        [Test]
        public void TimeOver_EqualLife_DrawPose175_BothSides()
        {
            (MBattleEngine engine, MRoundSystem round) = Build(roundTime: 4);
            TickToFight(round);
            engine.Chars[0].Life = 500;
            engine.Chars[1].Life = 500;
            TickUntil(round, MRoundState.Over);
            Assert.That(round.FinishType, Is.EqualTo(MFinishType.TimeDraw));
            Assert.That(round.Winner, Is.EqualTo(-1));
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(175), "平局双方 → draw pose 175");
            Assert.That(engine.Chars[1].StateNo, Is.EqualTo(175));
        }

        [Test]
        public void KodLoser_StaysDown_NotSentToLosePose()
        {
            (MBattleEngine engine, MRoundSystem round) = Build();
            TickToFight(round);
            engine.Chars[1].Life = 0;            // P2 被 KO
            engine.Chars[1].QueueTransition(170, engine.Chars[1].PlayerNo);   // 假装它本来想进别的态
            TickUntil(round, MRoundState.Over);
            // 胜者进 180；阵亡者不应被回合系统强行送进 170（保持倒地，由受击机的 5150 接管）。
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(180), "胜者 win pose");
            Assert.That(round.Winner, Is.EqualTo(0));
        }

        [Test]
        public void OverHitTime_CtrlRetainedInPreOver_RemovedAtOver()
        {
            (MBattleEngine engine, MRoundSystem round) = Build(overWait: 4);
            TickToFight(round);
            Assert.IsTrue(engine.Chars[0].Ctrl, "战斗期有控制权");
            engine.Chars[1].Life = 0;
            // 进入 PreOver：分胜负后的 over_hittime 缓冲，胜者仍可动（控制权暂留）。
            TickUntil(round, MRoundState.PreOver);
            Assert.IsTrue(engine.Chars[0].Ctrl, "PreOver 期控制权暂留（over_hittime 窗口，可双 KO）");
            // 进入 Over：win pose 首帧收回控制权。
            TickUntil(round, MRoundState.Over);
            Assert.IsFalse(engine.Chars[0].Ctrl, "进入 win pose 时收回控制权");
        }

        [Test]
        public void PostKO_WinnerRecovery_IsNotCutOffByWinPose()
        {
            (MBattleEngine engine, MRoundSystem round) = BuildWithCns(RecoveryCns);
            TickToFight(round);

            engine.Chars[0].QueueTransition(200, engine.Chars[0].PlayerNo);
            engine.Chars[1].Life = 0;
            round.Tick(NoInput);
            Assert.That(round.State, Is.EqualTo(MRoundState.PreOver));

            bool heldBeforeRecovery = false;
            for (int f = 0; f < 5; f++)
            {
                round.Tick(NoInput);
                if (engine.Chars[0].StateNo == 200)
                {
                    heldBeforeRecovery = true;
                    Assert.That(round.State, Is.EqualTo(MRoundState.PreOver),
                        "roundstate 4 must wait while the winner is still in attack recovery");
                }
            }

            Assert.IsTrue(heldBeforeRecovery, "test must cover the attacking recovery state");
            TickUntil(round, MRoundState.Over);
            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(180), "winner enters win pose after recovery");
        }

        [Test]
        public void PostKO_StuckRecovery_UsesFailsafeToEnterOver()
        {
            (MBattleEngine engine, MRoundSystem round) = BuildWithCns(StuckRecoveryCns, overReadyWait: 2);
            TickToFight(round);

            engine.Chars[0].QueueTransition(200, engine.Chars[0].PlayerNo);
            engine.Chars[1].Life = 0;

            TickUntil(round, MRoundState.Over, max: 30);

            Assert.That(engine.Chars[0].StateNo, Is.EqualTo(180),
                "failsafe should eventually force win pose for a stuck post-KO state");
        }

        [Test]
        public void PerfectWin_WhenWinnerAtFullLife()
        {
            (MBattleEngine engine, MRoundSystem round) = Build();
            TickToFight(round);
            engine.Chars[0].Life = 1000;   // 胜者满血
            engine.Chars[1].Life = 0;
            round.Tick(NoInput);
            Assert.That(round.Winner, Is.EqualTo(0));
            Assert.That(round.WinType[0], Is.EqualTo(MWinType.Perfect), "满血 KO 取胜 = Perfect");
        }

        [Test]
        public void NormalWin_WhenWinnerTookDamage()
        {
            (MBattleEngine engine, MRoundSystem round) = Build();
            TickToFight(round);
            engine.Chars[0].Life = 600;    // 胜者掉过血
            engine.Chars[1].Life = 0;
            round.Tick(NoInput);
            Assert.That(round.WinType[0], Is.EqualTo(MWinType.Normal));
        }
    }
}
