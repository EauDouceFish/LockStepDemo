namespace Lockstep.Network
{
    public enum MsgType : byte
    {
        JoinRoom = 1,
        RoomReady = 2,
        FrameInput = 3,
        FrameAuth = 4,
        GameOver = 5,
    }

    public interface IMessage
    {
        MsgType Type { get; }
    }
}
