using System;
using System.Collections.Generic;
using Lockstep.Mugen.Command;
using Lockstep.Network;

namespace Lockstep.Mugen.Battle.Net
{
    public sealed class MugenRoutedNetChannel : IMugenNetChannel
    {
        readonly ITransport _transport;
        readonly Action<MugenInputMsg> _populateOutgoingInput;
        readonly Queue<IMessage> _controlMessages = new Queue<IMessage>();
        readonly Queue<MugenInputMsg> _inputMessages = new Queue<MugenInputMsg>();

        public MugenRoutedNetChannel(ITransport transport, Action<MugenInputMsg> populateOutgoingInput = null)
        {
            _transport = transport;
            _populateOutgoingInput = populateOutgoingInput;
        }

        public void SendInput(int frame, int playerId, MInput input)
        {
            MugenInputMsg message = new MugenInputMsg
            {
                Frame = frame,
                PlayerId = playerId,
                Input = (int)input,
            };
            _populateOutgoingInput?.Invoke(message);
            _transport.Send(-1, message);
        }

        public bool TryReceiveInput(out int frame, out int playerId, out MInput input)
        {
            if (_inputMessages.Count > 0)
            {
                MugenInputMsg queued = _inputMessages.Dequeue();
                frame = queued.Frame;
                playerId = queued.PlayerId;
                input = (MInput)queued.Input;
                return true;
            }

            while (_transport.Poll(out int _, out IMessage message))
            {
                if (message is MugenInputMsg inputMessage)
                {
                    frame = inputMessage.Frame;
                    playerId = inputMessage.PlayerId;
                    input = (MInput)inputMessage.Input;
                    return true;
                }
                _controlMessages.Enqueue(message);
            }

            frame = 0;
            playerId = 0;
            input = MInput.None;
            return false;
        }

        public bool TryReceiveControl(out IMessage message)
        {
            if (_controlMessages.Count > 0)
            {
                message = _controlMessages.Dequeue();
                return true;
            }

            while (_transport.Poll(out int _, out IMessage polled))
            {
                if (polled is MugenInputMsg inputMessage)
                {
                    _inputMessages.Enqueue(inputMessage);
                    continue;
                }
                message = polled;
                return true;
            }

            message = null;
            return false;
        }
    }
}
