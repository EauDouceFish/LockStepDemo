// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go HitOverride container (c.ho[8]) + bytecode.go HitOverride StateController.
// Adapted to fixed-point. See Docs/移植方案_Ikemen.md.
using Lockstep.Core;

namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// HitOverride runtime slot. When an incoming hit attr matches an active slot, gethit routing can be
    /// replaced by StateNo, forced air/guard behavior, or KeepState.
    /// </summary>
    public struct MHitOverride
    {
        public int Attr;
        public int StateNo;
        public int Time;
        public bool ForceAir;
        public bool ForceGuard;
        public bool KeepState;

        public bool Active => Attr != 0 && Time != 0;

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(Attr);
            hash.AddInt32(StateNo);
            hash.AddInt32(Time);
            hash.AddBool(ForceAir);
            hash.AddBool(ForceGuard);
            hash.AddBool(KeepState);
        }
    }
}
