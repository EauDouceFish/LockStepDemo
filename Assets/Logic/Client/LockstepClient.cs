using System;
using Lockstep.Core;
using Lockstep.Input;
using Lockstep.Network;
using Lockstep.Predict;

namespace Lockstep.Client
{
    public class LockstepClient
    {
        public const int LogicTickMs = 33;
        public const int LogicTickHz = 30;
        public const int InputLag = 2;

        public ClientState State { get; private set; } = ClientState.Init;
        public int LocalPlayerId { get; private set; } = -1;
        public int PlayerCount { get; private set; }
        public int RoomSeed { get; private set; }
        public World World => _world;
        public int LogicFrame => _world == null ? -1 : _world.Frame;
        public event Action<ClientState, ClientState> OnStateChanged;

        /// <summary>
        /// 每模拟完一逻辑帧触发一次：(刚模拟完的帧号, 该帧 World 哈希)。
        /// 不同步检测器订阅它做双端对账。无订阅者时 ComputeHash 不会被调用（零开销）。
        /// </summary>
        public event Action<int, ulong> OnFrameSimulated;

        readonly ITransport _transport;
        readonly IInputProvider _inputProvider;
        readonly IPredictor _predictor;
        readonly GameConfigData _config;
        readonly IGameLogicFactory _gameLogic;
        string _pendingRoomId;
        World _world;
        FrameInputBuffer _buffer;
        FrameInput[] _inputBuf;
        long _accumulatorMs;
        long _lastTickMs;

        public LockstepClient(ITransport transport, IInputProvider inputProvider, IPredictor predictor,
            GameConfigData config, IGameLogicFactory gameLogic)
        {
            _transport = transport;
            _inputProvider = inputProvider;
            _predictor = predictor;
            _config = config;
            _gameLogic = gameLogic;
            _transport.OnPlayerConnected += OnConnected;
            _transport.OnPlayerDisconnected += OnDisconnected;
        }

        public void ConnectAndJoin(string roomId)
        {
            if (State != ClientState.Init) return;
            _pendingRoomId = roomId;
            SetState(ClientState.Connecting);
        }

        void SetState(ClientState s)
        {
            if (s == State) return;
            var old = State;
            State = s;
            OnStateChanged?.Invoke(old, s);
        }

        public void Tick(long nowMs)
        {
            while (_transport.Poll(out int _, out IMessage msg))
                HandleMessage(msg);

            switch (State)
            {
                case ClientState.Loading:
                    FinishLoading();
                    _lastTickMs = nowMs;
                    break;
                case ClientState.Playing:
                    long delta = nowMs - _lastTickMs;
                    _lastTickMs = nowMs;
                    _accumulatorMs += delta;
                    while (_accumulatorMs >= LogicTickMs)
                    {
                        _accumulatorMs -= LogicTickMs;
                        TryAdvance();
                    }
                    break;
            }
        }

        void OnConnected(int _)
        {
            if (State != ClientState.Connecting) return;
            SetState(ClientState.InRoom);
            _transport.Send(0, new JoinRoomMsg { RoomId = _pendingRoomId });
        }

        void OnDisconnected(int _)
        {
            SetState(ClientState.Finished);
        }

        void HandleMessage(IMessage msg)
        {
            switch (msg)
            {
                case RoomReadyMsg ready when State == ClientState.InRoom:
                    LocalPlayerId = ready.LocalPlayerId;
                    PlayerCount = ready.PlayerCount;
                    RoomSeed = ready.RoomSeed;
                    SetState(ClientState.Loading);
                    break;
                case FrameAuthMsg auth when _buffer != null:
                    _buffer.Push(auth.Frame, auth.Inputs);
                    break;
                case GameOverMsg _ when State == ClientState.Playing:
                    SetState(ClientState.Finished);
                    break;
            }
        }

        void FinishLoading()
        {
            _world = new World();
            _world.Init(RoomSeed);
            _world.Config = _config;
            _buffer = new FrameInputBuffer(PlayerCount);
            _inputBuf = new FrameInput[PlayerCount];
            _accumulatorMs = 0;

            _gameLogic?.Build(_world, PlayerCount);

            SetState(ClientState.Playing);
        }

        void TryAdvance()
        {
            int frame = _world.Frame;

            var local = _inputProvider.Sample();
            _transport.Send(0, new FrameInputMsg
            {
                Frame = frame + InputLag,
                PlayerId = LocalPlayerId,
                Input = local,
            });

            if (_predictor.TryGetInputs(frame, _buffer, _inputBuf))
            {
                _world.CurrentInputs = _inputBuf;
                _world.Tick();
                // ?. 短路：没有订阅者时 ComputeHash 不会被求值，生产模式零开销
                OnFrameSimulated?.Invoke(_world.Frame, _world.ComputeHash());
            }
        }
    }
}
