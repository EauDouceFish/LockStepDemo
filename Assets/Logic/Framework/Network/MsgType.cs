namespace Lockstep.Network
{
    public enum MsgType : byte
    {
        JoinRoom = 1,
        RoomReady = 2,
        FrameInput = 3,
        FrameAuth = 4,
        GameOver = 5,
        MugenInput = 6,   // MUGEN 引擎延迟锁步：单玩家单帧输入位（int），经中继 rebroadcast
        FindMatch = 20,
        CancelMatch = 21,
        MatchFound = 22,
        StartMatch = 23,
        LeaveRoom = 24,
        RoomClosed = 25,
        MugenHashReport = 26,
        Ping = 27,
        Pong = 28,
        LoadProgress = 29,
        ServerLog = 30,
        ClientTrace = 31,
        MugenNetStatus = 32,
    }

    public interface IMessage
    {
        MsgType Type { get; }
    }
}
