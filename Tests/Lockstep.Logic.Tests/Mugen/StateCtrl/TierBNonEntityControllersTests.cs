using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class TierBNonEntityControllersTests
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
        public void VarRangeSet_SetsInclusiveIntegerRange_WithIkemenDefaults()
        {
            MChar character = new MChar();

            // oracle: bytecode.go:10907-10935, first defaults to 0 and last defaults to 59.
            new VarRangeSetController
            {
                Value = Expression("7"),
            }.Run(character);

            Assert.That(character.IntVars[0], Is.EqualTo(7));
            Assert.That(character.IntVars[59], Is.EqualTo(7));
            Assert.IsFalse(character.IntVars.ContainsKey(60));
        }

        [Test]
        public void VarRangeSet_SetsInclusiveFloatRange()
        {
            MChar character = new MChar();

            // oracle: bytecode.go:10907-10935, last is inclusive.
            new VarRangeSetController
            {
                First = Expression("2"),
                Last = Expression("4"),
                FloatValue = Expression("1.5"),
            }.Run(character);

            Assert.That(character.FloatVars[2].Raw, Is.EqualTo((Fixed(3) / Fixed(2)).Raw));
            Assert.That(character.FloatVars[3].Raw, Is.EqualTo((Fixed(3) / Fixed(2)).Raw));
            Assert.That(character.FloatVars[4].Raw, Is.EqualTo((Fixed(3) / Fixed(2)).Raw));
        }

        [Test]
        public void VarRandom_UsesInclusiveIkemenRangeAndSharedRng()
        {
            MChar character = new MChar
            {
                Rng = new MRandom(1),
            };

            // oracle: bytecode.go:11049-11077 + common.go Rand(min,max), range=a,b is inclusive.
            new VarRandomController
            {
                Index = Expression("3"),
                Range = new BytecodeExp[] { Expression("5"), Expression("10") },
            }.Run(character);

            Assert.That(character.IntVars[3], Is.EqualTo(5));
            Assert.That(character.Rng.Seed, Is.EqualTo(16807));
        }

        [Test]
        public void MoveHitReset_ClearsCurrentMoveContactFlags()
        {
            MChar character = new MChar
            {
                MoveContact = 1,
                MoveHit = 1,
                MoveGuarded = 1,
                MoveReversed = 1,
                MoveContactTime = 5,
                CounterHit = true,
            };

            // oracle: bytecode.go:11262-11280 calls clearMoveHit; C# mirrors exposed trigger fields too.
            new MoveHitResetController().Run(character);

            Assert.That(character.MoveContact, Is.EqualTo(0));
            Assert.That(character.MoveHit, Is.EqualTo(0));
            Assert.That(character.MoveGuarded, Is.EqualTo(0));
            Assert.That(character.MoveReversed, Is.EqualTo(0));
            Assert.That(character.MoveContactTime, Is.EqualTo(0));
            Assert.That(character.CounterHit, Is.False);
        }

        [Test]
        public void SuperPause_AppliesPowerAddButLeavesMissingPauseFieldsToMCharBatch()
        {
            MChar character = new MChar
            {
                Power = 100,
                PowerMax = 3000,
            };

            // oracle: bytecode.go:10204-10213, poweradd is applied immediately on crun.
            new SuperPauseController
            {
                PowerAdd = Expression("250"),
            }.Run(character);

            Assert.That(character.Power, Is.EqualTo(350));
        }

        [Test]
        public void SoundAndScreenEffectControllers_AreLogicNoOps()
        {
            MChar character = new MChar
            {
                Life = 700,
                Power = 100,
                Pos = new FVector3(Fixed(1), Fixed(2), Fixed(3)),
            };
            Hash64 before = Hash64.Create();
            character.WriteHash(ref before);

            new PlaySndController().Run(character);
            new StopSndController().Run(character);
            new SndPanController().Run(character);
            new ExplodController().Run(character);
            new ModifyExplodController().Run(character);
            new RemoveExplodController().Run(character);
            new MakeDustController().Run(character);
            new GameMakeAnimController().Run(character);
            new EnvShakeController().Run(character);
            new FallEnvShakeController().Run(character);
            new ForceFeedbackController().Run(character);
            new DisplayToClipboardController().Run(character);
            new VictoryQuoteController().Run(character);

            Hash64 after = Hash64.Create();
            character.WriteHash(ref after);
            Assert.That(after.Value, Is.EqualTo(before.Value));
        }

        [Test]
        public void VisualControllers_EmitPresentationEvents()
        {
            MEntityWorld world = new MEntityWorld();
            MChar character = new MChar { Id = 11, World = world };

            new PalFXController
            {
                PalFX = new PalFXParamSet
                {
                    Time = Expression("6"),
                    Color = Expression("128"),
                    Add = new[] { Expression("1"), Expression("2"), Expression("3") },
                },
            }.Run(character);
            new AfterImageController
            {
                AfterImage = new AfterImageParamSet
                {
                    Time = Expression("8"),
                    Length = Expression("12"),
                },
            }.Run(character);
            new EnvShakeController
            {
                Time = Expression("10"),
                Amplitude = Expression("4"),
            }.Run(character);

            Assert.That(world.Events.Visuals.Count, Is.EqualTo(3));
            Assert.That(world.Events.Visuals[0].Type, Is.EqualTo(MVisualEventType.PalFX));
            Assert.That(world.Events.Visuals[0].Time, Is.EqualTo(6));
            Assert.That(world.Events.Visuals[0].Value0, Is.EqualTo(128));
            Assert.That(world.Events.Visuals[1].Type, Is.EqualTo(MVisualEventType.AfterImage));
            Assert.That(world.Events.Visuals[1].Value0, Is.EqualTo(12));
            Assert.That(world.Events.Visuals[2].Type, Is.EqualTo(MVisualEventType.EnvShake));
            Assert.That(world.Events.Visuals[2].Value0, Is.EqualTo(4));
        }

        [Test]
        public void Explod_StepsAndExpiresByRemoveTime()
        {
            MEntityWorld world = new MEntityWorld();
            MExplod explod = new MExplod
            {
                Pos = new FVector3(Fixed(1), Fixed(2), FFloat.Zero),
                Vel = new FVector3(Fixed(3), FFloat.Zero, FFloat.Zero),
                Accel = new FVector3(Fixed(1), FFloat.Zero, FFloat.Zero),
                RemoveTime = 2,
            };
            world.AddExplod(explod);

            world.StepExplods();

            Assert.That(world.Explods.Count, Is.EqualTo(1));
            Assert.That(world.Explods[0].Pos.X.Raw, Is.EqualTo(Fixed(5).Raw));
            Assert.That(world.Explods[0].RemoveTime, Is.EqualTo(1));

            world.StepExplods();

            Assert.That(world.Explods.Count, Is.EqualTo(1));

            world.StepExplods();

            Assert.That(world.Explods.Count, Is.EqualTo(0));
        }

        [Test]
        public void CnsParser_BuildsEveryTierBNonEntityControllerName()
        {
            string text = @"
[Statedef 200]
[State 200, pause]
type = Pause
trigger1 = 1
time = 2

[State 200, superpause]
type = SuperPause
trigger1 = 1
poweradd = 3

[State 200, posfreeze]
type = PosFreeze
trigger1 = 1
value = 1

[State 200, width]
type = Width
trigger1 = 1
value = 10, 20

[State 200, playerpush]
type = PlayerPush
trigger1 = 1
value = 0

[State 200, screenbound]
type = ScreenBound
trigger1 = 1
value = 1

[State 200, attackdist]
type = AttackDist
trigger1 = 1
x = 30, 40

[State 200, hitoverride]
type = HitOverride
trigger1 = 1
attr = SCA, AA
stateno = 120

[State 200, movehitreset]
type = MoveHitReset
trigger1 = 1

[State 200, reversaldef]
type = ReversalDef
trigger1 = 1
attr = SCA, AA

[State 200, varrandom]
type = VarRandom
trigger1 = 1
v = 5
range = 1, 3

[State 200, varrangeset]
type = VarRangeSet
trigger1 = 1
first = 1
last = 2
value = 9

[State 200, remappal]
type = RemapPal
trigger1 = 1
source = 1, 2
dest = 1, 3

[State 200, trans]
type = Trans
trigger1 = 1
trans = addalpha

[State 200, sprpriority]
type = SprPriority
trigger1 = 1
value = 5

[State 200, offset]
type = Offset
trigger1 = 1
x = 1

[State 200, angledraw]
type = AngleDraw
trigger1 = 1
value = 10

[State 200, angleset]
type = AngleSet
trigger1 = 1
value = 10

[State 200, angleadd]
type = AngleAdd
trigger1 = 1
value = 5

[State 200, anglemul]
type = AngleMul
trigger1 = 1
value = 2

[State 200, afterimage]
type = AfterImage
trigger1 = 1
time = 8

[State 200, afterimagetime]
type = AfterImageTime
trigger1 = 1
time = 4

[State 200, palfx]
type = PalFX
trigger1 = 1
time = 6

[State 200, allpalfx]
type = AllPalFX
trigger1 = 1
time = 6

[State 200, bgpalfx]
type = BGPalFX
trigger1 = 1
time = 6

[State 200, envcolor]
type = EnvColor
trigger1 = 1
value = 1, 2, 3

[State 200, playsnd]
type = PlaySnd
trigger1 = 1
value = 0, 1

[State 200, stopsnd]
type = StopSnd
trigger1 = 1
channel = 0

[State 200, sndpan]
type = SndPan
trigger1 = 1
channel = 0

[State 200, explod]
type = Explod
trigger1 = 1
anim = 100

[State 200, modifyexplod]
type = ModifyExplod
trigger1 = 1
id = 100

[State 200, removeexplod]
type = RemoveExplod
trigger1 = 1
id = 100

[State 200, makedust]
type = MakeDust
trigger1 = 1
spacing = 3

[State 200, gamemakeanim]
type = GameMakeAnim
trigger1 = 1
value = 0, 1

[State 200, envshake]
type = EnvShake
trigger1 = 1
time = 8

[State 200, fallenvshake]
type = FallEnvShake
trigger1 = 1

[State 200, forcefeedback]
type = ForceFeedback
trigger1 = 1
time = 8

[State 200, displaytoclipboard]
type = DisplayToClipboard
trigger1 = 1
text = 1

[State 200, victoryquote]
type = VictoryQuote
trigger1 = 1
value = 2
";

            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(text);
            IList<MStateController> controllers = states[200].Controllers;

            Assert.That(controllers[0], Is.TypeOf<PauseController>());
            Assert.That(controllers[1], Is.TypeOf<SuperPauseController>());
            Assert.That(controllers[2], Is.TypeOf<PosFreezeController>());
            Assert.That(controllers[3], Is.TypeOf<WidthController>());
            Assert.That(controllers[4], Is.TypeOf<PlayerPushController>());
            Assert.That(controllers[5], Is.TypeOf<ScreenBoundController>());
            Assert.That(controllers[6], Is.TypeOf<AttackDistController>());
            Assert.That(controllers[7], Is.TypeOf<HitOverrideController>());
            Assert.That(controllers[8], Is.TypeOf<MoveHitResetController>());
            Assert.That(controllers[9], Is.TypeOf<ReversalDefController>());
            Assert.That(controllers[10], Is.TypeOf<VarRandomController>());
            Assert.That(controllers[11], Is.TypeOf<VarRangeSetController>());
            Assert.That(controllers[12], Is.TypeOf<RemapPalController>());
            Assert.That(controllers[13], Is.TypeOf<TransController>());
            Assert.That(controllers[14], Is.TypeOf<SprPriorityController>());
            Assert.That(controllers[15], Is.TypeOf<OffsetController>());
            Assert.That(controllers[16], Is.TypeOf<AngleDrawController>());
            Assert.That(controllers[17], Is.TypeOf<AngleSetController>());
            Assert.That(controllers[18], Is.TypeOf<AngleAddController>());
            Assert.That(controllers[19], Is.TypeOf<AngleMulController>());
            Assert.That(controllers[20], Is.TypeOf<AfterImageController>());
            Assert.That(controllers[21], Is.TypeOf<AfterImageTimeController>());
            Assert.That(controllers[22], Is.TypeOf<PalFXController>());
            Assert.That(controllers[23], Is.TypeOf<AllPalFXController>());
            Assert.That(controllers[24], Is.TypeOf<BGPalFXController>());
            Assert.That(controllers[25], Is.TypeOf<EnvColorController>());
            Assert.That(controllers[26], Is.TypeOf<PlaySndController>());
            Assert.That(controllers[27], Is.TypeOf<StopSndController>());
            Assert.That(controllers[28], Is.TypeOf<SndPanController>());
            Assert.That(controllers[29], Is.TypeOf<ExplodController>());
            Assert.That(controllers[30], Is.TypeOf<ModifyExplodController>());
            Assert.That(controllers[31], Is.TypeOf<RemoveExplodController>());
            Assert.That(controllers[32], Is.TypeOf<MakeDustController>());
            Assert.That(controllers[33], Is.TypeOf<GameMakeAnimController>());
            Assert.That(controllers[34], Is.TypeOf<EnvShakeController>());
            Assert.That(controllers[35], Is.TypeOf<FallEnvShakeController>());
            Assert.That(controllers[36], Is.TypeOf<ForceFeedbackController>());
            Assert.That(controllers[37], Is.TypeOf<DisplayToClipboardController>());
            Assert.That(controllers[38], Is.TypeOf<VictoryQuoteController>());
        }
    }
}
