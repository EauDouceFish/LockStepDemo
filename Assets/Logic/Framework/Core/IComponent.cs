namespace Lockstep.Core
{
    /// <summary>
    /// 组件 = 纯数据。所有"会变的游戏状态"都必须装在组件里
    /// （System 必须无状态，否则回滚备份不到它）。
    /// </summary>
    public interface IComponent
    {
        /// <summary>
        /// 深拷贝。快照 / 回滚的基础契约。
        /// 新增组件必须实现它，否则编译不过 —— 这个强制是有意的。
        /// </summary>
        IComponent Clone();

        /// <summary>
        /// 把组件的全部"会变状态"按确定性顺序混入哈希。不同步检测 / 反作弊对账的基础契约。
        /// 只能混入整数 raw 值（定点数的 long raw、int、枚举底层值），禁止混入格式化字符串 ——
        /// 否则跨平台浮点格式差异会污染哈希。和 Clone 一样，新增组件必须实现，否则编译不过。
        /// </summary>
        void WriteHash(ref Hash64 hash);
    }
}
