// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go explod/modifyExplod/removeExplod/numexplod + src/char.go numExplod/removeExplod.
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class ExplodRuntimeTests
    {
        static readonly MugenExprCompiler Compiler = new MugenExprCompiler();

        static BytecodeExp Expression(string text)
        {
            return Compiler.Compile(text);
        }

        static MChar CharacterWithWorld()
        {
            MEntityWorld world = new MEntityWorld();
            MChar character = new MChar
            {
                Id = 7,
                World = world,
                Facing = FFloat.One,
            };
            return character;
        }

        [Test]
        public void Explod_CreatesLogicRecord_AndNumExplodCountsById()
        {
            MChar character = CharacterWithWorld();

            // oracle: char.go:5520 numExplod counts matching explods owned by the same player; id < 0 means all.
            new ExplodController
            {
                Id = Expression("30"),
                Anim = new BytecodeExp[] { Expression("100") },
                Position = new BytecodeExp[] { Expression("10"), Expression("20") },
                Velocity = new BytecodeExp[] { Expression("3"), Expression("-2") },
                RemoveTime = Expression("12"),
                BindTime = Expression("4"),
                SprPriority = Expression("5"),
            }.Run(character);
            new ExplodController
            {
                Id = Expression("40"),
                Anim = new BytecodeExp[] { Expression("101") },
            }.Run(character);

            BytecodeExp allCount = Compiler.Compile("numexplod");
            BytecodeExp idCount = Compiler.Compile("numexplod(30)");

            Assert.That(character.World.Explods.Count, Is.EqualTo(2));
            Assert.That(allCount.Run(character).ToI(), Is.EqualTo(2));
            Assert.That(idCount.Run(character).ToI(), Is.EqualTo(1));
            Assert.That(character.World.Explods[0].OwnerId, Is.EqualTo(7));
            Assert.That(character.World.Explods[0].ExplodId, Is.EqualTo(30));
            Assert.That(character.World.Explods[0].AnimNo, Is.EqualTo(100));
            Assert.That(character.World.Explods[0].Pos.X.Raw, Is.EqualTo(FFloat.FromInt(10).Raw));
            Assert.That(character.World.Explods[0].Vel.Y.Raw, Is.EqualTo(FFloat.FromInt(-2).Raw));
            Assert.That(character.World.Explods[0].RemoveTime, Is.EqualTo(12));
            Assert.That(character.World.Explods[0].BindTime, Is.EqualTo(4));
            Assert.That(character.World.Explods[0].SprPriority, Is.EqualTo(5));
        }

        [Test]
        public void ModifyAndRemoveExplod_FilterByIdAndIndex()
        {
            MChar character = CharacterWithWorld();

            new ExplodController { Id = Expression("30"), Anim = new BytecodeExp[] { Expression("100") } }.Run(character);
            new ExplodController { Id = Expression("30"), Anim = new BytecodeExp[] { Expression("101") } }.Run(character);
            new ExplodController { Id = Expression("40"), Anim = new BytecodeExp[] { Expression("102") } }.Run(character);

            // oracle: char.go:6836 getMultipleExplods index is among matches with the specified id.
            new ModifyExplodController
            {
                Id = Expression("30"),
                Index = Expression("1"),
                Position = new BytecodeExp[] { Expression("8"), Expression("9") },
                RemoveTime = Expression("20"),
            }.Run(character);

            Assert.That(character.World.Explods[0].RemoveTime, Is.Not.EqualTo(20));
            Assert.That(character.World.Explods[1].RemoveTime, Is.EqualTo(20));
            Assert.That(character.World.Explods[1].Pos.X.Raw, Is.EqualTo(FFloat.FromInt(8).Raw));

            // oracle: char.go:6976 removeExplod removes matching id and matching index immediately.
            new RemoveExplodController
            {
                Id = Expression("30"),
                Index = Expression("0"),
            }.Run(character);

            Assert.That(character.World.CountExplods(30, character.Id), Is.EqualTo(1));
            Assert.That(character.World.CountExplods(-1, character.Id), Is.EqualTo(2));
            Assert.That(character.World.Explods[0].AnimNo, Is.EqualTo(101));
        }

        [Test]
        public void Explod_WithIgnoreHitPauseFalse_FreezesLifecycleDuringOwnerHitPause()
        {
            MEntityWorld world = new MEntityWorld();
            MExplod explod = new MExplod
            {
                OwnerId = 7,
                Vel = new FVector3(FFloat.FromInt(3), FFloat.Zero, FFloat.Zero),
                RemoveTime = 2,
                IgnoreHitPause = false,
            };
            world.AddExplod(explod);

            world.StepExplods(ownerId => ownerId == 7);

            Assert.That(world.Explods.Count, Is.EqualTo(1));
            Assert.That(world.Explods[0].Pos.X.Raw, Is.EqualTo(FFloat.Zero.Raw));
            Assert.That(world.Explods[0].RemoveTime, Is.EqualTo(2));

            world.StepExplods(ownerId => false);

            Assert.That(world.Explods[0].Pos.X.Raw, Is.EqualTo(FFloat.FromInt(3).Raw));
            Assert.That(world.Explods[0].RemoveTime, Is.EqualTo(1));
        }
    }
}
