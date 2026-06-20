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
                    bw.Write(m.RoomId);
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
                case MugenInputMsg m:
                    bw.Write(m.Frame);
                    bw.Write(m.PlayerId);
                    bw.Write(m.Input);
                    bw.Write(m.TraceSeq);
                    bw.Write(m.ClientUnixMs);
                    bw.Write(m.ClientTickMs);
                    bw.Write(m.ClientId ?? string.Empty);
                    bw.Write(m.RunId ?? string.Empty);
                    break;
                case MugenHashReportMsg m:
                    bw.Write(m.RoomId);
                    bw.Write(m.Frame);
                    bw.Write(m.PlayerId);
                    bw.Write(m.Hash);
                    break;
                case MugenNetStatusMsg m:
                    bw.Write(m.RoomId);
                    bw.Write(m.PlayerId);
                    bw.Write(m.Frame);
                    bw.Write(m.WeakDelayMs);
                    bw.Write(m.LatencyMs);
                    break;
                case FindMatchMsg m:
                    bw.Write(m.RequestId);
                    bw.Write(m.Nickname ?? string.Empty);
                    bw.Write(m.TeamCsv ?? string.Empty);
                    bw.Write(m.ContentHash ?? string.Empty);
                    bw.Write(m.ClientVersion ?? string.Empty);
                    bw.Write(m.ClientInstanceId ?? string.Empty);
                    bw.Write(m.ClientBuildVersion ?? string.Empty);
                    bw.Write(m.ClientBuildGuid ?? string.Empty);
                    bw.Write(m.ClientPlatform ?? string.Empty);
                    bw.Write(m.ClientDeviceModel ?? string.Empty);
                    bw.Write(m.ClientDeviceType ?? string.Empty);
                    bw.Write(m.ClientOperatingSystem ?? string.Empty);
                    break;
                case CancelMatchMsg m:
                    bw.Write(m.RequestId);
                    break;
                case MatchFoundMsg m:
                    bw.Write(m.RequestId);
                    bw.Write(m.RoomId);
                    bw.Write(m.LocalPlayerId);
                    bw.Write(m.PlayerCount);
                    bw.Write(m.Seed);
                    bw.Write(m.Team0Csv ?? string.Empty);
                    bw.Write(m.Team1Csv ?? string.Empty);
                    bw.Write(m.OpponentName ?? string.Empty);
                    break;
                case StartMatchMsg m:
                    bw.Write(m.RoomId);
                    bw.Write(m.StartFrame);
                    break;
                case LoadProgressMsg m:
                    bw.Write(m.RoomId);
                    bw.Write(m.PlayerId);
                    bw.Write(m.ProgressPermille);
                    bw.Write(m.Ready);
                    break;
                case LeaveRoomMsg m:
                    bw.Write(m.RoomId);
                    bw.Write(m.MatchCompleted);
                    break;
                case RoomClosedMsg m:
                    bw.Write(m.RequestId);
                    bw.Write(m.RoomId);
                    bw.Write(m.Reason ?? string.Empty);
                    break;
                case PingMsg m:
                    bw.Write(m.Sequence);
                    bw.Write(m.ClientTimeMs);
                    break;
                case PongMsg m:
                    bw.Write(m.Sequence);
                    bw.Write(m.ClientTimeMs);
                    bw.Write(m.ServerTimeMs);
                    break;
                case ServerLogMsg m:
                    bw.Write(m.ServerTimeMs);
                    bw.Write(m.Message ?? string.Empty);
                    break;
                case ClientTraceMsg m:
                    bw.Write(m.RunId ?? string.Empty);
                    bw.Write(m.ClientId ?? string.Empty);
                    bw.Write(m.RoomId);
                    bw.Write(m.Seq);
                    bw.Write(m.ClientUnixMs);
                    bw.Write(m.ClientTickMs);
                    bw.Write(m.Frame);
                    bw.Write(m.Input);
                    bw.Write(m.Scene ?? string.Empty);
                    bw.Write(m.EventName ?? string.Empty);
                    bw.Write(m.Detail ?? string.Empty);
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
                        RoomId = br.ReadInt32(),
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
                case MsgType.MugenInput:
                {
                    MugenInputMsg message = new MugenInputMsg
                    {
                        Frame = br.ReadInt32(),
                        PlayerId = br.ReadInt32(),
                        Input = br.ReadInt32(),
                    };
                    if (HasRemaining(br, sizeof(long)))
                    {
                        message.TraceSeq = br.ReadInt64();
                    }
                    if (HasRemaining(br, sizeof(long)))
                    {
                        message.ClientUnixMs = br.ReadInt64();
                    }
                    if (HasRemaining(br, sizeof(int)))
                    {
                        message.ClientTickMs = br.ReadInt32();
                    }
                    if (HasRemaining(br))
                    {
                        message.ClientId = br.ReadString();
                    }
                    if (HasRemaining(br))
                    {
                        message.RunId = br.ReadString();
                    }
                    return message;
                }
                case MsgType.MugenHashReport:
                    return new MugenHashReportMsg
                    {
                        RoomId = br.ReadInt32(),
                        Frame = br.ReadInt32(),
                        PlayerId = br.ReadInt32(),
                        Hash = br.ReadUInt64(),
                    };
                case MsgType.MugenNetStatus:
                    return new MugenNetStatusMsg
                    {
                        RoomId = br.ReadInt32(),
                        PlayerId = br.ReadInt32(),
                        Frame = br.ReadInt32(),
                        WeakDelayMs = br.ReadInt32(),
                        LatencyMs = br.ReadInt32(),
                    };
                case MsgType.FindMatch:
                    return new FindMatchMsg
                    {
                        RequestId = br.ReadInt32(),
                        Nickname = br.ReadString(),
                        TeamCsv = br.ReadString(),
                        ContentHash = br.ReadString(),
                        ClientVersion = br.ReadString(),
                        ClientInstanceId = HasRemaining(br) ? br.ReadString() : string.Empty,
                        ClientBuildVersion = HasRemaining(br) ? br.ReadString() : string.Empty,
                        ClientBuildGuid = HasRemaining(br) ? br.ReadString() : string.Empty,
                        ClientPlatform = HasRemaining(br) ? br.ReadString() : string.Empty,
                        ClientDeviceModel = HasRemaining(br) ? br.ReadString() : string.Empty,
                        ClientDeviceType = HasRemaining(br) ? br.ReadString() : string.Empty,
                        ClientOperatingSystem = HasRemaining(br) ? br.ReadString() : string.Empty,
                    };
                case MsgType.CancelMatch:
                    return new CancelMatchMsg { RequestId = br.ReadInt32() };
                case MsgType.MatchFound:
                    return new MatchFoundMsg
                    {
                        RequestId = br.ReadInt32(),
                        RoomId = br.ReadInt32(),
                        LocalPlayerId = br.ReadInt32(),
                        PlayerCount = br.ReadInt32(),
                        Seed = br.ReadInt32(),
                        Team0Csv = br.ReadString(),
                        Team1Csv = br.ReadString(),
                        OpponentName = HasRemaining(br) ? br.ReadString() : string.Empty,
                    };
                case MsgType.StartMatch:
                    return new StartMatchMsg
                    {
                        RoomId = br.ReadInt32(),
                        StartFrame = br.ReadInt32(),
                    };
                case MsgType.LoadProgress:
                    return new LoadProgressMsg
                    {
                        RoomId = br.ReadInt32(),
                        PlayerId = br.ReadInt32(),
                        ProgressPermille = br.ReadInt32(),
                        Ready = br.ReadBoolean(),
                    };
                case MsgType.LeaveRoom:
                    return new LeaveRoomMsg
                    {
                        RoomId = br.ReadInt32(),
                        MatchCompleted = HasRemaining(br) && br.ReadBoolean(),
                    };
                case MsgType.RoomClosed:
                    return new RoomClosedMsg
                    {
                        RequestId = br.ReadInt32(),
                        RoomId = br.ReadInt32(),
                        Reason = br.ReadString(),
                    };
                case MsgType.Ping:
                    return new PingMsg
                    {
                        Sequence = br.ReadInt32(),
                        ClientTimeMs = br.ReadInt32(),
                    };
                case MsgType.Pong:
                    return new PongMsg
                    {
                        Sequence = br.ReadInt32(),
                        ClientTimeMs = br.ReadInt32(),
                        ServerTimeMs = br.ReadInt32(),
                    };
                case MsgType.ServerLog:
                    return new ServerLogMsg
                    {
                        ServerTimeMs = br.ReadInt32(),
                        Message = br.ReadString(),
                    };
                case MsgType.ClientTrace:
                    return new ClientTraceMsg
                    {
                        RunId = br.ReadString(),
                        ClientId = br.ReadString(),
                        RoomId = br.ReadInt32(),
                        Seq = br.ReadInt64(),
                        ClientUnixMs = br.ReadInt64(),
                        ClientTickMs = br.ReadInt32(),
                        Frame = br.ReadInt32(),
                        Input = br.ReadInt32(),
                        Scene = br.ReadString(),
                        EventName = br.ReadString(),
                        Detail = br.ReadString(),
                    };
            }
            return null;
        }

        static bool HasRemaining(BinaryReader br, int bytes = 1)
        {
            return br.BaseStream.Length - br.BaseStream.Position >= bytes;
        }
    }
}
