// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/system.go roundState()/stepRoundState() — 回合状态流转（定点/headless 化）。
// 离散量（RoundState 序列 / winner / 回合数）对账 Ikemen；连续计时为帧整数。
// 不照搬 Ikemen 与 fightScreen/motif/SDL 的耦合，只移植 intro→fight→KO/timeover→over→下一回合 的状态机骨架。
// See Docs/移植方案_Ikemen.md 与 Docs/对战完整化路线图.md（R-ROUND）。
using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle
{
    /// <summary>回合状态（对应 Ikemen roundState() 的离散返回值）。</summary>
    public enum MRoundState
    {
        Intro = 1,     // 入场（无 ctrl）
        Fight = 2,     // 战斗活动期（授 ctrl）
        PreOver = 3,   // 已分胜负的缓冲（over.waittime，角色仍在演出/倒地）
        Over = 4,      // 胜利姿态（win pose）
    }

    /// <summary>
    /// 回合编排器：驱动 <see cref="MBattleEngine"/> 走完 intro→fight→KO/timeover→over→下一回合/结算。
    /// 现 StartRound 只是授 ctrl 的 shim，本类把它升级为真正的回合状态机。无静态可变态（运行态全在字段，可快照/哈希）。
    /// </summary>
    public sealed class MRoundSystem
    {
        readonly MBattleEngine _engine;

        // ── 配置（帧）。默认值为可玩量级；测试可改小以快速跑完一局。 ──
        public int IntroTime = 60;       // 入场时长
        public int OverWaitTime = 45;    // KO 后到 win pose 的缓冲（对应 over.waittime）
        public int WinPoseTime = 90;     // 胜利姿态时长
        public int RoundTime = 99 * 60;  // 回合计时（tick）；<0 = 不计时（无限）
        public int RoundsToWin = 2;      // 几胜制（2 = 三局两胜）

        // ── 运行态 ──
        public MRoundState State = MRoundState.Intro;
        public int StateTimer;           // 当前回合状态已持续帧数
        public int RoundNo = 1;          // 当前回合（1-based）
        public int Timer;                // 回合倒计时（tick）
        public int Winner = -1;          // 本回合胜者玩家索引（-1=未分/平局）
        public bool MatchOver;           // 整场是否结束
        public int MatchWinner = -1;     // 整场胜者玩家索引（-1=未定/平局）
        public readonly int[] RoundsWon = new int[2];

        public MRoundSystem(MBattleEngine engine)
        {
            _engine = engine;
            Timer = RoundTime;
        }

        /// <summary>推进一帧：先按回合状态编排（授/收 ctrl、判胜负），再推进底层引擎一帧。</summary>
        public void Tick(IReadOnlyList<MInput> inputs)
        {
            StepRoundState();
            _engine.Tick(inputs);
            StateTimer++;
            if (State == MRoundState.Fight && RoundTime >= 0 && Timer > 0)
            {
                Timer--;
            }
        }

        // 回合状态流转（移植 Ikemen stepRoundState 主干，去 UI/计时器细节）。
        void StepRoundState()
        {
            switch (State)
            {
                case MRoundState.Intro:
                    // 入场无控制权；满 IntroTime 进入战斗并授 ctrl。
                    if (StateTimer >= IntroTime)
                    {
                        EnterFight();
                    }
                    break;

                case MRoundState.Fight:
                    CheckRoundEnd();
                    break;

                case MRoundState.PreOver:
                    // KO/timeover 后缓冲：满 OverWaitTime 进胜利姿态并记分。
                    if (StateTimer >= OverWaitTime)
                    {
                        EnterOver();
                    }
                    break;

                case MRoundState.Over:
                    // 胜利姿态结束：进入下一回合或结算整场。
                    if (StateTimer >= WinPoseTime)
                    {
                        AdvanceAfterRound();
                    }
                    break;
            }
        }

        void EnterFight()
        {
            _engine.StartRound();   // 授 ctrl/keyctrl（Ikemen RoundState 2 进入活动期）
            State = MRoundState.Fight;
            StateTimer = 0;
            Timer = RoundTime;      // 回合计时在进入战斗时起算（对齐 Ikemen fight 开始计时）
        }

        // 战斗中检测回合结束：任一方 KO，或计时归零（超时判血高者胜）。
        void CheckRoundEnd()
        {
            if (_engine.Chars.Count < 2)
            {
                return;
            }
            MChar p0 = _engine.Chars[0];
            MChar p1 = _engine.Chars[1];

            bool ko0 = p0.Life <= 0;
            bool ko1 = p1.Life <= 0;
            if (ko0 || ko1)
            {
                Winner = DecideWinner(ko0, ko1, p0.Life, p1.Life);
                EnterPreOver();
                return;
            }
            if (RoundTime >= 0 && Timer <= 0)
            {
                // 超时：血量高者胜，相等平局
                Winner = p0.Life > p1.Life ? 0 : (p1.Life > p0.Life ? 1 : -1);
                EnterPreOver();
            }
        }

        // KO 胜负：双 KO=平局；否则存活方胜（同帧双方都判 KO 但血量不同时取高者）。
        static int DecideWinner(bool ko0, bool ko1, int life0, int life1)
        {
            if (ko0 && ko1)
            {
                return life0 == life1 ? -1 : (life0 > life1 ? 0 : 1);
            }
            return ko0 ? 1 : 0;
        }

        void EnterPreOver()
        {
            // 双方收回控制权（对应 Ikemen over 期角色逐步失控）。
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                _engine.Chars[i].Ctrl = false;
                _engine.Chars[i].KeyCtrl = false;
            }
            State = MRoundState.PreOver;
            StateTimer = 0;
        }

        void EnterOver()
        {
            if (Winner >= 0 && Winner < RoundsWon.Length)
            {
                RoundsWon[Winner]++;
            }
            State = MRoundState.Over;
            StateTimer = 0;
        }

        void AdvanceAfterRound()
        {
            if (Winner >= 0 && RoundsWon[Winner] >= RoundsToWin)
            {
                MatchOver = true;
                MatchWinner = Winner;
                return;
            }
            // 下一回合：重置角色到站立满血，回合状态归 Intro。
            RoundNo++;
            Winner = -1;
            Timer = RoundTime;
            State = MRoundState.Intro;
            StateTimer = 0;
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                ResetCombatant(_engine.Chars[i]);
            }
        }

        // 回合间复位：满血、回站立 0、清受击态、收 ctrl（入场期再授）。位置复位归 demo 场景（R-ARENA）。
        static void ResetCombatant(MChar c)
        {
            c.Life = c.LifeMax;
            // 能量(Power)跨回合保留（MUGEN 行为），故此处不动。
            c.PendingStateNo = 0;
            c.PendingIsSelf = true;
            c.MoveType = 1;      // I
            c.Ctrl = false;
            c.KeyCtrl = false;
            c.Vel = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            c.Ghv = new MGetHitVar();
            c.FallTime = 0;
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32((int)State); hash.AddInt32(StateTimer); hash.AddInt32(RoundNo);
            hash.AddInt32(Timer); hash.AddInt32(Winner); hash.AddBool(MatchOver); hash.AddInt32(MatchWinner);
            hash.AddInt32(RoundsWon[0]); hash.AddInt32(RoundsWon[1]);
        }
    }
}
