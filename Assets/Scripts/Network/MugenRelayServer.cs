using System;
using Lockstep.Network;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Server;
using UnityEngine;

namespace Lockstep.Net
{
    /// <summary>
    /// Lightweight KCP matchmaking/relay server for the MUGEN demo. It owns queue/room
    /// assignment and relays room inputs; battle simulation still runs on clients.
    /// </summary>
    public sealed class MugenRelayServer : MonoBehaviour, IMugenMatchServerSink, IMugenMatchServerLogSink
    {
        const float StatusLogIntervalSeconds = 5f;

        public int Port = 7777;

        KcpServerTransport _server;
        MugenMatchServerCore _core;
        long _handled;
        float _statusLogTimer;

        void Start()
        {
            Port = ResolvePort(Port);
            _server = new KcpServerTransport();
            _server.OnPlayerConnected += OnJoin;
            _server.OnPlayerDisconnected += OnLeave;
            _server.Start(Port);
            _core = new MugenMatchServerCore(this);
            Application.runInBackground = true;
            Log("[匹配服务器] 已启动 UDP 端口 " + Port);
        }

        void OnJoin(int playerId)
        {
            _core.OnConnected(playerId);
            Log("[匹配服务器] 客户端连接 id=" + playerId);
        }

        void OnLeave(int playerId)
        {
            _core.OnDisconnected(playerId);
            Log("[匹配服务器] 客户端断开 id=" + playerId);
        }

        void Update()
        {
            if (_server == null)
            {
                return;
            }

            _server.Update();
            _core.Tick((int)(Time.realtimeSinceStartup * 1000f));
            LogStatusIfNeeded();
            while (_server.Poll(out int from, out IMessage msg))
            {
                LogControl("recv", from, msg);
                _core.HandleMessage(from, msg);
                _handled++;
            }
        }

        public void Send(int connectionId, IMessage message)
        {
            LogControl("send", connectionId, message);
            _server.Send(connectionId, message);
            _server.Flush(connectionId);
        }

        public void Log(string message)
        {
            Debug.Log(message);
            BroadcastServerLog(message);
        }

        void LogStatusIfNeeded()
        {
            _statusLogTimer -= Time.deltaTime;
            if (_statusLogTimer > 0f || _core == null)
            {
                return;
            }

            _statusLogTimer = StatusLogIntervalSeconds;
            Log(string.Format(
                "[匹配服务器] 状态：连接={0} 队列={1} 房间={2} 已处理={3} 端口={4}",
                _core.ConnectedCount, _core.WaitingCount, _core.RoomCount, _handled, Port));
        }

        void BroadcastServerLog(string message)
        {
            if (_server == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            _server.Broadcast(new ServerLogMsg
            {
                ServerTimeMs = Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f),
                Message = message,
            });
        }

        void LogControl(string direction, int connectionId, IMessage message)
        {
            string detail = Describe(message);
            if (detail == null)
            {
                return;
            }
            string directionText = direction == "recv" ? "收到" : "发送";
            Log(string.Format("[匹配服务器] {0} 控制消息 连接={1} {2}", directionText, connectionId, detail));
        }

        static string Describe(IMessage message)
        {
            switch (message)
            {
                case FindMatchMsg find:
                    return string.Format("匹配请求 request={0} 昵称={1} 版本={2} 内容Hash={3} 队伍={4}",
                        find.RequestId, find.Nickname, find.ClientVersion, find.ContentHash, find.TeamCsv);
                case MatchFoundMsg found:
                    return string.Format("匹配成功 request={0} 房间={1} 本地玩家={2} 种子={3}",
                        found.RequestId, found.RoomId, found.LocalPlayerId, found.Seed);
                case RoomReadyMsg ready:
                    return string.Format("房间就绪 房间={0} 本地玩家={1} 种子={2}",
                        ready.RoomId, ready.LocalPlayerId, ready.RoomSeed);
                case StartMatchMsg start:
                    return string.Format("开局 房间={0} 起始帧={1}", start.RoomId, start.StartFrame);
                case RoomClosedMsg closed:
                    return string.Format("房间关闭 request={0} 房间={1} 原因={2}",
                        closed.RequestId, closed.RoomId, ReasonText(closed.Reason));
                case LoadProgressMsg load when load.Ready || load.ProgressPermille >= 1000:
                    return string.Format("加载进度 房间={0} 玩家={1} 进度={2} 就绪={3}",
                        load.RoomId, load.PlayerId, load.ProgressPermille, load.Ready);
                default:
                    return null;
            }
        }

        static string ReasonText(string reason)
        {
            if (string.IsNullOrEmpty(reason)) { return "<空>"; }
            switch (reason)
            {
                case "missing client version": return "缺少客户端版本";
                case "missing content hash": return "缺少内容Hash";
                case "missing team": return "缺少队伍";
                case "invalid team size": return "队伍人数不正确";
                case "invalid character name": return "角色名非法";
                case "match timeout": return "匹配超时";
                case "match superseded": return "匹配已被新请求取代";
                case "opponent disconnected": return "对手断开连接";
                case "connection timeout": return "连接超时";
                case "match cancelled": return "匹配已取消";
                case "room not found": return "房间不存在";
                case "not in room": return "玩家不在房间内";
                case "ready mismatch": return "房间就绪信息不匹配";
                case "player left room": return "玩家离开房间";
                case "invalid hash report": return "Hash上报非法";
                case "conflicting hash report": return "Hash上报冲突";
                case "invalid input": return "输入非法";
                case "conflicting input": return "输入冲突";
                case "input frame jump": return "输入帧跳变过大";
            }
            if (reason.StartsWith("hash mismatch frame "))
            {
                return "Hash不一致 " + reason.Substring("hash mismatch ".Length);
            }
            return reason;
        }

        void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.font = Lockstep.View.MugenChineseText.Font();
            style.fontSize = 18;
            GUI.Label(new Rect(10f, 10f, 720f, 30f),
                string.Format("MUGEN 匹配服务器：端口 {0}  队列 {1}  房间 {2}  已处理 {3}",
                    Port, _core != null ? _core.WaitingCount : 0, _core != null ? _core.RoomCount : 0, _handled), style);
        }

        void OnDestroy()
        {
            _server?.Close();
        }

        static int ResolvePort(int fallback)
        {
            string env = Environment.GetEnvironmentVariable("MUGEN_SERVER_PORT");
            if (TryPort(env, out int envPort))
            {
                return envPort;
            }

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if ((args[i] == "-port" || args[i] == "--port") && TryPort(args[i + 1], out int argPort))
                {
                    return argPort;
                }
            }
            return fallback;
        }

        static bool TryPort(string text, out int port)
        {
            return int.TryParse(text, out port) && port > 0 && port <= 65535;
        }
    }
}
