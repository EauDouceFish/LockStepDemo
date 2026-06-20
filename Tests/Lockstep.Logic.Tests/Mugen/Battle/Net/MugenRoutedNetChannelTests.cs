using System.Collections.Generic;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Mugen.Command;
using Lockstep.Network;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle.Net
{
    public sealed class MugenRoutedNetChannelTests
    {
        [Test]
        public void ControlMessagesSurviveInputDrain()
        {
            MemoryTransport transport = new MemoryTransport();
            MugenRoutedNetChannel channel = new MugenRoutedNetChannel(transport);
            transport.Enqueue(new StartMatchMsg { RoomId = 7, StartFrame = 0 });
            transport.Enqueue(new MugenInputMsg { Frame = 3, PlayerId = 1, Input = (int)MInput.A });

            Assert.That(channel.TryReceiveInput(out int frame, out int playerId, out MInput input), Is.True);
            Assert.That(frame, Is.EqualTo(3));
            Assert.That(playerId, Is.EqualTo(1));
            Assert.That(input, Is.EqualTo(MInput.A));

            Assert.That(channel.TryReceiveControl(out IMessage control), Is.True);
            Assert.That(control, Is.InstanceOf<StartMatchMsg>());
        }

        [Test]
        public void InputsSurviveControlDrain()
        {
            MemoryTransport transport = new MemoryTransport();
            MugenRoutedNetChannel channel = new MugenRoutedNetChannel(transport);
            transport.Enqueue(new MugenInputMsg { Frame = 4, PlayerId = 0, Input = (int)MInput.Right });
            transport.Enqueue(new RoomClosedMsg { RoomId = 9, Reason = "left" });

            Assert.That(channel.TryReceiveControl(out IMessage control), Is.True);
            Assert.That(control, Is.InstanceOf<RoomClosedMsg>());

            Assert.That(channel.TryReceiveInput(out int frame, out int playerId, out MInput input), Is.True);
            Assert.That(frame, Is.EqualTo(4));
            Assert.That(playerId, Is.EqualTo(0));
            Assert.That(input, Is.EqualTo(MInput.Right));
        }

        sealed class MemoryTransport : ITransport
        {
            readonly Queue<IMessage> _inbox = new Queue<IMessage>();

            public event System.Action<int> OnPlayerConnected { add { } remove { } }
            public event System.Action<int> OnPlayerDisconnected { add { } remove { } }

            public void Enqueue(IMessage message)
            {
                _inbox.Enqueue(message);
            }

            public void Send(int playerId, IMessage msg)
            {
            }

            public bool Poll(out int playerId, out IMessage msg)
            {
                playerId = -1;
                if (_inbox.Count == 0)
                {
                    msg = null;
                    return false;
                }

                msg = _inbox.Dequeue();
                return true;
            }
        }
    }
}
