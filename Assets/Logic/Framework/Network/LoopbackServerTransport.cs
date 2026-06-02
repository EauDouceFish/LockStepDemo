using System;

namespace Lockstep.Network
{
    public class LoopbackServerTransport : ITransport
    {
        readonly LoopbackHub _hub;

        public event Action<int> OnPlayerConnected;
        public event Action<int> OnPlayerDisconnected;

        public LoopbackServerTransport(LoopbackHub hub)
        {
            _hub = hub;
            hub.AttachServer(this);
        }

        public void Send(int playerId, IMessage msg) => _hub.PostToClient(playerId, msg);

        public bool Poll(out int playerId, out IMessage msg) => _hub.PollServer(out playerId, out msg);

        public void RaiseConnected(int playerId) => OnPlayerConnected?.Invoke(playerId);
        public void RaiseDisconnected(int playerId) => OnPlayerDisconnected?.Invoke(playerId);
    }
}
