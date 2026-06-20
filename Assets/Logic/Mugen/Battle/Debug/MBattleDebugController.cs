// 战斗调试修改器（modifier / cheat）—— 纯 C# 定点，无 Unity/IO，可被 dotnet test 编入。
// 给 agent / 开发者一个统一命令面：改血、上帝模式、跳过场、设倒计时、强制胜负、设状态/位置、暂停/单步、复位。
// ⚠️ 仅调试用：会破坏 lockstep 确定性，联机/回放务必关闭（不要进哈希路径，不在 Tick 内自动调用）。
// 命令面同时供 OnGUI 作弊面板（预设按钮）与文件/CLI 桥（每行一条命令）复用 —— 单一入口 Execute(line)。
// Ikemen 对照：调试能力散落在 Lua debug 脚本 + sys.debugXXX；本类把 1v1 常用调试动作聚成一处。
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Lockstep.Math;
using Lockstep.Mugen.Char;

namespace Lockstep.Mugen.Battle.Debugging
{
    /// <summary>
    /// 战斗调试修改器：持有 <see cref="MBattleEngine"/> + <see cref="MRoundSystem"/> 引用，
    /// 通过公开字段（Life/Pos/State/...）操作运行态。命令经 <see cref="Execute"/> 单行解析执行。
    /// 帧步进/暂停由 <see cref="ShouldTick"/> 门控（宿主在推进前问一次）；上帝模式由 <see cref="PostTick"/> 兜底。
    /// </summary>
    public sealed class MBattleDebugController
    {
        readonly MBattleEngine _engine;
        readonly MRoundSystem _round;
        readonly HashSet<int> _godMode = new HashSet<int>();

        /// <summary>是否暂停模拟（宿主据 <see cref="ShouldTick"/> 决定是否推进）。</summary>
        public bool Paused;
        int _pendingSteps;

        // Project-specific: debug command facade over C# battle services; Ikemen exposes comparable debug state through engine/debug tooling, not this object.
        public MBattleDebugController(MBattleEngine engine, MRoundSystem round)
        {
            _engine = engine;
            _round = round;
        }

        /// <summary>宿主每帧推进前问一次：返回 true 才推进。暂停时仅放行排队的单步帧。</summary>
        // Project-specific: host-side pause/single-step gate; Ikemen frame advancement is driven from src/system.go main round loop.
        public bool ShouldTick()
        {
            if (!Paused)
            {
                return true;
            }
            if (_pendingSteps > 0)
            {
                _pendingSteps--;
                return true;
            }
            return false;
        }

        /// <summary>每次实际推进一帧后调用：兜底强制上帝模式角色满血（放在哈希之外）。</summary>
        // Project-specific: debug post-frame god-mode repair; Ikemen has no battle-logic equivalent outside debug/cheat tooling.
        public void PostTick()
        {
            foreach (int idx in _godMode)
            {
                if (idx >= 0 && idx < _engine.Chars.Count)
                {
                    MChar c = _engine.Chars[idx];
                    if (c.Life < c.LifeMax)
                    {
                        c.Life = c.LifeMax;
                    }
                }
            }
        }

        /// <summary>
        /// 执行一行命令（空白分隔）。返回人类可读结果（也作 CLI 回显）。未知命令返回 "ERR ..."。
        /// 命令清单见 <see cref="Help"/>。
        /// </summary>
        // Project-specific: parses C# debug console commands; Ikemen debug controls are not part of src/char.go action logic.
        public string Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#"))   // 注释行
            {
                return string.Empty;
            }
            string[] tok = trimmed.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            string cmd = tok[0].ToLowerInvariant();

            switch (cmd)
            {
                case "help": return Help();
                case "state": return StateJson();
                case "sethp": return WithIntInt(tok, (i, v) => { Char(i).Life = Clamp(v, 0, Char(i).LifeMax); return Ok("HP", i, Char(i).Life); });
                case "sethpp": return WithIntInt(tok, (i, p) => { Char(i).Life = Clamp(Char(i).LifeMax * p / 100, 0, Char(i).LifeMax); return Ok("HP%", i, Char(i).Life); });
                case "damage": return WithIntInt(tok, (i, v) => { Char(i).Life = Clamp(Char(i).Life - v, 0, Char(i).LifeMax); return Ok("HP", i, Char(i).Life); });
                case "heal": return WithInt(tok, i => { Char(i).Life = Char(i).LifeMax; return Ok("HP", i, Char(i).Life); });
                case "kill": return WithInt(tok, i => { Char(i).Life = 0; return Ok("HP", i, 0); });
                case "power": return WithIntInt(tok, (i, v) => { Char(i).Power = Clamp(v, 0, Char(i).PowerMax); return Ok("Power", i, Char(i).Power); });
                case "god": return WithIntFlag(tok, (i, on) => { if (on) { _godMode.Add(i); } else { _godMode.Remove(i); } return "OK god[" + i + "]=" + on; });
                case "setstate": return WithIntInt(tok, (i, s) => { Char(i).QueueTransition(s, Char(i).PlayerNo); return "OK state[" + i + "]->" + s; });
                case "pos": return SetPos(tok);
                case "face": return SetFace(tok);
                case "sep": return SetSeparation(tok);
                case "skipintro": return SkipIntro();
                case "timer": return SetTimer(tok);
                case "win": return WithInt(tok, ForceWin);
                case "resetround": return ResetRound();
                case "pause": Paused = true; return "OK paused";
                case "resume": Paused = false; _pendingSteps = 0; return "OK resumed";
                case "step": return Step(tok);
                default: return "ERR unknown cmd '" + cmd + "' (try help)";
            }
        }

        // Project-specific: returns C# debug command text; no Ikemen battle-runtime counterpart.
        public string Help()
        {
            return "cmds: state | sethp <i> <v> | sethpp <i> <pct> | damage <i> <v> | heal <i> | kill <i> | "
                 + "power <i> <v> | god <i> <on|off> | setstate <i> <no> | pos <i> <x> <y> | face <i> <l|r> | "
                 + "sep <units> | skipintro | timer <sec> | win <i> | resetround | pause | resume | step [n]";
        }

        // ── 状态导出（手搓 JSON，避免依赖；供 CLI/MCP agent 读取断言） ──
        // Project-specific: serializes C# debug state for tools; fields mirror src/system.go round state and src/char.go Char runtime state.
        public string StateJson()
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append("{\"round\":{");
            sb.Append("\"state\":\"").Append(_round.State).Append("\",");
            sb.Append("\"stateName\":").Append((int)_round.State).Append(',');
            sb.Append("\"stateTimer\":").Append(_round.StateTimer).Append(',');
            sb.Append("\"roundNo\":").Append(_round.RoundNo).Append(',');
            sb.Append("\"timer\":").Append(_round.Timer).Append(',');
            sb.Append("\"timerSeconds\":").Append(_round.TimerSeconds).Append(',');
            sb.Append("\"winner\":").Append(_round.Winner).Append(',');
            sb.Append("\"matchOver\":").Append(_round.MatchOver ? "true" : "false").Append(',');
            sb.Append("\"matchWinner\":").Append(_round.MatchWinner).Append(',');
            sb.Append("\"roundsWon\":[").Append(_round.RoundsWon[0]).Append(',').Append(_round.RoundsWon[1]).Append("]},");
            sb.Append("\"paused\":").Append(Paused ? "true" : "false").Append(',');
            sb.Append("\"chars\":[");
            for (int i = 0; i < _engine.Chars.Count; i++)
            {
                MChar c = _engine.Chars[i];
                if (i > 0) { sb.Append(','); }
                sb.Append("{\"idx\":").Append(i);
                sb.Append(",\"life\":").Append(c.Life).Append(",\"lifeMax\":").Append(c.LifeMax);
                sb.Append(",\"power\":").Append(c.Power);
                sb.Append(",\"state\":").Append(c.StateNo).Append(",\"anim\":").Append(c.AnimNo).Append(",\"elem\":").Append(c.AnimElem);
                sb.Append(",\"moveType\":").Append(c.MoveType).Append(",\"ctrl\":").Append(c.Ctrl ? "true" : "false");
                sb.Append(",\"god\":").Append(_godMode.Contains(i) ? "true" : "false");
                sb.Append(",\"posX\":").Append(FixedToString(c.Pos.X)).Append(",\"posY\":").Append(FixedToString(c.Pos.Y));
                sb.Append(",\"facing\":").Append(c.Facing.Raw >= 0 ? 1 : -1);
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ── 命令实现 ──
        // Project-specific: indexes C# runtime characters for debug commands; Ikemen keeps player lookup inside System/CharList.
        MChar Char(int i) => _engine.Chars[i];

        // Project-specific: debug shortcut into Fight; related Ikemen flow is src/system.go round intro-to-fight state progression.
        string SkipIntro()
        {
            // 跳过出场直接进入战斗（intro=0、授控、入场态角色回 0、计时重置）。
            _round.ForceFight();
            return "OK skipintro -> Fight";
        }

        // Project-specific: debug timer mutation; related Ikemen timer ownership lives in src/system.go round/match state.
        string SetTimer(string[] tok)
        {
            if (tok.Length < 2 || !int.TryParse(tok[1], out int sec))
            {
                return "ERR timer <seconds>";
            }
            _round.Timer = sec * MRoundSystem.TicksPerSecond;
            return "OK timer=" + sec + "s (" + _round.Timer + " ticks)";
        }

        // 强制 idx 胜：把对手打到 0 血，并确保处于 Fight 期，让 CheckRoundEnd 正常判定（走真实回合流程）。
        // Project-specific: debug KO helper; related Ikemen win detection is src/system.go round end/check win logic.
        string ForceWin(int i)
        {
            int opp = i == 0 ? 1 : 0;
            if (opp >= _engine.Chars.Count)
            {
                return "ERR need 2 chars";
            }
            _godMode.Remove(opp);
            _engine.Chars[opp].Life = 0;
            if (_round.State != MRoundState.Fight)
            {
                SkipIntro();
            }
            return "OK forcing win[" + i + "] (opp KO next fight tick)";
        }

        // 把双方满血、回站立 0、授 ctrl、进入 Fight（不推进回合号；纯调试复位当前局面）。
        // Ikemen reference: src/system.go resetRound/round reset flow; C# exposes it as a debug command.
        string ResetRound()
        {
            _round.ForceReset();
            return "OK resetround";
        }

        // Project-specific: debug single-step command; Ikemen advances frames through src/system.go round loop rather than a console step API.
        string Step(string[] tok)
        {
            int n = 1;
            if (tok.Length >= 2)
            {
                int.TryParse(tok[1], out n);
            }
            if (n < 1) { n = 1; }
            Paused = true;
            _pendingSteps += n;
            return "OK step +" + n;
        }

        // Ikemen reference: src/char.go setPos/posUpdate; C# debug command directly mutates character position.
        string SetPos(string[] tok)
        {
            if (tok.Length < 4 || !int.TryParse(tok[1], out int i)
                || !TryParseFixed(tok[2], out FFloat x) || !TryParseFixed(tok[3], out FFloat y))
            {
                return "ERR pos <i> <x> <y>";
            }
            if (!Valid(i)) { return "ERR idx"; }
            Char(i).Pos = new FVector3(x, y, FFloat.Zero);
            return "OK pos[" + i + "]=(" + FixedToString(x) + "," + FixedToString(y) + ")";
        }

        // Ikemen reference: src/char.go facing/turn handling; C# debug command directly mutates Facing.
        string SetFace(string[] tok)
        {
            if (tok.Length < 3 || !int.TryParse(tok[1], out int i) || !Valid(i))
            {
                return "ERR face <i> <l|r>";
            }
            string d = tok[2].ToLowerInvariant();
            Char(i).Facing = d == "l" || d == "-1" ? -FFloat.One : FFloat.One;
            return "OK face[" + i + "]=" + (Char(i).Facing.Raw < 0 ? "L" : "R");
        }

        // 面对面摆位：idx0 在左面右，idx1 在右面左，间距 units（MUGEN 单位）。
        // Project-specific: debug 1v1 spacing helper; Ikemen stage/round positioning is handled during system round setup.
        string SetSeparation(string[] tok)
        {
            if (tok.Length < 2 || !int.TryParse(tok[1], out int units) || _engine.Chars.Count < 2)
            {
                return "ERR sep <units> (need 2 chars)";
            }
            int half = units / 2;
            _engine.Chars[0].Pos = new FVector3(FFloat.FromInt(-half), FFloat.Zero, FFloat.Zero);
            _engine.Chars[0].Facing = FFloat.One;
            _engine.Chars[1].Pos = new FVector3(FFloat.FromInt(half), FFloat.Zero, FFloat.Zero);
            _engine.Chars[1].Facing = -FFloat.One;
            return "OK sep=" + units;
        }

        // ── 解析辅助 ──
        // Project-specific: debug index validation for C# character list; Ikemen uses System/CharList ownership checks.
        bool Valid(int i) => i >= 0 && i < _engine.Chars.Count;

        // Project-specific: debug parser helper for one integer argument; no Ikemen battle-runtime counterpart.
        string WithInt(string[] tok, System.Func<int, string> body)
        {
            if (tok.Length < 2 || !int.TryParse(tok[1], out int i) || !Valid(i))
            {
                return "ERR " + tok[0] + " <i>";
            }
            return body(i);
        }

        // Project-specific: debug parser helper for two integer arguments; no Ikemen battle-runtime counterpart.
        string WithIntInt(string[] tok, System.Func<int, int, string> body)
        {
            if (tok.Length < 3 || !int.TryParse(tok[1], out int i) || !int.TryParse(tok[2], out int v) || !Valid(i))
            {
                return "ERR " + tok[0] + " <i> <v>";
            }
            return body(i, v);
        }

        // Project-specific: debug parser helper for integer plus boolean flag; no Ikemen battle-runtime counterpart.
        string WithIntFlag(string[] tok, System.Func<int, bool, string> body)
        {
            if (tok.Length < 3 || !int.TryParse(tok[1], out int i) || !Valid(i))
            {
                return "ERR " + tok[0] + " <i> <on|off>";
            }
            string f = tok[2].ToLowerInvariant();
            bool on = f == "on" || f == "1" || f == "true";
            return body(i, on);
        }

        // Project-specific: debug response formatter; no Ikemen battle-runtime counterpart.
        static string Ok(string label, int idx, int val) => "OK " + label + "[" + idx + "]=" + val;

        // Project-specific: bounds debug mutations before applying them to C# battle state; Ikemen clamps at each owning system.
        static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        // 定点解析：整数 MUGEN 单位（位置粒度足够调试用）。Logic 层禁 float/double，故只收整数。
        // Project-specific: debug integer-to-fixed parser; Ikemen parses author data/commands through compiler/input systems.
        static bool TryParseFixed(string s, out FFloat result)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
            {
                result = FFloat.FromInt(iv);
                return true;
            }
            result = FFloat.Zero;
            return false;
        }

        // Project-specific: debug fixed-point formatter; no Ikemen battle-runtime counterpart.
        static string FixedToString(FFloat f) => f.ToInt().ToString(CultureInfo.InvariantCulture);
    }
}
