using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Math;

namespace Lockstep.Game.Expr
{
    /// <summary>
    /// 运行期表达式上下文：把 MUGEN trigger 名解析成定点值，数据来自 World + 当前实体的组件。
    /// 全程读组件、无 float、无随机污染 → 确定性。用 Bind 复用同一实例（避免每帧分配）。
    /// 枚举型比较（StateType=A 等）由 CNS 导入器在导入期翻成数值，VM 不见枚举 token。
    /// </summary>
    public sealed class WorldEvalContext : IEvalContext
    {
        World _world;
        Entity _self;

        public void Bind(World world, Entity self)
        {
            _world = world;
            _self = self;
        }

        public bool TryGetTrigger(string name, out FFloat value)
        {
            value = FFloat.Zero;
            string lower = name.ToLowerInvariant();

            MugenStateC state = _self.Get<MugenStateC>();
            TransformC transform = _self.Get<TransformC>();
            VelocityC velocity = _self.Get<VelocityC>();
            AnimC anim = _self.Get<AnimC>();
            HealthC health = _self.Get<HealthC>();

            switch (lower)
            {
                case "time":
                    return FromState(state, ref value, FFloat.FromInt(state != null ? state.Time : 0));
                case "stateno":
                    return FromState(state, ref value, FFloat.FromInt(state != null ? state.StateNo : 0));
                case "prevstateno":
                    return FromState(state, ref value, FFloat.FromInt(state != null ? state.PrevStateNo : 0));
                case "ctrl":
                    return FromState(state, ref value, FFloat.FromInt(state != null && state.Ctrl ? 1 : 0));
                case "statetype":
                    return FromState(state, ref value, FFloat.FromInt(state != null ? (int)state.StateType : 0));
                case "movetype":
                    return FromState(state, ref value, FFloat.FromInt(state != null ? (int)state.MoveType : 0));
                case "physics":
                    return FromState(state, ref value, FFloat.FromInt(state != null ? (int)state.Physics : 0));
                case "hitshakeover":
                case "hitover":
                    value = FFloat.One;
                    return true;
            }

            if (transform != null)
            {
                switch (lower)
                {
                    case "pos x":
                    case "posx":
                        value = transform.Pos.X;
                        return true;
                    case "pos y":
                    case "posy":
                        value = transform.Pos.Y;
                        return true;
                    case "facing":
                        value = transform.FacingX;
                        return true;
                }
            }

            if (velocity != null)
            {
                switch (lower)
                {
                    case "vel x":
                    case "velx":
                        value = velocity.Vel.X;
                        return true;
                    case "vel y":
                    case "vely":
                        value = velocity.Vel.Y;
                        return true;
                }
            }

            if (anim != null)
            {
                switch (lower)
                {
                    case "anim":
                        value = FFloat.FromInt(anim.AnimNo);
                        return true;
                    case "animtime":
                        value = FFloat.FromInt(anim.AnimTime);
                        return true;
                    case "animelem":
                        value = FFloat.FromInt(anim.FrameIndex + 1);   // MUGEN AnimElem 从 1 起
                        return true;
                }
            }

            if (health != null)
            {
                switch (lower)
                {
                    case "life":
                        value = FFloat.FromInt(health.HP);
                        return true;
                    case "lifemax":
                        value = FFloat.FromInt(health.MaxHP);
                        return true;
                }
            }

            return false;
        }

        public bool TryGetFunction(string name, FFloat[] args, out FFloat value)
        {
            value = FFloat.Zero;
            VarsC vars = _self.Get<VarsC>();
            if (vars == null || args.Length != 1)
            {
                return false;
            }
            int index = args[0].ToInt();
            switch (name)
            {
                case "var":
                    if (index >= 0 && index < VarsC.IntCount)
                    {
                        value = FFloat.FromInt(vars.Var[index]);
                        return true;
                    }
                    return false;
                case "fvar":
                    if (index >= 0 && index < VarsC.FloatCount)
                    {
                        value = vars.FVar[index];
                        return true;
                    }
                    return false;
                case "sysvar":
                    if (index >= 0 && index < VarsC.SysIntCount)
                    {
                        value = FFloat.FromInt(vars.SysVar[index]);
                        return true;
                    }
                    return false;
            }
            return false;
        }

        static bool FromState(MugenStateC state, ref FFloat target, FFloat value)
        {
            if (state == null)
            {
                return false;
            }
            target = value;
            return true;
        }
    }
}
