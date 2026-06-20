using System;
using System.Collections.Generic;
using Lockstep.Client;
using Lockstep.Network;

namespace Lockstep.View
{
    public sealed class MugenServerLatencyProbe : IDisposable
    {
        readonly Dictionary<int, int> _sentAtMs = new Dictionary<int, int>();
        readonly Action<int> _onLatencyMeasured;
        KcpClientTransport _client;
        int _sequence;
        int _nextPingAtMs;
        string _host;
        int _port;

        public int LastLatencyMs { get; private set; } = -1;
        public bool IsRunning => _client != null;

        public MugenServerLatencyProbe(Action<int> onLatencyMeasured)
        {
            _onLatencyMeasured = onLatencyMeasured;
        }

        public void Start(string host, int port)
        {
            Dispose();
            _host = host;
            _port = port;
            LastLatencyMs = -1;
            try
            {
                _client = new KcpClientTransport();
                _client.Connect(_host, _port);
                SendPing();
            }
            catch
            {
                Dispose();
            }
        }

        public void Update()
        {
            if (_client == null)
            {
                return;
            }

            try
            {
                _client.Update();
                while (_client.Poll(out int _, out IMessage message))
                {
                    if (message is PongMsg pong && _sentAtMs.TryGetValue(pong.Sequence, out int sentAt))
                    {
                        int now = Environment.TickCount;
                        int measured = System.Math.Max(1, unchecked(now - sentAt));
                        LastLatencyMs = measured;
                        _onLatencyMeasured?.Invoke(measured);
                        _sentAtMs.Remove(pong.Sequence);
                    }
                }

                int intervalMs = LastLatencyMs > 0 ? 3000 : 700;
                int nowMs = Environment.TickCount;
                if (unchecked(nowMs - _nextPingAtMs) >= 0)
                {
                    SendPing();
                    _nextPingAtMs = unchecked(nowMs + intervalMs);
                }
            }
            catch
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (_client != null)
            {
                try { _client.Close(); } catch { }
            }
            _client = null;
            _sentAtMs.Clear();
            _nextPingAtMs = 0;
        }

        void SendPing()
        {
            if (_client == null)
            {
                return;
            }

            int now = Environment.TickCount;
            int sequence = ++_sequence;
            _sentAtMs[sequence] = now;
            _client.Send(-1, new PingMsg
            {
                Sequence = sequence,
                ClientTimeMs = now,
            });
        }
    }
}
