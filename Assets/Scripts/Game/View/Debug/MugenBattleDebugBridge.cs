// 战斗调试桥（表现层 / IO 边界）——把纯 C# 的 MBattleDebugController 接到 Unity：
//   ① OnGUI 作弊面板（预设按钮，鼠标点）
//   ② 文件桥：轮询命令文件（每行一条命令），执行后清空；每帧把状态 JSON 写出。
// 这样 agent / 开发者 / PowerShell / bash 都能"写一行命令进文件 + 读状态文件"来驱动调试，无需 Unity 焦点。
// ⚠️ 仅 Editor / Development build 生效；破坏确定性，联机务必关。文件默认在工程 Temp/ 下。
using System;
using System.IO;
using UnityEngine;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Battle.Debugging;

namespace Lockstep.View.Debugging
{
    /// <summary>
    /// 把 <see cref="MBattleDebugController"/> 接到 Unity。宿主（MugenVersusView）在 Tick 循环里：
    /// 1) 调 <see cref="ShouldTick"/> 决定本帧是否推进；2) 推进后调 <see cref="AfterTick"/>；
    /// 3) 每帧调 <see cref="Poll"/> 处理命令文件 + 写状态文件。
    /// </summary>
    public sealed class MugenBattleDebugBridge
    {
        readonly MBattleDebugController _controller;
        readonly string _cmdPath;
        readonly string _statePath;
        readonly string _logPath;
        string _lastResult = "";
        bool _panelVisible = true;
        float _fileTimer;

        const float FilePollInterval = 0.1f;   // 文件轮询节流（秒）；OnGUI 按钮即时。

        public MugenBattleDebugBridge(MBattleEngine engine, MRoundSystem round, string dir = null)
        {
            _controller = new MBattleDebugController(engine, round);
            string baseDir = dir ?? Path.Combine(Application.dataPath, "..", "Temp", "battle_debug");
            Directory.CreateDirectory(baseDir);
            _cmdPath = Path.Combine(baseDir, "cmd.txt");
            _statePath = Path.Combine(baseDir, "state.json");
            _logPath = Path.Combine(baseDir, "log.txt");
            // 清掉上一局遗留命令，避免开局即执行。
            try { if (File.Exists(_cmdPath)) { File.WriteAllText(_cmdPath, ""); } } catch { }
            Debug.Log("[BattleDebug] 桥已就绪。命令文件: " + _cmdPath + "  状态文件: " + _statePath);
        }

        /// <summary>宿主每帧推进前问：本帧是否推进模拟（暂停/单步门控）。</summary>
        public bool ShouldTick() => _controller.ShouldTick();

        /// <summary>宿主每推进一帧后调：兜底上帝模式等。</summary>
        public void AfterTick() => _controller.PostTick();

        /// <summary>每帧（Update 末）调：节流轮询命令文件 + 写状态文件。</summary>
        public void Poll(float deltaTime)
        {
            _fileTimer += deltaTime;
            if (_fileTimer < FilePollInterval)
            {
                return;
            }
            _fileTimer = 0f;
            DrainCommandFile();
            WriteStateFile();
        }

        void DrainCommandFile()
        {
            try
            {
                if (!File.Exists(_cmdPath))
                {
                    return;
                }
                string content = File.ReadAllText(_cmdPath);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return;
                }
                File.WriteAllText(_cmdPath, "");   // 先清空：命令只执行一次
                string[] lines = content.Replace("\r\n", "\n").Split('\n');
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    string result = _controller.Execute(line);
                    _lastResult = result;
                    AppendLog("> " + line.Trim() + "\n  " + result);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BattleDebug] 命令文件处理失败: " + e.Message);
            }
        }

        void WriteStateFile()
        {
            try
            {
                File.WriteAllText(_statePath, _controller.StateJson());
            }
            catch { /* 文件被读占用时跳过本次 */ }
        }

        void AppendLog(string text)
        {
            try { File.AppendAllText(_logPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + "\n"); }
            catch { }
        }

        // ── OnGUI 作弊面板（宿主在 OnGUI 里转调） ──
        public void DrawPanel()
        {
            GUI.skin.font = Lockstep.View.MugenChineseText.Font();
            float x = Screen.width - 230f;
            if (GUI.Button(new Rect(x, 8f, 90f, 22f), _panelVisible ? "调试 ▲" : "调试 ▼"))
            {
                _panelVisible = !_panelVisible;
            }
            if (!_panelVisible)
            {
                return;
            }
            GUILayout.BeginArea(new Rect(x, 34f, 222f, 320f), GUI.skin.box);
            GUILayout.Label("战斗调试修改器");
            if (GUILayout.Button("跳过出场 (skipintro)")) { Run("skipintro"); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("P1 残血")) { Run("sethpp 0 10"); }
            if (GUILayout.Button("P2 残血")) { Run("sethpp 1 10"); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("P1 满血")) { Run("heal 0"); }
            if (GUILayout.Button("P2 满血")) { Run("heal 1"); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("P1 胜")) { Run("win 0"); }
            if (GUILayout.Button("P2 胜")) { Run("win 1"); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("P1 上帝")) { Run("god 0 on"); }
            if (GUILayout.Button("P2 上帝")) { Run("god 1 on"); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("超时即至")) { Run("timer 1"); }
            if (GUILayout.Button("复位本局")) { Run("resetround"); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_controller.Paused ? "继续" : "暂停"))
            {
                Run(_controller.Paused ? "resume" : "pause");
            }
            if (GUILayout.Button("单步 ▶")) { Run("step 1"); }
            GUILayout.EndHorizontal();
            GUILayout.Label("最近: " + _lastResult);
            GUILayout.EndArea();
        }

        void Run(string cmd)
        {
            _lastResult = _controller.Execute(cmd);
            AppendLog("[btn] " + cmd + "\n  " + _lastResult);
        }
    }
}
