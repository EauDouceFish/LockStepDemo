using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using KcpProject;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Network;

const uint KcpConv = 0xFEEDFACE;

string host = Arg(args, "--host", "127.0.0.1");
int port = int.Parse(Arg(args, "--port", "17777"));
int matches = int.Parse(Arg(args, "--matches", "20"));
int frames = int.Parse(Arg(args, "--frames", "20"));
int timeoutMs = int.Parse(Arg(args, "--timeout-ms", "5000"));
Random random = new Random(0x51A7);

Console.WriteLine($"[Stress] host={host} port={port} matches={matches} frames={frames}");
int totalInputs = 0;
for (int match = 0; match < matches; match++)
{
    using StressClient a = new StressClient(host, port, KcpConv, $"A{match}");
    using StressClient b = new StressClient(host, port, KcpConv, $"B{match}");
    int requestA = match * 2 + 1;
    int requestB = match * 2 + 2;
    a.Send(new FindMatchMsg
    {
        RequestId = requestA,
        Nickname = $"StressA{match}",
        TeamCsv = "a,b,c",
        ContentHash = "stress",
        ClientVersion = "stress-client",
    });
    b.Send(new FindMatchMsg
    {
        RequestId = requestB,
        Nickname = $"StressB{match}",
        TeamCsv = "d,e,f",
        ContentHash = "stress",
        ClientVersion = "stress-client",
    });

    MatchFoundMsg foundA = WaitFor<MatchFoundMsg>(a, b, timeoutMs, "match found A", _ => a.Take<MatchFoundMsg>());
    MatchFoundMsg foundB = WaitFor<MatchFoundMsg>(a, b, timeoutMs, "match found B", _ => b.Take<MatchFoundMsg>());
    if (foundA.RoomId != foundB.RoomId || foundA.LocalPlayerId != 0 || foundB.LocalPlayerId != 1)
    {
        throw new InvalidOperationException($"bad match result roomA={foundA.RoomId} roomB={foundB.RoomId} localA={foundA.LocalPlayerId} localB={foundB.LocalPlayerId}");
    }

    a.Send(Ready(foundA.RoomId, foundA.Seed, 0));
    b.Send(Ready(foundB.RoomId, foundB.Seed, 1));
    StartMatchMsg startA = WaitFor<StartMatchMsg>(a, b, timeoutMs, "start A", _ => a.Take<StartMatchMsg>());
    StartMatchMsg startB = WaitFor<StartMatchMsg>(a, b, timeoutMs, "start B", _ => b.Take<StartMatchMsg>());
    if (startA.RoomId != foundA.RoomId || startB.RoomId != foundA.RoomId)
    {
        throw new InvalidOperationException($"bad start result room={foundA.RoomId}");
    }

    for (int frame = 0; frame < frames; frame++)
    {
        int inputA = random.Next(0, 2048);
        int inputB = random.Next(0, 2048);
        a.Send(new MugenInputMsg { Frame = frame, PlayerId = 0, Input = inputA });
        b.Send(new MugenInputMsg { Frame = frame, PlayerId = 1, Input = inputB });

        MugenInputMsg toA = WaitFor<MugenInputMsg>(a, b, timeoutMs, "input to A", _ => a.Take<MugenInputMsg>());
        MugenInputMsg toB = WaitFor<MugenInputMsg>(a, b, timeoutMs, "input to B", _ => b.Take<MugenInputMsg>());
        if (toA.Frame != frame || toA.PlayerId != 1 || toA.Input != inputB)
        {
            throw new InvalidOperationException($"bad input relay to A frame={frame}");
        }
        if (toB.Frame != frame || toB.PlayerId != 0 || toB.Input != inputA)
        {
            throw new InvalidOperationException($"bad input relay to B frame={frame}");
        }
        totalInputs += 2;
    }

    if (match % 2 == 0)
    {
        a.Send(new LeaveRoomMsg { RoomId = foundA.RoomId });
        RoomClosedMsg closedA = WaitFor<RoomClosedMsg>(a, b, timeoutMs, "close A", _ => a.Take<RoomClosedMsg>());
        RoomClosedMsg closedB = WaitFor<RoomClosedMsg>(a, b, timeoutMs, "close B", _ => b.Take<RoomClosedMsg>());
        if (closedA.RoomId != foundA.RoomId || closedB.RoomId != foundA.RoomId)
        {
            throw new InvalidOperationException($"bad close room={foundA.RoomId}");
        }
    }
    else
    {
        a.Send(new CancelMatchMsg { RequestId = requestA });
        RoomClosedMsg closedA = WaitFor<RoomClosedMsg>(a, b, timeoutMs, "cancel close A", _ => a.Take<RoomClosedMsg>());
        RoomClosedMsg closedB = WaitFor<RoomClosedMsg>(a, b, timeoutMs, "cancel close B", _ => b.Take<RoomClosedMsg>());
        if (closedA.RoomId != foundA.RoomId || closedB.RoomId != foundA.RoomId)
        {
            throw new InvalidOperationException($"bad cancel close room={foundA.RoomId}");
        }
    }

    PumpFor(a, b, 50);
    Console.WriteLine($"[Stress] match {match + 1}/{matches} ok room={foundA.RoomId}");
}

Console.WriteLine($"[Stress] PASS matches={matches} relayedInputs={totalInputs}");

static RoomReadyMsg Ready(int roomId, int seed, int playerId)
{
    return new RoomReadyMsg
    {
        RoomId = roomId,
        RoomSeed = seed,
        LocalPlayerId = playerId,
        PlayerCount = 2,
    };
}

static T WaitFor<T>(StressClient a, StressClient b, int timeoutMs, string label, Func<int, T> take)
    where T : class, IMessage
{
    Stopwatch watch = Stopwatch.StartNew();
    while (watch.ElapsedMilliseconds < timeoutMs)
    {
        a.Update();
        b.Update();
        T value = take(0);
        if (value != null)
        {
            return value;
        }
        Thread.Sleep(2);
    }
    throw new TimeoutException("timed out waiting for " + label);
}

static void PumpFor(StressClient a, StressClient b, int ms)
{
    Stopwatch watch = Stopwatch.StartNew();
    while (watch.ElapsedMilliseconds < ms)
    {
        a.Update();
        b.Update();
        Thread.Sleep(2);
    }
}

static string Arg(string[] args, string name, string fallback)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == name)
        {
            return args[i + 1];
        }
    }
    return fallback;
}

sealed class StressClient : IDisposable
{
    readonly Socket _socket;
    readonly EndPoint _remote;
    readonly KCP _kcp;
    readonly byte[] _udpBuffer = new byte[65536];
    readonly byte[] _kcpBuffer = new byte[65536];
    readonly Queue<IMessage> _inbox = new Queue<IMessage>();

    public StressClient(string host, int port, uint conv, string name)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            Blocking = false,
        };
        _remote = new IPEndPoint(IPAddress.Parse(host), port);
        _socket.Connect(_remote);
        _kcp = new KCP(conv, (data, len) => _socket.Send(data, 0, len, SocketFlags.None));
        _kcp.NoDelay(1, 10, 2, 1);
        _kcp.WndSize(128, 128);
        Name = name;
    }

    public string Name { get; }

    public void Send(IMessage message)
    {
        byte[] bytes = MessageCodec.Encode(message);
        _kcp.Send(bytes, 0, bytes.Length);
        _kcp.Update();
    }

    public T Take<T>() where T : class, IMessage
    {
        int count = _inbox.Count;
        for (int i = 0; i < count; i++)
        {
            IMessage value = _inbox.Dequeue();
            if (value is T typed)
            {
                return typed;
            }
            _inbox.Enqueue(value);
        }
        return null;
    }

    public void Update()
    {
        while (_socket.Available > 0)
        {
            int count;
            try
            {
                count = _socket.Receive(_udpBuffer);
            }
            catch (SocketException)
            {
                break;
            }
            if (count <= 0)
            {
                break;
            }
            _kcp.Input(_udpBuffer, 0, count, true, true);
        }

        while (true)
        {
            int size = _kcp.PeekSize();
            if (size <= 0)
            {
                break;
            }
            int got = _kcp.Recv(_kcpBuffer, 0, size);
            if (got <= 0)
            {
                break;
            }
            IMessage message = MessageCodec.Decode(_kcpBuffer, 0, got);
            if (message != null)
            {
                _inbox.Enqueue(message);
            }
        }

        _kcp.Update();
    }

    public void Dispose()
    {
        try
        {
            _socket.Close();
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
