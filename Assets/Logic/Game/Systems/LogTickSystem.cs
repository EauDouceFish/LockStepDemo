using System;
using Lockstep.Core;

namespace Lockstep.Game.Systems
{
    public class LogTickSystem : ISystem
    {
        readonly Action<string> _log;

        public LogTickSystem(Action<string> log) { _log = log; }

        public void Tick(World world)
        {
            _log($"Frame {world.Frame}");
        }
    }
}
