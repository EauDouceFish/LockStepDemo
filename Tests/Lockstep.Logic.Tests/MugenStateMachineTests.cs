using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Game;
using Lockstep.Game.Components;
using Lockstep.Game.Data;
using Lockstep.Game.Expr;
using Lockstep.Game.Systems;

namespace Lockstep.Tests
{
    /// <summary>
    /// T2.4/2.5/2.6 端到端：手搓 stand↔walk 角色，跑数据驱动状态机 + 物理，
    /// 验证状态切换时机、走路位移、确定性。证明"角色 = 数据、引擎一份代码"。
    /// </summary>
    [TestFixture]
    public sealed class MugenStateMachineTests
    {
        readonly ExpressionVM _vm = new ExpressionVM();

        StateController ChangeState(int value, string trigger)
        {
            return new StateController
            {
                Type = ControllerType.ChangeState,
                Trigger = _vm.Compile(trigger),
                Params = new Dictionary<string, IExpr> { ["value"] = _vm.Compile(value.ToString()) },
            };
        }

        StateController VelSetX(int velocityX, string trigger)
        {
            return new StateController
            {
                Type = ControllerType.VelSet,
                Trigger = _vm.Compile(trigger),
                Params = new Dictionary<string, IExpr> { ["x"] = _vm.Compile(velocityX.ToString()) },
            };
        }

        CharacterDef BuildCharacter()
        {
            StateDef stand = new StateDef
            {
                Id = 0,
                StateType = StateType.Stand,
                Physics = Physics.Stand,
                Ctrl = true,
                Controllers = new[]
                {
                    VelSetX(0, "Time = 0"),
                    ChangeState(20, "Time >= 2"),
                },
            };
            StateDef walk = new StateDef
            {
                Id = 20,
                StateType = StateType.Stand,
                Physics = Physics.Stand,
                Controllers = new[]
                {
                    VelSetX(1, "Time = 0"),
                    ChangeState(0, "Time >= 3"),
                },
            };
            return new CharacterDef
            {
                Id = 0,
                Name = "Test",
                States = new Dictionary<int, StateDef> { [0] = stand, [20] = walk },
                Anims = new Dictionary<int, AnimData>(),
                Const = new CharConstants(),
            };
        }

        World BuildWorld()
        {
            World world = new World();
            world.Init(1);
            world.GameData = new MugenGameData(new Dictionary<int, CharacterDef> { [0] = BuildCharacter() });

            Entity entity = world.CreateEntity();
            entity.Add(new MugenStateC
            {
                StateNo = 0,
                StateType = StateType.Stand,
                Physics = Physics.Stand,
                Ctrl = true,
            });
            entity.Add(new TransformC());
            entity.Add(new VelocityC());
            entity.Add(new CharacterRefC { CharacterId = 0 });
            entity.Add(new AnimC());
            entity.Add(new VarsC());

            world.RegisterSystem(new MugenStateMachineSystem());
            world.RegisterSystem(new MugenPhysicsSystem());
            return world;
        }

        static int StateNo(World world)
        {
            return world.Entities[0].Get<MugenStateC>().StateNo;
        }

        static int PosX(World world)
        {
            return world.Entities[0].Get<TransformC>().Pos.X.ToInt();
        }

        [Test]
        public void DataDrivenStateMachine_TransitionsAndMoves()
        {
            World world = BuildWorld();

            world.Tick();
            world.Tick();
            Assert.That(StateNo(world), Is.EqualTo(0), "前 2 帧 Time<2，应仍在 stand");
            Assert.That(PosX(world), Is.EqualTo(0), "stand 时 vel=0，不位移");

            world.Tick();
            Assert.That(StateNo(world), Is.EqualTo(20), "第 3 帧 Time>=2，切到 walk");
            Assert.That(PosX(world), Is.EqualTo(1), "walk 当帧 VelSet x=1，物理推进 1");

            world.Tick();
            world.Tick();
            world.Tick();
            Assert.That(StateNo(world), Is.EqualTo(0), "walk 跑满 Time>=3，切回 stand");
            Assert.That(PosX(world), Is.EqualTo(3), "共走 3 帧，位移 3");
            Assert.That(world.Entities[0].Get<VelocityC>().Vel.X.Raw, Is.EqualTo(Lockstep.Math.FFloat.Zero.Raw),
                "回到 stand 后 VelSet x=0 停下");
        }

        [Test]
        public void Simulation_IsDeterministic()
        {
            World first = BuildWorld();
            World second = BuildWorld();
            for (int i = 0; i < 20; i++)
            {
                first.Tick();
                second.Tick();
            }
            Assert.That(second.ComputeHash(), Is.EqualTo(first.ComputeHash()), "同输入同结果（确定性）");
        }
    }
}
