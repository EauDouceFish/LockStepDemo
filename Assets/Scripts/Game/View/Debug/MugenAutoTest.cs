using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Lockstep.Client;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Mugen.Command;
using Lockstep.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockstep.View
{
    public static class MugenAutoTest
    {
        static bool _initialized;
        static readonly Dictionary<int, MInput> ScheduledInputs = new Dictionary<int, MInput>();
        static readonly HashSet<int> ScheduledEscapes = new HashSet<int>();
        static readonly HashSet<int> ScheduledWeakNetworkToggles = new HashSet<int>();
        static readonly HashSet<int> ScheduledScreenshots = new HashSet<int>();
        static readonly HashSet<int> TracedInputFrames = new HashSet<int>();
        static readonly HashSet<int> TracedEscapeFrames = new HashSet<int>();
        static readonly HashSet<int> TracedWeakNetworkFrames = new HashSet<int>();
        static readonly HashSet<int> CapturedScreenshotFrames = new HashSet<int>();
        static readonly Queue<ClientTraceMsg> PendingTraceMessages = new Queue<ClientTraceMsg>();
        static long _seq;
        static bool _scheduleBuilt;
        static bool _matchOverTraced;
        static float _quitAt = -1f;
        static bool _quitAfterDuration;
        static bool _durationQuitTraced;
        static string _logPath = "";

        public static bool Enabled { get; private set; }
        public static string ClientId { get; private set; } = "client";
        public static string RunId { get; private set; } = "";
        public static string ClientInstanceId { get; private set; } = "";
        public static string ServerHost { get; private set; } = "";
        public static int ServerPort { get; private set; }
        public static int Seed { get; private set; } = 1;
        public static int DurationSeconds { get; private set; } = 60;
        public static int InputEvents { get; private set; } = 300;
        public static int RoundSeconds { get; private set; } = 8;
        public static string TeamCsv { get; private set; } = "";
        public static string LogDir { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RuntimeInit()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            string[] args = Environment.GetCommandLineArgs();
            Enabled = HasArg(args, "--autotest");
            ClientInstanceId = Arg(args, "--client-instance-id", "session-" + Guid.NewGuid().ToString("N"));
            if (!Enabled)
            {
                return;
            }
            Application.runInBackground = true;

            ClientId = Arg(args, "--client-id", "client-" + Environment.TickCount.ToString(CultureInfo.InvariantCulture));
            RunId = Arg(args, "--run-id", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            ClientInstanceId = Arg(args, "--client-instance-id", RunId + ":" + ClientId);
            Seed = IntArg(args, "--seed", 1);
            DurationSeconds = System.Math.Max(1, IntArg(args, "--duration", 60));
            InputEvents = System.Math.Max(0, IntArg(args, "--inputs", 300));
            RoundSeconds = System.Math.Max(2, IntArg(args, "--round-seconds", 8));
            TeamCsv = Arg(args, "--team", "");
            _quitAfterDuration = HasArg(args, "--quit-after-duration");
            LogDir = Arg(args, "--log-dir", "");
            AddFrameArgs(ScheduledEscapes, Arg(args, "--escape-frames", ""));
            AddFrameArgs(ScheduledWeakNetworkToggles, Arg(args, "--weak-toggle-frames", ""));
            AddFrameArgs(ScheduledScreenshots, Arg(args, "--screenshot-frames", ""));
            string server = Arg(args, "--server", "");
            if (!string.IsNullOrEmpty(server))
            {
                string[] parts = server.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int parsedPort))
                {
                    ServerHost = parts[0];
                    ServerPort = parsedPort;
                }
            }
            ServerHost = Arg(args, "--host", ServerHost);
            ServerPort = IntArg(args, "--port", ServerPort);

            if (string.IsNullOrEmpty(LogDir))
            {
                LogDir = Path.Combine(Application.persistentDataPath, "MugenAutoTest");
            }
            Directory.CreateDirectory(LogDir);
            _logPath = Path.Combine(LogDir, SafeFile(ClientId) + ".jsonl");
            File.WriteAllText(_logPath, string.Empty);
            Trace("autotest_init", "args=" + string.Join(" ", args), roomId: 0, frame: -1, input: 0);
        }

        public static void ApplyNetworkOverride()
        {
            EnsureInitialized();
            if (!Enabled || string.IsNullOrEmpty(ServerHost) || ServerPort <= 0)
            {
                return;
            }
            MugenMatchSetup.NetHost = ServerHost;
            MugenMatchSetup.NetPort = ServerPort;
        }

        public static void PopulateInputTrace(MugenInputMsg message)
        {
            EnsureInitialized();
            if (!Enabled || message == null)
            {
                return;
            }
            message.TraceSeq = NextSeq();
            message.ClientUnixMs = UnixMs();
            message.ClientTickMs = TickMs();
            message.ClientId = ClientId;
            message.RunId = RunId;
        }

        public static void SendTrace(KcpClientTransport transport, string eventName, string detail = "",
            int roomId = 0, int frame = -1, int input = 0)
        {
            Trace(eventName, detail, roomId, frame, input);
            FlushPendingTrace(transport);
        }

        public static void FlushPendingTrace(KcpClientTransport transport)
        {
            if (!Enabled || transport == null || !transport.IsConnected || transport.Faulted)
            {
                return;
            }

            while (PendingTraceMessages.Count > 0)
            {
                transport.Send(-1, PendingTraceMessages.Dequeue());
            }
            transport.Flush();
        }

        public static void Trace(string eventName, string detail = "", int roomId = 0, int frame = -1, int input = 0)
        {
            EnsureInitialized();
            if (!Enabled)
            {
                return;
            }
            long seq = NextSeq();
            long unixMs = UnixMs();
            int tickMs = TickMs();
            string scene = SceneManager.GetActiveScene().name;
            string json = string.Format(CultureInfo.InvariantCulture,
                "{{\"kind\":\"client\",\"runId\":\"{0}\",\"clientId\":\"{1}\",\"seq\":{2},\"unixMs\":{3},\"tickMs\":{4},\"scene\":\"{5}\",\"event\":\"{6}\",\"roomId\":{7},\"frame\":{8},\"input\":{9},\"detail\":\"{10}\"}}",
                Json(RunId),
                Json(ClientId),
                seq,
                unixMs,
                tickMs,
                Json(scene),
                Json(eventName),
                roomId,
                frame,
                input,
                Json(detail ?? string.Empty));
            File.AppendAllText(_logPath, json + Environment.NewLine);
            PendingTraceMessages.Enqueue(new ClientTraceMsg
            {
                RunId = RunId,
                ClientId = ClientId,
                RoomId = roomId,
                Seq = seq,
                ClientUnixMs = unixMs,
                ClientTickMs = tickMs,
                Frame = frame,
                Input = input,
                Scene = scene,
                EventName = eventName,
                Detail = detail ?? string.Empty,
            });
            Debug.Log("[AutoTest] " + eventName + " " + detail);
        }

        public static MInput SampleInput(int frame)
        {
            EnsureSchedule();
            if (ScheduledInputs.TryGetValue(frame, out MInput input))
            {
                if (!TracedInputFrames.Contains(frame))
                {
                    TracedInputFrames.Add(frame);
                    Trace("input_event", "scheduled", MugenMatchSetup.NetRoomId, frame, (int)input);
                }
                return input;
            }
            return MInput.None;
        }

        public static bool ConsumeEscape(int frame)
        {
            EnsureSchedule();
            if (!TryConsumeDueFrame(ScheduledEscapes, TracedEscapeFrames, frame, out _))
            {
                return false;
            }
            Trace("escape_event", "scheduled", MugenMatchSetup.NetRoomId, frame, 0);
            return true;
        }

        public static bool ConsumeWeakNetworkToggle(int frame)
        {
            EnsureSchedule();
            return TryConsumeDueFrame(ScheduledWeakNetworkToggles, TracedWeakNetworkFrames, frame, out _);
        }

        public static bool CaptureScreenshotIfNeeded(int frame, string tag)
        {
            EnsureInitialized();
            if (!Enabled || !TryConsumeDueFrame(ScheduledScreenshots, CapturedScreenshotFrames, frame, out int dueFrame))
            {
                return false;
            }

            string file = SafeFile(ClientId) + "_" + SafeFile(tag) + "_frame" + dueFrame.ToString("D4", CultureInfo.InvariantCulture) + ".png";
            string path = Path.Combine(LogDir, file);
            ScreenCapture.CaptureScreenshot(path);
            Trace("screenshot", path, MugenMatchSetup.NetRoomId, frame, 0);
            return true;
        }

        public static void MarkMatchOver(int winner, int boutNo)
        {
            if (!Enabled || _matchOverTraced)
            {
                return;
            }
            _matchOverTraced = true;
            Trace("match_over", "winner=" + winner + " bout=" + boutNo, MugenMatchSetup.NetRoomId, -1, 0);
            _quitAt = Time.realtimeSinceStartup + 2f;
        }

        public static void MarkMatchClosed(string reason, int roomId, int frame)
        {
            if (!Enabled || _matchOverTraced)
            {
                return;
            }
            _matchOverTraced = true;
            Trace("match_closed", reason ?? string.Empty, roomId, frame, 0);
            _quitAt = Time.realtimeSinceStartup + 2f;
        }

        public static void TickQuit()
        {
            if (!Enabled || _quitAt < 0f || Time.realtimeSinceStartup < _quitAt)
            {
                return;
            }
            Trace("application_quit", "match complete", MugenMatchSetup.NetRoomId, -1, 0);
            Application.Quit(0);
            _quitAt = -1f;
        }

        public static void TickFrameQuit(int frame)
        {
            if (!Enabled || !_quitAfterDuration || _durationQuitTraced)
            {
                return;
            }
            if (frame < DurationSeconds * 60)
            {
                return;
            }

            _durationQuitTraced = true;
            Trace("application_quit", "duration complete", MugenMatchSetup.NetRoomId, frame, 0);
            Application.Quit(0);
        }

        static void EnsureSchedule()
        {
            if (_scheduleBuilt)
            {
                return;
            }
            _scheduleBuilt = true;
            int totalFrames = System.Math.Max(1, DurationSeconds * 60);
            int count = System.Math.Min(InputEvents, totalFrames - 1);
            System.Random random = new System.Random(Seed ^ ClientId.GetHashCode());
            HashSet<int> used = new HashSet<int>();
            int escapeCount = System.Math.Min(System.Math.Max(6, count / 20), count);

            for (int i = 0; i < escapeCount; i++)
            {
                int frame = UniqueFrame(random, used, totalFrames);
                ScheduledEscapes.Add(frame);
            }
            for (int i = escapeCount; i < count; i++)
            {
                int frame = UniqueFrame(random, used, totalFrames);
                ScheduledInputs[frame] = RandomInput(random);
            }
            Trace("input_schedule_ready", "inputs=" + ScheduledInputs.Count + " escapes=" + ScheduledEscapes.Count, 0, -1, 0);
        }

        static void AddFrameArgs(HashSet<int> target, string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return;
            }

            string[] parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int frame) && frame >= 0)
                {
                    target.Add(frame);
                }
            }
        }

        static bool TryConsumeDueFrame(HashSet<int> scheduled, HashSet<int> consumed, int frame, out int dueFrame)
        {
            dueFrame = -1;
            foreach (int scheduledFrame in scheduled)
            {
                if (scheduledFrame > frame || consumed.Contains(scheduledFrame))
                {
                    continue;
                }
                if (dueFrame < 0 || scheduledFrame < dueFrame)
                {
                    dueFrame = scheduledFrame;
                }
            }

            if (dueFrame < 0)
            {
                return false;
            }
            consumed.Add(dueFrame);
            return true;
        }

        static int UniqueFrame(System.Random random, HashSet<int> used, int totalFrames)
        {
            for (int i = 0; i < 10000; i++)
            {
                int frame = random.Next(1, totalFrames);
                if (used.Add(frame))
                {
                    return frame;
                }
            }
            for (int frame = 1; frame < totalFrames; frame++)
            {
                if (used.Add(frame))
                {
                    return frame;
                }
            }
            return 0;
        }

        static MInput RandomInput(System.Random random)
        {
            MInput[] buttons =
            {
                MInput.A, MInput.B, MInput.C, MInput.X, MInput.Y, MInput.Z, MInput.S,
                MInput.Left, MInput.Right, MInput.Down, MInput.Up,
                MInput.Left | MInput.A,
                MInput.Right | MInput.B,
                MInput.Down | MInput.C,
                MInput.Left | MInput.X,
                MInput.Right | MInput.Y,
                MInput.Down | MInput.Z,
            };
            return buttons[random.Next(buttons.Length)];
        }

        static bool HasArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        static string Arg(string[] args, string name, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return fallback;
        }

        static int IntArg(string[] args, string name, int fallback)
        {
            string value = Arg(args, name, "");
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        static long NextSeq()
        {
            _seq++;
            return _seq;
        }

        static long UnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        static int TickMs()
        {
            return Environment.TickCount;
        }

        static string SafeFile(string value)
        {
            foreach (char ch in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(ch, '_');
            }
            return value;
        }

        static string Json(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
