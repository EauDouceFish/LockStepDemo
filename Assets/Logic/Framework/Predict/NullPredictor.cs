using Lockstep.Input;
using Lockstep.Network;

namespace Lockstep.Predict
{
    public class NullPredictor : IPredictor
    {
        public bool TryGetInputs(int frame, FrameInputBuffer buf, FrameInput[] outBuf)
            => buf.TryGet(frame, outBuf);
    }
}
