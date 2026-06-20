using System.Net;
using System.Net.Sockets;
using System.Text;
using KcpProject;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Network;

const uint KcpConv = 0xFEEDFACE;

int port = ResolvePort(args, 7777);
string traceLogPath = ResolveTraceLogPath(args);
using CancellationTokenSource cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using TraceLog traceLog = new TraceLog(traceLogPath);
using RelayTransport transport = new RelayTransport(KcpConv, traceLog);
MugenMatchServerCore core = new MugenMatchServerCore(transport);
transport.OnPlayerConnected += core.OnConnected;
transport.OnPlayerDisconnected += core.OnDisconnected;
transport.Start(port);

Console.WriteLine($"[MugenRelayServer] listening on UDP {port}");
Console.WriteLine("[MugenRelayServer] Ctrl+C to stop.");

long handled = 0;
long lastStats = Environment.TickCount64;
while (!cts.IsCancellationRequested)
{
    transport.Update();
    core.Tick(unchecked((int)Environment.TickCount));
    while (transport.Poll(out int from, out IMessage message))
    {
        bool logControl = RelayLog.IsControl(message);
        if (logControl)
        {
            Console.WriteLine($"[MugenRelayServer] recv from={from} {RelayLog.Describe(message)}");
        }
        traceLog.Write("recv", from, message, core.WaitingCount, core.RoomCount);
        core.HandleMessage(from, message);
        handled++;
        if (logControl)
        {
            Console.WriteLine($"[MugenRelayServer] state queue={core.WaitingCount} rooms={core.RoomCount} handled={handled}");
            traceLog.WriteState(core.WaitingCount, core.RoomCount, handled);
        }
    }

    long now = Environment.TickCount64;
    if (now - lastStats >= 5000)
    {
        lastStats = now;
        Console.WriteLine($"[MugenRelayServer] queue={core.WaitingCount} rooms={core.RoomCount} handled={handled}");
    }

    Thread.Sleep(5);
}

Console.WriteLine("[MugenRelayServer] stopped.");

static int ResolvePort(string[] args, int fallback)
{
    string env = Environment.GetEnvironmentVariable("MUGEN_SERVER_PORT");
    if (TryPort(env, out int envPort))
    {
        return envPort;
    }

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

static string ResolveTraceLogPath(string[] args)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--trace-log")
        {
            return args[i + 1];
        }
    }
    return "";
}

sealed class TraceLog : IDisposable
{
    readonly object _lock = new object();
    readonly StreamWriter _writer;

    public TraceLog(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _writer = new StreamWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
        WriteRaw("{\"kind\":\"server\",\"event\":\"trace_start\",\"serverUnixMs\":" + UnixMs() + "}");
    }

    public void Write(string direction, int connectionId, IMessage message, int queue, int rooms)
    {
        if (_writer == null || message == null)
        {
            return;
        }

        string extra = "";
        switch (message)
        {
            case MugenInputMsg m:
                extra = ",\"frame\":" + m.Frame +
                        ",\"playerId\":" + m.PlayerId +
                        ",\"input\":" + m.Input +
                        ",\"traceSeq\":" + m.TraceSeq +
                        ",\"clientUnixMs\":" + m.ClientUnixMs +
                        ",\"clientTickMs\":" + m.ClientTickMs +
                        ",\"clientId\":\"" + Json(m.ClientId) + "\"" +
                        ",\"runId\":\"" + Json(m.RunId) + "\"";
                break;
            case ClientTraceMsg m:
                extra = ",\"roomId\":" + m.RoomId +
                        ",\"traceSeq\":" + m.Seq +
                        ",\"clientUnixMs\":" + m.ClientUnixMs +
                        ",\"clientTickMs\":" + m.ClientTickMs +
                        ",\"frame\":" + m.Frame +
                        ",\"input\":" + m.Input +
                        ",\"clientId\":\"" + Json(m.ClientId) + "\"" +
                        ",\"runId\":\"" + Json(m.RunId) + "\"" +
                        ",\"scene\":\"" + Json(m.Scene) + "\"" +
                        ",\"clientEvent\":\"" + Json(m.EventName) + "\"" +
                        ",\"detail\":\"" + Json(m.Detail) + "\"";
                break;
            case FindMatchMsg m:
                extra = ",\"requestId\":" + m.RequestId +
                        ",\"clientId\":\"" + Json(m.Nickname) + "\"" +
                        ",\"team\":\"" + Json(m.TeamCsv) + "\"" +
                        ",\"contentHash\":\"" + Json(m.ContentHash) + "\"" +
                        ",\"version\":\"" + Json(m.ClientVersion) + "\"" +
                        ",\"clientInstanceId\":\"" + Json(m.ClientInstanceId) + "\"";
                break;
            case MatchFoundMsg m:
                extra = ",\"requestId\":" + m.RequestId +
                        ",\"roomId\":" + m.RoomId +
                        ",\"localPlayerId\":" + m.LocalPlayerId +
                        ",\"team0\":\"" + Json(m.Team0Csv) + "\"" +
                        ",\"team1\":\"" + Json(m.Team1Csv) + "\"" +
                        ",\"opponent\":\"" + Json(m.OpponentName) + "\"";
                break;
            case RoomReadyMsg m:
                extra = ",\"roomId\":" + m.RoomId + ",\"localPlayerId\":" + m.LocalPlayerId;
                break;
            case MugenHashReportMsg m:
                extra = ",\"roomId\":" + m.RoomId +
                        ",\"frame\":" + m.Frame +
                        ",\"playerId\":" + m.PlayerId +
                        ",\"hash\":\"" + m.Hash.ToString("X16") + "\"";
                break;
            case MugenNetStatusMsg m:
                extra = ",\"roomId\":" + m.RoomId +
                        ",\"frame\":" + m.Frame +
                        ",\"playerId\":" + m.PlayerId +
                        ",\"weakDelayMs\":" + m.WeakDelayMs +
                        ",\"latencyMs\":" + m.LatencyMs;
                break;
            case StartMatchMsg m:
                extra = ",\"roomId\":" + m.RoomId + ",\"startFrame\":" + m.StartFrame;
                break;
            case RoomClosedMsg m:
                extra = ",\"requestId\":" + m.RequestId + ",\"roomId\":" + m.RoomId + ",\"reason\":\"" + Json(m.Reason) + "\"";
                break;
            case LeaveRoomMsg m:
                extra = ",\"roomId\":" + m.RoomId + ",\"matchCompleted\":" + (m.MatchCompleted ? "true" : "false");
                break;
            case CancelMatchMsg m:
                extra = ",\"requestId\":" + m.RequestId;
                break;
        }

        WriteRaw("{\"kind\":\"server\",\"event\":\"" + Json(direction) + "\",\"serverUnixMs\":" + UnixMs() +
                 ",\"serverTickMs\":" + Environment.TickCount +
                 ",\"connectionId\":" + connectionId +
                 ",\"msgType\":\"" + message.Type + "\"" +
                 ",\"queue\":" + queue +
                 ",\"rooms\":" + rooms +
                 extra + "}");
    }

    public void WriteState(int queue, int rooms, long handled)
    {
        if (_writer == null)
        {
            return;
        }
        WriteRaw("{\"kind\":\"server\",\"event\":\"state\",\"serverUnixMs\":" + UnixMs() +
                 ",\"serverTickMs\":" + Environment.TickCount +
                 ",\"queue\":" + queue +
                 ",\"rooms\":" + rooms +
                 ",\"handled\":" + handled + "}");
    }

    public void WriteCore(string message)
    {
        if (_writer == null)
        {
            return;
        }
        WriteRaw("{\"kind\":\"server\",\"event\":\"core\",\"serverUnixMs\":" + UnixMs() +
                 ",\"serverTickMs\":" + Environment.TickCount +
                 ",\"message\":\"" + Json(message) + "\"}");
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }

    void WriteRaw(string line)
    {
        lock (_lock)
        {
            _writer?.WriteLine(line);
        }
    }

    static long UnixMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    static string Json(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        StringBuilder builder = new StringBuilder(value.Length + 8);
        foreach (char ch in value)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                default:
                    if (char.IsControl(ch) || ch > 0x7E)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)ch).ToString("X4"));
                    }
                    else
                    {
                        builder.Append(ch);
                    }
                    break;
            }
        }
        return builder.ToString();
    }
}

sealed class RelayTransport : IMugenMatchServerSink, IMugenMatchServerLogSink, IDisposable
{
    readonly uint _conv;
    readonly TraceLog _traceLog;
    readonly Dictionary<IPEndPoint, ClientLink> _clientsByEndpoint = new Dictionary<IPEndPoint, ClientLink>();
    readonly Dictionary<int, ClientLink> _clientsById = new Dictionary<int, ClientLink>();
    readonly Queue<PendingMessage> _inbox = new Queue<PendingMessage>();
    readonly byte[] _udpBuffer = new byte[65536];
    readonly byte[] _kcpBuffer = new byte[65536];
    Socket _socket;
    int _nextConnectionId;

    public RelayTransport(uint conv, TraceLog traceLog)
    {
        _conv = conv;
        _traceLog = traceLog;
    }

    public event Action<int> OnPlayerConnected;
    public event Action<int> OnPlayerDisconnected;

    public void Start(int port)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            Blocking = false,
        };
        _socket.Bind(new IPEndPoint(IPAddress.Any, port));
    }

    public void Update()
    {
        if (_socket == null)
        {
            return;
        }

        EndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (_socket.Available > 0)
        {
            EndPoint from = any;
            int count;
            try
            {
                count = _socket.ReceiveFrom(_udpBuffer, ref from);
            }
            catch (SocketException)
            {
                break;
            }

            if (count <= 0 || from is not IPEndPoint endpoint)
            {
                break;
            }

            ClientLink link = GetOrCreateClient(endpoint);
            link.LastSeenMs = unchecked((int)Environment.TickCount);
            link.Kcp.Input(_udpBuffer, 0, count, true, true);
        }

        List<int> disconnected = null;
        foreach (ClientLink link in _clientsByEndpoint.Values)
        {
            while (true)
            {
                int size = link.Kcp.PeekSize();
                if (size <= 0)
                {
                    break;
                }
                if (size > _kcpBuffer.Length)
                {
                    break;
                }

                int got = link.Kcp.Recv(_kcpBuffer, 0, size);
                if (got <= 0)
                {
                    break;
                }

                IMessage message = MessageCodec.Decode(_kcpBuffer, 0, got);
                if (message != null)
                {
                    _inbox.Enqueue(new PendingMessage(link.ConnectionId, message));
                }
            }
            link.Kcp.Update();

            int now = unchecked((int)Environment.TickCount);
            if (unchecked(now - link.LastSeenMs) > 30000)
            {
                disconnected ??= new List<int>();
                disconnected.Add(link.ConnectionId);
            }
        }

        if (disconnected != null)
        {
            for (int i = 0; i < disconnected.Count; i++)
            {
                RemoveClient(disconnected[i], "timeout");
            }
        }
    }

    public bool Poll(out int connectionId, out IMessage message)
    {
        if (_inbox.Count > 0)
        {
            PendingMessage pending = _inbox.Dequeue();
            connectionId = pending.ConnectionId;
            message = pending.Message;
            return true;
        }

        connectionId = -1;
        message = null!;
        return false;
    }

    public void Send(int connectionId, IMessage message)
    {
        if (!_clientsById.TryGetValue(connectionId, out ClientLink link))
        {
            return;
        }

        if (RelayLog.IsControl(message))
        {
            Console.WriteLine($"[MugenRelayServer] send to={connectionId} {RelayLog.Describe(message)}");
        }
        _traceLog.Write("send", connectionId, message, -1, -1);
        byte[] bytes = MessageCodec.Encode(message);
        link.Kcp.Send(bytes, 0, bytes.Length);
    }

    public void Log(string message)
    {
        Console.WriteLine("[MugenRelayServer] core " + message);
        _traceLog.WriteCore(message);
    }

    public void Dispose()
    {
        try
        {
            _socket?.Close();
        }
        catch
        {
            // Best-effort shutdown.
        }
        _clientsByEndpoint.Clear();
        _clientsById.Clear();
    }

    ClientLink GetOrCreateClient(IPEndPoint endpoint)
    {
        IPEndPoint stable = new IPEndPoint(endpoint.Address, endpoint.Port);
        if (_clientsByEndpoint.TryGetValue(stable, out ClientLink existing))
        {
            return existing;
        }

        int id = _nextConnectionId++;
        ClientLink link = new ClientLink
        {
            ConnectionId = id,
            Endpoint = stable,
            LastSeenMs = unchecked((int)Environment.TickCount),
        };
        link.Kcp = new KCP(_conv, (data, len) =>
        {
            try
            {
                _socket?.SendTo(data, 0, len, SocketFlags.None, link.Endpoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MugenRelayServer] send failed id={link.ConnectionId} ep={link.Endpoint}: {ex.Message}");
            }
        });
        link.Kcp.NoDelay(1, 10, 2, 1);
        link.Kcp.WndSize(128, 128);

        _clientsByEndpoint[stable] = link;
        _clientsById[id] = link;
        Console.WriteLine($"[MugenRelayServer] connected id={id} endpoint={stable}");
        OnPlayerConnected?.Invoke(id);
        return link;
    }

    void RemoveClient(int connectionId, string reason)
    {
        if (!_clientsById.TryGetValue(connectionId, out ClientLink link))
        {
            return;
        }

        _clientsById.Remove(connectionId);
        _clientsByEndpoint.Remove(link.Endpoint);
        Console.WriteLine($"[MugenRelayServer] disconnected id={connectionId} reason={reason}");
        OnPlayerDisconnected?.Invoke(connectionId);
    }

    sealed class ClientLink
    {
        public int ConnectionId;
        public IPEndPoint Endpoint = new IPEndPoint(IPAddress.None, 0);
        public KCP Kcp = null!;
        public int LastSeenMs;
    }

    readonly struct PendingMessage
    {
        public readonly int ConnectionId;
        public readonly IMessage Message;

        public PendingMessage(int connectionId, IMessage message)
        {
            ConnectionId = connectionId;
            Message = message;
        }
    }
}

static class RelayLog
{
    public static bool IsControl(IMessage message)
    {
        return message is not MugenInputMsg && message is not PingMsg && message is not PongMsg;
    }

    public static string Describe(IMessage message)
    {
        switch (message)
        {
            case FindMatchMsg m:
                return $"FindMatch request={m.RequestId} nick={Safe(m.Nickname)} team={Safe(m.TeamCsv)} hash={Safe(m.ContentHash)} version={Safe(m.ClientVersion)}";
            case CancelMatchMsg m:
                return $"CancelMatch request={m.RequestId}";
            case MatchFoundMsg m:
                return $"MatchFound request={m.RequestId} room={m.RoomId} local={m.LocalPlayerId} players={m.PlayerCount} seed={m.Seed} team0={Safe(m.Team0Csv)} team1={Safe(m.Team1Csv)} opponent={Safe(m.OpponentName)}";
            case RoomReadyMsg m:
                return $"RoomReady room={m.RoomId} local={m.LocalPlayerId} players={m.PlayerCount} seed={m.RoomSeed}";
            case StartMatchMsg m:
                return $"StartMatch room={m.RoomId} startFrame={m.StartFrame}";
            case RoomClosedMsg m:
                return $"RoomClosed request={m.RequestId} room={m.RoomId} reason={Safe(m.Reason)}";
            case LeaveRoomMsg m:
                return $"LeaveRoom room={m.RoomId} completed={m.MatchCompleted}";
            case MugenNetStatusMsg m:
                return $"MugenNetStatus room={m.RoomId} player={m.PlayerId} frame={m.Frame} weakDelay={m.WeakDelayMs}ms latency={m.LatencyMs}ms";
            default:
                return message.GetType().Name;
        }
    }

    static string Safe(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "-";
        }
        return value.Replace('\n', ' ').Replace('\r', ' ');
    }
}
