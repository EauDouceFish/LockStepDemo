using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KcpProject;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Network;

const uint KcpConv = 0xFEEDFACE;

int port = ResolvePort(args, 7777);
string traceLogPath = ResolveTraceLogPath(args);
int auditHttpPort = ResolveIntOption(args, "--audit-http-port", "MUGEN_AUDIT_HTTP_PORT", 0);
string auditHttpHost = ResolveStringOption(args, "--audit-http-host", "MUGEN_AUDIT_HTTP_HOST", "127.0.0.1");
string auditToken = ResolveStringOption(args, "--audit-token", "MUGEN_AUDIT_TOKEN", "");
long startedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
using CancellationTokenSource cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

AuditStore auditStore = new AuditStore(port);
using TraceLog traceLog = new TraceLog(traceLogPath, auditStore);
using RelayTransport transport = new RelayTransport(KcpConv, traceLog, auditStore);
MugenMatchServerCore core = new MugenMatchServerCore(transport);
long handled = 0;
transport.OnPlayerConnected += core.OnConnected;
transport.OnPlayerDisconnected += core.OnDisconnected;
transport.Start(port);
using AuditHttpServer auditHttp = new AuditHttpServer(auditStore, auditHttpHost, auditHttpPort, auditToken, () => new
{
    kind = "server",
    udpPort = port,
    auditHttpHost,
    auditHttpPort,
    startedUnixMs,
    uptimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startedUnixMs,
    queue = core.WaitingCount,
    rooms = core.RoomCount,
    connected = core.ConnectedCount,
    handled,
});
auditHttp.Start();

Console.WriteLine($"[MugenRelayServer] listening on UDP {port}");
if (auditHttp.IsRunning)
{
    Console.WriteLine($"[MugenRelayServer] audit HTTP http://{auditHttpHost}:{auditHttpPort}/health");
}
Console.WriteLine("[MugenRelayServer] Ctrl+C to stop.");

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
        auditStore.RecordMessage("recv", from, transport.EndpointText(from), message, core.WaitingCount, core.RoomCount);
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

static int ResolveIntOption(string[] args, string argName, string envName, int fallback)
{
    string env = Environment.GetEnvironmentVariable(envName);
    if (int.TryParse(env, out int envValue))
    {
        return envValue;
    }

    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == argName && int.TryParse(args[i + 1], out int argValue))
        {
            return argValue;
        }
    }
    return fallback;
}

static string ResolveStringOption(string[] args, string argName, string envName, string fallback)
{
    string env = Environment.GetEnvironmentVariable(envName);
    if (!string.IsNullOrWhiteSpace(env))
    {
        return env;
    }

    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == argName)
        {
            return args[i + 1];
        }
    }
    return fallback;
}

sealed class TraceLog : IDisposable
{
    readonly object _lock = new object();
    readonly StreamWriter _writer;
    readonly AuditStore _auditStore;

    public TraceLog(string path, AuditStore auditStore)
    {
        _auditStore = auditStore;
        if (string.IsNullOrWhiteSpace(path))
        {
            WriteRaw("{\"kind\":\"server\",\"event\":\"trace_start\",\"serverUnixMs\":" + UnixMs() + "}");
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
        if (message == null)
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
                        ",\"nickname\":\"" + Json(m.Nickname) + "\"" +
                        ",\"team\":\"" + Json(m.TeamCsv) + "\"" +
                        ",\"contentHash\":\"" + Json(m.ContentHash) + "\"" +
                        ",\"version\":\"" + Json(m.ClientVersion) + "\"" +
                        ",\"clientInstanceId\":\"" + Json(m.ClientInstanceId) + "\"" +
                        ",\"clientBuildVersion\":\"" + Json(m.ClientBuildVersion) + "\"" +
                        ",\"clientBuildGuid\":\"" + Json(m.ClientBuildGuid) + "\"" +
                        ",\"clientPlatform\":\"" + Json(m.ClientPlatform) + "\"" +
                        ",\"clientDeviceModel\":\"" + Json(m.ClientDeviceModel) + "\"" +
                        ",\"clientDeviceType\":\"" + Json(m.ClientDeviceType) + "\"" +
                        ",\"clientOperatingSystem\":\"" + Json(m.ClientOperatingSystem) + "\"";
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
        WriteRaw("{\"kind\":\"server\",\"event\":\"state\",\"serverUnixMs\":" + UnixMs() +
                 ",\"serverTickMs\":" + Environment.TickCount +
                 ",\"queue\":" + queue +
                 ",\"rooms\":" + rooms +
                 ",\"handled\":" + handled + "}");
    }

    public void WriteCore(string message)
    {
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
            _auditStore?.RecordTraceLine(line);
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

sealed class AuditStore
{
    const int MaxTailLines = 5000;
    const int MaxRecentRooms = 128;
    readonly object _lock = new object();
    readonly Queue<string> _tail = new Queue<string>(MaxTailLines);
    readonly Dictionary<int, ClientAudit> _clients = new Dictionary<int, ClientAudit>();
    readonly Dictionary<int, RoomAudit> _rooms = new Dictionary<int, RoomAudit>();
    readonly int _udpPort;

    public AuditStore(int udpPort)
    {
        _udpPort = udpPort;
    }

    public void RecordTraceLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        lock (_lock)
        {
            EnqueueTraceLine(line);
        }
    }

    public void RecordConnect(int connectionId, string endpoint)
    {
        lock (_lock)
        {
            ClientAudit client = GetClient(connectionId);
            client.Endpoint = endpoint ?? "";
            client.ConnectedUnixMs = UnixMs();
            client.DisconnectedUnixMs = 0;
            client.DisconnectReason = "";
            EnqueueTraceLine("{\"kind\":\"server\",\"event\":\"connect\",\"serverUnixMs\":" + client.ConnectedUnixMs +
                             ",\"connectionId\":" + connectionId +
                             ",\"endpoint\":\"" + Json(client.Endpoint) + "\"}");
        }
    }

    public void RecordDisconnect(int connectionId, string endpoint, string reason)
    {
        lock (_lock)
        {
            ClientAudit client = GetClient(connectionId);
            if (!string.IsNullOrEmpty(endpoint))
            {
                client.Endpoint = endpoint;
            }
            client.DisconnectedUnixMs = UnixMs();
            client.DisconnectReason = reason ?? "";
            EnqueueTraceLine("{\"kind\":\"server\",\"event\":\"disconnect\",\"serverUnixMs\":" + client.DisconnectedUnixMs +
                             ",\"connectionId\":" + connectionId +
                             ",\"endpoint\":\"" + Json(client.Endpoint) + "\"" +
                             ",\"reason\":\"" + Json(client.DisconnectReason) + "\"}");
            if (client.RoomId != 0 && _rooms.TryGetValue(client.RoomId, out RoomAudit room) && room.ClosedUnixMs == 0)
            {
                room.ClosedUnixMs = client.DisconnectedUnixMs;
                room.CloseReason = string.IsNullOrEmpty(reason) ? "disconnect" : reason;
            }
        }
    }

    public void RecordMessage(string direction, int connectionId, string endpoint, IMessage message, int queue, int rooms)
    {
        if (message == null)
        {
            return;
        }

        long now = UnixMs();
        lock (_lock)
        {
            ClientAudit client = GetClient(connectionId);
            if (!string.IsNullOrEmpty(endpoint))
            {
                client.Endpoint = endpoint;
            }
            client.LastMessageUnixMs = now;
            client.LastDirection = direction ?? "";
            client.LastMessageType = message.Type.ToString();

            switch (message)
            {
                case FindMatchMsg m when direction == "recv":
                    client.RequestId = m.RequestId;
                    client.Nickname = Limit(m.Nickname, 80);
                    client.TeamCsv = Limit(m.TeamCsv, 256);
                    client.ContentHash = Limit(m.ContentHash, 80);
                    client.ClientVersion = Limit(m.ClientVersion, 80);
                    client.ClientInstanceId = Limit(m.ClientInstanceId, 160);
                    client.ClientBuildVersion = Limit(m.ClientBuildVersion, 80);
                    client.ClientBuildGuid = Limit(m.ClientBuildGuid, 80);
                    client.ClientPlatform = Limit(m.ClientPlatform, 80);
                    client.ClientDeviceModel = Limit(m.ClientDeviceModel, 160);
                    client.ClientDeviceType = Limit(m.ClientDeviceType, 80);
                    client.ClientOperatingSystem = Limit(m.ClientOperatingSystem, 160);
                    client.MatchRequestUnixMs = now;
                    break;
                case MatchFoundMsg m when direction == "send":
                    RecordMatchFound(client, connectionId, m, now);
                    break;
                case RoomReadyMsg m when direction == "recv":
                    RecordReady(client, m, now);
                    break;
                case StartMatchMsg m when direction == "send":
                    if (m.RoomId != 0)
                    {
                        RoomAudit started = GetRoom(m.RoomId);
                        started.StartedUnixMs = FirstNonZero(started.StartedUnixMs, now);
                    }
                    break;
                case RoomClosedMsg m when direction == "send":
                    if (m.RoomId != 0)
                    {
                        RoomAudit closed = GetRoom(m.RoomId);
                        closed.ClosedUnixMs = FirstNonZero(closed.ClosedUnixMs, now);
                        closed.CloseReason = m.Reason ?? "";
                    }
                    break;
                case LeaveRoomMsg m when direction == "recv":
                    if (m.RoomId != 0)
                    {
                        RoomAudit left = GetRoom(m.RoomId);
                        left.ClosedUnixMs = FirstNonZero(left.ClosedUnixMs, now);
                        left.CloseReason = m.MatchCompleted ? "match completed" : "player left room";
                    }
                    break;
                case MugenInputMsg m when direction == "recv":
                    RecordInput(client, m, now);
                    break;
                case MugenNetStatusMsg m when direction == "recv":
                    RecordNetStatus(client, m, now);
                    break;
                case MugenHashReportMsg m when direction == "recv":
                    RecordHash(client, m, now);
                    break;
                case ClientTraceMsg m when direction == "recv":
                    client.LastClientTrace = Limit(m.EventName, 80);
                    client.LastClientTraceDetail = Limit(m.Detail, 160);
                    client.LastClientFrame = m.Frame;
                    break;
            }

            PruneClosedRooms(now);
        }
    }

    public string TailNdjson(int lines)
    {
        int count = Math.Clamp(lines <= 0 ? 200 : lines, 1, MaxTailLines);
        lock (_lock)
        {
            string[] all = _tail.ToArray();
            int start = Math.Max(0, all.Length - count);
            StringBuilder sb = new StringBuilder();
            for (int i = start; i < all.Length; i++)
            {
                sb.AppendLine(all[i]);
            }
            return sb.ToString();
        }
    }

    public object Snapshot()
    {
        lock (_lock)
        {
            return new
            {
                kind = "audit",
                unixMs = UnixMs(),
                udpPort = _udpPort,
                clients = _clients.Values.Select(c => c.Clone()).ToArray(),
                rooms = _rooms.Values.OrderByDescending(r => Math.Max(Math.Max(r.StartedUnixMs, r.ClosedUnixMs), r.CreatedUnixMs))
                    .Select(r => r.Clone()).ToArray(),
            };
        }
    }

    void RecordMatchFound(ClientAudit client, int connectionId, MatchFoundMsg message, long now)
    {
        client.RoomId = message.RoomId;
        client.PlayerId = message.LocalPlayerId;
        RoomAudit room = GetRoom(message.RoomId);
        room.CreatedUnixMs = FirstNonZero(room.CreatedUnixMs, now);
        room.RoomId = message.RoomId;
        room.Seed = message.Seed;
        room.Team0Csv = Limit(message.Team0Csv, 256);
        room.Team1Csv = Limit(message.Team1Csv, 256);
        room.Players[message.LocalPlayerId] = RoomPlayerAudit.From(connectionId, client);
    }

    void RecordReady(ClientAudit client, RoomReadyMsg message, long now)
    {
        RoomAudit room = GetRoom(message.RoomId);
        room.ReadyUnixMs = FirstNonZero(room.ReadyUnixMs, now);
        if (message.LocalPlayerId >= 0 && message.LocalPlayerId < room.Ready.Length)
        {
            room.Ready[message.LocalPlayerId] = true;
        }
    }

    void RecordInput(ClientAudit client, MugenInputMsg message, long now)
    {
        int roomId = client.RoomId;
        if (roomId == 0)
        {
            return;
        }

        RoomAudit room = GetRoom(roomId);
        int player = message.PlayerId >= 0 && message.PlayerId < room.Inputs.Length ? message.PlayerId : Math.Clamp(client.PlayerId, 0, room.Inputs.Length - 1);
        InputAudit input = room.Inputs[player];
        input.Count++;
        input.LastServerUnixMs = now;
        input.LastTraceSeq = message.TraceSeq;
        input.LastClientUnixMs = message.ClientUnixMs;
        input.LastClientTickMs = message.ClientTickMs;
        if (input.FirstFrame < 0)
        {
            input.FirstFrame = message.Frame;
        }
        if (input.LastFrame >= 0)
        {
            if (message.Frame == input.LastFrame)
            {
                input.DuplicateOrResent++;
            }
            else if (message.Frame < input.LastFrame)
            {
                input.OutOfOrder++;
            }
            else
            {
                int jump = message.Frame - input.LastFrame;
                if (jump > input.MaxFrameJump)
                {
                    input.MaxFrameJump = jump;
                }
                if (jump > 1)
                {
                    input.GapFrames += jump - 1;
                }
            }
        }
        input.LastFrame = Math.Max(input.LastFrame, message.Frame);
    }

    void RecordNetStatus(ClientAudit client, MugenNetStatusMsg message, long now)
    {
        RoomAudit room = GetRoom(message.RoomId != 0 ? message.RoomId : client.RoomId);
        int player = message.PlayerId >= 0 && message.PlayerId < room.NetStatuses.Length ? message.PlayerId : Math.Clamp(client.PlayerId, 0, room.NetStatuses.Length - 1);
        NetStatusAudit status = room.NetStatuses[player];
        status.Count++;
        status.LastServerUnixMs = now;
        status.LastFrame = message.Frame;
        status.LastWeakDelayMs = message.WeakDelayMs;
        status.LastLatencyMs = message.LatencyMs;
        status.MaxWeakDelayMs = Math.Max(status.MaxWeakDelayMs, message.WeakDelayMs);
        status.MaxLatencyMs = Math.Max(status.MaxLatencyMs, message.LatencyMs);
    }

    void RecordHash(ClientAudit client, MugenHashReportMsg message, long now)
    {
        RoomAudit room = GetRoom(message.RoomId != 0 ? message.RoomId : client.RoomId);
        int player = message.PlayerId >= 0 && message.PlayerId < room.Hashes.Length ? message.PlayerId : Math.Clamp(client.PlayerId, 0, room.Hashes.Length - 1);
        HashAudit hash = room.Hashes[player];
        hash.Count++;
        hash.LastServerUnixMs = now;
        hash.LastFrame = message.Frame;
        hash.LastHashHex = message.Hash.ToString("X16");
    }

    ClientAudit GetClient(int connectionId)
    {
        if (!_clients.TryGetValue(connectionId, out ClientAudit client))
        {
            client = new ClientAudit { ConnectionId = connectionId, PlayerId = -1 };
            _clients.Add(connectionId, client);
        }
        return client;
    }

    RoomAudit GetRoom(int roomId)
    {
        if (!_rooms.TryGetValue(roomId, out RoomAudit room))
        {
            room = new RoomAudit(roomId);
            _rooms.Add(roomId, room);
        }
        return room;
    }

    void PruneClosedRooms(long now)
    {
        if (_rooms.Count <= MaxRecentRooms)
        {
            return;
        }

        List<int> closed = _rooms.Values
            .Where(r => r.ClosedUnixMs > 0)
            .OrderBy(r => r.ClosedUnixMs)
            .Take(_rooms.Count - MaxRecentRooms)
            .Select(r => r.RoomId)
            .ToList();
        for (int i = 0; i < closed.Count; i++)
        {
            _rooms.Remove(closed[i]);
        }
    }

    static long FirstNonZero(long current, long value) => current != 0 ? current : value;
    static long UnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    void EnqueueTraceLine(string line)
    {
        _tail.Enqueue(line);
        while (_tail.Count > MaxTailLines)
        {
            _tail.Dequeue();
        }
    }

    static string Limit(string value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        value = value.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Trim();
        return value.Length <= max ? value : value.Substring(0, max);
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
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (char.IsControl(ch) || ch > 0x7E)
                    {
                        builder.Append("\\u").Append(((int)ch).ToString("X4"));
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

sealed class AuditHttpServer : IDisposable
{
    static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true, WriteIndented = false };
    readonly AuditStore _store;
    readonly string _host;
    readonly int _port;
    readonly string _token;
    readonly Func<object> _runtimeSnapshot;
    HttpListener _listener;
    Task _loop;

    public AuditHttpServer(AuditStore store, string host, int port, string token, Func<object> runtimeSnapshot)
    {
        _store = store;
        _host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
        _port = port;
        _token = token ?? "";
        _runtimeSnapshot = runtimeSnapshot;
    }

    public bool IsRunning => _listener != null && _listener.IsListening;

    public void Start()
    {
        if (_port <= 0)
        {
            return;
        }
        if (!IsLoopbackHost(_host) && string.IsNullOrEmpty(_token))
        {
            Console.WriteLine("[MugenRelayServer] audit HTTP disabled: non-loopback host requires --audit-token");
            return;
        }

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://" + _host + ":" + _port + "/");
            _listener.Start();
            _loop = Task.Run(ListenLoop);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[MugenRelayServer] audit HTTP unavailable: " + ex.Message);
            try { _listener?.Close(); } catch { }
            _listener = null;
        }
    }

    async Task ListenLoop()
    {
        while (_listener != null && _listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                break;
            }
            _ = Task.Run(() => Handle(context));
        }
    }

    void Handle(HttpListenerContext context)
    {
        try
        {
            string path = context.Request.Url?.AbsolutePath ?? "/";
            if (path == "/health")
            {
                WriteJson(context, _runtimeSnapshot());
                return;
            }

            if (path == "/audit/tail")
            {
                if (!Authorized(context))
                {
                    WriteText(context, 401, "unauthorized\n", "text/plain; charset=utf-8");
                    return;
                }

                int lines = ParseInt(context.Request.QueryString["lines"], ParseInt(context.Request.QueryString["n"], 200));
                WriteText(context, 200, _store.TailNdjson(lines), "application/x-ndjson; charset=utf-8");
                return;
            }

            if (path == "/audit/rooms")
            {
                if (!Authorized(context))
                {
                    WriteText(context, 401, "unauthorized\n", "text/plain; charset=utf-8");
                    return;
                }

                WriteJson(context, _store.Snapshot());
                return;
            }

            WriteText(context, 404, "not found\n", "text/plain; charset=utf-8");
        }
        catch (Exception ex)
        {
            WriteText(context, 500, ex.Message + "\n", "text/plain; charset=utf-8");
        }
    }

    bool Authorized(HttpListenerContext context)
    {
        if (string.IsNullOrEmpty(_token))
        {
            return true;
        }

        string auth = context.Request.Headers["Authorization"];
        if (auth == "Bearer " + _token)
        {
            return true;
        }
        return context.Request.Headers["X-Audit-Token"] == _token;
    }

    static void WriteJson(HttpListenerContext context, object value)
    {
        WriteText(context, 200, JsonSerializer.Serialize(value, JsonOptions) + "\n", "application/json; charset=utf-8");
    }

    static void WriteText(HttpListenerContext context, int status, string text, string contentType)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
        context.Response.StatusCode = status;
        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.OutputStream.Close();
    }

    static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, out int parsed) ? parsed : fallback;
    }

    static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host == "127.0.0.1" ||
            host == "::1")
        {
            return true;
        }
        return IPAddress.TryParse(host, out IPAddress address) && IPAddress.IsLoopback(address);
    }

    public void Dispose()
    {
        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
    }
}

sealed class ClientAudit
{
    public int ConnectionId;
    public string Endpoint = "";
    public long ConnectedUnixMs;
    public long DisconnectedUnixMs;
    public string DisconnectReason = "";
    public long LastMessageUnixMs;
    public string LastDirection = "";
    public string LastMessageType = "";
    public int RequestId;
    public string Nickname = "";
    public string TeamCsv = "";
    public string ContentHash = "";
    public string ClientVersion = "";
    public string ClientInstanceId = "";
    public string ClientBuildVersion = "";
    public string ClientBuildGuid = "";
    public string ClientPlatform = "";
    public string ClientDeviceModel = "";
    public string ClientDeviceType = "";
    public string ClientOperatingSystem = "";
    public long MatchRequestUnixMs;
    public int RoomId;
    public int PlayerId;
    public string LastClientTrace = "";
    public string LastClientTraceDetail = "";
    public int LastClientFrame = -1;

    public ClientAudit Clone() => (ClientAudit)MemberwiseClone();
}

sealed class RoomAudit
{
    public int RoomId;
    public int Seed;
    public long CreatedUnixMs;
    public long ReadyUnixMs;
    public long StartedUnixMs;
    public long ClosedUnixMs;
    public string CloseReason = "";
    public string Team0Csv = "";
    public string Team1Csv = "";
    public RoomPlayerAudit[] Players = { new RoomPlayerAudit(), new RoomPlayerAudit() };
    public bool[] Ready = new bool[2];
    public InputAudit[] Inputs = { new InputAudit(), new InputAudit() };
    public NetStatusAudit[] NetStatuses = { new NetStatusAudit(), new NetStatusAudit() };
    public HashAudit[] Hashes = { new HashAudit(), new HashAudit() };

    public RoomAudit(int roomId)
    {
        RoomId = roomId;
    }

    public RoomAudit Clone()
    {
        return new RoomAudit(RoomId)
        {
            Seed = Seed,
            CreatedUnixMs = CreatedUnixMs,
            ReadyUnixMs = ReadyUnixMs,
            StartedUnixMs = StartedUnixMs,
            ClosedUnixMs = ClosedUnixMs,
            CloseReason = CloseReason,
            Team0Csv = Team0Csv,
            Team1Csv = Team1Csv,
            Players = Players.Select(p => p.Clone()).ToArray(),
            Ready = (bool[])Ready.Clone(),
            Inputs = Inputs.Select(i => i.Clone()).ToArray(),
            NetStatuses = NetStatuses.Select(s => s.Clone()).ToArray(),
            Hashes = Hashes.Select(h => h.Clone()).ToArray(),
        };
    }
}

sealed class RoomPlayerAudit
{
    public int ConnectionId;
    public string Endpoint = "";
    public string Nickname = "";
    public string ClientInstanceId = "";
    public string ClientVersion = "";
    public string ClientPlatform = "";
    public string ClientDeviceModel = "";
    public string ClientDeviceType = "";
    public string ClientOperatingSystem = "";

    public static RoomPlayerAudit From(int connectionId, ClientAudit client)
    {
        return new RoomPlayerAudit
        {
            ConnectionId = connectionId,
            Endpoint = client.Endpoint,
            Nickname = client.Nickname,
            ClientInstanceId = client.ClientInstanceId,
            ClientVersion = client.ClientVersion,
            ClientPlatform = client.ClientPlatform,
            ClientDeviceModel = client.ClientDeviceModel,
            ClientDeviceType = client.ClientDeviceType,
            ClientOperatingSystem = client.ClientOperatingSystem,
        };
    }

    public RoomPlayerAudit Clone() => (RoomPlayerAudit)MemberwiseClone();
}

sealed class InputAudit
{
    public int Count;
    public int FirstFrame = -1;
    public int LastFrame = -1;
    public int MaxFrameJump;
    public int GapFrames;
    public int DuplicateOrResent;
    public int OutOfOrder;
    public long LastTraceSeq;
    public long LastClientUnixMs;
    public int LastClientTickMs;
    public long LastServerUnixMs;

    public InputAudit Clone() => (InputAudit)MemberwiseClone();
}

sealed class NetStatusAudit
{
    public int Count;
    public int LastFrame = -1;
    public int LastWeakDelayMs;
    public int LastLatencyMs = -1;
    public int MaxWeakDelayMs;
    public int MaxLatencyMs = -1;
    public long LastServerUnixMs;

    public NetStatusAudit Clone() => (NetStatusAudit)MemberwiseClone();
}

sealed class HashAudit
{
    public int Count;
    public int LastFrame = -1;
    public string LastHashHex = "";
    public long LastServerUnixMs;

    public HashAudit Clone() => (HashAudit)MemberwiseClone();
}

sealed class RelayTransport : IMugenMatchServerSink, IMugenMatchServerLogSink, IDisposable
{
    readonly uint _conv;
    readonly TraceLog _traceLog;
    readonly AuditStore _auditStore;
    readonly Dictionary<IPEndPoint, ClientLink> _clientsByEndpoint = new Dictionary<IPEndPoint, ClientLink>();
    readonly Dictionary<int, ClientLink> _clientsById = new Dictionary<int, ClientLink>();
    readonly Queue<PendingMessage> _inbox = new Queue<PendingMessage>();
    readonly byte[] _udpBuffer = new byte[65536];
    readonly byte[] _kcpBuffer = new byte[65536];
    Socket _socket;
    int _nextConnectionId;

    public RelayTransport(uint conv, TraceLog traceLog, AuditStore auditStore)
    {
        _conv = conv;
        _traceLog = traceLog;
        _auditStore = auditStore;
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

    public string EndpointText(int connectionId)
    {
        return _clientsById.TryGetValue(connectionId, out ClientLink link) ? link.Endpoint.ToString() : "";
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
        _auditStore?.RecordMessage("send", connectionId, link.Endpoint.ToString(), message, -1, -1);
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
            List<ClientLink> links = new List<ClientLink>(_clientsById.Values);
            for (int i = 0; i < links.Count; i++)
            {
                _auditStore?.RecordDisconnect(links[i].ConnectionId, links[i].Endpoint.ToString(), "shutdown");
            }
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
        _auditStore?.RecordConnect(id, stable.ToString());
        OnPlayerConnected?.Invoke(id);
        return link;
    }

    void RemoveClient(int connectionId, string reason)
    {
        if (!_clientsById.TryGetValue(connectionId, out ClientLink link))
        {
            return;
        }

        Console.WriteLine($"[MugenRelayServer] disconnected id={connectionId} reason={reason}");
        _auditStore?.RecordDisconnect(connectionId, link.Endpoint.ToString(), reason);
        _clientsById.Remove(connectionId);
        _clientsByEndpoint.Remove(link.Endpoint);
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
                return $"FindMatch request={m.RequestId} nick={Safe(m.Nickname)} team={Safe(m.TeamCsv)} hash={Safe(m.ContentHash)} version={Safe(m.ClientVersion)} platform={Safe(m.ClientPlatform)} device={Safe(m.ClientDeviceModel)} os={Safe(m.ClientOperatingSystem)}";
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
