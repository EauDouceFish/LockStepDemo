using System.Collections.Generic;
using Lockstep.Mugen.Char;

namespace Lockstep.Mugen.Battle
{
    /// <summary>
    /// Native battle snapshot for rollback. Static character data stays in MBattleEngine.Data/Resources;
    /// this object only owns mutable simulation state and stores object links for two-phase relinking.
    /// </summary>
    public sealed class MBattleEngineSnapshot
    {
        public int FrameNo;
        public int RandomSeed;
        public MPauseState Pause = new MPauseState();
        public int NextEntityId;
        public readonly List<MChar> Chars = new List<MChar>();
        public readonly List<MChar> Helpers = new List<MChar>();
        public readonly List<MProjectile> Projectiles = new List<MProjectile>();
        public readonly List<MExplod> Explods = new List<MExplod>();
        public readonly List<MHelperRequest> SpawnQueue = new List<MHelperRequest>();
        public readonly List<MProjectileRequest> ProjectileSpawnQueue = new List<MProjectileRequest>();
    }
}
