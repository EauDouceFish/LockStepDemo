using System;

namespace Lockstep.Network
{
    public interface ITransport
    {
        void Send(int playerId, IMessage msg);
        bool Poll(out int playerId, out IMessage msg);
        event Action<int> OnPlayerConnected;
        event Action<int> OnPlayerDisconnected;
    }
}
