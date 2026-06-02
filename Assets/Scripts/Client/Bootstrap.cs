using System;
using System.Diagnostics;
using UnityEngine;
using Lockstep.Core;
using Lockstep.Game;
using Lockstep.Game.View;
using Lockstep.Input;
using Lockstep.Network;
using Lockstep.Predict;
using Lockstep.Server;
using Debug = UnityEngine.Debug;

namespace Lockstep.Client
{
    public enum BootMode
    {
        Loopback,
        HostAndPlay,
        ClientConnect,
    }

    public class Bootstrap : MonoBehaviour
    {
        [Header("Mode")]
        public BootMode Mode = BootMode.Loopback;
        public string RoomId = "main";

        [Header("KCP networking")]
        public int ServerListenPort = 12345;
        public string ClientServerHost = "127.0.0.1";
        public int ClientServerPort = 12345;

        [Header("Game data")]
        public LogicConfigAsset LogicConfig;

        [Header("Debug")]
        public int LogFrameEvery = 30;
        public bool RunSnapshotSelfTest = true;
        public bool EnableDesyncCheck = true;   // 仅 Loopback：逐帧比对两端 World 哈希

        public LockstepClient LocalClient => _client0;
        public event Action<LockstepClient> OnLocalClientReady;

        LoopbackHub _hub;
        LockstepServer _server;
        LockstepClient _client0;
        LockstepClient _client1;
        KcpServerTransport _kcpServerT;
        KcpClientTransport _kcpClientT;
        DesyncDetector _desyncDetector;
        Stopwatch _clock;

        int _lastLoggedFrame0 = -1;
        int _lastLoggedFrame1 = -1;

        void Awake()
        {
            // 把逻辑层日志出口接到 Unity 控制台（桥接模式：逻辑层不直连 Unity）
            Lockstep.Logging.LLog.Sink = Debug.Log;
            Lockstep.Logging.LLog.WarnSink = Debug.LogWarning;
            Lockstep.Logging.LLog.ErrorSink = Debug.LogError;

            if (RunSnapshotSelfTest)
                Debug.Log(Lockstep.Core.SnapshotSelfTest.Run());

            ApplyCommandLineOverrides();
            _clock = new Stopwatch();
            switch (Mode)
            {
                case BootMode.Loopback: SetupLoopback(); break;
                case BootMode.HostAndPlay: SetupHostAndPlay(); break;
                case BootMode.ClientConnect: SetupClientConnect(); break;
            }
        }

        void ApplyCommandLineOverrides()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a == "-host" || a == "--host") Mode = BootMode.HostAndPlay;
                else if (a == "-client" || a == "--client") Mode = BootMode.ClientConnect;
                else if (a == "-loopback" || a == "--loopback") Mode = BootMode.Loopback;
                else if ((a == "-port" || a == "--port") && i + 1 < args.Length && int.TryParse(args[i + 1], out var p))
                {
                    ServerListenPort = p;
                    ClientServerPort = p;
                }
                else if ((a == "-host-ip" || a == "--host-ip") && i + 1 < args.Length)
                {
                    ClientServerHost = args[i + 1];
                }
            }
        }

        void Update()
        {
            if (_clock == null || !_clock.IsRunning) return;
            long nowMs = _clock.ElapsedMilliseconds;

            _kcpServerT?.Update();
            _kcpClientT?.Update();

            _server?.Tick();
            _client0?.Tick(nowMs);
            _client1?.Tick(nowMs);
            LogFrame();
        }

        void OnDestroy()
        {
            try { _kcpServerT?.Close(); } catch { }
            try { _kcpClientT?.Close(); } catch { }
        }

        // ─────────────────────────── Loopback ───────────────────────────
        void SetupLoopback()
        {
            if (LogicConfig == null) { Debug.LogError("[Bootstrap] LogicConfig 未配置"); return; }

            var logicData = LogicConfig.ToLogicData();
            Debug.Log($"[Bootstrap] LogicConfig raw: {logicData.DumpRaw()}");
            var gameLogic = new Lockstep.Game.BattleGameLogic();

            _hub = new LoopbackHub();
            var serverT = new LoopbackServerTransport(_hub);
            var clientT0 = new LoopbackClientTransport(_hub);
            var clientT1 = new LoopbackClientTransport(_hub);

            LocalInputProvider input0 = new LocalInputProvider();
            LocalInputProvider input1 = new LocalInputProvider
            {
                Up = KeyCode.UpArrow,
                Down = KeyCode.DownArrow,
                Left = KeyCode.LeftArrow,
                Right = KeyCode.RightArrow,
                LightPunch = KeyCode.Keypad1,
                HeavyPunch = KeyCode.Keypad2,
                Kick = KeyCode.Keypad3,
                Jump = KeyCode.RightShift,
            };

            _server = new LockstepServer(serverT);
            _client0 = new LockstepClient(clientT0, input0, new NullPredictor(), logicData, gameLogic);
            _client1 = new LockstepClient(clientT1, input1, new NullPredictor(), logicData, gameLogic);

            _client0.OnStateChanged += (a, b) => Debug.Log($"[client0] {a} → {b}");
            _client1.OnStateChanged += (a, b) => Debug.Log($"[client1] {a} → {b}");

            // S3 不同步检测：两端各自上报每帧 World 哈希，检测器在同一帧号上配对比对
            if (EnableDesyncCheck)
            {
                _desyncDetector = new DesyncDetector();
                _client0.OnFrameSimulated += (frame, hash) => _desyncDetector.Report(0, frame, hash);
                _client1.OnFrameSimulated += (frame, hash) => _desyncDetector.Report(1, frame, hash);
            }

            _server.Start();
            _clock.Start();

            _client0.ConnectAndJoin(RoomId);
            _client1.ConnectAndJoin(RoomId);

            clientT0.Activate();
            clientT1.Activate();

            OnLocalClientReady?.Invoke(_client0);
            Debug.Log("[Bootstrap] Loopback ready. p1=WASD+JKL+Space, p2=Arrows+Numpad123+RShift");
        }

        // ─────────────────────────── HostAndPlay ───────────────────────────
        void SetupHostAndPlay()
        {
            if (LogicConfig == null) { Debug.LogError("[Bootstrap] LogicConfig 未配置"); return; }

            var logicData = LogicConfig.ToLogicData();
            Debug.Log($"[Bootstrap] LogicConfig raw: {logicData.DumpRaw()}");
            var gameLogic = new Lockstep.Game.BattleGameLogic();

            _kcpServerT = new KcpServerTransport();
            _kcpServerT.Start(ServerListenPort);

            _server = new LockstepServer(_kcpServerT);
            _server.Start();

            _kcpClientT = new KcpClientTransport();

            var input0 = new LocalInputProvider();
            _client0 = new LockstepClient(_kcpClientT, input0, new NullPredictor(), logicData, gameLogic);
            _client0.OnStateChanged += (a, b) => Debug.Log($"[hostClient] {a} → {b}");

            _clock.Start();
            _client0.ConnectAndJoin(RoomId);
            _kcpClientT.Connect("127.0.0.1", ServerListenPort);

            OnLocalClientReady?.Invoke(_client0);
            Debug.Log($"[Bootstrap] HostAndPlay ready on UDP {ServerListenPort}. WASD+J. Waiting opponent...");
        }

        // ─────────────────────────── ClientConnect ───────────────────────────
        void SetupClientConnect()
        {
            if (LogicConfig == null) { Debug.LogError("[Bootstrap] LogicConfig 未配置"); return; }

            var logicData = LogicConfig.ToLogicData();
            Debug.Log($"[Bootstrap] LogicConfig raw: {logicData.DumpRaw()}");
            var gameLogic = new Lockstep.Game.BattleGameLogic();

            _kcpClientT = new KcpClientTransport();

            var input0 = new LocalInputProvider();
            _client0 = new LockstepClient(_kcpClientT, input0, new NullPredictor(), logicData, gameLogic);
            _client0.OnStateChanged += (a, b) => Debug.Log($"[client] {a} → {b}");

            _clock.Start();
            _client0.ConnectAndJoin(RoomId);
            _kcpClientT.Connect(ClientServerHost, ClientServerPort);

            OnLocalClientReady?.Invoke(_client0);
            Debug.Log($"[Bootstrap] ClientConnect to {ClientServerHost}:{ClientServerPort}. WASD+J");
        }

        void LogFrame()
        {
            if (LogFrameEvery <= 0) return;
            if (_client0 != null && _client0.LogicFrame > 0
                && _client0.LogicFrame != _lastLoggedFrame0
                && _client0.LogicFrame % LogFrameEvery == 0)
            {
                Debug.Log($"[client0] frame={_client0.LogicFrame}");
                _lastLoggedFrame0 = _client0.LogicFrame;
                if (_desyncDetector != null)
                {
                    Debug.Log(_desyncDetector.Summary());
                }
            }
            if (_client1 != null && _client1.LogicFrame > 0
                && _client1.LogicFrame != _lastLoggedFrame1
                && _client1.LogicFrame % LogFrameEvery == 0)
            {
                Debug.Log($"[client1] frame={_client1.LogicFrame}");
                _lastLoggedFrame1 = _client1.LogicFrame;
            }
        }
    }
}
