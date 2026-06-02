namespace Lockstep.Core
{
    /// <summary>
    /// 标识接口：World 持有的"游戏专属只读数据"（如招式表、buff 表、忍者档案）。
    /// 由 Game 层注入实现，Framework 不感知具体类型。
    ///
    /// Why：让 World 能在不引用 Game 层类型的前提下，承载战斗所需的查表数据。
    /// 这是依赖反转 —— Framework 定义槽位，Game 层负责填。
    /// </summary>
    public interface IGameData
    {
    }
}
