using System.Collections.Generic;
using Lockstep.Mugen.Anim;

namespace Lockstep.Mugen.Battle
{
    /// <summary>Match-scoped immutable character resource registry, keyed by stable player number.</summary>
    public sealed class MPlayerResourceRegistry
    {
        readonly List<MCharData> _players = new List<MCharData>();

        public int Count => _players.Count;

        // Project-specific: assigns stable C# player resource ids; Ikemen stores resources directly on Char/player slots.
        public int Register(MCharData data)
        {
            int playerNo = _players.Count;
            _players.Add(data);
            return playerNo;
        }

        // Project-specific: resolves immutable character data by C# player number for rollback and helper ownership.
        public MCharData Get(int playerNo)
        {
            return playerNo >= 0 && playerNo < _players.Count ? _players[playerNo] : null;
        }

        // Project-specific: exposes the C# animation table for Ikemen-style player-local animation lookup.
        public IReadOnlyDictionary<int, MAnimData> Animations(int playerNo)
        {
            return Get(playerNo)?.Anims;
        }

        // Ikemen reference: src/char.go localcoord scaling reads player definition coordinates, defaulting to 320.
        public int LocalCoordWidth(int playerNo)
        {
            int width = Get(playerNo)?.Definition?.LocalCoordWidth ?? 320;
            return width > 0 ? width : 320;
        }
    }
}
