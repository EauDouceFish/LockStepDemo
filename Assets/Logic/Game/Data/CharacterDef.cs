using System.Collections.Generic;

namespace Lockstep.Game.Data
{
    /// <summary>
    /// 一个角色的全部静态数据（一次导入，多实体共享，开局后只读 → 不入快照）。
    /// 引擎代码只有一份，角色全是这种数据 —— "加角色/加招式永远不写新 C#"。
    /// </summary>
    public sealed class CharacterDef
    {
        public int Id;
        public string Name;
        public Dictionary<int, StateDef> States;   // 键 = 状态号（含负数 -1/-2/-3）
        public Dictionary<int, AnimData> Anims;    // 键 = 动画号
        public CommandData[] Commands;
        public CharConstants Const;
    }
}
