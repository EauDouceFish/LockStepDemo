using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
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
        public void CustomState_UsesRegisteredOwnerTableAndRescalesRuntimeCoordinates()
        {
            MCharData ownerData = Data(320);
            MStateDef custom = new MStateDef { No = 1100 };
            custom.Controllers.Add(new VarSetController
            {
                Index = 7,
                Value = new Lockstep.Mugen.Expr.MugenExprCompiler().Compile("77"),
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
