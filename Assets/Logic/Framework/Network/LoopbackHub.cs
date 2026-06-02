using System.Collections.Generic;

namespace Lockstep.Network
{
    public class LoopbackHub
    {
        readonly Queue<ServerEntry> _serverInbox = new Queue<ServerEntry>();
        readonly Dictionary<int, Queue<IMessage>> _clientInboxes = new Dictionary<int, Queue<IMessage>>();
        LoopbackServerTransport _server;
        int _nextPlayerId;

        struct ServerEntry { public int SrcPlayerId; public IMessage Msg; }

        public void AttachServer(LoopbackServerTransport server) => _server = server;

        public int Connect()
        {
            int id = _nextPlayerId++;
            _clientInboxes[id] = new Queue<IMessage>();
            return id;
        }

        public void NotifyConnected(int playerId)
        {
            _server?.RaiseConnected(playerId);
        }

        public void Disconnect(int playerId)
        {
            if (!_clientInboxes.Remove(playerId)) return;
            _server?.RaiseDisconnected(playerId);
        }

        public void PostToServer(int srcPlayerId, IMessage msg)
        {
            _serverInbox.Enqueue(new ServerEntry { SrcPlayerId = srcPlayerId, Msg = msg });
        }

        public void PostToClient(int playerId, IMessage msg)
        {
            if (_clientInboxes.TryGetValue(playerId, out var q))
                q.Enqueue(msg);
        }

        public bool PollServer(out int srcPlayerId, out IMessage msg)
        {
            if (_serverInbox.Count == 0)
            {
                srcPlayerId = -1;
                msg = null;
                return false;
            }
            var e = _serverInbox.Dequeue();
            srcPlayerId = e.SrcPlayerId;
            msg = e.Msg;
            return true;
        }

        public bool PollClient(int playerId, out IMessage msg)
        {
            if (!_clientInboxes.TryGetValue(playerId, out var q) || q.Count == 0)
            {
                msg = null;
                return false;
            }
            msg = q.Dequeue();
            return true;
        }
    }
}
