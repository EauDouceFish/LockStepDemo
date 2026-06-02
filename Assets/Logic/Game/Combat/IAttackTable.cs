namespace Lockstep.Game.Combat
{
    /// <summary>
    /// 招式表查询接口。
    ///   v1 实现：StaticAttackTable —— 招式数据写在 C# 里。
    ///   v2 实现：可换 SoAttackTable —— 招式数据来自 ScriptableObject + 自定义 Inspector，
    ///            策划在编辑器里改数，业务代码 0 改动。
    /// </summary>
    public interface IAttackTable
    {
        /// <summary>取一个招式定义；不存在时返回 null。</summary>
        MoveDef Get(MoveId id);

        bool TryGet(MoveId id, out MoveDef def);
    }
}
