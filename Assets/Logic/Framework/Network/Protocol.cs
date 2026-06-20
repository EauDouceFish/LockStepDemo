using Lockstep.Input;

namespace Lockstep.Network
{
    public class JoinRoomMsg : IMessage
    {
        public MsgType Type => MsgType.JoinRoom;
        public string RoomId;
    }

    public class RoomReadyMsg : IMessage
    {
        public MsgType Type => MsgType.RoomReady;
        public int RoomId;
        public int RoomSeed;
        public int LocalPlayerId;
        public int PlayerCount;
    }

    public class FrameInputMsg : IMessage
    {
        public MsgType Type => MsgType.FrameInput;
        public int Frame;
        public int PlayerId;
        public FrameInput Input;
    }

    public class FrameAuthMsg : IMessage
    {
        public MsgType Type => MsgType.FrameAuth;
        public int Frame;
        public FrameInput[] Inputs;
    }

    public class GameOverMsg : IMessage
    {
        public MsgType Type => MsgType.GameOver;
        public int WinnerPlayerId;
    }

    /// <summary>MUGEN 引擎延迟锁步：某玩家某帧输入位（Input = (int)MInput，框架层不耦合 Mugen 类型）。</summary>
    public class MugenInputMsg : IMessage
    {
        public MsgType Type => MsgType.MugenInput;
        public int Frame;
        public int PlayerId;
        public int Input;
        public long TraceSeq;
        public long ClientUnixMs;
        public int ClientTickMs;
        public string ClientId;
        public string RunId;
    }

    public class MugenHashReportMsg : IMessage
    {
        public MsgType Type => MsgType.MugenHashReport;
        public int RoomId;
        public int Frame;
        public int PlayerId;
        public ulong Hash;
    }

    public class MugenNetStatusMsg : IMessage
    {
        public MsgType Type => MsgType.MugenNetStatus;
        public int RoomId;
        public int PlayerId;
        public int Frame;
        public int WeakDelayMs;
        public int LatencyMs;
    }

    public class FindMatchMsg : IMessage
    {
        public MsgType Type => MsgType.FindMatch;
        public int RequestId;
        public string Nickname;
        public string TeamCsv;
        public string ContentHash;
        public string ClientVersion;
        public string ClientInstanceId;
        public string ClientBuildVersion;
        public string ClientBuildGuid;
        public string ClientPlatform;
        public string ClientDeviceModel;
        public string ClientDeviceType;
        public string ClientOperatingSystem;
    }

    public class CancelMatchMsg : IMessage
    {
        public MsgType Type => MsgType.CancelMatch;
        public int RequestId;
    }

    public class MatchFoundMsg : IMessage
    {
        public MsgType Type => MsgType.MatchFound;
        public int RequestId;
        public int RoomId;
        public int LocalPlayerId;
        public int PlayerCount;
        public int Seed;
        public string Team0Csv;
        public string Team1Csv;
        public string OpponentName;
    }

    public class StartMatchMsg : IMessage
    {
        public MsgType Type => MsgType.StartMatch;
        public int RoomId;
        public int StartFrame;
    }

    public class LoadProgressMsg : IMessage
    {
        public MsgType Type => MsgType.LoadProgress;
        public int RoomId;
        public int PlayerId;
        public int ProgressPermille;
        public bool Ready;
    }

    public class LeaveRoomMsg : IMessage
    {
        public MsgType Type => MsgType.LeaveRoom;
        public int RoomId;
        public bool MatchCompleted;
    }

    public class RoomClosedMsg : IMessage
    {
        public MsgType Type => MsgType.RoomClosed;
        public int RequestId;
        public int RoomId;
        public string Reason;
    }

    public class PingMsg : IMessage
    {
        public MsgType Type => MsgType.Ping;
        public int Sequence;
        public int ClientTimeMs;
    }

    public class PongMsg : IMessage
    {
        public MsgType Type => MsgType.Pong;
        public int Sequence;
        public int ClientTimeMs;
        public int ServerTimeMs;
    }

    public class ServerLogMsg : IMessage
    {
        public MsgType Type => MsgType.ServerLog;
        public int ServerTimeMs;
        public string Message;
    }

    public class ClientTraceMsg : IMessage
    {
        public MsgType Type => MsgType.ClientTrace;
        public string RunId;
        public string ClientId;
        public int RoomId;
        public long Seq;
        public long ClientUnixMs;
        public int ClientTickMs;
        public int Frame;
        public int Input;
        public string Scene;
        public string EventName;
        public string Detail;
    }
}
