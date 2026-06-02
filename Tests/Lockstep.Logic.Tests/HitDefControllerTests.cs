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
    /// <summary>T3.2：HitDef 控制器——触发即激活 HitDefStateC；切状态即停用。</summary>
    [TestFixture]
    public sealed class HitDefControllerTests
    {
        readonly ExpressionVM _vm = new ExpressionVM();

        [Test]
        public void HitDefController_ActivatesHitDefState()
        {
            World world = new World();
            world.Init(1);
            Entity entity = world.CreateEntity();
            HitDefStateC hitState = new HitDefStateC { HitTargetsBits = 0xFF };   // 预置脏位，验证会清零
            entity.Add(hitState);

            HitDef hit = new HitDef { Damage = 20, PauseTimeDefender = 12 };
            StateController controller = new StateController
            {
                Type = ControllerType.HitDef,
                Trigger = _vm.Compile("1"),
                Hit = hit,
            };

            StateControllerExecutor.Execute(controller, entity, null);

            Assert.IsTrue(hitState.Active);
            Assert.That(hitState.Current, Is.SameAs(hit));
            Assert.That(hitState.HitTargetsBits, Is.EqualTo(0UL), "新 HitDef 应清零已命中目标");
        }

        StateController HitDefCtrl(string trigger)
        {
            return new StateController
            {
                Type = ControllerType.HitDef,
                Trigger = _vm.Compile(trigger),
                Hit = new HitDef { Damage = 20 },
            };
        }

        StateController ChangeState(int value, string trigger)
        {
            return new StateController
            {
                Type = ControllerType.ChangeState,
                Trigger = _vm.Compile(trigger),
                Params = new Dictionary<string, IExpr> { ["value"] = _vm.Compile(value.ToString()) },
            };
        }

        [Test]
        public void StateChange_DeactivatesHitDef()
        {
            StateDef attack = new StateDef
            {
                Id = 200,
                StateType = StateType.Stand,
                Physics = Physics.Stand,
                Controllers = new[]
                {
                    HitDefCtrl("Time = 0"),
                    ChangeState(0, "Time >= 1"),
                },
            };
            StateDef idle = new StateDef
            {
                Id = 0,
                StateType = StateType.Stand,
                Physics = Physics.Stand,
                Ctrl = true,
                Controllers = new StateController[0],
            };
            CharacterDef character = new CharacterDef
            {
                Id = 0,
                Name = "Tester",
                States = new Dictionary<int, StateDef> { [200] = attack, [0] = idle },
                Anims = new Dictionary<int, AnimData>(),
                Commands = new CommandData[0],
                Const = new CharConstants(),
            };

            World world = new World();
            world.Init(1);
            world.GameData = new MugenGameData(new Dictionary<int, CharacterDef> { [0] = character });
            Entity entity = world.CreateEntity();
            entity.Add(new MugenStateC { StateNo = 200, StateType = StateType.Stand, Physics = Physics.Stand });
            entity.Add(new TransformC());
            entity.Add(new VelocityC());
            entity.Add(new CharacterRefC { CharacterId = 0 });
            entity.Add(new AnimC());
            entity.Add(new VarsC());
            entity.Add(new HitDefStateC());
            world.RegisterSystem(new MugenStateMachineSystem());

            world.Tick();
            Assert.IsTrue(entity.Get<HitDefStateC>().Active, "攻击状态 Time=0 触发 HitDef，应激活");
            Assert.That(entity.Get<MugenStateC>().StateNo, Is.EqualTo(200));

            world.Tick();
            Assert.That(entity.Get<MugenStateC>().StateNo, Is.EqualTo(0), "Time>=1 切回 idle");
            Assert.IsFalse(entity.Get<HitDefStateC>().Active, "切状态应停用上一招 HitDef");
        }
    }
}
