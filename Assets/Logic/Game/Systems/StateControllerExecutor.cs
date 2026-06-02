using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Game.Data;
using Lockstep.Game.Expr;
using Lockstep.Math;

namespace Lockstep.Game.Systems
{
    /// <summary>
    /// 执行单个状态控制器（已通过 trigger 的）。无静态可变状态 —— 全部读写实体组件。
    /// 返回是否发生了状态切换（ChangeState/SelfState）：发生则调用方应停止本状态剩余控制器并切换。
    /// v1 子集：ChangeState/SelfState/ChangeAnim/VelSet/VelAdd/PosSet/PosAdd/CtrlSet/StateTypeSet/VarSet/VarAdd/Null。
    /// 其余类型（HitDef/Turn/Width/PlaySnd/AssertSpecial/Pause...）v1 暂为空操作，后续阶段补。
    /// </summary>
    public static class StateControllerExecutor
    {
        public static bool Execute(StateController controller, Entity entity, IEvalContext context)
        {
            switch (controller.Type)
            {
                case ControllerType.ChangeState:
                case ControllerType.SelfState:
                {
                    MugenStateC state = entity.Get<MugenStateC>();
                    if (state != null && HasParam(controller, "value"))
                    {
                        state.PendingStateNo = ParamInt(controller, "value", context, 0);
                        return true;
                    }
                    return false;
                }

                case ControllerType.ChangeAnim:
                {
                    AnimC anim = entity.Get<AnimC>();
                    if (anim != null && HasParam(controller, "value"))
                    {
                        anim.AnimNo = ParamInt(controller, "value", context, anim.AnimNo);
                        anim.FrameIndex = 0;
                        anim.ElemTime = 0;
                        anim.AnimTime = 0;
                    }
                    return false;
                }

                case ControllerType.VelSet:
                {
                    VelocityC velocity = entity.Get<VelocityC>();
                    if (velocity != null)
                    {
                        FFloat x = HasParam(controller, "x") ? ParamFixed(controller, "x", context, velocity.Vel.X) : velocity.Vel.X;
                        FFloat y = HasParam(controller, "y") ? ParamFixed(controller, "y", context, velocity.Vel.Y) : velocity.Vel.Y;
                        velocity.Vel = new FVector3(x, y, FFloat.Zero);
                    }
                    return false;
                }

                case ControllerType.VelAdd:
                {
                    VelocityC velocity = entity.Get<VelocityC>();
                    if (velocity != null)
                    {
                        FFloat x = velocity.Vel.X + (HasParam(controller, "x") ? ParamFixed(controller, "x", context, FFloat.Zero) : FFloat.Zero);
                        FFloat y = velocity.Vel.Y + (HasParam(controller, "y") ? ParamFixed(controller, "y", context, FFloat.Zero) : FFloat.Zero);
                        velocity.Vel = new FVector3(x, y, FFloat.Zero);
                    }
                    return false;
                }

                case ControllerType.PosSet:
                {
                    TransformC transform = entity.Get<TransformC>();
                    if (transform != null)
                    {
                        FFloat x = HasParam(controller, "x") ? ParamFixed(controller, "x", context, transform.Pos.X) : transform.Pos.X;
                        FFloat y = HasParam(controller, "y") ? ParamFixed(controller, "y", context, transform.Pos.Y) : transform.Pos.Y;
                        transform.Pos = new FVector3(x, y, FFloat.Zero);
                    }
                    return false;
                }

                case ControllerType.PosAdd:
                {
                    TransformC transform = entity.Get<TransformC>();
                    if (transform != null)
                    {
                        FFloat x = transform.Pos.X + (HasParam(controller, "x") ? ParamFixed(controller, "x", context, FFloat.Zero) : FFloat.Zero);
                        FFloat y = transform.Pos.Y + (HasParam(controller, "y") ? ParamFixed(controller, "y", context, FFloat.Zero) : FFloat.Zero);
                        transform.Pos = new FVector3(x, y, FFloat.Zero);
                    }
                    return false;
                }

                case ControllerType.CtrlSet:
                {
                    MugenStateC state = entity.Get<MugenStateC>();
                    if (state != null && HasParam(controller, "value"))
                    {
                        state.Ctrl = ParamInt(controller, "value", context, 0) != 0;
                    }
                    return false;
                }

                case ControllerType.StateTypeSet:
                {
                    MugenStateC state = entity.Get<MugenStateC>();
                    if (state != null)
                    {
                        if (HasParam(controller, "statetype"))
                        {
                            state.StateType = (StateType)ParamInt(controller, "statetype", context, (int)state.StateType);
                        }
                        if (HasParam(controller, "movetype"))
                        {
                            state.MoveType = (MoveType)ParamInt(controller, "movetype", context, (int)state.MoveType);
                        }
                        if (HasParam(controller, "physics"))
                        {
                            state.Physics = (Physics)ParamInt(controller, "physics", context, (int)state.Physics);
                        }
                    }
                    return false;
                }

                case ControllerType.VarSet:
                {
                    VarsC vars = entity.Get<VarsC>();
                    if (vars != null)
                    {
                        if (HasParam(controller, "v"))
                        {
                            int index = ParamInt(controller, "v", context, 0);
                            if (index >= 0 && index < VarsC.IntCount)
                            {
                                vars.Var[index] = ParamInt(controller, "value", context, 0);
                            }
                        }
                        else if (HasParam(controller, "fv"))
                        {
                            int index = ParamInt(controller, "fv", context, 0);
                            if (index >= 0 && index < VarsC.FloatCount)
                            {
                                vars.FVar[index] = ParamFixed(controller, "value", context, FFloat.Zero);
                            }
                        }
                    }
                    return false;
                }

                case ControllerType.VarAdd:
                {
                    VarsC vars = entity.Get<VarsC>();
                    if (vars != null && HasParam(controller, "v"))
                    {
                        int index = ParamInt(controller, "v", context, 0);
                        if (index >= 0 && index < VarsC.IntCount)
                        {
                            vars.Var[index] += ParamInt(controller, "value", context, 0);
                        }
                    }
                    return false;
                }

                default:
                    // Null / NotImplemented / 尚未支持的类型：空操作
                    return false;
            }
        }

        // ───────── 参数读取（编译后的表达式按需求值）─────────

        static bool HasParam(StateController controller, string key)
        {
            return controller.Params != null && controller.Params.ContainsKey(key);
        }

        static FFloat ParamFixed(StateController controller, string key, IEvalContext context, FFloat fallback)
        {
            if (controller.Params != null && controller.Params.TryGetValue(key, out IExpr expr) && expr != null)
            {
                return expr.Eval(context);
            }
            return fallback;
        }

        static int ParamInt(StateController controller, string key, IEvalContext context, int fallback)
        {
            if (controller.Params != null && controller.Params.TryGetValue(key, out IExpr expr) && expr != null)
            {
                return expr.Eval(context).ToInt();
            }
            return fallback;
        }
    }
}
