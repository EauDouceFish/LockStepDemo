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
        const int MaxSimulatedPacketsPerDirection = 512;
        const int MaxConsecutiveSendFailures = 6;
        const int MaxUdpDatagramsPerUpdate = 256;
        const int MaxKcpMessagesPerUpdate = 512;

        public event Action<int> OnPlayerConnected;
        public event Action<int> OnPlayerDisconnected;

        Socket _socket;
        KCP _kcp;
        readonly Queue<IMessage> _inbox = new Queue<IMessage>();
        readonly byte[] _udpRecv = new byte[65536];
        readonly byte[] _kcpRecv = new byte[65536];
        readonly List<SimDatagram> _simulatedOutgoing = new List<SimDatagram>(32);
        readonly List<SimDatagram> _simulatedIncoming = new List<SimDatagram>(32);
        readonly System.Random _simulationRandom = new System.Random(0x5EED);
        bool _connected;
        bool _networkSimulationEnabled;
        int _simulationDropPercent = 8;
        int _simulationMinDelayMs = 45;
        int _simulationJitterMs = 95;
        int _simulationBurstDropPercent = 2;
        float _simulationBurstDropUntil;
        int _consecutiveSendFailures;
        string _faultReason = "";

        public bool IsConnected => _connected;
        public int LatencyMs => _kcp != null ? _kcp.Srtt : -1;
        public bool NetworkSimulationEnabled => _networkSimulationEnabled;
        public int NetworkSimulationPendingPackets => _simulatedOutgoing.Count + _simulatedIncoming.Count;
        public bool Faulted => !string.IsNullOrEmpty(_faultReason);
        public string FaultReason => _faultReason;

        struct SimDatagram
        {
            public byte[] Bytes;
            public int Length;
            public float DueTime;
        }

        public void Connect(string host, int port)
        {
            Close();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Blocking = false;
            _socket.Connect(ResolveIPv4(host), port);

            _kcp = new KCP(Conv, OnKcpOutput);
            _kcp.NoDelay(1, 10, 2, 1);
            _kcp.WndSize(128, 128);

            _connected = true;
            _faultReason = "";
            _consecutiveSendFailures = 0;
            OnPlayerConnected?.Invoke(0);
        }

        static IPAddress ResolveIPv4(string host)
        {
            if (IPAddress.TryParse(host, out IPAddress parsed))
            {
                return parsed;
            }

            IPAddress[] addresses = Dns.GetHostAddresses(host);
            for (int i = 0; i < addresses.Length; i++)
            {
                if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    return addresses[i];
                }
            }

            throw new SocketException((int)SocketError.HostNotFound);
        }

        public void Close()
        {
            bool wasConnected = _connected;
            SetNetworkSimulation(false, flushPendingOnDisable: false);
            _connected = false;
            _faultReason = "";
            _consecutiveSendFailures = 0;
            _inbox.Clear();
            try { _socket?.Close(); } catch { }
            _socket = null;
            _kcp = null;
            if (wasConnected)
            {
                OnPlayerDisconnected?.Invoke(0);
            }
        }

        public void SetNetworkSimulation(bool enabled, int dropPercent = 8, int minDelayMs = 45,
            int jitterMs = 95, int burstDropPercent = 2, bool flushPendingOnDisable = true)
        {
            if (!enabled)
            {
                if (flushPendingOnDisable)
                {
                    FlushAllSimulatedNow();
                }
                else
                {
                    ClearSimulationQueues();
                }
                _networkSimulationEnabled = false;
                _simulationBurstDropUntil = 0f;
                return;
            }

            int clampedDropPercent = Mathf.Clamp(dropPercent, 0, 90);
            int clampedMinDelayMs = Mathf.Clamp(minDelayMs, 0, 2000);
            int clampedJitterMs = Mathf.Clamp(jitterMs, 0, 2000);
            int clampedBurstDropPercent = Mathf.Clamp(burstDropPercent, 0, 90);
            bool rescheduleQueuedPackets =
                _networkSimulationEnabled &&
                (_simulationMinDelayMs != clampedMinDelayMs || _simulationJitterMs != clampedJitterMs);

            _simulationDropPercent = clampedDropPercent;
            _simulationMinDelayMs = clampedMinDelayMs;
            _simulationJitterMs = clampedJitterMs;
            _simulationBurstDropPercent = clampedBurstDropPercent;
            _networkSimulationEnabled = true;
            if (rescheduleQueuedPackets)
            {
                RescheduleSimulationQueues(_simulationMinDelayMs + _simulationJitterMs);
            }
        }

        void OnKcpOutput(byte[] data, int len)
        {
            if (_socket == null) return;

            if (_networkSimulationEnabled)
            {
                if (ShouldDropSimulatedDatagram())
                {
                    return;
                }

                int delayMs = SimulatedDelayMs();
                if (delayMs > 0)
                {
                    if (!EnqueueSimulated(_simulatedOutgoing, data, len, delayMs))
                    {
                        MarkFault("weak network send queue overflow");
                    }
                    return;
                }
            }

            SendDatagram(data, len);
        }

        void SendDatagram(byte[] data, int len)
        {
            try
            {
                if (_socket == null)
                {
                    MarkFault("socket closed");
                    return;
                }
                _socket.Send(data, 0, len, SocketFlags.None);
                _consecutiveSendFailures = 0;
            }
            catch (Exception ex)
            {
                _consecutiveSendFailures++;
                Debug.LogError($"[KcpClient] send failed: {ex.Message}");
                if (_consecutiveSendFailures >= MaxConsecutiveSendFailures)
                {
                    MarkFault("send failed: " + ex.Message);
                }
            }
        }

        public void Send(int playerId, IMessage msg)
        {
            if (!_connected) return;
            var bytes = MessageCodec.Encode(msg);
            _kcp.Send(bytes, 0, bytes.Length);
        }

        public void Flush()
        {
            if (!_connected || _kcp == null) return;
            _kcp.Update();
            FlushDueOutgoing();
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

            FlushDueOutgoing();
            if (!_connected || _socket == null) return;

            int datagrams = 0;
            while (_connected && _socket != null && _socket.Available > 0 && datagrams < MaxUdpDatagramsPerUpdate)
            {
                int n;
                try { n = _socket.Receive(_udpRecv); }
                catch (SocketException) { break; }
                if (n <= 0) break;
                datagrams++;

                if (_networkSimulationEnabled)
                {
                    if (ShouldDropSimulatedDatagram())
                    {
                        continue;
                    }
                    int delayMs = SimulatedDelayMs();
                    if (delayMs > 0)
                    {
                        if (!EnqueueSimulated(_simulatedIncoming, _udpRecv, n, delayMs))
                        {
                            MarkFault("weak network receive queue overflow");
                            return;
                        }
                        continue;
                    }
                }

                _kcp.Input(_udpRecv, 0, n, true, true);
            }

            FlushDueIncoming();

            int messages = 0;
            while (messages < MaxKcpMessagesPerUpdate)
            {
                int size = _kcp.PeekSize();
                if (size <= 0) break;
                if (size > _kcpRecv.Length) break;
                int got = _kcp.Recv(_kcpRecv, 0, size);
                if (got <= 0) break;
                var msg = MessageCodec.Decode(_kcpRecv, 0, got);
                if (msg != null) _inbox.Enqueue(msg);
                messages++;
            }

            _kcp.Update();
            FlushDueOutgoing();
        }

        int SimulatedDelayMs()
        {
            return _simulationMinDelayMs + (_simulationJitterMs > 0 ? _simulationRandom.Next(_simulationJitterMs + 1) : 0);
        }

        bool ShouldDropSimulatedDatagram()
        {
            float now = Time.realtimeSinceStartup;
            if (_simulationBurstDropUntil > now)
            {
                return true;
            }

            if (_simulationBurstDropPercent > 0 && _simulationRandom.Next(100) < _simulationBurstDropPercent)
            {
                _simulationBurstDropUntil = now + 0.08f;
                return true;
            }

            return _simulationDropPercent > 0 && _simulationRandom.Next(100) < _simulationDropPercent;
        }

        static bool EnqueueSimulated(List<SimDatagram> queue, byte[] data, int len, int delayMs)
        {
            if (queue.Count >= MaxSimulatedPacketsPerDirection)
            {
                return false;
            }
            byte[] copy = new byte[len];
            Buffer.BlockCopy(data, 0, copy, 0, len);
            queue.Add(new SimDatagram
            {
                Bytes = copy,
                Length = len,
                DueTime = Time.realtimeSinceStartup + delayMs / 1000f,
            });
            return true;
        }

        void FlushDueOutgoing()
        {
            if (!_connected || _socket == null)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < _simulatedOutgoing.Count;)
            {
                SimDatagram datagram = _simulatedOutgoing[i];
                if (datagram.DueTime > now)
                {
                    i++;
                    continue;
                }
                SendDatagram(datagram.Bytes, datagram.Length);
                _simulatedOutgoing.RemoveAt(i);
                if (!_connected || _socket == null)
                {
                    return;
                }
            }
        }

        void FlushDueIncoming()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < _simulatedIncoming.Count;)
            {
                SimDatagram datagram = _simulatedIncoming[i];
                if (datagram.DueTime > now)
                {
                    i++;
                    continue;
                }
                _kcp.Input(datagram.Bytes, 0, datagram.Length, true, true);
                _simulatedIncoming.RemoveAt(i);
            }
        }

        void RescheduleSimulationQueues(int delayMs)
        {
            float dueTime = Time.realtimeSinceStartup + Mathf.Max(0, delayMs) / 1000f;
            RescheduleQueue(_simulatedOutgoing, dueTime);
            RescheduleQueue(_simulatedIncoming, dueTime);
        }

        static void RescheduleQueue(List<SimDatagram> queue, float dueTime)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                SimDatagram datagram = queue[i];
                datagram.DueTime = dueTime;
                queue[i] = datagram;
            }
        }

        void ClearSimulationQueues()
        {
            _simulatedOutgoing.Clear();
            _simulatedIncoming.Clear();
        }

        void FlushAllSimulatedNow()
        {
            if (!_connected || _socket == null || _kcp == null)
            {
                ClearSimulationQueues();
                return;
            }

            for (int i = 0; i < _simulatedOutgoing.Count; i++)
            {
                SimDatagram datagram = _simulatedOutgoing[i];
                SendDatagram(datagram.Bytes, datagram.Length);
                if (!_connected || _socket == null)
                {
                    ClearSimulationQueues();
                    return;
                }
            }
            _simulatedOutgoing.Clear();

            for (int i = 0; i < _simulatedIncoming.Count; i++)
            {
                SimDatagram datagram = _simulatedIncoming[i];
                _kcp.Input(datagram.Bytes, 0, datagram.Length, true, true);
            }
            _simulatedIncoming.Clear();
        }

        void MarkFault(string reason)
        {
            if (!string.IsNullOrEmpty(_faultReason))
            {
                return;
            }
            _faultReason = string.IsNullOrEmpty(reason) ? "network fault" : reason;
            _networkSimulationEnabled = false;
            ClearSimulationQueues();
            _connected = false;
            try { _socket?.Close(); } catch { }
            _socket = null;
            OnPlayerDisconnected?.Invoke(0);
        }
    }
}
