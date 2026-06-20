using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using KcpProject;
using Lockstep.Client;
using Lockstep.Network;
using UnityEngine;

namespace Lockstep.Server
{
    public class KcpServerTransport : ITransport
    {
        const float ClientTimeoutSeconds = 20f;

        public event Action<int> OnPlayerConnected;
        public event Action<int> OnPlayerDisconnected;

        Socket _socket;
        readonly Dictionary<EndPoint, ClientLink> _clientsByEp = new Dictionary<EndPoint, ClientLink>();
        readonly Dictionary<int, ClientLink> _clientsByPid = new Dictionary<int, ClientLink>();
        readonly Queue<Pending> _inbox = new Queue<Pending>();
        readonly byte[] _udpRecv = new byte[65536];
        readonly byte[] _kcpRecv = new byte[65536];
        int _nextPlayerId;

        struct Pending { public int PlayerId; public IMessage Msg; }

        class ClientLink
        {
            public IPEndPoint Endpoint;
            public KCP Kcp;
            public int PlayerId;
            public float LastSeenTime;
        }

        public void Start(int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Blocking = false;
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));
            Debug.Log($"[KcpServer] listening on UDP {port}");
        }

        public void Close()
        {
            try { _socket?.Close(); } catch { }
            _socket = null;
            _clientsByEp.Clear();
            _clientsByPid.Clear();
        }

        public void Send(int playerId, IMessage msg)
        {
            if (!_clientsByPid.TryGetValue(playerId, out var link))
            {
                return;
            }

            var bytes = MessageCodec.Encode(msg);
            link.Kcp.Send(bytes, 0, bytes.Length);
        }

        public void Flush(int playerId)
        {
            if (_clientsByPid.TryGetValue(playerId, out var link))
            {
                link.Kcp.Update();
            }
        }

        public int Broadcast(IMessage msg)
        {
            int count = 0;
            foreach (var link in _clientsByPid.Values)
            {
                var bytes = MessageCodec.Encode(msg);
                link.Kcp.Send(bytes, 0, bytes.Length);
                link.Kcp.Update();
                count++;
            }
            return count;
        }

        public bool Poll(out int playerId, out IMessage msg)
        {
            if (_inbox.Count > 0)
            {
                var p = _inbox.Dequeue();
                playerId = p.PlayerId;
                msg = p.Msg;
                return true;
            }
            playerId = -1;
            msg = null;
            return false;
        }

        public void Update()
        {
            if (_socket == null)
            {
                return;
            }

            EndPoint anyEp = new IPEndPoint(IPAddress.Any, 0);
            while (_socket.Available > 0)
            {
                int n;
                EndPoint fromEp = anyEp;
                try
                {
                    n = _socket.ReceiveFrom(_udpRecv, ref fromEp); 
                }
                catch (SocketException) { break; }
                if (n <= 0)
                {
                    break;
                }

                var ip = (IPEndPoint)fromEp;
                var link = GetOrCreateClient(ip);
                link.LastSeenTime = Time.realtimeSinceStartup;
                link.Kcp.Input(_udpRecv, 0, n, true, true);
            }

            foreach (var link in _clientsByEp.Values)
            {
                while (true)
                {
                    int size = link.Kcp.PeekSize();
                    if (size <= 0) break;
                    if (size > _kcpRecv.Length) break;
                    int got = link.Kcp.Recv(_kcpRecv, 0, size);
                    if (got <= 0) break;
                    var msg = MessageCodec.Decode(_kcpRecv, 0, got);
                    if (msg != null) _inbox.Enqueue(new Pending { PlayerId = link.PlayerId, Msg = msg });
                }
                link.Kcp.Update();
            }
            RemoveIdleClients();
        }

        ClientLink GetOrCreateClient(IPEndPoint ep)
        {
            if (_clientsByEp.TryGetValue(ep, out var existing))
            {
                return existing;
            }

            var stable = new IPEndPoint(ep.Address, ep.Port);
            var link = new ClientLink
            {
                Endpoint = stable,
                PlayerId = _nextPlayerId++,
                LastSeenTime = Time.realtimeSinceStartup,
            };
            link.Kcp = new KCP(KcpClientTransport.Conv, (data, len) =>
            {
                try { _socket.SendTo(data, 0, len, SocketFlags.None, link.Endpoint); }
                catch (Exception ex) { Debug.LogError($"[KcpServer] send to {link.Endpoint}: {ex.Message}"); }
            });
            link.Kcp.NoDelay(1, 10, 2, 1);
            link.Kcp.WndSize(128, 128);

            _clientsByEp[stable] = link;
            _clientsByPid[link.PlayerId] = link;
            Debug.Log($"[KcpServer] new client {stable} -> playerId={link.PlayerId}");
            OnPlayerConnected?.Invoke(link.PlayerId);
            return link;
        }

        void RemoveIdleClients()
        {
            if (_clientsByPid.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            List<ClientLink> stale = null;
            foreach (var link in _clientsByPid.Values)
            {
                if (now - link.LastSeenTime < ClientTimeoutSeconds)
                {
                    continue;
                }
                if (stale == null) { stale = new List<ClientLink>(); }
                stale.Add(link);
            }

            if (stale == null)
            {
                return;
            }

            for (int i = 0; i < stale.Count; i++)
            {
                ClientLink link = stale[i];
                _clientsByPid.Remove(link.PlayerId);
                _clientsByEp.Remove(link.Endpoint);
                Debug.Log($"[KcpServer] client timeout {link.Endpoint} playerId={link.PlayerId}");
                OnPlayerDisconnected?.Invoke(link.PlayerId);
            }
        }
    }
}
