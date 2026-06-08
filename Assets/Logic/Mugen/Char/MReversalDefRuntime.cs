// Runtime ReversalDef container. It lives on MChar so rollback clone/hash can track active reversal state.
using Lockstep.Core;
using Lockstep.Mugen.Hit;

namespace Lockstep.Mugen.Char
{
    public sealed class MReversalDefRuntime
    {
        public bool Active;
        public int Attr;
        public int GuardFlag;
        public int GuardFlagNot;
        public MHitDef Template = new MHitDef();

        public MReversalDefRuntime Clone()
        {
            return new MReversalDefRuntime
            {
                Active = Active,
                Attr = Attr,
                GuardFlag = GuardFlag,
                GuardFlagNot = GuardFlagNot,
                Template = Template != null ? Template.Clone() : null,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddBool(Active);
            hash.AddInt32(Attr);
            hash.AddInt32(GuardFlag);
            hash.AddInt32(GuardFlagNot);
            if (Template != null)
            {
                Template.WriteHash(ref hash);
            }
            else
            {
                hash.AddInt32(0);
            }
        }
    }
}
