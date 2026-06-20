// 车轮战 / turns 模式（对齐 Ikemen TM_Turns）：1v1 同屏，但每名玩家拥有一队角色（默认 3 名），
// 当前出场角色被 KO（或本场判负）后换上队内下一名；胜方角色留场并保留生命值；
// 一方整队打光 → 整场结束。每场用一个 SingleRound 的 MRoundSystem 跑完一回合再交接。
//
// 与回滚/网络：每场重建 MBattleEngine（确定性：双端同 roster、同帧因 BoutComplete 触发换人），
// 当前出场角色经 Engine.ComputeHash 纳入对账。胜方生命继承并按上一名对手登场生命的 10% 回血。
using System;
using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle
{
    /// <summary>车轮战编排器：两队各 N 名，1v1 同屏轮流出场，整队打光判负。</summary>
    public sealed class MTeamMatchSnapshot
    {
        public int BoutNo;
        public int[] ActiveIndex;
        public bool MatchOver;
        public int MatchWinner;
        public MBattleEngineSnapshot Engine;
        public MRoundSystemSnapshot Round;
        public int[][] StoredLife;
        public int[][] StoredLifeMax;
        public int[] BoutStartLife;
    }

    public sealed class MTeamMatch
    {
        readonly List<MCharData>[] _rosters;
        readonly int[] _activeIndex = new int[2];
        readonly int[][] _storedLife;
        readonly int[][] _storedLifeMax;
        readonly int[] _boutStartLife = new int[2];
        readonly Action<MRoundSystem> _configureRound;
        readonly int _startSeparation;
        readonly int _stageHalfWidth;

        public MBattleEngine Engine { get; private set; }
        public MRoundSystem Round { get; private set; }
        public int BoutNo { get; private set; } = 1;
        public bool MatchOver { get; private set; }
        public int MatchWinner { get; private set; } = -1;

        /// <summary>每场分出胜负后触发：(本场胜方 0/1，-1=平局)。</summary>
        public event Action<int> OnBoutResolved;
        /// <summary>每场开打时触发（角色已就位）。</summary>
        public event Action OnBoutStarted;

        // Ikemen reference: src/system.go TeamMode TM_Turns sets per-side rosters and advances active fighters across bouts.
        public MTeamMatch(List<MCharData> team0, List<MCharData> team1,
            Action<MRoundSystem> configureRound = null, int startSeparation = 50, int stageHalfWidth = 240)
        {
            if (team0 == null || team0.Count == 0 || team1 == null || team1.Count == 0)
            {
                throw new ArgumentException("每队至少 1 名角色");
            }
            _rosters = new[] { team0, team1 };
            _storedLife = new[] { CreateLifeSlots(team0.Count, -1), CreateLifeSlots(team1.Count, -1) };
            _storedLifeMax = new[] { CreateLifeSlots(team0.Count, 0), CreateLifeSlots(team1.Count, 0) };
            _configureRound = configureRound;
            _startSeparation = startSeparation;
            _stageHalfWidth = stageHalfWidth;
            StartBout();
        }

        /// <summary>玩家当前出场角色在其队内的索引。</summary>
        // Project-specific: exposes current C# roster slot for UI/tests; Ikemen keeps this in System team selection state.
        public int ActiveIndex(int player) => _activeIndex[player];

        /// <summary>玩家剩余（含当前出场）角色数。</summary>
        // Project-specific: exposes remaining C# turns roster count for HUD/tests.
        public int Remaining(int player) => _rosters[player].Count - _activeIndex[player];

        public MTeamMatchSnapshot Snapshot()
        {
            return new MTeamMatchSnapshot
            {
                BoutNo = BoutNo,
                ActiveIndex = new[] { _activeIndex[0], _activeIndex[1] },
                MatchOver = MatchOver,
                MatchWinner = MatchWinner,
                Engine = Engine != null ? Engine.Snapshot() : null,
                Round = Round != null ? Round.Snapshot() : null,
                StoredLife = CloneJagged(_storedLife),
                StoredLifeMax = CloneJagged(_storedLifeMax),
                BoutStartLife = new[] { _boutStartLife[0], _boutStartLife[1] },
            };
        }

        public void Restore(MTeamMatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.ActiveIndex != null)
            {
                for (int i = 0; i < _activeIndex.Length && i < snapshot.ActiveIndex.Length; i++)
                {
                    _activeIndex[i] = snapshot.ActiveIndex[i];
                }
            }
            CopyJagged(snapshot.StoredLife, _storedLife);
            CopyJagged(snapshot.StoredLifeMax, _storedLifeMax);
            if (snapshot.BoutStartLife != null)
            {
                for (int i = 0; i < _boutStartLife.Length && i < snapshot.BoutStartLife.Length; i++)
                {
                    _boutStartLife[i] = snapshot.BoutStartLife[i];
                }
            }

            if (CanStartBout())
            {
                StartBout(notify: false);
                if (snapshot.Engine != null)
                {
                    Engine.Restore(snapshot.Engine);
                }
                if (snapshot.Round != null)
                {
                    Round.Restore(snapshot.Round);
                }
            }
            else
            {
                if (snapshot.Engine != null && Engine != null)
                {
                    Engine.Restore(snapshot.Engine);
                }
                if (snapshot.Round != null && Round != null)
                {
                    Round.Restore(snapshot.Round);
                }
            }

            BoutNo = snapshot.BoutNo;
            MatchOver = snapshot.MatchOver;
            MatchWinner = snapshot.MatchWinner;
        }

        /// <summary>车轮战对账哈希：当前出场、外层胜负、回合状态和引擎状态一起入账。</summary>
        public ulong ComputeHash()
        {
            Hash64 hash = Hash64.Create();
            hash.AddInt32(BoutNo);
            hash.AddInt32(_activeIndex[0]);
            hash.AddInt32(_activeIndex[1]);
            WriteJaggedArrayHash(ref hash, _storedLife);
            WriteJaggedArrayHash(ref hash, _storedLifeMax);
            hash.AddInt32(_boutStartLife[0]);
            hash.AddInt32(_boutStartLife[1]);
            hash.AddBool(MatchOver);
            hash.AddInt32(MatchWinner);
            if (Round != null)
            {
                Round.WriteHash(ref hash);
            }
            hash.AddUInt64(Engine != null ? Engine.ComputeHash() : 0UL);
            return hash.Value;
        }

        /// <summary>推进一帧：跑当前场；本场演完则换人 / 判整场。</summary>
        // Ikemen reference: src/system.go step/update loop advances a turns bout until roundOver then team state changes.
        public void Tick(IReadOnlyList<MInput> inputs)
        {
            if (MatchOver)
            {
                return;
            }
            Round.Tick(inputs);
            if (Round.BoutComplete)
            {
                ResolveBout();
            }
        }

        // Ikemen reference: src/system.go TM_Turns character setup spawns the current roster member for each side.
        bool CanStartBout()
        {
            return _activeIndex[0] >= 0 && _activeIndex[0] < _rosters[0].Count &&
                   _activeIndex[1] >= 0 && _activeIndex[1] < _rosters[1].Count;
        }

        void StartBout(bool notify = true)
        {
            MCharData data0 = _rosters[0][_activeIndex[0]];
            MCharData data1 = _rosters[1][_activeIndex[1]];
            MChar p0 = MCharLoader.SpawnChar(data0, 0, startStateNo: 0, startAnimNo: 0);
            MChar p1 = MCharLoader.SpawnChar(data1, 1, startStateNo: 0, startAnimNo: 0);
            EnsureAlive(p0);
            EnsureAlive(p1);
            ApplyRosterLife(0, _activeIndex[0], p0);
            ApplyRosterLife(1, _activeIndex[1], p1);
            _boutStartLife[0] = p0.Life;
            _boutStartLife[1] = p1.Life;

            int half = _startSeparation / 2;
            p0.Pos = new FVector3(FFloat.FromInt(-half), FFloat.Zero, FFloat.Zero);
            p0.Facing = FFloat.One;
            p1.Pos = new FVector3(FFloat.FromInt(half), FFloat.Zero, FFloat.Zero);
            p1.Facing = -FFloat.One;

            MBattleEngine engine = new MBattleEngine();
            engine.Add(p0, data0);
            engine.Add(p1, data1);
            engine.Chars[0].Id = 1;
            engine.Chars[1].Id = 2;
            engine.LinkPair();
            if (_stageHalfWidth > 0)
            {
                engine.Stage.SetSymmetric(_stageHalfWidth);
            }

            MRoundSystem round = new MRoundSystem(engine) { SingleRound = true };
            _configureRound?.Invoke(round);

            Engine = engine;
            Round = round;
            if (notify)
            {
                OnBoutStarted?.Invoke();
            }
        }

        // Ikemen reference: src/system.go TM_Turns loss handling advances the defeated side's active roster index.
        void ResolveBout()
        {
            int winner = Round.Winner;
            StoreBoutResult(winner);
            OnBoutResolved?.Invoke(winner);

            if (winner < 0)
            {
                // 平局（双 KO / 超时同血）：双方各损一员。
                _activeIndex[0]++;
                _activeIndex[1]++;
            }
            else
            {
                // 负方换下一员；胜方留场（同索引，保留生命并按上一名对手登场生命回血）。
                _activeIndex[1 - winner]++;
            }

            bool team0Out = Remaining(0) <= 0;
            bool team1Out = Remaining(1) <= 0;
            if (team0Out || team1Out)
            {
                MatchOver = true;
                MatchWinner = team0Out && team1Out ? -1 : (team0Out ? 1 : 0);
                FreezeFinalBout(MatchWinner);
                return;
            }

            BoutNo++;
            StartBout();
        }

        void FreezeFinalBout(int matchWinner)
        {
            if (Engine == null)
            {
                return;
            }

            for (int i = 0; i < Engine.Chars.Count && i < 2; i++)
            {
                MChar c = Engine.Chars[i];
                c.Ctrl = false;
                c.KeyCtrl = false;
                c.Vel = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
                c.PendingTransition = MStateTransition.None;
                c.Guarding = false;

                bool shouldIdle = c.Life > 0 && (matchWinner < 0 || i == matchWinner);
                if (shouldIdle)
                {
                    PutCombatantInIdle(c);
                }
            }
        }

        static void PutCombatantInIdle(MChar c)
        {
            c.PrevStateNo = c.StateNo;
            c.PrevStateType = c.StateType;
            c.StateNo = 0;
            c.StatePlayerNo = c.PlayerNo;
            c.StateOwner = null;
            c.Time = 0;
            c.StateType = 1;
            c.MoveType = 1;
            c.Physics = 1;
            c.Ctrl = false;
            c.KeyCtrl = false;
            c.Hitstop = 0;
            c.PendingLifeDamage = 0;
            c.FallTime = 0;
            c.Ghv = new MGetHitVar();
            c.PlayAnimation(0, c.PlayerNo, c.PlayerNo);
        }

        // Project-specific: normalizes C# fixture life values before starting an Ikemen-style turns bout.
        static void EnsureAlive(MChar c)
        {
            if (c.LifeMax <= 0)
            {
                c.LifeMax = 1000;
            }
            c.Life = c.LifeMax;
        }

        void ApplyRosterLife(int player, int slot, MChar c)
        {
            _storedLifeMax[player][slot] = c.LifeMax;
            int storedLife = _storedLife[player][slot];
            if (storedLife >= 0)
            {
                c.Life = ClampLife(storedLife, c.LifeMax);
            }
            else
            {
                c.Life = c.LifeMax;
            }
            _storedLife[player][slot] = c.Life;
        }

        void StoreBoutResult(int winner)
        {
            if (Engine == null || Engine.Chars.Count < 2)
            {
                return;
            }

            int life0 = ClampLife(Engine.Chars[0].Life, Engine.Chars[0].LifeMax);
            int life1 = ClampLife(Engine.Chars[1].Life, Engine.Chars[1].LifeMax);

            if (winner < 0)
            {
                _storedLife[0][_activeIndex[0]] = 0;
                _storedLife[1][_activeIndex[1]] = 0;
                return;
            }

            int loser = 1 - winner;
            int winnerSlot = _activeIndex[winner];
            int loserSlot = _activeIndex[loser];
            int winnerMax = _storedLifeMax[winner][winnerSlot] > 0
                ? _storedLifeMax[winner][winnerSlot]
                : Engine.Chars[winner].LifeMax;
            int winnerLife = winner == 0 ? life0 : life1;
            int heal = _boutStartLife[loser] / 10;
            _storedLife[winner][winnerSlot] = ClampLife(System.Math.Max(1, winnerLife + heal), winnerMax);
            _storedLife[loser][loserSlot] = 0;
        }

        static int ClampLife(int value, int max)
        {
            if (max <= 0)
            {
                max = 1000;
            }
            if (value < 0)
            {
                return 0;
            }
            return value > max ? max : value;
        }

        static int[] CreateLifeSlots(int count, int value)
        {
            int[] slots = new int[count];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = value;
            }
            return slots;
        }

        static int[][] CloneJagged(int[][] source)
        {
            if (source == null)
            {
                return null;
            }
            int[][] clone = new int[source.Length][];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                {
                    continue;
                }
                clone[i] = new int[source[i].Length];
                Array.Copy(source[i], clone[i], source[i].Length);
            }
            return clone;
        }

        static void CopyJagged(int[][] source, int[][] target)
        {
            if (source == null || target == null)
            {
                return;
            }
            for (int i = 0; i < source.Length && i < target.Length; i++)
            {
                if (source[i] == null || target[i] == null)
                {
                    continue;
                }
                int count = System.Math.Min(source[i].Length, target[i].Length);
                for (int j = 0; j < count; j++)
                {
                    target[i][j] = source[i][j];
                }
            }
        }

        static void WriteJaggedArrayHash(ref Hash64 hash, int[][] values)
        {
            if (values == null)
            {
                hash.AddInt32(-1);
                return;
            }
            hash.AddInt32(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                {
                    hash.AddInt32(-1);
                    continue;
                }
                hash.AddInt32(values[i].Length);
                for (int j = 0; j < values[i].Length; j++)
                {
                    hash.AddInt32(values[i][j]);
                }
            }
        }
    }
}
