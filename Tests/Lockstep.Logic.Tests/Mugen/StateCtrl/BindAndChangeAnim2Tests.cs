// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go bindToParent/bindToRoot/bindToTarget/targetBind/changeAnim2 controllers.
using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class BindAndChangeAnim2Tests
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

        static Dictionary<int, MAnimData> AnimTableWith(params int[] numbers)
        {
            Dictionary<int, MAnimData> table = new Dictionary<int, MAnimData>();
            for (int index = 0; index < numbers.Length; index++)
            {
                table[numbers[index]] = new MAnimData
                {
                    Frames = new[] { new MAnimFrame { Time = 1 } },
                    TotalTime = 1,
                };
            }
            return table;
        }

        [Test]
        public void BindToParent_SavesBindStateAndAppliesPosition()
        {
            MChar parent = new MChar
            {
                Id = 1,
                Pos = new FVector3(Fixed(100), Fixed(20), FFloat.Zero),
                Facing = FFloat.One,
            };
            MChar helper = new MChar
            {
                Id = 2,
                Parent = parent,
                Root = parent,
                Pos = new FVector3(Fixed(0), Fixed(0), FFloat.Zero),
                Facing = FFloat.One,
            };

            // oracle: bytecode.go:11113 BindToParent defaults time=1 and stores pos offset relative to parent/root bind target.
            new BindToParentController
            {
                Time = Expression("3"),
                Position = new BytecodeExp[] { Expression("10"), Expression("-5") },
            }.Run(helper);
            helper.ApplyBind();

            Assert.That(ReferenceEquals(helper.BindTarget, parent), Is.True);
            Assert.That(helper.BindTime, Is.EqualTo(2));
            Assert.That(helper.Pos.X.Raw, Is.EqualTo(Fixed(110).Raw));
            Assert.That(helper.Pos.Y.Raw, Is.EqualTo(Fixed(15).Raw));
        }

        [Test]
        public void TargetBind_BindsSelectedTargetToAttacker()
        {
            MChar attacker = new MChar { Id = 10, Pos = new FVector3(Fixed(50), Fixed(0), FFloat.Zero), Facing = FFloat.One };
            MChar target = new MChar { Id = 20, Pos = new FVector3(Fixed(0), Fixed(0), FFloat.Zero), Facing = -FFloat.One };
            attacker.Targets.Add(target);

            // oracle: bytecode.go:9427 TargetBind selects target and calls char.go targetBind with attacker as bind target.
            new TargetBindController
            {
                Time = Expression("2"),
                Position = new BytecodeExp[] { Expression("-12"), Expression("6") },
            }.Run(attacker);
            target.ApplyBind();

            Assert.That(ReferenceEquals(target.BindTarget, attacker), Is.True);
            Assert.That(target.Pos.X.Raw, Is.EqualTo(Fixed(38).Raw));
            Assert.That(target.Pos.Y.Raw, Is.EqualTo(Fixed(6).Raw));
        }

        [Test]
        public void BindToTarget_BindsSelfToSelectedTarget()
        {
            MChar self = new MChar { Id = 10, Pos = new FVector3(Fixed(0), Fixed(0), FFloat.Zero), Facing = FFloat.One };
            MChar target = new MChar { Id = 20, Pos = new FVector3(Fixed(40), Fixed(5), FFloat.Zero), Facing = -FFloat.One };
            self.Targets.Add(target);

            // oracle: bytecode.go:9475 BindToTarget selects target, moves self relative to that target, and stores bind state.
            new BindToTargetController
            {
                Time = Expression("2"),
                Position = new BytecodeExp[] { Expression("7"), Expression("3") },
            }.Run(self);
            self.ApplyBind();

            Assert.That(ReferenceEquals(self.BindTarget, target), Is.True);
            Assert.That(self.Pos.X.Raw, Is.EqualTo(Fixed(33).Raw));
            Assert.That(self.Pos.Y.Raw, Is.EqualTo(Fixed(8).Raw));
        }

        [Test]
        public void ChangeAnim2_UsesStateOwnerAnimationTable()
        {
            MChar owner = new MChar { AnimTable = AnimTableWith(900) };
            MChar target = new MChar
            {
                AnimNo = 0,
                AnimTable = AnimTableWith(0),
                StateOwner = owner,
            };

            // oracle: bytecode.go:5478 ChangeAnim2 defaults anim player to state owner.
            new ChangeAnim2Controller { Value = Expression("900") }.Run(target);

            Assert.That(target.AnimNo, Is.EqualTo(900));
            Assert.That(target.AnimTable, Is.SameAs(owner.AnimTable));
        }

        [Test]
        public void Parser_BuildsBindAndChangeAnim2Controllers()
        {
            string text = @"
[Statedef 100]
[State 100, p]
type = BindToParent
trigger1 = 1
time = 2
pos = 1, 2

[State 100, r]
type = BindToRoot
trigger1 = 1
time = 2
pos = 1, 2

[State 100, t]
type = BindToTarget
trigger1 = 1
time = 2
pos = 1, 2

[State 100, tb]
type = TargetBind
trigger1 = 1
time = 2
pos = 1, 2

[State 100, ca2]
type = ChangeAnim2
trigger1 = 1
value = 900
";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(text);
            IList<MStateController> controllers = states[100].Controllers;

            Assert.That(controllers[0], Is.TypeOf<BindToParentController>());
            Assert.That(controllers[1], Is.TypeOf<BindToRootController>());
            Assert.That(controllers[2], Is.TypeOf<BindToTargetController>());
            Assert.That(controllers[3], Is.TypeOf<TargetBindController>());
            Assert.That(controllers[4], Is.TypeOf<ChangeAnim2Controller>());
        }
    }
}
