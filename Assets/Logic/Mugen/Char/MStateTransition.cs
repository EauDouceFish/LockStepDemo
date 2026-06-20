namespace Lockstep.Mugen.Char
{
    /// <summary>Atomic state change request, including the destination state bytecode owner.</summary>
    public struct MStateTransition
    {
        public bool Active;
        public int StateNo;
        public int OwnerPlayerNo;
        public int AnimNo;
        public int Ctrl;
        public bool InitPending;
        public bool ReturningToSelf;

        public static MStateTransition None => new MStateTransition
        {
            StateNo = -1,
            OwnerPlayerNo = -1,
            AnimNo = -1,
            Ctrl = -1,
            InitPending = false,
            ReturningToSelf = false,
        };
    }
}
