namespace Lockstep.Input
{
    public class NullInputProvider : IInputProvider
    {
        public FrameInput Sample() => FrameInput.Empty;
    }
}
