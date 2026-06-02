using System.Collections.Generic;
using System.Diagnostics;
using Lockstep.Network;

namespace Lockstep.Server
{
    public class LockstepServer : IRoomSink
    {
        public const string DefaultRoomId = "main";

        readonly ITransport _transport;
        readonly Dictionary<string, Room> _rooms = new Dictionary<string, Room>();
        readonly Dictionary<int, Room> _playerToRoom = new Dictionary<int, Room>();
        readonly Stopwatch _clock = new Stopwatch();
        int _nextSeed = unchecked((int)0xC0FFEE01);

        public LockstepServer(ITransport transport)
        {
            _transport = transport;
            _transport.OnPlayerDisconnected += HandleDisconnected;
        }

        public void Start()
        {
            _clock.Start();
        }

        public void Tick()
        {
            long nowMs = _clock.ElapsedMilliseconds;

            while (_transport.Poll(out int playerId, out IMessage msg))
                HandleMessage(playerId, msg, nowMs);

            foreach (var room in _rooms.Values)
                room.Update(nowMs);
        }

        public void SendTo(int playerId, IMessage msg)
        {
            _transport.Send(playerId, msg);
        }

        void HandleMessage(int playerId, IMessage msg, long nowMs)
        {
            switch (msg)
            {
                case JoinRoomMsg join:
                    HandleJoin(playerId, join, nowMs);
                    break;
                case FrameInputMsg input:
                    if (_playerToRoom.TryGetValue(playerId, out var room))
                        room.OnInput(playerId, input.Frame, input.Input);
                    break;
            }
        }

        void HandleJoin(int playerId, JoinRoomMsg msg, long nowMs)
        {
            if (_playerToRoom.ContainsKey(playerId)) return;

            string roomId = string.IsNullOrEmpty(msg.RoomId) ? DefaultRoomId : msg.RoomId;
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                room = new Room(roomId, _nextSeed++, this);
                _rooms[roomId] = room;
            }
            if (room.PlayerCount >= Room.MaxPlayers) return;
            if (room.State != RoomState.WaitingPlayers) return;

            _playerToRoom[playerId] = room;
            room.OnJoin(playerId, nowMs);
        }

        void HandleDisconnected(int playerId)
        {
            if (!_playerToRoom.TryGetValue(playerId, out var room)) return;
            _playerToRoom.Remove(playerId);
            int winner = -1;
            room.EndGame(winner);
        }
    }
}
