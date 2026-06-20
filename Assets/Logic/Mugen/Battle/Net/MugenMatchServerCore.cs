using System.Collections.Generic;
using Lockstep.Network;

namespace Lockstep.Mugen.Battle.Net
{
    public interface IMugenMatchServerSink
    {
        void Send(int connectionId, IMessage message);
    }

    public interface IMugenMatchServerLogSink
    {
        void Log(string message);
    }

    public sealed class MugenMatchServerCore
    {
        const int QueueTimeoutMs = 120000;
        const int RoomTimeoutMs = 10000;
        const int MatchFoundResendIntervalMs = 1000;
        const int MatchFoundResendWindowMs = 12000;
        const int MaxSequentialInputFrameJump = 12;
        const int InputHistoryToKeep = 180;
        const int HashHistoryToKeep = 180;
        const int MaxReportedLatencyMs = 5000;
        const int MaxReportedWeakDelayMs = 2000;
        // Mirrors MInput.Up..S. Keep protocol validation decoupled from the full MUGEN command assembly.
        const int ValidMugenInputMask = 2047;

        readonly IMugenMatchServerSink _sink;
        readonly IMugenMatchServerLogSink _logSink;
        readonly Dictionary<int, PlayerSession> _sessions = new Dictionary<int, PlayerSession>();
        readonly List<PlayerSession> _queue = new List<PlayerSession>();
        readonly Dictionary<int, RoomSession> _rooms = new Dictionary<int, RoomSession>();
        int _nextRoomId = 1000;
        int _seed = 1;
        int _nowMs;

        public int WaitingCount => _queue.Count;
        public int RoomCount => _rooms.Count;
        public int ConnectedCount => _sessions.Count;

        public MugenMatchServerCore(IMugenMatchServerSink sink)
        {
            _sink = sink;
            _logSink = sink as IMugenMatchServerLogSink;
        }

        public void OnConnected(int connectionId)
        {
            if (!_sessions.ContainsKey(connectionId))
            {
                _sessions.Add(connectionId, new PlayerSession { ConnectionId = connectionId, LastSeenMs = _nowMs });
                Log("客户端连接 id=" + connectionId + " " + StatusSummary());
            }
        }

        public void OnDisconnected(int connectionId)
        {
            if (!_sessions.TryGetValue(connectionId, out PlayerSession session))
            {
                return;
            }

            RemoveFromQueue(session);
            CloseRoom(session.RoomId, "opponent disconnected", connectionId);
            _sessions.Remove(connectionId);
            Log("客户端断开 id=" + connectionId + " " + StatusSummary());
        }

        public void HandleMessage(int connectionId, IMessage message)
        {
            OnConnected(connectionId);
            _sessions[connectionId].LastSeenMs = _nowMs;
            switch (message)
            {
                case FindMatchMsg find:
                    HandleFindMatch(connectionId, find);
                    break;
                case CancelMatchMsg cancel:
                    HandleCancelMatch(connectionId, cancel);
                    break;
                case RoomReadyMsg ready:
                    HandleReady(connectionId, ready);
                    break;
                case LoadProgressMsg load:
                    HandleLoadProgress(connectionId, load);
                    break;
                case LeaveRoomMsg leave:
                    HandleLeave(connectionId, leave);
                    break;
                case MugenInputMsg input:
                    HandleInput(connectionId, input);
                    break;
                case MugenHashReportMsg hash:
                    HandleHashReport(connectionId, hash);
                    break;
                case MugenNetStatusMsg status:
                    HandleNetStatus(connectionId, status);
                    break;
                case PingMsg ping:
                    HandlePing(connectionId, ping);
                    break;
            }
        }

        public void Tick(int nowMs)
        {
            _nowMs = nowMs;

            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                PlayerSession player = _queue[i];
                if (TimedOut(player, QueueTimeoutMs))
                {
                    RemoveFromQueue(player);
                    Log("匹配队列超时 " + PlayerSummary(player));
                    _sink.Send(player.ConnectionId, new RoomClosedMsg
                    {
                        RequestId = player.RequestId,
                        RoomId = 0,
                        Reason = "match timeout",
                    });
                }
            }

            List<int> roomsToClose = null;
            foreach (KeyValuePair<int, RoomSession> pair in _rooms)
            {
                RoomSession room = pair.Value;
                bool timedOut = false;
                for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
                {
                    if (TimedOut(room.Players[i], RoomTimeoutMs))
                    {
                        if (roomsToClose == null) { roomsToClose = new List<int>(); }
                        roomsToClose.Add(room.RoomId);
                        Log("房间连接超时 房间=" + room.RoomId + " 玩家=" + PlayerSummary(room.Players[i]));
                        timedOut = true;
                        break;
                    }
                }

                if (!timedOut)
                {
                    ResendMatchFoundIfNeeded(room);
                }
            }

            if (roomsToClose == null) { return; }
            for (int i = 0; i < roomsToClose.Count; i++)
            {
                CloseRoom(roomsToClose[i], "connection timeout", exceptConnectionId: -1);
            }
        }

        void HandleFindMatch(int connectionId, FindMatchMsg message)
        {
            PlayerSession session = _sessions[connectionId];
            if (session.RoomId != 0)
            {
                int oldRoomId = session.RoomId;
                Log("新匹配请求取代旧房间 房间=" + oldRoomId + " 玩家=" + PlayerSummary(session));
                if (_rooms.ContainsKey(oldRoomId))
                {
                    CloseRoom(oldRoomId, "match superseded", exceptConnectionId: -1);
                }
                else
                {
                    session.RoomId = 0;
                }
            }

            Log("收到匹配请求 连接=" + connectionId +
                " request=" + message.RequestId +
                " 昵称=" + Safe(message.Nickname) +
                " 版本=" + Safe(message.ClientVersion) +
                " 内容Hash=" + Safe(message.ContentHash) +
                " 队伍=" + Safe(message.TeamCsv));

            string invalidReason = ValidateFindMatch(message);
            if (invalidReason != null)
            {
                RemoveFromQueue(session);
                Log("拒绝匹配请求 连接=" + connectionId + " 原因=" + ReasonText(invalidReason) + " " + StatusSummary());
                _sink.Send(connectionId, new RoomClosedMsg
                {
                    RequestId = message.RequestId,
                    RoomId = 0,
                    Reason = invalidReason,
                });
                return;
            }

            RemoveFromQueue(session);
            RemoveQueuedSameInstance(session, message.ClientInstanceId);
            session.Nickname = string.IsNullOrEmpty(message.Nickname) ? "Player" + connectionId : message.Nickname;
            session.TeamCsv = message.TeamCsv ?? string.Empty;
            session.ContentHash = message.ContentHash ?? string.Empty;
            session.ClientVersion = message.ClientVersion ?? string.Empty;
            session.ClientInstanceId = message.ClientInstanceId ?? string.Empty;
            session.RequestId = message.RequestId;
            session.InQueue = true;
            _queue.Add(session);
            Log("加入匹配队列 " + PlayerSummary(session) + " 队列=[" + QueueSummary() + "] " + StatusSummary());
            TryMatch();
        }

        void HandleCancelMatch(int connectionId, CancelMatchMsg message)
        {
            PlayerSession session = _sessions[connectionId];
            if (message.RequestId != 0 && message.RequestId != session.RequestId)
            {
                return;
            }

            if (session.RoomId != 0)
            {
                Log("取消匹配并关闭房间 房间=" + session.RoomId + " 玩家=" + PlayerSummary(session));
                CloseRoom(session.RoomId, "match cancelled", exceptConnectionId: -1);
                return;
            }

            RemoveFromQueue(session);
            Log("取消队列匹配 " + PlayerSummary(session) + " " + StatusSummary());
            _sink.Send(connectionId, new RoomClosedMsg
            {
                RequestId = message.RequestId,
                RoomId = 0,
                Reason = "match cancelled",
            });
        }

        void HandleReady(int connectionId, RoomReadyMsg message)
        {
            if (!_rooms.TryGetValue(message.RoomId, out RoomSession room))
            {
                _sink.Send(connectionId, new RoomClosedMsg
                {
                    RequestId = _sessions[connectionId].RequestId,
                    RoomId = message.RoomId,
                    Reason = "room not found",
                });
                return;
            }

            int index = room.IndexOf(connectionId);
            if (index < 0)
            {
                _sink.Send(connectionId, new RoomClosedMsg
                {
                    RequestId = _sessions[connectionId].RequestId,
                    RoomId = message.RoomId,
                    Reason = "not in room",
                });
                return;
            }

            if (message.LocalPlayerId != index || message.PlayerCount != MugenMatchProtocol.PlayerCount || message.RoomSeed != room.Seed)
            {
                CloseRoom(message.RoomId, "ready mismatch", exceptConnectionId: -1);
                Log("房间就绪信息不匹配 房间=" + message.RoomId +
                    " 来源连接=" + connectionId +
                    " 期望本地玩家=" + index +
                    " 实际本地玩家=" + message.LocalPlayerId +
                    " 实际种子=" + message.RoomSeed +
                    " 期望种子=" + room.Seed);
                return;
            }

            room.Ready[index] = true;
            Log("房间就绪 房间=" + room.RoomId + " 玩家序号=" + index + " 就绪=" + ReadySummary(room));
            SendKnownNetStatusesTo(room, index);
            if (AllReady(room) && !room.Started)
            {
                room.Started = true;
                Log("开局 房间=" + room.RoomId + " 玩家=" + RoomPlayersSummary(room));
                SendToRoom(room, new StartMatchMsg { RoomId = room.RoomId, StartFrame = 0 });
                SendKnownNetStatusesToAll(room);
            }
        }

        void HandleLoadProgress(int connectionId, LoadProgressMsg message)
        {
            if (!_rooms.TryGetValue(message.RoomId, out RoomSession room))
            {
                return;
            }

            int index = room.IndexOf(connectionId);
            if (index < 0 || message.PlayerId != index)
            {
                Log("忽略加载进度 房间=" + message.RoomId +
                    " 来源连接=" + connectionId +
                    " 消息玩家=" + message.PlayerId +
                    " 解析玩家=" + index);
                return;
            }

            if (message.ProgressPermille < 0)
            {
                message.ProgressPermille = 0;
            }
            if (message.ProgressPermille > 1000)
            {
                message.ProgressPermille = 1000;
            }
            room.LoadProgressSeen[index] = true;
            if (message.Ready || message.ProgressPermille >= 1000 || message.ProgressPermille == 0)
            {
                Log("加载进度 房间=" + room.RoomId +
                    " 玩家=" + index +
                    " 进度=" + message.ProgressPermille +
                    " 就绪=" + message.Ready +
                    " 已收到=" + LoadSeenSummary(room));
            }

            for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
            {
                int targetConnection = room.Players[i].ConnectionId;
                if (targetConnection != connectionId)
                {
                    _sink.Send(targetConnection, message);
                }
            }
        }

        void HandleLeave(int connectionId, LeaveRoomMsg message)
        {
            if (!_sessions.TryGetValue(connectionId, out PlayerSession session) ||
                session.RoomId == 0 || session.RoomId != message.RoomId ||
                !_rooms.TryGetValue(message.RoomId, out RoomSession room) ||
                room.IndexOf(connectionId) < 0)
            {
                return;
            }

            string reason = message.MatchCompleted ? "match completed" : "player left room";
            CloseRoom(message.RoomId, reason, exceptConnectionId: -1);
            Log("玩家离开房间 房间=" + message.RoomId + " 玩家=" + PlayerSummary(session));
        }

        void HandlePing(int connectionId, PingMsg message)
        {
            _sink.Send(connectionId, new PongMsg
            {
                Sequence = message.Sequence,
                ClientTimeMs = message.ClientTimeMs,
                ServerTimeMs = _nowMs,
            });
        }

        void HandleInput(int connectionId, MugenInputMsg message)
        {
            PlayerSession session = _sessions[connectionId];
            if (session.RoomId == 0 || !_rooms.TryGetValue(session.RoomId, out RoomSession room))
            {
                return;
            }

            int fromIndex = room.IndexOf(connectionId);
            if (fromIndex < 0 || message.PlayerId != fromIndex)
            {
                return;
            }

            if (!ValidateInput(room, fromIndex, message))
            {
                return;
            }

            for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
            {
                int targetConnection = room.Players[i].ConnectionId;
                if (targetConnection != connectionId)
                {
                    _sink.Send(targetConnection, message);
                }
            }
        }

        void HandleHashReport(int connectionId, MugenHashReportMsg message)
        {
            PlayerSession session = _sessions[connectionId];
            if (session.RoomId == 0 || !_rooms.TryGetValue(session.RoomId, out RoomSession room))
            {
                return;
            }

            int fromIndex = room.IndexOf(connectionId);
            if (fromIndex < 0 || message.PlayerId != fromIndex || message.RoomId != room.RoomId)
            {
                return;
            }
            if (message.Frame < 0)
            {
                CloseRoom(room.RoomId, "invalid hash report", exceptConnectionId: -1);
                return;
            }

            Dictionary<int, ulong> own = room.HashReports[fromIndex];
            if (own.TryGetValue(message.Frame, out ulong existingHash))
            {
                if (existingHash != message.Hash)
                {
                    CloseRoom(room.RoomId, "conflicting hash report", exceptConnectionId: -1);
                }
                return;
            }

            own[message.Frame] = message.Hash;
            PruneHashHistory(own, message.Frame - HashHistoryToKeep);

            int otherIndex = 1 - fromIndex;
            Dictionary<int, ulong> other = room.HashReports[otherIndex];
            if (other.TryGetValue(message.Frame, out ulong otherHash) && otherHash != message.Hash)
            {
                CloseRoom(room.RoomId, "hash mismatch frame " + message.Frame, exceptConnectionId: -1);
            }
        }

        void HandleNetStatus(int connectionId, MugenNetStatusMsg message)
        {
            PlayerSession session = _sessions[connectionId];
            if (session.RoomId == 0 || !_rooms.TryGetValue(session.RoomId, out RoomSession room))
            {
                return;
            }

            int fromIndex = room.IndexOf(connectionId);
            if (fromIndex < 0 || message.PlayerId != fromIndex || message.RoomId != room.RoomId)
            {
                return;
            }

            message.WeakDelayMs = ClampInt(message.WeakDelayMs, 0, MaxReportedWeakDelayMs);
            message.LatencyMs = ClampInt(message.LatencyMs, -1, MaxReportedLatencyMs);
            MugenNetStatusMsg status = CloneNetStatus(message);
            room.NetStatusSeen[fromIndex] = true;
            room.NetStatuses[fromIndex] = status;

            for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
            {
                int targetConnection = room.Players[i].ConnectionId;
                if (targetConnection != connectionId)
                {
                    _sink.Send(targetConnection, CloneNetStatus(status));
                }
            }
        }

        void SendKnownNetStatusesToAll(RoomSession room)
        {
            for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
            {
                SendKnownNetStatusesTo(room, i);
            }
        }

        void SendKnownNetStatusesTo(RoomSession room, int targetIndex)
        {
            if (room == null || targetIndex < 0 || targetIndex >= MugenMatchProtocol.PlayerCount)
            {
                return;
            }

            int targetConnection = room.Players[targetIndex].ConnectionId;
            if (!_sessions.ContainsKey(targetConnection))
            {
                return;
            }

            for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
            {
                if (i == targetIndex || !room.NetStatusSeen[i] || room.NetStatuses[i] == null)
                {
                    continue;
                }
                _sink.Send(targetConnection, CloneNetStatus(room.NetStatuses[i]));
            }
        }

        static MugenNetStatusMsg CloneNetStatus(MugenNetStatusMsg source)
        {
            return new MugenNetStatusMsg
            {
                RoomId = source.RoomId,
                PlayerId = source.PlayerId,
                Frame = source.Frame,
                WeakDelayMs = source.WeakDelayMs,
                LatencyMs = source.LatencyMs,
            };
        }

        bool ValidateInput(RoomSession room, int playerIndex, MugenInputMsg message)
        {
            if (message.Frame < 0 || (message.Input & ~ValidMugenInputMask) != 0)
            {
                CloseRoom(room.RoomId, "invalid input", exceptConnectionId: -1);
                return false;
            }

            Dictionary<int, int> history = room.InputHistory[playerIndex];
            if (history.TryGetValue(message.Frame, out int existingInput))
            {
                if (existingInput != message.Input)
                {
                    CloseRoom(room.RoomId, "conflicting input", exceptConnectionId: -1);
                }
                return false;
            }

            int lastFrame = room.LastInputFrame[playerIndex];
            if (lastFrame >= 0)
            {
                if (message.Frame > lastFrame + MaxSequentialInputFrameJump)
                {
                    CloseRoom(room.RoomId, "input frame jump", exceptConnectionId: -1);
                    return false;
                }
                if (message.Frame < lastFrame - InputHistoryToKeep)
                {
                    return false;
                }
            }
            else if (message.Frame > MaxSequentialInputFrameJump)
            {
                CloseRoom(room.RoomId, "input frame jump", exceptConnectionId: -1);
                return false;
            }

            history[message.Frame] = message.Input;
            if (message.Frame > lastFrame)
            {
                room.LastInputFrame[playerIndex] = message.Frame;
                PruneInputHistory(history, message.Frame - InputHistoryToKeep);
            }
            return true;
        }

        static void PruneInputHistory(Dictionary<int, int> history, int minFrame)
        {
            if (history.Count <= InputHistoryToKeep)
            {
                return;
            }

            List<int> remove = null;
            foreach (KeyValuePair<int, int> pair in history)
            {
                if (pair.Key >= minFrame)
                {
                    continue;
                }
                if (remove == null) { remove = new List<int>(); }
                remove.Add(pair.Key);
            }
            if (remove == null)
            {
                return;
            }
            for (int i = 0; i < remove.Count; i++)
            {
                history.Remove(remove[i]);
            }
        }

        static void PruneHashHistory(Dictionary<int, ulong> history, int minFrame)
        {
            if (history.Count <= HashHistoryToKeep)
            {
                return;
            }

            List<int> remove = null;
            foreach (KeyValuePair<int, ulong> pair in history)
            {
                if (pair.Key >= minFrame)
                {
                    continue;
                }
                if (remove == null) { remove = new List<int>(); }
                remove.Add(pair.Key);
            }
            if (remove == null)
            {
                return;
            }
            for (int i = 0; i < remove.Count; i++)
            {
                history.Remove(remove[i]);
            }
        }

        void TryMatch()
        {
            while (_queue.Count >= MugenMatchProtocol.PlayerCount)
            {
                Log("尝试匹配 队列=[" + QueueSummary() + "]");
                int firstIndex = -1;
                int compatibleIndex = -1;
                for (int i = 0; i < _queue.Count && compatibleIndex < 0; i++)
                {
                    for (int j = i + 1; j < _queue.Count; j++)
                    {
                        if (CompatibilityBlockReason(_queue[i], _queue[j]) == null)
                        {
                            firstIndex = i;
                            compatibleIndex = j;
                            break;
                        }
                    }
                }

                if (compatibleIndex < 0)
                {
                    Log("没有找到兼容对手 队列=[" + QueueSummary() + "] 原因=[" + CompatibilitySummary() + "]");
                    return;
                }

                PlayerSession first = _queue[firstIndex];
                PlayerSession second = _queue[compatibleIndex];
                _queue.RemoveAt(compatibleIndex);
                _queue.RemoveAt(firstIndex);
                first.InQueue = false;
                second.InQueue = false;

                Log("找到兼容对手 玩家一=" + PlayerSummary(first) + " 玩家二=" + PlayerSummary(second));
                CreateRoom(first, second);
            }
        }

        string CompatibilityBlockReason(PlayerSession first, PlayerSession second)
        {
            if (!string.IsNullOrEmpty(first.ClientInstanceId) &&
                first.ClientInstanceId == second.ClientInstanceId)
            {
                return "同一客户端实例 " + Safe(first.ClientInstanceId);
            }
            if (!string.IsNullOrEmpty(first.ClientVersion) && !string.IsNullOrEmpty(second.ClientVersion) &&
                first.ClientVersion != second.ClientVersion)
            {
                return "客户端版本不一致 " + Safe(first.ClientVersion) + " != " + Safe(second.ClientVersion);
            }
            if (!string.IsNullOrEmpty(first.ContentHash) && !string.IsNullOrEmpty(second.ContentHash) &&
                first.ContentHash != second.ContentHash)
            {
                return "内容Hash不一致 " + Safe(first.ContentHash) + " != " + Safe(second.ContentHash);
            }
            return null;
        }

        static string ValidateFindMatch(FindMatchMsg message)
        {
            if (string.IsNullOrEmpty(message.ClientVersion))
            {
                return "missing client version";
            }
            if (string.IsNullOrEmpty(message.ContentHash))
            {
                return "missing content hash";
            }
            if (string.IsNullOrEmpty(message.TeamCsv))
            {
                return "missing team";
            }
            if (!string.IsNullOrEmpty(message.ClientInstanceId))
            {
                if (message.ClientInstanceId.Length > 128)
                {
                    return "invalid client instance";
                }
                for (int i = 0; i < message.ClientInstanceId.Length; i++)
                {
                    char ch = message.ClientInstanceId[i];
                    if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '-' && ch != '.' && ch != ':')
                    {
                        return "invalid client instance";
                    }
                }
            }

            string[] parts = message.TeamCsv.Split(',');
            if (parts.Length != MugenMatchProtocol.TeamSize)
            {
                return "invalid team size";
            }

            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i].Trim();
                if (name.Length == 0 || name.Length > 64)
                {
                    return "invalid character name";
                }
                for (int c = 0; c < name.Length; c++)
                {
                    char ch = name[c];
                    if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '-' && ch != '.')
                    {
                        return "invalid character name";
                    }
                }
            }

            return null;
        }

        void CreateRoom(PlayerSession first, PlayerSession second)
        {
            int roomId = _nextRoomId++;
            int seed = NextSeed();
            RoomSession room = new RoomSession
            {
                RoomId = roomId,
                Seed = seed,
                Players = new[] { first, second },
                Ready = new bool[MugenMatchProtocol.PlayerCount],
                LoadProgressSeen = new bool[MugenMatchProtocol.PlayerCount],
                NetStatusSeen = new bool[MugenMatchProtocol.PlayerCount],
                NetStatuses = new MugenNetStatusMsg[MugenMatchProtocol.PlayerCount],
                CreatedMs = _nowMs,
                LastMatchFoundSendMs = _nowMs,
                LastInputFrame = new[] { -1, -1 },
                InputHistory = new[]
                {
                    new Dictionary<int, int>(),
                    new Dictionary<int, int>(),
                },
                HashReports = new[]
                {
                    new Dictionary<int, ulong>(),
                    new Dictionary<int, ulong>(),
                },
            };
            _rooms.Add(roomId, room);
            first.RoomId = roomId;
            second.RoomId = roomId;
            Log("匹配成功 房间=" + roomId +
                " 种子=" + seed +
                " 队列人数=" + _queue.Count +
                " 房间数=" + _rooms.Count +
                " 玩家一=" + PlayerSummary(first) +
                " 玩家二=" + PlayerSummary(second));

            room.MatchFoundMessages = new[]
            {
                new MatchFoundMsg
                {
                    RequestId = first.RequestId,
                    RoomId = roomId,
                    LocalPlayerId = 0,
                    PlayerCount = MugenMatchProtocol.PlayerCount,
                    Seed = seed,
                    Team0Csv = first.TeamCsv,
                    Team1Csv = second.TeamCsv,
                    OpponentName = second.Nickname,
                },
                new MatchFoundMsg
                {
                    RequestId = second.RequestId,
                    RoomId = roomId,
                    LocalPlayerId = 1,
                    PlayerCount = MugenMatchProtocol.PlayerCount,
                    Seed = seed,
                    Team0Csv = first.TeamCsv,
                    Team1Csv = second.TeamCsv,
                    OpponentName = first.Nickname,
                },
            };

            SendMatchFound(room, 0);
            SendMatchFound(room, 1);
        }

        void SendMatchFound(RoomSession room, int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= MugenMatchProtocol.PlayerCount)
            {
                return;
            }
            PlayerSession player = room.Players[playerIndex];
            if (_sessions.ContainsKey(player.ConnectionId))
            {
                Log("发送匹配成功 房间=" + room.RoomId +
                    " 目标连接=" + player.ConnectionId +
                    " 本地玩家=" + playerIndex +
                    " request=" + room.MatchFoundMessages[playerIndex].RequestId);
                _sink.Send(player.ConnectionId, room.MatchFoundMessages[playerIndex]);
            }
        }

        void ResendMatchFoundIfNeeded(RoomSession room)
        {
            if (room.Started || room.MatchFoundMessages == null ||
                _nowMs - room.CreatedMs > MatchFoundResendWindowMs ||
                _nowMs - room.LastMatchFoundSendMs < MatchFoundResendIntervalMs)
            {
                return;
            }

            bool resent = false;
            for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
            {
                if (!room.LoadProgressSeen[i])
                {
                    Log("重发匹配成功 房间=" + room.RoomId + " 玩家序号=" + i +
                        " 连接=" + room.Players[i].ConnectionId);
                    SendMatchFound(room, i);
                    resent = true;
                }
            }

            if (resent)
            {
                room.LastMatchFoundSendMs = _nowMs;
            }
        }

        int NextSeed()
        {
            _seed = _seed * 1103515245 + 12345;
            return (_seed & 0x7fffffff) + 1;
        }

        void CloseRoom(int roomId, string reason, int exceptConnectionId)
        {
            if (roomId == 0 || !_rooms.TryGetValue(roomId, out RoomSession room))
            {
                return;
            }

            _rooms.Remove(roomId);
            Log("关闭房间 房间=" + roomId + " 原因=" + ReasonText(reason) + " 玩家=" + RoomPlayersSummary(room));
            for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
            {
                PlayerSession player = room.Players[i];
                player.RoomId = 0;
                player.InQueue = false;
                if (player.ConnectionId != exceptConnectionId && _sessions.ContainsKey(player.ConnectionId))
                {
                    _sink.Send(player.ConnectionId, new RoomClosedMsg
                    {
                        RequestId = player.RequestId,
                        RoomId = roomId,
                        Reason = reason,
                    });
                }
            }
        }

        void SendToRoom(RoomSession room, IMessage message)
        {
            for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
            {
                _sink.Send(room.Players[i].ConnectionId, message);
            }
        }

        static bool AllReady(RoomSession room)
        {
            for (int i = 0; i < MugenMatchProtocol.PlayerCount; i++)
            {
                if (!room.Ready[i]) { return false; }
            }
            return true;
        }

        void Log(string message)
        {
            _logSink?.Log("[匹配核心] " + message);
        }

        string StatusSummary()
        {
            return "连接=" + _sessions.Count + " 队列=" + _queue.Count + " 房间=" + _rooms.Count;
        }

        string QueueSummary()
        {
            if (_queue.Count == 0)
            {
                return "空";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _queue.Count; i++)
            {
                if (i > 0) { sb.Append(" | "); }
                sb.Append(PlayerSummary(_queue[i]));
            }
            return sb.ToString();
        }

        string CompatibilitySummary()
        {
            if (_queue.Count < 2)
            {
                return "等待更多玩家";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _queue.Count; i++)
            {
                for (int j = i + 1; j < _queue.Count; j++)
                {
                    if (sb.Length > 0) { sb.Append(" | "); }
                    string reason = CompatibilityBlockReason(_queue[i], _queue[j]);
                    sb.Append(_queue[i].ConnectionId).Append("<->").Append(_queue[j].ConnectionId)
                        .Append(": ").Append(reason ?? "兼容");
                }
            }
            return sb.ToString();
        }

        static string PlayerSummary(PlayerSession player)
        {
            if (player == null)
            {
                return "空";
            }
            return "连接=" + player.ConnectionId +
                   " request=" + player.RequestId +
                   " 昵称=" + Safe(player.Nickname) +
                   " 实例=" + Safe(player.ClientInstanceId) +
                   " 版本=" + Safe(player.ClientVersion) +
                   " 内容Hash=" + Safe(player.ContentHash) +
                   " 队伍=" + Safe(player.TeamCsv) +
                   " 房间=" + player.RoomId +
                   " 队列中=" + player.InQueue;
        }

        static string RoomPlayersSummary(RoomSession room)
        {
            if (room == null || room.Players == null)
            {
                return "无";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < room.Players.Length; i++)
            {
                if (i > 0) { sb.Append(" | "); }
                sb.Append("p").Append(i).Append("{").Append(PlayerSummary(room.Players[i])).Append("}");
            }
            return sb.ToString();
        }

        static string ReadySummary(RoomSession room)
        {
            return BoolArraySummary(room != null ? room.Ready : null);
        }

        static string LoadSeenSummary(RoomSession room)
        {
            return BoolArraySummary(room != null ? room.LoadProgressSeen : null);
        }

        static string BoolArraySummary(bool[] values)
        {
            if (values == null)
            {
                return "无";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) { sb.Append(","); }
                sb.Append(i).Append("=").Append(values[i] ? "是" : "否");
            }
            return sb.ToString();
        }

        static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<空>" : value;
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

        void RemoveFromQueue(PlayerSession target)
        {
            if (!target.InQueue)
            {
                return;
            }

            target.InQueue = false;
            _queue.Remove(target);
        }

        void RemoveQueuedSameInstance(PlayerSession current, string clientInstanceId)
        {
            if (string.IsNullOrEmpty(clientInstanceId))
            {
                return;
            }

            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                PlayerSession queued = _queue[i];
                if (queued.ConnectionId == current.ConnectionId || queued.ClientInstanceId != clientInstanceId)
                {
                    continue;
                }

                queued.InQueue = false;
                _queue.RemoveAt(i);
                Log("移除同实例旧队列项 新连接=" + current.ConnectionId + " 旧玩家=" + PlayerSummary(queued));
                _sink.Send(queued.ConnectionId, new RoomClosedMsg
                {
                    RequestId = queued.RequestId,
                    RoomId = 0,
                    Reason = "match superseded",
                });
            }
        }

        bool TimedOut(PlayerSession player, int timeoutMs)
        {
            return _nowMs - player.LastSeenMs > timeoutMs;
        }

        static int ClampInt(int value, int min, int max)
        {
            if (value < min) { return min; }
            if (value > max) { return max; }
            return value;
        }

        sealed class PlayerSession
        {
            public int ConnectionId;
            public string Nickname = "";
            public string TeamCsv = "";
            public string ContentHash = "";
            public string ClientVersion = "";
            public string ClientInstanceId = "";
            public bool InQueue;
            public int RoomId;
            public int LastSeenMs;
            public int RequestId;
        }

        sealed class RoomSession
        {
            public int RoomId;
            public int Seed;
            public PlayerSession[] Players;
            public bool[] Ready;
            public bool[] LoadProgressSeen;
            public bool[] NetStatusSeen;
            public MugenNetStatusMsg[] NetStatuses;
            public MatchFoundMsg[] MatchFoundMessages;
            public int CreatedMs;
            public int LastMatchFoundSendMs;
            public int[] LastInputFrame;
            public Dictionary<int, int>[] InputHistory;
            public Dictionary<int, ulong>[] HashReports;
            public bool Started;

            public int IndexOf(int connectionId)
            {
                for (int i = 0; i < Players.Length; i++)
                {
                    if (Players[i].ConnectionId == connectionId)
                    {
                        return i;
                    }
                }
                return -1;
            }
        }
    }
}
