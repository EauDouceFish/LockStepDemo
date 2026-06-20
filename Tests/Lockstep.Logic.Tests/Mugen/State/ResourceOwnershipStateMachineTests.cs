using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.State
{
    [TestFixture]
    public sealed class ResourceOwnershipStateMachineTests
    {
        static FFloat F(int value) => FFloat.FromInt(value);
        static BytecodeExp Expr(string text) => new MugenExprCompiler().Compile(text);

        static ulong HashOf(MChar c)
        {
            Hash64 hash = Hash64.Create();
            c.WriteHash(ref hash);
            return hash.Value;
        }

        static MCharData Data(int localCoordWidth)
        {
            return new MCharData
            {
                Definition = new MCharacterDefinition
                {
                    LocalCoordWidth = localCoordWidth,
                    LocalCoordHeight = localCoordWidth * 3 / 4,
                },
            };
        }

        [Test]
        public void CustomState_ConstAndWidthUseCurrentStateOwnerLocalCoordScale()
        {
            MCharData ownerData = Data(320);
            MStateDef custom = new MStateDef { No = 1200 };
            custom.Controllers.Add(new VarSetController
            {
                Index = 9,
                IsFloat = true,
                Value = Expr("const(velocity.run.fwd.x)"),
            });
            custom.Controllers.Add(new WidthController
            {
                Value = new[] { Expr("const(size.ground.front)") },
            });
            ownerData.States[1200] = custom;

            MCharData selfData = Data(640);
            selfData.Constants = new MConstants
            {
                RunFwdX = F(8),
                SizeGroundFront = F(16),
                SizeGroundBack = F(12),
            };
            selfData.States[0] = new MStateDef { No = 0 };

            MPlayerResourceRegistry resources = new MPlayerResourceRegistry();
            int ownerPlayerNo = resources.Register(ownerData);
            int selfPlayerNo = resources.Register(selfData);
            MChar character = new MChar
            {
                Resources = resources,
                OwnData = selfData,
                Constants = selfData.Constants,
                PlayerNo = selfPlayerNo,
                StatePlayerNo = selfPlayerNo,
                StateNo = 0,
            };
            character.QueueTransition(1200, ownerPlayerNo);

            new MStateMachine().RunFrame(character, selfData.States, selfData.CommonStates);

            Assert.That(character.FloatVars[9].Raw, Is.EqualTo(F(4).Raw), "640 self constants must be read in the 320 state owner's coordinate space.");
            Assert.That(character.WidthPlayerFront.Raw, Is.EqualTo(F(16).Raw), "base front 16 and value 16 both scale to 8 in the 320 owner state.");
            Assert.That(character.WidthEdgeFront.Raw, Is.EqualTo(F(8).Raw));
            Assert.That(character.WidthPlayerBackSet, Is.False);
        }

        [Test]
        public void CustomState_StatedefAnimKeepsSelfAnimationOwner()
        {
            MCharData ownerData = Data(320);
            ownerData.States[1300] = new MStateDef { No = 1300, Anim = Expr("700") };

            MCharData selfData = Data(640);
            selfData.Anims[700] = new Lockstep.Mugen.Anim.MAnimData
            {
                No = 700,
                Frames = new[] { new Lockstep.Mugen.Anim.MAnimFrame { Time = 1 } },
            };
            selfData.States[0] = new MStateDef { No = 0 };

            MPlayerResourceRegistry resources = new MPlayerResourceRegistry();
            int ownerPlayerNo = resources.Register(ownerData);
            int selfPlayerNo = resources.Register(selfData);
            MChar character = new MChar
            {
                Resources = resources,
                OwnData = selfData,
                Constants = selfData.Constants,
                PlayerNo = selfPlayerNo,
                StatePlayerNo = selfPlayerNo,
                AnimPlayerNo = selfPlayerNo,
                SpritePlayerNo = selfPlayerNo,
                StateNo = 0,
            };
            character.QueueTransition(1300, ownerPlayerNo);

            new MStateMachine().RunFrame(character, selfData.States, selfData.CommonStates);

            Assert.That(character.AnimNo, Is.EqualTo(700));
            Assert.That(character.AnimPlayerNo, Is.EqualTo(selfPlayerNo), "Statedef anim is regular ChangeAnim semantics, not ChangeAnim2.");
            Assert.That(character.SpritePlayerNo, Is.EqualTo(selfPlayerNo));
        }

        [Test]
        public void CloneAndHash_PreserveResourceOwnerNumbers()
        {
            MPlayerResourceRegistry resources = new MPlayerResourceRegistry();
            int selfPlayerNo = resources.Register(Data(640));
            int ownerPlayerNo = resources.Register(Data(320));
            MChar character = new MChar
            {
                Resources = resources,
                PlayerNo = selfPlayerNo,
                StatePlayerNo = ownerPlayerNo,
                AnimPlayerNo = ownerPlayerNo,
                SpritePlayerNo = selfPlayerNo,
                StateNo = 1200,
                AnimNo = 900,
            };

            MChar clone = character.Clone();
            Assert.That(clone.Resources, Is.SameAs(resources));
            Assert.That(clone.PlayerNo, Is.EqualTo(selfPlayerNo));
            Assert.That(clone.StatePlayerNo, Is.EqualTo(ownerPlayerNo));
            Assert.That(clone.AnimPlayerNo, Is.EqualTo(ownerPlayerNo));
            Assert.That(clone.SpritePlayerNo, Is.EqualTo(selfPlayerNo));
            Assert.That(HashOf(clone), Is.EqualTo(HashOf(character)));

            clone.AnimPlayerNo = selfPlayerNo;
            Assert.That(HashOf(clone), Is.Not.EqualTo(HashOf(character)), "rollback hash must include animation owner changes.");
        }

        [Test]
        public void CustomState_UsesRegisteredOwnerTableAndRescalesRuntimeCoordinates()
        {
            MCharData ownerData = Data(320);
            MStateDef custom = new MStateDef { No = 1100 };
            custom.Controllers.Add(new VarSetController
            {
                Index = 7,
                Value = Expr("77"),
            });
            ownerData.States[1100] = custom;

            MCharData selfData = Data(640);
            selfData.States[0] = new MStateDef { No = 0 };

            MPlayerResourceRegistry resources = new MPlayerResourceRegistry();
            int ownerPlayerNo = resources.Register(ownerData);
            int selfPlayerNo = resources.Register(selfData);
            MChar character = new MChar
            {
                Resources = resources,
                OwnData = selfData,
                PlayerNo = selfPlayerNo,
                StatePlayerNo = selfPlayerNo,
                StateNo = 0,
                Pos = new FVector3(F(100), F(40), F(20)),
                OldPos = new FVector3(F(70), F(30), F(10)),
                Vel = new FVector3(F(12), F(8), F(4)),
                BindPos = new FVector3(F(6), F(4), F(2)),
                WidthPlayerFront = F(20),
                WidthPlayerBack = F(18),
                WidthEdgeFront = F(16),
                WidthEdgeBack = F(14),
            };
            character.Ghv.XVel = F(10);
            character.Ghv.YVel = F(8);
            character.Ghv.ZVel = F(6);
            character.Ghv.YAccel = F(4);
            character.Ghv.FallXVel = F(2);
            character.Ghv.FallYVel = F(12);
            character.QueueTransition(1100, ownerPlayerNo);

            new MStateMachine().RunFrame(character, selfData.States, selfData.CommonStates);

            Assert.That(character.StateNo, Is.EqualTo(1100));
            Assert.That(character.StatePlayerNo, Is.EqualTo(ownerPlayerNo));
            Assert.That(character.IntVars[7], Is.EqualTo(77), "foreign state must come from the registered owner table");
            Assert.That(character.Pos, Is.EqualTo(new FVector3(F(50), F(20), F(10))));
            Assert.That(character.OldPos, Is.EqualTo(character.Pos), "Ikemen resets oldPos to the rescaled current position");
            Assert.That(character.Vel, Is.EqualTo(new FVector3(F(6), F(4), F(2))));
            Assert.That(character.BindPos, Is.EqualTo(new FVector3(F(3), F(2), F(1))));
            Assert.That(character.WidthPlayerFront, Is.EqualTo(F(10)));
            Assert.That(character.WidthPlayerBack, Is.EqualTo(F(9)));
            Assert.That(character.WidthEdgeFront, Is.EqualTo(F(8)));
            Assert.That(character.WidthEdgeBack, Is.EqualTo(F(7)));
            Assert.That(character.Ghv.XVel, Is.EqualTo(F(5)));
            Assert.That(character.Ghv.YVel, Is.EqualTo(F(4)));
            Assert.That(character.Ghv.ZVel, Is.EqualTo(F(3)));
            Assert.That(character.Ghv.YAccel, Is.EqualTo(F(2)));
            Assert.That(character.Ghv.FallXVel, Is.EqualTo(F(1)));
            Assert.That(character.Ghv.FallYVel, Is.EqualTo(F(6)));
        }

        [Test]
        public void SelfState_RestoresOwnResourceOwnerAndCoordinateScale()
        {
            MCharData ownerData = Data(320);
            ownerData.States[1100] = new MStateDef { No = 1100 };
            MCharData selfData = Data(640);
            selfData.States[0] = new MStateDef { No = 0 };

            MPlayerResourceRegistry resources = new MPlayerResourceRegistry();
            int ownerPlayerNo = resources.Register(ownerData);
            int selfPlayerNo = resources.Register(selfData);
            MChar character = new MChar
            {
                Resources = resources,
                OwnData = selfData,
                PlayerNo = selfPlayerNo,
                StatePlayerNo = ownerPlayerNo,
                StateNo = 1100,
                Pos = new FVector3(F(50), F(20), F(10)),
                OldPos = new FVector3(F(40), F(10), F(5)),
                Vel = new FVector3(F(6), F(4), F(2)),
            };
            character.QueueTransition(0, selfPlayerNo);

            new MStateMachine().RunFrame(character, selfData.States, selfData.CommonStates);

            Assert.That(character.StateNo, Is.EqualTo(0));
            Assert.That(character.StatePlayerNo, Is.EqualTo(selfPlayerNo));
            Assert.That(character.Pos, Is.EqualTo(new FVector3(F(100), F(40), F(20))));
            Assert.That(character.OldPos, Is.EqualTo(character.Pos));
            Assert.That(character.Vel, Is.EqualTo(new FVector3(F(12), F(8), F(4))));
        }
    }
}
