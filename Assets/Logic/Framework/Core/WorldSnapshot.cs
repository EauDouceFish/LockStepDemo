using System.Collections.Generic;

namespace Lockstep.Core
{
    /// <summary>
    /// World 在某一帧的完整状态备份。回滚 = World.Restore(某个 WorldSnapshot)。
    ///
    /// 不备份 Systems —— System 必须无状态，可变状态全在组件里。
    /// 不备份 CurrentInputs —— 那是每帧外部喂入的输入，回滚重算时会重新喂。
    /// 不备份 Config —— 开局后不变。
    /// </summary>
    public sealed class WorldSnapshot
    {
        public int Frame;
        public ulong RandomSeed;
        public int NextId;
        public List<Entity> Entities;
    }
}
