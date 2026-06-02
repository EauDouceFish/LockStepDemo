using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Game.Data;
using Lockstep.Game.Expr;
using Lockstep.Math;

namespace Lockstep.Tests
{
    /// <summary>T2.1 下半：WorldEvalContext 从 World+Entity 组件解析真实 trigger。</summary>
    [TestFixture]
    public sealed class WorldEvalContextTests
    {
        readonly ExpressionVM _vm = new ExpressionVM();

        static (World, Entity) BuildWorldWithFighter()
        {
            World world = new World();
            world.Init(1);
            Entity entity = world.CreateEntity();
            entity.Add(new MugenStateC
            {
                StateNo = 200,
                Time = 3,
                Ctrl = true,
                StateType = StateType.Stand,
            });
            entity.Add(new TransformC { Pos = new FVector3(FFloat.FromInt(5), FFloat.FromInt(-2), FFloat.Zero), FacingX = FFloat.One });
            entity.Add(new VelocityC { Vel = new FVector3(FFloat.FromInt(1), FFloat.Zero, FFloat.Zero) });
            entity.Add(new HealthC { HP = 900, MaxHP = 1000 });
            entity.Add(new AnimC { AnimNo = 20, FrameIndex = 2, AnimTime = 4 });
            VarsC vars = new VarsC();
            vars.Var[2] = 7;
            entity.Add(vars);
            return (world, entity);
        }

        WorldEvalContext Bound()
        {
            (World world, Entity entity) = BuildWorldWithFighter();
            WorldEvalContext context = new WorldEvalContext();
            context.Bind(world, entity);
            return context;
        }

        [Test]
        public void ResolvesCoreTriggers()
        {
            WorldEvalContext context = Bound();
            Assert.That(_vm.Compile("Time").Eval(context).Raw, Is.EqualTo(FFloat.FromInt(3).Raw));
            Assert.That(_vm.Compile("StateNo").Eval(context).Raw, Is.EqualTo(FFloat.FromInt(200).Raw));
            Assert.That(_vm.Compile("Ctrl").Eval(context).Raw, Is.EqualTo(FFloat.One.Raw));
            Assert.That(_vm.Compile("PosX").Eval(context).Raw, Is.EqualTo(FFloat.FromInt(5).Raw));
            Assert.That(_vm.Compile("PosY").Eval(context).Raw, Is.EqualTo(FFloat.FromInt(-2).Raw));
            Assert.That(_vm.Compile("Anim").Eval(context).Raw, Is.EqualTo(FFloat.FromInt(20).Raw));
            Assert.That(_vm.Compile("Life").Eval(context).Raw, Is.EqualTo(FFloat.FromInt(900).Raw));
        }

        [Test]
        public void ResolvesVarFunction()
        {
            WorldEvalContext context = Bound();
            Assert.That(_vm.Compile("var(2)").Eval(context).Raw, Is.EqualTo(FFloat.FromInt(7).Raw));
        }

        [Test]
        public void CompoundConditionWorks()
        {
            WorldEvalContext context = Bound();
            Assert.IsTrue(_vm.Compile("Time >= 3 && StateNo = 200 && Ctrl").EvalBool(context));
            Assert.IsFalse(_vm.Compile("Time > 5 || PosX < 0").EvalBool(context));
        }
    }
}
