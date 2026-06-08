namespace Lockstep.Mugen.Char
{
    /// <summary>Ikemen target list entry: runtime target plus the HitDef id that created the relationship.</summary>
    public struct MTargetRef
    {
        public MChar Target;
        public int HitDefId;
    }
}
