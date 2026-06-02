using Lockstep.Math;

namespace Lockstep.Game.Expr
{
    /// <summary>
    /// 表达式求值上下文：把 trigger 名解析成定点值。
    /// 运行期由包裹 World+Entity 的 context 提供（Time/Pos/Vel/Command...）；
    /// 测试期可用简化实现。trigger 名按 MUGEN 惯例大小写不敏感（由实现处理）。
    /// </summary>
    public interface IEvalContext
    {
        bool TryGetTrigger(string name, out FFloat value);
    }
}
