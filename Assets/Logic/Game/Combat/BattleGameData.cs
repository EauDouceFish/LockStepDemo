using Lockstep.Core;

namespace Lockstep.Game.Combat
{
    /// <summary>
    /// 战斗专属只读数据集合 —— 注入到 World.GameData。
    /// 当前只持有招式表；v2 会加：BuffTable / CharacterTable / SkillTable / ThrowTable 等。
    /// </summary>
    public sealed class BattleGameData : IGameData
    {
        public readonly IAttackTable AttackTable;

        public BattleGameData(IAttackTable attackTable)
        {
            AttackTable = attackTable;
        }
    }
}
