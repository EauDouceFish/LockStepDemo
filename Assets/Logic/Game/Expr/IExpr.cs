using Lockstep.Math;

namespace Lockstep.Game.Expr
{
    /// <summary>
    /// 编译后的表达式 —— 可反复对不同上下文求值。trigger 和"值参数"都用它。
    /// </summary>
    public interface IExpr
    {
        FFloat Eval(IEvalContext ctx);

        /// <summary>布尔求值，MUGEN 约定：非 0 即真。</summary>
        bool EvalBool(IEvalContext ctx);
    }

    /// <summary>
    /// 表达式 VM：导入期把 MUGEN 表达式字符串编译成 <see cref="IExpr"/>。
    /// 运行期只 Eval，不再 parse。实现必须确定性（全 FFloat，禁 float/随机）。
    /// </summary>
    public interface IExpressionVM
    {
        IExpr Compile(string expression);
    }
}
