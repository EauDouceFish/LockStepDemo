// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/system.go stepRoundState()/roundState()/roundEndDecision()/matchOver() — 回合状态机（定点/headless 化）。
//
// 忠实点：整局由【单个有符号计数器 Intro】驱动（= Ikemen sys.intro），各阶段只是对 Intro 区间的分类（roundState()）：
//   Intro > 0                       → Intro   出场（无 ctrl）
//   Intro == 0                      → Fight   战斗活动期（授 ctrl，curRoundTime 递减）
//   -OverWaitTime <= Intro < 0      → PreOver 已分胜负缓冲（over_hittime 窗口；ctrl 暂留，可双 KO）
//   Intro < -OverWaitTime           → Over    胜利/失败/平局姿态（180/170/175）
//   Intro < -(OverWaitTime+WinPose) → roundOver → 下一回合 / 整场结算
// 每帧 stepRoundState 先跑（在角色状态机之前，对齐 Ikemen system.go:2998 注释），再推进引擎一帧。
//
// 不照搬 Ikemen 的 fightScreen/motif/SDL/fade 耦合，只移植离散状态机骨架。离散量（RoundState 序列 / winner /
// finishType / 回合记分 / 整场结束）对账 Ikemen；连续计时为帧整数。See Docs/Ikemen_1v1对战环节全解_对照移植.md。
using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle
{
    /// <summary>回合状态（对应 Ikemen roundState() 的离散返回值；0=Pre-intro 已折进 Intro）。</summary>
    public enum MRoundState
    {
        Intro = 1,     // 入场（无 ctrl）
        Fight = 2,     // 战斗活动期（授 ctrl）
        PreOver = 3,   // 已分胜负缓冲（over_hittime 窗口，角色仍可动 → 可能双 KO）
        Over = 4,      // 胜利 / 失败 / 平局姿态（win/lose/draw pose）
    }

    /// <summary>本回合结束方式（对应 Ikemen finishType FT_*）。</summary>
    public enum MFinishType
    {
        NotYet = 0,    // 未结束（战斗中）
        Ko = 1,        // 单杀
        DoubleKo = 2,  // 双 KO / over_hittime 窗口内双杀 → 平局
        TimeOver = 3,  // 超时分血量
        TimeDraw = 4,  // 超时同血 → 平局
    }

    /// <summary>胜利修饰（对应 Ikemen winType WT_*；仅离散标记，供 HUD 图标）。</summary>
    public enum MWinType
    {
        Normal = 0,
        Perfect = 1,   // 满血取胜
        Time = 2,      // 超时取胜
    }

    /// <summary>
    /// 回合编排器（1:1 移植 Ikemen system.go 的 intro 计数器模型）：驱动一局
    /// intro→fight→KO/timeover→preover→over→下一回合/结算。无静态可变态（运行态全在字段，可快照/哈希）。
    /// </summary>
    public sealed class MRoundSystemSnapshot
    {
        public int IntroTime;
        public int OverWaitTime;
        public int OverHitTime;
        public int WinPoseTime;
        public int OverReadyWaitTime;
        public int RoundTime;
        public int RoundsToWin;
        public int MaxAssertIntroHoldTime;
        public bool SingleRound;
        public int[] IntroStateCandidates;
        public int[] WinPoseStateCandidates;
        public int[] LosePoseStateCandidates;
        public int[] DrawPoseStateCandidates;
        public int Intro;
        public int RoundNo;
        public int Timer;
        public int Winner;
        public MFinishType FinishType;
        public bool MatchOver;
        public int MatchWinner;
        public int[] RoundsWon;
        public MWinType[] WinType;
        public bool BoutComplete;
        public bool Ticked;
        public bool[] Posed;
        public bool Scored;
        public bool CtrlRemoved;
        public int StateTimer;
        public MRoundState PrevState;
        public int AssertIntroHoldFrames;
        public int OverReadyWaitDown;
    }

    public sealed class MRoundSystem
    {
        readonly MBattleEngine _engine;

        // ── 配置（帧）。默认值为可玩量级；测试可改小以快速跑完一局。对应 fightScreen.round.* ──
        int _introTime = 60;
        bool _ticked;
        /// <summary>round.start_waittime+ctrl_time+1 折算：开局到授控的总帧数。开打前调整会同步 <see cref="Intro"/> 起点
        /// （兼容对象初始化器 new MRoundSystem(e){ IntroTime = n } 在构造后才赋值的语义）。</summary>
        public int IntroTime
        {
            get { return _introTime; }
            set
            {
                _introTime = value;
                if (!_ticked)
                {
                    Intro = value + 1;
                }
            }
        }
        public int OverWaitTime = 45;    // round.over_waittime：KO 到进入 win pose（RoundState 4）的缓冲
        public int OverHitTime = 10;     // round.over_hittime：KO 后仍可改判/记分的窗口（在 -over_hittime 处记分）
        public int WinPoseTime = 90;     // round.over_wintime 折算：胜利姿态（Over）时长
        public int OverReadyWaitTime = 900; // Ikemen waitdown failsafe before forcing roundstate 4.
        public int RoundTime = 60 * 60;  // maxRoundTime：回合计时（tick）；<0 = 不计时（无限）
        public int RoundsToWin = 2;      // matchWins：几胜制（2 = 三局两胜）
        public int MaxAssertIntroHoldTime = 120;
        public const int TicksPerSecond = 60;

        // 单场模式（车轮战/turns 用）：一场分出胜负并演完胜利姿态后，不自动复位进下一回合，
        // 而是置 BoutComplete=true 冻结于 Over，交由外层编排器（MTeamMatch）换人/开下一场。
        public bool SingleRound;
        public bool BoutComplete { get; private set; }

        // 入场自动演出：Intro 期把角色送入入场态（MUGEN 约定 190；KFM 用 191=鞠躬，anim 190）。
        // 胜利/失败/平局姿态：Over 期送 win=180、lose=170、draw=175（按候选顺序取首个存在的态，缺则不动）。
        public int[] IntroStateCandidates = { 190, 191 };
        public int[] WinPoseStateCandidates = { 180, 181, 195 };
        public int[] LosePoseStateCandidates = { 170 };
        public int[] DrawPoseStateCandidates = { 175, 170 };

        // ── 运行态 ──
        public int Intro;                // 主计数器（= Ikemen sys.intro）：正=入场，0=战斗，负=结算
        public int RoundNo = 1;          // 当前回合（1-based）
        public int Timer;                // 回合倒计时（tick；= curRoundTime）
        public int Winner = -1;          // 本回合胜方玩家索引（= winTeam；-1=未分/平局）
        public MFinishType FinishType;   // 本回合结束方式
        public bool MatchOver;           // 整场是否结束
        public int MatchWinner = -1;     // 整场胜者玩家索引（-1=未定/平局）
        public readonly int[] RoundsWon = new int[2];
        public readonly MWinType[] WinType = { MWinType.Normal, MWinType.Normal };

        // 内部门控：胜负姿态/记分/收控各只触发一次。
        readonly bool[] _posed = new bool[2];
        bool _scored;
        bool _ctrlRemoved;
        int _stateTimer;                 // 当前 RoundState 已持续帧数（仅 HUD/调试用）
        MRoundState _prevState;
        int _assertIntroHoldFrames;
        int _overReadyWaitDown = -1;

        // Ikemen reference: src/system.go round initialization seeds sys.intro, timers, and intro state before the first tick.
        public MRoundSystem(MBattleEngine engine)
        {
            _engine = engine;
            Intro = IntroTime + 1;   // = Ikemen start_waittime+ctrl_time+1：ctrl 在 intro 倒到 0 时授予
            Timer = RoundTime;
            _prevState = State;
            SyncRoundStateToChars();
            BeginRoundIntro();   // 开局把角色送入入场态（鞠躬）；无入场态者保持站立。
        }

        /// <summary>当前回合状态（对应 Ikemen roundState()，从 <see cref="Intro"/> 区间分类）。</summary>
        public MRoundState State
        {
            get
            {
                if (Intro > 0)
                {
                    return MRoundState.Intro;
                }
                if (Intro == 0 || FinishType == MFinishType.NotYet)
                {
                    return MRoundState.Fight;
                }
                if (Intro < -OverWaitTime)
                {
                    return MRoundState.Over;
                }
                return MRoundState.PreOver;
            }
        }

        /// <summary>当前 RoundState 已持续帧数（HUD "Fight!" 闪现 / 调试用，不影响判定）。</summary>
        public int StateTimer => _stateTimer;

        /// <summary>回合倒计时（秒，向上取整）；RoundTime &lt; 0（无限）时返回 -1。供 HUD 显示。</summary>
        public int TimerSeconds => RoundTime < 0 ? -1 : (Timer + TicksPerSecond - 1) / TicksPerSecond;

        void SyncRoundStateToChars()
        {
            int roundState = (int)State;
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                _engine.Chars[i].RoundState = roundState;
            }
            for (int i = 0; i < _engine.Helpers.Count; i++)
            {
                _engine.Helpers[i].RoundState = roundState;
            }
        }

        public MRoundSystemSnapshot Snapshot()
        {
            return new MRoundSystemSnapshot
            {
                IntroTime = _introTime,
                OverWaitTime = OverWaitTime,
                OverHitTime = OverHitTime,
                WinPoseTime = WinPoseTime,
                OverReadyWaitTime = OverReadyWaitTime,
                RoundTime = RoundTime,
                RoundsToWin = RoundsToWin,
                MaxAssertIntroHoldTime = MaxAssertIntroHoldTime,
                SingleRound = SingleRound,
                IntroStateCandidates = CloneArray(IntroStateCandidates),
                WinPoseStateCandidates = CloneArray(WinPoseStateCandidates),
                LosePoseStateCandidates = CloneArray(LosePoseStateCandidates),
                DrawPoseStateCandidates = CloneArray(DrawPoseStateCandidates),
                Intro = Intro,
                RoundNo = RoundNo,
                Timer = Timer,
                Winner = Winner,
                FinishType = FinishType,
                MatchOver = MatchOver,
                MatchWinner = MatchWinner,
                RoundsWon = CloneArray(RoundsWon),
                WinType = CloneArray(WinType),
                BoutComplete = BoutComplete,
                Ticked = _ticked,
                Posed = CloneArray(_posed),
                Scored = _scored,
                CtrlRemoved = _ctrlRemoved,
                StateTimer = _stateTimer,
                PrevState = _prevState,
                AssertIntroHoldFrames = _assertIntroHoldFrames,
                OverReadyWaitDown = _overReadyWaitDown,
            };
        }

        public void Restore(MRoundSystemSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _introTime = snapshot.IntroTime;
            OverWaitTime = snapshot.OverWaitTime;
            OverHitTime = snapshot.OverHitTime;
            WinPoseTime = snapshot.WinPoseTime;
            OverReadyWaitTime = snapshot.OverReadyWaitTime;
            RoundTime = snapshot.RoundTime;
            RoundsToWin = snapshot.RoundsToWin;
            MaxAssertIntroHoldTime = snapshot.MaxAssertIntroHoldTime;
            SingleRound = snapshot.SingleRound;
            IntroStateCandidates = CloneArray(snapshot.IntroStateCandidates) ?? new[] { 190, 191 };
            WinPoseStateCandidates = CloneArray(snapshot.WinPoseStateCandidates) ?? new[] { 180, 181, 195 };
            LosePoseStateCandidates = CloneArray(snapshot.LosePoseStateCandidates) ?? new[] { 170 };
            DrawPoseStateCandidates = CloneArray(snapshot.DrawPoseStateCandidates) ?? new[] { 175, 170 };

            Intro = snapshot.Intro;
            RoundNo = snapshot.RoundNo;
            Timer = snapshot.Timer;
            Winner = snapshot.Winner;
            FinishType = snapshot.FinishType;
            MatchOver = snapshot.MatchOver;
            MatchWinner = snapshot.MatchWinner;
            CopyArray(snapshot.RoundsWon, RoundsWon);
            CopyArray(snapshot.WinType, WinType);
            BoutComplete = snapshot.BoutComplete;
            _ticked = snapshot.Ticked;
            CopyArray(snapshot.Posed, _posed);
            _scored = snapshot.Scored;
            _ctrlRemoved = snapshot.CtrlRemoved;
            _stateTimer = snapshot.StateTimer;
            _prevState = snapshot.PrevState;
            _assertIntroHoldFrames = snapshot.AssertIntroHoldFrames;
            _overReadyWaitDown = snapshot.OverReadyWaitDown;
            SyncRoundStateToChars();
        }

        /// <summary>推进一帧：先按回合状态编排（授/收 ctrl、判胜负、推进 intro），再推进底层引擎一帧。</summary>
        // Ikemen reference: src/system.go:2959 stepRoundState runs before character update in the per-frame system loop.
        public void Tick(IReadOnlyList<MInput> inputs)
        {
            _ticked = true;
            StepRoundState();
            SyncRoundStateToChars();
            _engine.Tick(inputs);

            MRoundState now = State;
            _stateTimer = now == _prevState ? _stateTimer + 1 : 0;
            _prevState = now;
        }

        // 移植 Ikemen system.go:2959 stepRoundState 主干（去 fade/motif/pause/super）。
        // Ikemen reference: src/system.go:2959 stepRoundState drives intro, fight, KO/timeover, scoring, poses, and round advance.
        void StepRoundState()
        {
            // 禁伤窗口（system.go roundNoDamage:1592）：分胜负后过了 over_hittime 双 KO 窗口、进 win pose 之前，命中不扣血。
            int hit = ClampPositive(OverHitTime);
            _engine.NoDamage = Intro < 0 && Intro <= -hit && Intro >= -OverWaitTime;

            // ── 入场（system.go:2974）──
            if (Intro > 0)
            {
                if (Intro == 1 && AnyIntroAsserted() && _assertIntroHoldFrames < ClampNonNegative(MaxAssertIntroHoldTime))
                {
                    _assertIntroHoldFrames++;
                    return;
                }

                Intro--;
                if (Intro == 0)
                {
                    EnterFight();   // intro 倒到 0：授 ctrl + selfState(0)（system.go:2991）
                }
            }

            // ── 战斗中：回合计时递减（system.go:3012，仅 intro==0）──
            if (Intro == 0 && RoundTime >= 0 && Timer > 0)
            {
                Timer--;
            }

            // ── 结算（system.go:3019 post round）──
            if (RoundEnded() || RoundEndDecision())
            {
                EnsureOverReadyWaitDown();
                int rs4t = -OverWaitTime;
                Intro--;

                // 记分：intro 到 -over_hittime 时锁定本回合胜场（system.go:3028）。over_hittime 夹到 over_waittime 内。
                int scorePoint = -ClampPositive(OverHitTime > OverWaitTime ? OverWaitTime : OverHitTime);
                if (!_scored && Intro <= scorePoint && FinishType != MFinishType.NotYet)
                {
                    if (Winner >= 0 && Winner < RoundsWon.Length)
                    {
                        RoundsWon[Winner]++;
                    }
                    _scored = true;
                }

                // Ikemen system.go gates roundstate 4 until fighters are ready, otherwise post-KO
                // recovery can be cut off by a forced win pose.
                if (_overReadyWaitDown > 0 && Intro == rs4t - 1 && !AllCombatantsReadyForOver())
                {
                    Intro = rs4t;
                }

                // 进入 RoundState 4（win pose）首帧：收回控制权（system.go:3097 intro==rs4t-1）。
                if (!_ctrlRemoved && Intro == rs4t - 1)
                {
                    for (int i = 0; i < _engine.Chars.Count; i++)
                    {
                        _engine.Chars[i].Ctrl = false;
                        _engine.Chars[i].KeyCtrl = false;
                    }
                    _ctrlRemoved = true;
                }

                // 送胜负/平局姿态（system.go:3144；仅存活且未送过的角色，KO 者保持倒地不摆 pose）。
                if (Intro < -OverWaitTime)
                {
                    SendPoses();
                }

                if (_overReadyWaitDown > 0)
                {
                    _overReadyWaitDown--;
                }

                // 回合结束 → 下一回合 / 整场结算（system.go roundOver():1682）。
                if (Intro < -(OverWaitTime + WinPoseTime))
                {
                    AdvanceAfterRound();
                }
            }
            else if (Intro < 0)
            {
                Intro = 0;   // 安全兜底（system.go:3174）
            }
        }

        // roundEnded：intro 越过 over_hittime（system.go:1587）。
        // Ikemen reference: src/system.go:1587 roundEnded checks whether sys.intro has passed the over_hittime window.
        bool RoundEnded()
        {
            return Intro < -ClampPositive(OverHitTime);
        }

        void EnsureOverReadyWaitDown()
        {
            if (_overReadyWaitDown < 0)
            {
                _overReadyWaitDown = ClampNonNegative(OverWaitTime) + ClampNonNegative(OverReadyWaitTime);
            }
        }

        bool AllCombatantsReadyForOver()
        {
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                if (!ReadyForOver(_engine.Chars[i]))
                {
                    return false;
                }
            }
            return true;
        }

        static bool ReadyForOver(MChar c)
        {
            if (c == null || !Alive(c) || c.StateNo == 5150)
            {
                return true;
            }
            return c.Ctrl && c.MoveType == 1 && c.StateType != 4 && c.StateType != 8;
        }

        // 移植 Ikemen system.go:3180 roundEndDecision：判 KO / 超时并定胜负 + winType。
        // 返回 true 表示本回合应进入结算（KO 或超时）。
        // Ikemen reference: src/system.go:3180 roundEndDecision decides KO, double KO, and time-over outcomes.
        bool RoundEndDecision()
        {
            if (Intro > 0 || _engine.Chars.Count < 2)
            {
                return false;
            }

            MChar p0 = _engine.Chars[0];
            MChar p1 = _engine.Chars[1];
            bool ko0 = !Alive(p0);   // 队 0（玩家 0）阵亡
            bool ko1 = !Alive(p1);   // 队 1（玩家 1）阵亡
            bool timeOver = RoundTime >= 0 && Timer <= 0;

            if (FinishType == MFinishType.Ko && Intro >= -ClampPositive(OverHitTime) && ko0 && ko1)
            {
                UpgradeToDoubleKo();
            }
            else if (FinishType == MFinishType.NotYet)
            {
                // KO 优先（同帧双 KO = 平局；单 KO 活方胜）。over_hittime 窗口内仍可改判成双 KO。
                if (Intro >= -ClampPositive(OverHitTime) && (ko0 || ko1))
                {
                    if (ko0 && ko1)
                    {
                        FinishType = MFinishType.DoubleKo;
                        Winner = -1;
                    }
                    else
                    {
                        FinishType = MFinishType.Ko;
                        Winner = ko0 ? 1 : 0;
                        SetWinTypeKo(p0, p1);
                    }
                }
                else if (timeOver)
                {
                    DecideTimeOver(p0, p1);
                }
            }

            return ko0 || ko1 || timeOver;
        }

        void UpgradeToDoubleKo()
        {
            if (_scored && Winner >= 0 && Winner < RoundsWon.Length && RoundsWon[Winner] > 0)
            {
                RoundsWon[Winner]--;
            }
            FinishType = MFinishType.DoubleKo;
            Winner = -1;
            WinType[0] = MWinType.Normal;
            WinType[1] = MWinType.Normal;
        }

        // 超时判定：比双方血量百分比（life/lifeMax），高者胜；相等平局（system.go:3233）。
        // Ikemen reference: src/system.go:3233 time-over decision compares remaining life ratios and assigns win type.
        void DecideTimeOver(MChar p0, MChar p1)
        {
            // 交叉相乘避免浮点：life0/max0 ? life1/max1。
            long lhs = (long)p0.Life * p1.LifeMax;
            long rhs = (long)p1.Life * p0.LifeMax;
            if (lhs == rhs)
            {
                FinishType = MFinishType.TimeDraw;
                Winner = -1;
                return;
            }
            FinishType = MFinishType.TimeOver;
            Winner = lhs > rhs ? 0 : 1;
            WinType[Winner] = MWinType.Time;
            MChar winner = Winner == 0 ? p0 : p1;
            if (winner.Life >= winner.LifeMax)
            {
                WinType[Winner] = MWinType.Perfect;
            }
        }

        // KO 胜方 winType：满血取胜 = Perfect（system.go:3181 checkPerfect 简化）。
        // Ikemen reference: src/system.go checkPerfect logic marks perfect wins when the KO winner kept full life.
        void SetWinTypeKo(MChar p0, MChar p1)
        {
            if (Winner < 0)
            {
                return;
            }
            MChar w = Winner == 0 ? p0 : p1;
            WinType[Winner] = w.Life >= w.LifeMax ? MWinType.Perfect : MWinType.Normal;
        }

        // intro 倒到 0：授控 + 离开入场态（system.go:2991-3006）。
        // Ikemen reference: src/system.go:2991 grants ctrl and self-states intro characters back to state 0 when fight starts.
        void EnterFight()
        {
            bool forceIntroAssertExit = _assertIntroHoldFrames >= ClampNonNegative(MaxAssertIntroHoldTime);
            _assertIntroHoldFrames = 0;
            _engine.StartRound();   // 授 ctrl/keyctrl（对齐 Ikemen setCtrl(true)）
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                MChar c = _engine.Chars[i];
                if (IsInState(c, IntroStateCandidates) || (forceIntroAssertExit && IsIntroAsserted(c)))
                {
                    c.QueueTransition(0, c.PlayerNo);   // 鞠躬结束回中立（仅入场态角色，保护自定义态）
                }
            }
            Timer = RoundTime;   // 计时在进入战斗起算（对齐 Ikemen fight 开始）
        }

        bool AnyIntroAsserted()
        {
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                MChar c = _engine.Chars[i];
                if (Alive(c) && (c.AssertFlags & (int)MAssertFlag.Intro) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        // 送胜负/平局姿态：每方仅一次。胜者 180/195、活着的败者 170、平局活方 175；KO 倒地者不摆（保持 5150）。
        // Ikemen reference: src/system.go:3144 sends surviving characters to win, lose, or draw pose states.
        void SendPoses()
        {
            for (int i = 0; i < _engine.Chars.Count && i < 2; i++)
            {
                if (_posed[i])
                {
                    continue;
                }
                MChar c = _engine.Chars[i];
                if (!Alive(c))
                {
                    _posed[i] = true;   // 阵亡者不摆 pose（保持倒地），但标记免重复检查
                    continue;
                }
                int[] candidates;
                if (Winner < 0)
                {
                    candidates = DrawPoseStateCandidates;       // 平局
                }
                else if (i == Winner)
                {
                    candidates = WinPoseStateCandidates;        // 胜者
                }
                else
                {
                    candidates = LosePoseStateCandidates;       // 活着的败者（如超时败方）
                }
                SendToStateIfExists(c, candidates);
                _posed[i] = true;
            }
        }

        // 把角色送入候选列表里首个存在的状态（缺则不动，保护无对应态的角色）。
        // Ikemen reference: src/char.go:5966 selfStatenoExist guards selfState transitions to optional common pose states.
        void SendToStateIfExists(MChar c, int[] candidates)
        {
            if (c == null || c.OwnData == null || candidates == null)
            {
                return;
            }
            for (int i = 0; i < candidates.Length; i++)
            {
                if (c.OwnData.States.ContainsKey(candidates[i]))
                {
                    c.QueueTransition(candidates[i], c.PlayerNo);
                    c.Ctrl = false;
                    c.KeyCtrl = false;
                    return;
                }
            }
        }

        // Project-specific: helper for detecting C# intro/pose candidate states before issuing Ikemen-style transitions.
        static bool IsInState(MChar c, int[] candidates)
        {
            if (c == null || candidates == null)
            {
                return false;
            }
            for (int i = 0; i < candidates.Length; i++)
            {
                if (c.StateNo == candidates[i])
                {
                    return true;
                }
            }
            return false;
        }

        // Ikemen reference: src/char.go life checks use life > 0 for KO/living character decisions.
        static bool Alive(MChar c)
        {
            return c != null && c.Life > 0;
        }

        // Project-specific: clamps configurable C# timing values before applying Ikemen-style round thresholds.
        static int ClampPositive(int v)
        {
            return v < 1 ? 1 : v;
        }

        static int ClampNonNegative(int v)
        {
            return v < 0 ? 0 : v;
        }

        static bool IsIntroAsserted(MChar c)
        {
            return c != null && (c.AssertFlags & (int)MAssertFlag.Intro) != 0;
        }

        // 开局/回合开始：把双方送入入场态（鞠躬）。无入场态者保持当前态。
        // Ikemen reference: src/system.go intro setup sends characters to intro states such as 190/191 before fight.
        void BeginRoundIntro()
        {
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                SendToStateIfExists(_engine.Chars[i], IntroStateCandidates);
            }
        }

        // Ikemen reference: src/system.go:1682 roundOver and src/system.go:1420 matchOver advance rounds or finish the match.
        void AdvanceAfterRound()
        {
            // 单场模式：本场已演完，冻结于 Over，交外层编排（换人/下一场）。
            if (SingleRound)
            {
                BoutComplete = true;
                return;
            }

            // 几胜制满足 → 整场结束（matchOver():1420）。
            if (Winner >= 0 && Winner < RoundsWon.Length && RoundsWon[Winner] >= RoundsToWin)
            {
                MatchOver = true;
                MatchWinner = Winner;
                return;
            }

            // 下一回合：复位 intro / 计时 / 胜负标记 / 角色，重新鞠躬入场。
            RoundNo++;
            Winner = -1;
            FinishType = MFinishType.NotYet;
            WinType[0] = WinType[1] = MWinType.Normal;
            Intro = IntroTime + 1;
            Timer = RoundTime;
            _scored = false;
            _ctrlRemoved = false;
            _assertIntroHoldFrames = 0;
            _overReadyWaitDown = -1;
            _posed[0] = _posed[1] = false;
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                ResetCombatant(_engine.Chars[i]);
            }
            BeginRoundIntro();
        }

        // 回合间复位：满血、回站立 0、清受击态、收 ctrl（入场期再授）。位置复位归 demo 场景（R-ARENA）。
        // 能量(Power)跨回合保留（MUGEN 行为），故此处不动。
        // Ikemen reference: src/system.go new-round setup restores life/state/control while preserving power across rounds.
        static void ResetCombatant(MChar c)
        {
            c.Life = c.LifeMax;
            c.QueueTransition(0, c.PlayerNo);
            c.MoveType = 1;      // I
            c.Ctrl = false;
            c.KeyCtrl = false;
            c.Vel = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            c.Ghv = new MGetHitVar();
            c.FallTime = 0;
        }

        // ── 调试修改器入口（供 MBattleDebugController；不进哈希路径，仅 Editor/CLI 用）──

        /// <summary>跳过出场直接进入战斗（调试）：intro=0、授控、入场态角色回 0、计时重置。</summary>
        // Project-specific: debug entry point that skips Ikemen intro timing for editor and CLI tests.
        public void ForceFight()
        {
            Intro = 0;
            FinishType = MFinishType.NotYet;
            Winner = -1;
            _scored = false;
            _ctrlRemoved = false;
            _assertIntroHoldFrames = 0;
            _overReadyWaitDown = -1;
            _posed[0] = _posed[1] = false;
            EnterFight();
            _stateTimer = 0;
            _prevState = State;
            SyncRoundStateToChars();
        }

        /// <summary>复位当前局面（调试）：双方满血、回站立、进入战斗。不推进回合号。</summary>
        // Project-specific: debug reset entry point for the C# harness; Ikemen reaches this through normal round setup.
        public void ForceReset()
        {
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                MChar c = _engine.Chars[i];
                c.Life = c.LifeMax;
                c.QueueTransition(0, c.PlayerNo);
                c.MoveType = 1;
                c.FallTime = 0;
                c.Vel = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            }
            ForceFight();
        }

        // Project-specific: rollback determinism hash for C# round-system state; Ikemen has no equivalent API.
        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(_introTime); hash.AddInt32(OverWaitTime); hash.AddInt32(OverHitTime);
            hash.AddInt32(WinPoseTime); hash.AddInt32(OverReadyWaitTime);
            hash.AddInt32(RoundTime); hash.AddInt32(RoundsToWin);
            hash.AddInt32(MaxAssertIntroHoldTime); hash.AddInt32(_assertIntroHoldFrames);
            hash.AddInt32(_overReadyWaitDown);
            WriteArrayHash(ref hash, IntroStateCandidates);
            WriteArrayHash(ref hash, WinPoseStateCandidates);
            WriteArrayHash(ref hash, LosePoseStateCandidates);
            WriteArrayHash(ref hash, DrawPoseStateCandidates);
            hash.AddInt32(Intro); hash.AddInt32(RoundNo); hash.AddInt32(Timer);
            hash.AddInt32(Winner); hash.AddInt32((int)FinishType);
            hash.AddBool(MatchOver); hash.AddInt32(MatchWinner);
            hash.AddInt32(RoundsWon[0]); hash.AddInt32(RoundsWon[1]);
            hash.AddInt32((int)WinType[0]); hash.AddInt32((int)WinType[1]);
            hash.AddBool(_scored); hash.AddBool(_ctrlRemoved);
            hash.AddBool(_posed[0]); hash.AddBool(_posed[1]);
            hash.AddBool(SingleRound); hash.AddBool(BoutComplete);
            hash.AddInt32(_stateTimer); hash.AddInt32((int)_prevState);
            hash.AddBool(_ticked);
        }

        static void WriteArrayHash(ref Hash64 hash, int[] values)
        {
            hash.AddInt32(values != null ? values.Length : 0);
            if (values == null)
            {
                return;
            }
            for (int i = 0; i < values.Length; i++)
            {
                hash.AddInt32(values[i]);
            }
        }

        static int[] CloneArray(int[] source)
        {
            return source != null ? (int[])source.Clone() : null;
        }

        static MWinType[] CloneArray(MWinType[] source)
        {
            return source != null ? (MWinType[])source.Clone() : null;
        }

        static bool[] CloneArray(bool[] source)
        {
            return source != null ? (bool[])source.Clone() : null;
        }

        static void CopyArray(int[] source, int[] target)
        {
            if (source == null || target == null)
            {
                return;
            }
            int count = source.Length < target.Length ? source.Length : target.Length;
            for (int i = 0; i < count; i++)
            {
                target[i] = source[i];
            }
        }

        static void CopyArray(MWinType[] source, MWinType[] target)
        {
            if (source == null || target == null)
            {
                return;
            }
            int count = source.Length < target.Length ? source.Length : target.Length;
            for (int i = 0; i < count; i++)
            {
                target[i] = source[i];
            }
        }

        static void CopyArray(bool[] source, bool[] target)
        {
            if (source == null || target == null)
            {
                return;
            }
            int count = source.Length < target.Length ? source.Length : target.Length;
            for (int i = 0; i < count; i++)
            {
                target[i] = source[i];
            }
        }
    }
}
