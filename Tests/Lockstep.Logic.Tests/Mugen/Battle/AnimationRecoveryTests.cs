using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.State;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class AnimationRecoveryTests
    {
        [Test]
        public void Ananzi_WalkRelease_ReturnsToStateZeroAndIdleAnim()
        {
            string directory = TestAssets.CharDir("Ananzi");
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("Ananzi test character is missing.");
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            MBattleEngine engine = new MBattleEngine();
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();

            for (int i = 0; i < 12; i++)
            {
                engine.Tick(new[] { MInput.Right, MInput.None });
            }
            Assert.That(p1.StateNo, Is.EqualTo(20), "precondition: hardcoded walk should enter state 20.");

            for (int i = 0; i < 12; i++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
            }

            Assert.That(p1.StateNo, Is.EqualTo(0));
            Assert.That(p1.AnimNo, Is.EqualTo(0), "state 0 ChangeAnim must recover idle animation after walk release.");
        }

        [Test]
        public void Noroko_State210_OnHit_UsesChangeAnimElemAndRecovers()
        {
            string directory = TestAssets.CharDir("Noroko");
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("Noroko test character is missing.");
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            MBattleEngine engine = new MBattleEngine();
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();
            p1.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(-20), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p2.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(20), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p1.Facing = Lockstep.Math.FFloat.One;
            p2.Facing = -Lockstep.Math.FFloat.One;
            p1.QueueTransition(210, p1.PlayerNo);

            bool reachedLateElem = false;
            for (int i = 0; i < 180; i++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
                reachedLateElem |= p1.StateNo == 210 && p1.AnimNo == 210 && p1.AnimElemNo >= 4;
            }

            Assert.That(reachedLateElem, Is.True, "on contact ChangeAnim elem=4 should skip the repeated startup segment.");
            Assert.That(p1.StateNo, Is.EqualTo(0));
            Assert.That(p1.AnimNo, Is.EqualTo(0));
            Assert.That(p1.Ctrl, Is.True);
        }

        [Test]
        public void RealKfm_CrouchingLightPunchKeepsAuthoredRecoveryAndDoesNotRetriggerWhenHeld()
        {
            string directory = TestAssets.KfmDir();
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("KFM test character is missing.");
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            MBattleEngine engine = new MBattleEngine { EnableDemoAutoTurnFallback = false };
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();

            p1.QueueTransition(11, p1.PlayerNo);
            engine.Tick(new[] { MInput.None, MInput.None });
            Assert.That(p1.StateNo, Is.EqualTo(11), "precondition: KFM should be in crouch state 11.");
            Assert.That(p1.Ctrl, Is.True, "precondition: crouch idle should be controllable.");

            engine.Tick(new[] { MInput.Down | MInput.X, MInput.None });
            Assert.That(p1.StateNo, Is.EqualTo(400), "Down+X from crouch should enter KFM crouching light punch.");
            Assert.That(p1.Ctrl, Is.False, "KFM state 400 has ctrl=0 until its authored CtrlSet.");

            int framesIn400 = 1;
            int firstCtrlTime = -1;
            bool returnedToCrouch = false;
            for (int i = 0; i < 40; i++)
            {
                engine.Tick(new[] { MInput.Down | MInput.X, MInput.None });
                if (p1.StateNo == 400)
                {
                    framesIn400++;
                    if (firstCtrlTime < 0 && p1.Ctrl)
                    {
                        firstCtrlTime = p1.Time;
                    }
                    continue;
                }

                Assert.That(p1.StateNo, Is.EqualTo(11), "KFM state 400 should recover to crouch state 11.");
                returnedToCrouch = true;
                break;
            }

            Assert.That(returnedToCrouch, Is.True, "KFM state 400 should finish within the test window.");
            Assert.That(framesIn400, Is.GreaterThanOrEqualTo(10),
                "KFM crouching light punch should keep its authored recovery instead of ending immediately.");
            Assert.That(firstCtrlTime, Is.GreaterThanOrEqualTo(6),
                "KFM state 400 CtrlSet is authored at Time=6.");

            for (int i = 0; i < 4; i++)
            {
                engine.Tick(new[] { MInput.Down | MInput.X, MInput.None });
                Assert.That(p1.StateNo, Is.EqualTo(11),
                    "holding X after recovery must not retrigger the single-button crouch command.");
            }
        }

        [Test]
        public void RealKfm_AnimTimeZeroChangeStateWaitsForFullAction200And400()
        {
            string directory = TestAssets.KfmDir();
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("KFM test character is missing.");
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);

            AssertKfmAnimTimeRecovery(data, 200, 0);
            AssertKfmAnimTimeRecovery(data, 400, 11);
        }

        static void AssertKfmAnimTimeRecovery(MCharData data, int stateNo, int expectedStateNo)
        {
            Assert.That(data.Anims.TryGetValue(stateNo, out var anim), Is.True,
                "precondition: KFM action " + stateNo + " must be loaded.");
            Assert.That(anim.TotalTime, Is.EqualTo(12),
                "precondition: KFM action " + stateNo + " authored total time.");

            MBattleEngine engine = new MBattleEngine { EnableDemoAutoTurnFallback = false };
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: stateNo, startAnimNo: stateNo);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            p1.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p2.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(1000), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p1.Facing = Lockstep.Math.FFloat.One;
            p2.Facing = -Lockstep.Math.FFloat.One;

            for (int tick = 1; tick <= anim.TotalTime; tick++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });

                Assert.That(p1.StateNo, Is.EqualTo(stateNo),
                    "KFM state " + stateNo + " recovered before its authored animation end.");
                if (tick < anim.TotalTime)
                {
                    Assert.That(p1.AnimTime, Is.LessThan(0),
                        "KFM state " + stateNo + " should not expose AnimTime=0 before total time.");
                }
                else
                {
                    Assert.That(p1.AnimTime, Is.EqualTo(0),
                        "KFM state " + stateNo + " should expose AnimTime=0 exactly on the boundary tick.");
                }
            }

            engine.Tick(new[] { MInput.None, MInput.None });

            Assert.That(p1.StateNo, Is.EqualTo(expectedStateNo),
                "KFM state " + stateNo + " should run ChangeState on the controller frame after AnimTime=0.");
        }

        [TestCase(true, MInput.Right, TestName = "SyntheticAttackCtrlTrue_HoldForward_WaitsForAnimTimeRecovery")]
        [TestCase(true, MInput.Down, TestName = "SyntheticAttackCtrlTrue_HoldDown_WaitsForAnimTimeRecovery")]
        [TestCase(false, MInput.Right, TestName = "SyntheticAttackCtrlFalse_HoldForward_WaitsForAnimTimeRecovery")]
        [TestCase(false, MInput.Down, TestName = "SyntheticAttackCtrlFalse_HoldDown_WaitsForAnimTimeRecovery")]
        public void SyntheticAttackRecovery_HeldMovement_WaitsForAnimTimeZero(bool attackCtrl, MInput heldInput)
        {
            MCharData data = LoadSyntheticAttackRecoveryData(attackCtrl);
            MBattleEngine engine = new MBattleEngine { EnableDemoAutoTurnFallback = false };
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 200, startAnimNo: 200);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            p1.KeyCtrl = true;
            p2.KeyCtrl = true;
            p1.Ctrl = attackCtrl;
            p2.Ctrl = true;

            Assert.That(p1.StateNo, Is.EqualTo(200));
            Assert.That(p1.MoveType, Is.EqualTo(4));
            Assert.That(p1.AnimTime, Is.LessThan(0));

            for (int i = 0; i < 4; i++)
            {
                engine.Tick(new[] { heldInput, MInput.None });
                Assert.That(p1.StateNo, Is.EqualTo(200),
                    "hardcoded movement must not interrupt an attacking state before AnimTime reaches zero");
                Assert.That(p1.MoveType, Is.EqualTo(4));
            }

            Assert.That(p1.AnimTime, Is.EqualTo(0), "precondition: authored recovery trigger is now true");

            engine.Tick(new[] { heldInput, MInput.None });

            Assert.That(p1.StateNo, Is.EqualTo(0));
            Assert.That(p1.MoveType, Is.EqualTo(1));
            Assert.That(p1.Ctrl, Is.True);
        }

        static MCharData LoadSyntheticAttackRecoveryData(bool attackCtrl)
        {
            string cns =
                "[Statedef 0]\n" +
                "type = S\n" +
                "movetype = I\n" +
                "physics = S\n" +
                "anim = 0\n" +
                "ctrl = 1\n\n" +
                "[Statedef 200]\n" +
                "type = S\n" +
                "movetype = A\n" +
                "physics = S\n" +
                "anim = 200\n" +
                "ctrl = " + (attackCtrl ? "1" : "0") + "\n\n" +
                "[State 200, done]\n" +
                "type = ChangeState\n" +
                "trigger1 = AnimTime = 0\n" +
                "value = 0\n" +
                "ctrl = 1\n";
            const string air =
                "[Begin Action 0]\n" +
                "0,0, 0,0, 1\n" +
                "[Begin Action 200]\n" +
                "200,0, 0,0, 1\n" +
                "200,1, 0,0, 1\n" +
                "200,2, 0,0, 1\n" +
                "200,3, 0,0, 1\n";

            return MCharLoader.Load(new[] { cns }, cns, null, air, null, "SyntheticAttackRecovery");
        }

        [Test]
        public void AnimElemTriggerSurvivesEarlierSameBlockChangeAnimJump()
        {
            const string cns =
                "[Statedef 10]\n" +
                "type = A\n" +
                "physics = N\n" +
                "anim = 0\n" +
                "ctrl = 0\n\n" +
                "[State 10, jump]\n" +
                "type = ChangeAnim\n" +
                "trigger1 = 1\n" +
                "value = 0\n" +
                "elem = 7\n\n" +
                "[State 10, air physics]\n" +
                "type = StateTypeSet\n" +
                "trigger1 = animelem = 4\n" +
                "statetype = A\n" +
                "physics = A\n";
            const string air =
                "[Begin Action 0]\n" +
                "0,0, 0,0, 1\n" +
                "0,1, 0,0, 1\n" +
                "0,2, 0,0, 1\n" +
                "0,3, 0,0, 1\n" +
                "0,4, 0,0, 1\n" +
                "0,5, 0,0, 1\n" +
                "0,6, 0,0, 1\n";

            MCharData data = MCharLoader.Load(new[] { cns }, cns, null, air, null, "AnimElemBlock");
            MChar c = MCharLoader.SpawnChar(data, 1, startStateNo: 10, startAnimNo: 0);
            c.AnimRunningNo = 0;
            c.AnimElem = 3;
            c.AnimElemNo = 4;
            c.AnimElemTime = 0;
            c.AnimCurTime = 3;
            c.StateType = 4;
            c.Physics = 16;

            new MStateMachine().RunFrame(c, data.States, data.CommonStates);

            Assert.That(c.AnimElemNo, Is.EqualTo(7), "precondition: first controller jumps within the same animation.");
            Assert.That(c.Physics, Is.EqualTo(4),
                "later StateTypeSet must still see the block-start animelem=4 trigger.");
        }

        [Test]
        public void Maxine_RisingKickContactDoesNotKeepAscendingWithNeutralPhysics()
        {
            string directory = TestAssets.CharDir("Maxine");
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("Maxine test character is missing.");
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            MBattleEngine engine = new MBattleEngine { EnableDemoAutoTurnFallback = false };
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 1300, startAnimNo: 1300);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();

            p1.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p2.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(50), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p1.Facing = Lockstep.Math.FFloat.One;
            p2.Facing = -Lockstep.Math.FFloat.One;

            bool contacted = false;
            bool enabledAirPhysicsAfterLaunch = false;
            long lowestY = 0;
            for (int i = 0; i < 240; i++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
                contacted |= p1.MoveContact > 0 || p1.MoveHit > 0;
                enabledAirPhysicsAfterLaunch |= p1.StateNo == 1300 && p1.Time > 0 && p1.Physics == 4;
                if (p1.Pos.Y.Raw < lowestY)
                {
                    lowestY = p1.Pos.Y.Raw;
                }
            }

            Assert.That(contacted, Is.True, "precondition: Maxine state 1300 should hit the nearby opponent.");
            Assert.That(enabledAirPhysicsAfterLaunch, Is.True,
                "state 1300 must switch from neutral physics to air physics after the launch frame.");
            Assert.That(lowestY, Is.GreaterThan(Lockstep.Math.FFloat.FromInt(-400).Raw),
                "without air physics, state 1300 keeps VelY=-4 and flies upward forever.");
        }
    }
}
