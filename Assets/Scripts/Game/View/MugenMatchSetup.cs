// 跨场景对战配置：选人页填写 → 战斗场读取。静态保存（场景切换不丢；非联机态，不入哈希）。
// 每名玩家一队角色（车轮战）。模式：本地双人 / vs AI / 联机（KCP）。
using System;
using System.Collections.Generic;
using Lockstep.Client;
using Lockstep.Mugen.Battle.Net;

namespace Lockstep.View
{
    public enum MugenMatchMode
    {
        LocalVersus = 0,   // 本地双人（P1 键盘 / P2 键盘）
        VersusAI = 1,      // P1 键盘 / P2 由 AI 控制
        NetKcp = 2,        // 联机：本端键盘，对端经 KCP
    }

    /// <summary>选人页 → 战斗场的对战配置（静态单例式）。</summary>
    public static class MugenMatchSetup
    {
        public const string MainMenuSceneName = "MainMenu";
        public const string SelectSceneName = "MugenTeamSelect";
        public const string BattleSceneName = "BattleScene";

        public sealed class ServerEndpoint
        {
            public readonly string Id;
            public readonly string RegionName;
            public readonly string Host;
            public readonly int Port;

            public ServerEndpoint(string id, string regionName, string host, int port)
            {
                Id = id;
                RegionName = regionName;
                Host = host;
                Port = port;
            }

            public string ButtonText => RegionName + "的服务器";
            public string AddressText => Host + ":" + Port;
        }

        public static readonly ServerEndpoint[] Servers =
        {
            new ServerEndpoint("guangzhou", "广州", "8.163.135.18", MugenMatchProtocol.DefaultServerPort),
            new ServerEndpoint("singapore", "新加坡", "47.84.193.58", MugenMatchProtocol.DefaultServerPort),
        };

        static readonly int[] ServerLatencyMs = CreateLatencySlots();
        public static int SelectedServerIndex;

        public static ServerEndpoint SelectedServer
        {
            get
            {
                if (Servers.Length == 0)
                {
                    return null;
                }
                if (SelectedServerIndex < 0 || SelectedServerIndex >= Servers.Length)
                {
                    SelectedServerIndex = 0;
                }
                return Servers[SelectedServerIndex];
            }
        }

        public static MugenMatchMode Mode = MugenMatchMode.VersusAI;

        /// <summary>两队角色文件夹名（MugenSource 下），每队 1..N（默认 3）。</summary>
        public static readonly List<string> Team0 = new List<string>();
        public static readonly List<string> Team1 = new List<string>();

        public static string CommonFolder = "Terrarian";   // common1.cns 来源
        public static int AiSeed = 1;

        // 联机参数（NetKcp）。
        public static bool NetIsHost;          // 本端是否为 P0（房主）
        public static int NetPlayerId;
        public static int NetRoomId;
        public static string NetHost = "8.163.135.18";
        public static int NetPort = MugenMatchProtocol.DefaultServerPort;
        public static KcpClientTransport NetTransport;
        public static string NetOpponentName = "";
        public static Action ReturnToSelect;

        public static bool HasSelection => Team0.Count > 0 && Team1.Count > 0;

        /// <summary>未经选人页直接进战斗场时的兜底（KFM 三连镜像，便于独立调试）。</summary>
        public static void EnsureDefaults(string fallbackChar = "kfm", int teamSize = MugenMatchProtocol.TeamSize)
        {
            if (Team0.Count == 0)
            {
                for (int i = 0; i < teamSize; i++) { Team0.Add(fallbackChar); }
            }
            if (Team1.Count == 0)
            {
                for (int i = 0; i < teamSize; i++) { Team1.Add(fallbackChar); }
            }
        }

        public static void Clear()
        {
            Team0.Clear();
            Team1.Clear();
            ApplySelectedServer();
            if (NetTransport != null)
            {
                NetTransport.Close();
            }
            NetIsHost = false;
            NetPlayerId = 0;
            NetRoomId = 0;
            NetTransport = null;
            NetOpponentName = "";
        }

        public static void SelectServer(int index)
        {
            if (Servers.Length == 0)
            {
                SelectedServerIndex = 0;
                return;
            }
            if (index < 0)
            {
                index = 0;
            }
            if (index >= Servers.Length)
            {
                index = Servers.Length - 1;
            }
            SelectedServerIndex = index;
            ApplySelectedServer();
        }

        public static int GetServerLatencyMs(int index)
        {
            if (index < 0 || index >= ServerLatencyMs.Length)
            {
                return -1;
            }
            return ServerLatencyMs[index];
        }

        public static void SetServerLatencyMs(int index, int latencyMs)
        {
            if (index < 0 || index >= ServerLatencyMs.Length)
            {
                return;
            }
            ServerLatencyMs[index] = latencyMs > 0 ? latencyMs : -1;
        }

        public static string FormatServerStatus(int index)
        {
            if (index < 0 || index >= Servers.Length)
            {
                return "当前服务器：未知的服务器 延迟：测算中";
            }

            ServerEndpoint server = Servers[index];
            int latencyMs = GetServerLatencyMs(index);
            string latency = latencyMs > 0 ? latencyMs + "ms" : "测算中";
            return "当前服务器：" + server.ButtonText + " 延迟：" + latency;
        }

        public static string FormatSelectedServerStatus()
        {
            return FormatServerStatus(SelectedServerIndex);
        }

        public static void ApplySelectedServer()
        {
            ServerEndpoint server = SelectedServer;
            if (server == null)
            {
                return;
            }
            NetHost = server.Host;
            NetPort = server.Port;
            MugenAutoTest.ApplyNetworkOverride();
        }

        public static string ToCsv(List<string> values)
        {
            return string.Join(",", values);
        }

        public static void FillFromCsv(List<string> target, string csv)
        {
            target.Clear();
            if (string.IsNullOrEmpty(csv)) { return; }
            string[] parts = csv.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                {
                    target.Add(parts[i].Trim());
                }
            }
        }

        static int[] CreateLatencySlots()
        {
            int[] values = new int[Servers.Length];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = -1;
            }
            return values;
        }
    }
}
