// 把延迟锁步输入通道架在通用 ITransport 上（KCP 跨进程 / 任意中继）。
// 发：本端输入 → MugenInputMsg → transport.Send(到服务器)；服务器把它 rebroadcast 给其余端。
// 收：transport.Poll 出 MugenInputMsg → 转成 (frame,playerId,input)。非输入消息忽略（本通道仅管对战帧输入）。
// 与 LoopbackNetBus 可互换：会话只认 IMugenNetChannel。KCP 具体 socket 实现在表现层（Scripts/Network/KcpClientTransport）。
using Lockstep.Mugen.Command;
using Lockstep.Network;

namespace Lockstep.Mugen.Battle.Net
{
    /// <summary>架在 <see cref="ITransport"/> 上的延迟锁步输入通道（KCP/中继跨进程用）。</summary>
    public sealed class TransportNetChannel : IMugenNetChannel
    {
        readonly ITransport _transport;
        readonly int _serverPlayerId;

        public TransportNetChannel(ITransport transport, int serverPlayerId = 0)
        {
            _transport = transport;
            _serverPlayerId = serverPlayerId;
        }

        public void SendInput(int frame, int playerId, MInput input)
        {
            _transport.Send(_serverPlayerId, new MugenInputMsg
            {
                Frame = frame,
                PlayerId = playerId,
                Input = (int)input,
            });
        }

        public bool TryReceiveInput(out int frame, out int playerId, out MInput input)
        {
            while (_transport.Poll(out int _, out IMessage msg))
            {
                if (msg is MugenInputMsg m)
                {
                    frame = m.Frame;
                    playerId = m.PlayerId;
                    input = (MInput)m.Input;
                    return true;
                }
                // 非输入消息（房间/结算等）不归本通道处理，跳过。
            }
            frame = 0;
            playerId = -1;
            input = MInput.None;
            return false;
        }
    }
}
