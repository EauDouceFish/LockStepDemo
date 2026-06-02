using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Game.Data;

namespace Lockstep.Game
{
    /// <summary>
    /// 数据驱动引擎的只读游戏数据：角色定义表（开局后只读 → 不入快照）。
    /// 注入 World.GameData，供 StateMachine/Physics/Anim 系统按 CharacterId 查角色。
    /// </summary>
    public sealed class MugenGameData : IGameData
    {
        public readonly Dictionary<int, CharacterDef> Characters;

        public MugenGameData(Dictionary<int, CharacterDef> characters)
        {
            Characters = characters;
        }
    }
}
