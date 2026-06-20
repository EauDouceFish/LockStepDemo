// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go Target* StateController blocks (roughly bytecode.go:9382-9771).
// Tests fixed-point deterministic behavior for the MUGEN target controllers.
using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class TargetControllersTests
    {
        static readonly MugenExprCompiler Compiler = new MugenExprCompiler();

        static BytecodeExp Expression(string text)
        {
            return Compiler.Compile(text);
        }

        static FFloat Fixed(int value)
        {
            return FFloat.FromInt(value);
        }

        [Test]
        public void TargetState_SetsPendingStateOnSelectedTarget()
        {
            MChar target = new MChar { Id = 20, PendingStateNo = -1, Ctrl = true };
            MChar attacker = new MChar { Targets = new List<MChar> { target } };

            // oracle: bytecode.go:9563-9595 selects target and calls targetState(value).
            new TargetStateController { Value = Expression("5050") }.Run(attacker);

            Assert.That(target.PendingStateNo, Is.EqualTo(5050));
            Assert.IsFalse(target.Ctrl);
        }

        [Test]
        public void TargetState_FiltersByHitDefIdNotRuntimeEntityId()
        {
            MChar first = new MChar { Id = 20, PendingStateNo = -1, Ctrl = true };
            MChar second = new MChar { Id = 30, PendingStateNo = -1, Ctrl = true };
            MChar attacker = new MChar();
            attacker.AddTarget(first, 7);
            attacker.AddTarget(second, 9);

            new TargetStateController { Id = Expression("9"), Value = Expression("5050") }.Run(attacker);

            Assert.That(first.PendingStateNo, Is.EqualTo(-1));
            Assert.That(second.PendingStateNo, Is.EqualTo(5050));
        }

        [Test]
        public void TargetState_FromCustomStateKeepsCurrentStateOwner()
        {
            MCharData ownerData = new MCharData();
            MStateDef ownerTargetState = new MStateDef { No = 1200 };
            ownerTargetState.Controllers.Add(new VarSetController
            {
                Index = 8,
                Value = Expression("88"),
            });
            ownerData.States[1200] = ownerTargetState;

            MCharData controlledData = new MCharData();
            MCharData victimData = new MCharData();
            victimData.States[0] = new MStateDef { No = 0 };

            MPlayerResourceRegistry resources = new MPlayerResourceRegistry();
            int ownerPlayerNo = resources.Register(ownerData);
            int controlledPlayerNo = resources.Register(controlledData);
            int victimPlayerNo = resources.Register(victimData);
            MChar owner = new MChar
            {
                Resources = resources,
                OwnData = ownerData,
                PlayerNo = ownerPlayerNo,
                StatePlayerNo = ownerPlayerNo,
            };
            MChar controlled = new MChar
            {
                Resources = resources,
                OwnData = controlledData,
                PlayerNo = controlledPlayerNo,
                StatePlayerNo = ownerPlayerNo,
                StateOwner = owner,
            };
            MChar victim = new MChar
            {
                Resources = resources,
                OwnData = victimData,
                PlayerNo = victimPlayerNo,
                StatePlayerNo = victimPlayerNo,
                StateNo = 0,
            };
            controlled.Targets.Add(victim);

            new TargetStateController { Value = Expression("1200") }.Run(controlled);
            new MStateMachine().RunFrame(victim, victimData.States, victimData.CommonStates);

            Assert.That(victim.StateNo, Is.EqualTo(1200));
            Assert.That(victim.StatePlayerNo, Is.EqualTo(ownerPlayerNo));
            Assert.That(victim.StateOwner, Is.SameAs(owner), "stateowner redirect should point to the actual custom-state owner.");
            Assert.That(victim.IntVars[8], Is.EqualTo(88), "target should run the owner state table on the victim.");
        }

        [Test]
        public void TargetLifeAdd_ClampsAndHonorsKillFlag()
        {
            MChar target = new MChar { Id = 3, Life = 20, LifeMax = 100 };
            MChar attacker = new MChar { Targets = new List<MChar> { target } };

            // oracle: bytecode.go:9515-9560, kill=false prevents lethal target life add.
            new TargetLifeAddController
            {
                Value = Expression("0 - 50"),
                Kill = Expression("0"),
            }.Run(attacker);

            Assert.That(target.Life, Is.EqualTo(1));

            new TargetLifeAddController
            {
                Value = Expression("150"),
                Absolute = Expression("1"),
            }.Run(attacker);

            Assert.That(target.Life, Is.EqualTo(100));
        }

        [Test]
        public void TargetPowerAdd_ClampsPower()
        {
            MChar target = new MChar { Id = 4, Power = 2900, PowerMax = 3000 };
            MChar attacker = new MChar { Targets = new List<MChar> { target } };

            // oracle: bytecode.go:9708-9741 targetPowerAdd(value).
            new TargetPowerAddController { Value = Expression("250") }.Run(attacker);

            Assert.That(target.Power, Is.EqualTo(3000));
        }

        [Test]
        public void TargetVelSetAndAdd_ApplyTargetFacingToX()
        {
            MChar target = new MChar
            {
                Id = 5,
                Facing = -FFloat.One,
                Vel = new FVector3(Fixed(1), Fixed(2), Fixed(3)),
            };
            MChar attacker = new MChar { Targets = new List<MChar> { target } };

            // oracle: bytecode.go:9598-9705; C# velocity convention stores X before physics facing integration.
            new TargetVelSetController
            {
                X = Expression("4"),
                Y = Expression("5"),
                Z = Expression("6"),
            }.Run(attacker);

            Assert.That(target.Vel.X.Raw, Is.EqualTo(Fixed(-4).Raw));
            Assert.That(target.Vel.Y.Raw, Is.EqualTo(Fixed(5).Raw));
            Assert.That(target.Vel.Z.Raw, Is.EqualTo(Fixed(6).Raw));

            new TargetVelAddController
            {
                X = Expression("2"),
                Y = Expression("3"),
            }.Run(attacker);

            Assert.That(target.Vel.X.Raw, Is.EqualTo(Fixed(-6).Raw));
            Assert.That(target.Vel.Y.Raw, Is.EqualTo(Fixed(8).Raw));
        }

        [Test]
        public void TargetFacing_UsesAttackerFacingAndValueSign()
        {
            MChar target = new MChar { Id = 6, Facing = FFloat.One };
            MChar attacker = new MChar { Facing = -FFloat.One, Targets = new List<MChar> { target } };

            // oracle: char.go:8161-8171, value<0 flips attacker facing, value>0 copies it.
            new TargetFacingController { Value = Expression("1") }.Run(attacker);
            Assert.That(target.Facing.Raw, Is.EqualTo((-FFloat.One).Raw));

            new TargetFacingController { Value = Expression("0 - 1") }.Run(attacker);
            Assert.That(target.Facing.Raw, Is.EqualTo(FFloat.One.Raw));
        }

        [Test]
        public void TargetDrop_TrimsTargetsByExcludeId()
        {
            MChar first = new MChar { Id = 1 };
            MChar second = new MChar { Id = 2 };
            MChar attacker = new MChar { Targets = new List<MChar> { first, second } };

            // oracle: bytecode.go:9743-9771, excludeID keeps matching targets; -1 drops all in this local target-list model.
            new TargetDropController { ExcludeId = Expression("2"), KeepOne = Expression("0") }.Run(attacker);

            Assert.That(attacker.Targets.Count, Is.EqualTo(1));
            Assert.That(attacker.Targets[0].Id, Is.EqualTo(2));

            new TargetDropController().Run(attacker);
            Assert.That(attacker.Targets.Count, Is.EqualTo(0));
        }

        [Test]
        public void CnsParser_BuildsTargetControllers()
        {
            string text = @"
[Statedef 400]
[State 400, target state]
type = TargetState
trigger1 = 1
value = 5000

[State 400, target life]
type = TargetLifeAdd
trigger1 = 1
value = -40
kill = 0

[State 400, target power]
type = TargetPowerAdd
trigger1 = 1
value = 50

[State 400, target vel set]
type = TargetVelSet
trigger1 = 1
x = 2

[State 400, target vel add]
type = TargetVelAdd
trigger1 = 1
y = 3

[State 400, target facing]
type = TargetFacing
trigger1 = 1
value = -1

[State 400, target drop]
type = TargetDrop
trigger1 = 1
excludeID = 7
";

            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(text);
            IList<MStateController> controllers = states[400].Controllers;

            Assert.That(controllers[0], Is.TypeOf<TargetStateController>());
            Assert.That(controllers[1], Is.TypeOf<TargetLifeAddController>());
            Assert.That(controllers[2], Is.TypeOf<TargetPowerAddController>());
            Assert.That(controllers[3], Is.TypeOf<TargetVelSetController>());
            Assert.That(controllers[4], Is.TypeOf<TargetVelAddController>());
            Assert.That(controllers[5], Is.TypeOf<TargetFacingController>());
            Assert.That(controllers[6], Is.TypeOf<TargetDropController>());
        }

        [Test]
        public void HitDef_ParsesIdForTargetOwnership()
        {
            string text = @"
[Statedef 200]
[State 200, hit]
type = HitDef
trigger1 = 1
id = 77
attr = S, NA
damage = 10
";

            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(text);
            HitDefController controller = (HitDefController)states[200].Controllers[0];

            Assert.That(controller.Template.Id, Is.EqualTo(77));
        }
    }
}
