using Lockstep.Input;
using Lockstep.Network;

namespace Lockstep.Predict
{
    public interface IPredictor
    {
        bool TryGetInputs(int frame, FrameInputBuffer buf, FrameInput[] outBuf);
    }
}
