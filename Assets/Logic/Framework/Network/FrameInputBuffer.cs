using Lockstep.Input;

namespace Lockstep.Network
{
    public class FrameInputBuffer
    {
        public const int Capacity = 1024;
        const int Mask = Capacity - 1;

        readonly FrameInput[] _slots;
        readonly int[] _frames;
        readonly int _playerCount;

        public int HighestReceivedFrame { get; private set; } = -1;
        public int PlayerCount => _playerCount;

        public FrameInputBuffer(int playerCount)
        {
            _playerCount = playerCount;
            _slots = new FrameInput[Capacity * playerCount];
            _frames = new int[Capacity];
            for (int i = 0; i < Capacity; i++) _frames[i] = -1;
        }

        public void Push(int frame, FrameInput[] inputs)
        {
            int slot = frame & Mask;
            int baseIdx = slot * _playerCount;
            for (int i = 0; i < _playerCount; i++)
                _slots[baseIdx + i] = inputs[i];
            _frames[slot] = frame;
            if (frame > HighestReceivedFrame) HighestReceivedFrame = frame;
        }

        public bool TryGet(int frame, FrameInput[] outBuf)
        {
            int slot = frame & Mask;
            if (_frames[slot] != frame) return false;
            int baseIdx = slot * _playerCount;
            for (int i = 0; i < _playerCount; i++)
                outBuf[i] = _slots[baseIdx + i];
            return true;
        }

        public bool Has(int frame) => _frames[frame & Mask] == frame;
    }
}
