// 延迟锁步（delay lockstep）输入通道抽象。会话只依赖本接口，与具体传输解耦：
//   • 本机双开 / 单测 → LoopbackNetBus（即时广播）
//   • 跨进程 / 联机 → KCP 传输包装（序列化 frame+player+input，经中继服务器 rebroadcast）
// 模型：每端把"某逻辑帧的本地输入"广播给其余端；收齐该帧全部玩家输入才推进模拟（= Ikemen 锁步同源思路）。
using System.Collections.Generic;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle.Net
{
    /// <summary>延迟锁步输入通道：发本端某帧输入 + 收远端输入。实现负责传输/序列化/中继。</summary>
    public interface IMugenNetChannel
    {
        /// <summary>把本地玩家 <paramref name="playerId"/> 第 <paramref name="frame"/> 帧的输入广播出去。</summary>
        void SendInput(int frame, int playerId, MInput input);

        /// <summary>取一条收到的远端输入；无则返回 false。</summary>
        bool TryReceiveInput(out int frame, out int playerId, out MInput input);
    }

    /// <summary>
    /// 本机回环输入总线：建 N 个互通通道，任一通道发的输入即时投递给其余通道（广播模型，
    /// 等价于中继服务器把各端输入 rebroadcast）。供本机双开演示 + 确定性单测，无延迟无丢包。
    /// </summary>
    public sealed class LoopbackNetBus
    {
        struct Packet
        {
            public int Frame;
            public int PlayerId;
            public MInput Input;
        }

        readonly List<Queue<Packet>> _inboxes = new List<Queue<Packet>>();

        /// <summary>为某玩家建一个通道；它发的包进其余玩家收件箱，它收自己的收件箱。</summary>
        public IMugenNetChannel CreateChannel(int playerId)
        {
            while (_inboxes.Count <= playerId)
            {
                _inboxes.Add(new Queue<Packet>());
            }
            return new Channel(this, playerId);
        }

        void Broadcast(int senderId, Packet packet)
        {
            for (int i = 0; i < _inboxes.Count; i++)
            {
                if (i != senderId)
                {
                    _inboxes[i].Enqueue(packet);
                }
            }
        }

        bool Receive(int playerId, out Packet packet)
        {
            if (playerId >= 0 && playerId < _inboxes.Count && _inboxes[playerId].Count > 0)
            {
                packet = _inboxes[playerId].Dequeue();
                return true;
            }
            packet = default;
            return false;
        }

        sealed class Channel : IMugenNetChannel
        {
            readonly LoopbackNetBus _bus;
            readonly int _playerId;

            public Channel(LoopbackNetBus bus, int playerId)
            {
                _bus = bus;
                _playerId = playerId;
            }

            public void SendInput(int frame, int playerId, MInput input)
            {
                _bus.Broadcast(_playerId, new Packet { Frame = frame, PlayerId = playerId, Input = input });
            }

            public bool TryReceiveInput(out int frame, out int playerId, out MInput input)
            {
                if (_bus.Receive(_playerId, out Packet packet))
                {
                    frame = packet.Frame;
                    playerId = packet.PlayerId;
                    input = packet.Input;
                    return true;
                }
                frame = 0;
                playerId = -1;
                input = MInput.None;
                return false;
            }
        }
    }
}
