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
}
