using System;

namespace Lockstep.Network
{
    public class LoopbackClientTransport : ITransport
    {
        readonly LoopbackHub _hub;
        public int MyPlayerId { get; }

        public event Action<int> OnPlayerConnected;
        public event Action<int> OnPlayerDisconnected;

        public LoopbackClientTransport(LoopbackHub hub)
        {
            _hub = hub;
            MyPlayerId = hub.Connect();
        }

        public void Activate()
        {
            OnPlayerConnected?.Invoke(MyPlayerId);
            _hub.NotifyConnected(MyPlayerId);
        }

        public void Disconnect()
        {
            _hub.Disconnect(MyPlayerId);
            OnPlayerDisconnected?.Invoke(MyPlayerId);
        }

        public void Send(int playerId, IMessage msg) => _hub.PostToServer(MyPlayerId, msg);

        public bool Poll(out int playerId, out IMessage msg)
        {
            playerId = -1;
            return _hub.PollClient(MyPlayerId, out msg);
        }
    }
}
