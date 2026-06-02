using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using KcpProject;
using Lockstep.Network;
using UnityEngine;

namespace Lockstep.Client
{
    public class KcpClientTransport : ITransport
    {
        public const uint Conv = 0xFEEDFACE;

        public event Action<int> OnPlayerConnected;
        public event Action<int> OnPlayerDisconnected;

        Socket _socket;
        KCP _kcp;
        readonly Queue<IMessage> _inbox = new Queue<IMessage>();
        readonly byte[] _udpRecv = new byte[65536];
        readonly byte[] _kcpRecv = new byte[65536];
        bool _connected;

        public bool IsConnected => _connected;

        public void Connect(string host, int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Blocking = false;
            _socket.Connect(IPAddress.Parse(host), port);

            _kcp = new KCP(Conv, OnKcpOutput);
            _kcp.NoDelay(1, 10, 2, 1);
            _kcp.WndSize(128, 128);

            _connected = true;
            OnPlayerConnected?.Invoke(0);
        }

        public void Close()
        {
            if (!_connected) return;
            _connected = false;
            try { _socket?.Close(); } catch { }
            _socket = null;
            OnPlayerDisconnected?.Invoke(0);
        }

        void OnKcpOutput(byte[] data, int len)
        {
            if (_socket == null) return;
            try { _socket.Send(data, 0, len, SocketFlags.None); }
            catch (Exception ex) { Debug.LogError($"[KcpClient] send failed: {ex.Message}"); }
        }

        public void Send(int playerId, IMessage msg)
        {
            if (!_connected) return;
            var bytes = MessageCodec.Encode(msg);
            _kcp.Send(bytes, 0, bytes.Length);
        }

        public bool Poll(out int playerId, out IMessage msg)
        {
            if (_inbox.Count > 0)
            {
                playerId = -1;
                msg = _inbox.Dequeue();
                return true;
            }
            playerId = -1;
            msg = null;
            return false;
        }

        public void Update()
        {
            if (!_connected) return;

            while (_socket.Available > 0)
            {
                int n;
                try { n = _socket.Receive(_udpRecv); }
                catch (SocketException) { break; }
                if (n <= 0) break;
                _kcp.Input(_udpRecv, 0, n, true, true);
            }

            while (true)
            {
                int size = _kcp.PeekSize();
                if (size <= 0) break;
                if (size > _kcpRecv.Length) break;
                int got = _kcp.Recv(_kcpRecv, 0, size);
                if (got <= 0) break;
                var msg = MessageCodec.Decode(_kcpRecv, 0, got);
                if (msg != null) _inbox.Enqueue(msg);
            }

            _kcp.Update();
        }
    }
}
