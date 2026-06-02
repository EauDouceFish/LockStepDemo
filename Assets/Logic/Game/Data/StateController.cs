using System.Collections.Generic;
using Lockstep.Game.Expr;

namespace Lockstep.Game.Data
{
    /// <summary>
    /// 状态控制器（≈ MUGEN [State N, x]）= 类型 + 触发条件 + 参数。
    /// Trigger 是编译后的表达式（triggerall &amp;&amp; (trigger1 || trigger2 ...) 在导入期合成为一棵树）。
    /// Params 用编译后的表达式持有 —— MUGEN 里参数本身也可能是表达式（如 value = ifelse(...)）。
    /// Type==HitDef 时用强类型 Hit 字段（命中是热点）。
    /// </summary>
    public sealed class StateController
    {
        public ControllerType Type;
        public IExpr Trigger;
        public Dictionary<string, IExpr> Params;
        public HitDef Hit;
    }
}
