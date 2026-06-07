using System.Collections.Generic;
using Lockstep.Mugen.Anim;

namespace Lockstep.Mugen.Battle
{
    /// <summary>Match-scoped immutable character resource registry, keyed by stable player number.</summary>
    public sealed class MPlayerResourceRegistry
    {
        readonly List<MCharData> _players = new List<MCharData>();

        public int Count => _players.Count;

        public int Register(MCharData data)
        {
            int playerNo = _players.Count;
            _players.Add(data);
            return playerNo;
        }

        public MCharData Get(int playerNo)
        {
            return playerNo >= 0 && playerNo < _players.Count ? _players[playerNo] : null;
        }

        public IReadOnlyDictionary<int, MAnimData> Animations(int playerNo)
        {
            return Get(playerNo)?.Anims;
        }

        public int LocalCoordWidth(int playerNo)
        {
            int width = Get(playerNo)?.Definition?.LocalCoordWidth ?? 320;
            return width > 0 ? width : 320;
        }
    }
}
