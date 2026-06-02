using System.IO;
using Lockstep.Input;

namespace Lockstep.Network
{
    public static class MessageCodec
    {
        public static byte[] Encode(IMessage msg)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((byte)msg.Type);
            switch (msg)
            {
                case JoinRoomMsg m:
                    bw.Write(m.RoomId ?? string.Empty);
                    break;
                case RoomReadyMsg m:
                    bw.Write(m.RoomSeed);
                    bw.Write(m.LocalPlayerId);
                    bw.Write(m.PlayerCount);
                    break;
                case FrameInputMsg m:
                    bw.Write(m.Frame);
                    bw.Write(m.PlayerId);
                    bw.Write(m.Input.MoveX);
                    bw.Write(m.Input.MoveY);
                    bw.Write(m.Input.Buttons);
                    break;
                case FrameAuthMsg m:
                    bw.Write(m.Frame);
                    bw.Write(m.Inputs.Length);
                    for (int i = 0; i < m.Inputs.Length; i++)
                    {
                        bw.Write(m.Inputs[i].MoveX);
                        bw.Write(m.Inputs[i].MoveY);
                        bw.Write(m.Inputs[i].Buttons);
                    }
                    break;
                case GameOverMsg m:
                    bw.Write(m.WinnerPlayerId);
                    break;
            }
            return ms.ToArray();
        }

        public static IMessage Decode(byte[] data, int offset, int length)
        {
            using var ms = new MemoryStream(data, offset, length);
            using var br = new BinaryReader(ms);
            var type = (MsgType)br.ReadByte();
            switch (type)
            {
                case MsgType.JoinRoom:
                    return new JoinRoomMsg { RoomId = br.ReadString() };
                case MsgType.RoomReady:
                    return new RoomReadyMsg
                    {
                        RoomSeed = br.ReadInt32(),
                        LocalPlayerId = br.ReadInt32(),
                        PlayerCount = br.ReadInt32(),
                    };
                case MsgType.FrameInput:
                    return new FrameInputMsg
                    {
                        Frame = br.ReadInt32(),
                        PlayerId = br.ReadInt32(),
                        Input = new FrameInput
                        {
                            MoveX = br.ReadSByte(),
                            MoveY = br.ReadSByte(),
                            Buttons = br.ReadByte(),
                        },
                    };
                case MsgType.FrameAuth:
                {
                    int frame = br.ReadInt32();
                    int count = br.ReadInt32();
                    var inputs = new FrameInput[count];
                    for (int i = 0; i < count; i++)
                    {
                        inputs[i] = new FrameInput
                        {
                            MoveX = br.ReadSByte(),
                            MoveY = br.ReadSByte(),
                            Buttons = br.ReadByte(),
                        };
                    }
                    return new FrameAuthMsg { Frame = frame, Inputs = inputs };
                }
                case MsgType.GameOver:
                    return new GameOverMsg { WinnerPlayerId = br.ReadInt32() };
            }
            return null;
        }
    }
}
