using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>M9 物理：位置积分 + 物理类型摩擦(S/C)/重力(A)，移植 Ikemen posUpdate。</summary>
    [TestFixture]
    public sealed class MPhysicsTests
    {
        static FFloat F(int n, int d) => FFloat.FromInt(n) / FFloat.FromInt(d);
        static readonly MInput[] NoInputs = { MInput.None, MInput.None };

        [Test]
        public void Air_AppliesGravityToVelY()
        {
            MChar c = new MChar
            {
                Physics = 4,   // A
                Constants = new MConstants { Yaccel = F(1, 2) },   // 0.5
                Vel = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero),
            };
            MPhysics.Step(c);
            Assert.That(c.Vel.Y, Is.EqualTo(F(1, 2)), "空中每帧 vel.y += yaccel");
            MPhysics.Step(c);
            Assert.That(c.Vel.Y, Is.EqualTo(FFloat.FromInt(1)), "重力累加");
        }

        [Test]
        public void Stand_AppliesFrictionAndSnapsToZero()
        {
            MChar c = new MChar
            {
                Physics = 1,   // S
                Constants = new MConstants { StandFriction = F(1, 2), StandFrictionThreshold = FFloat.FromInt(1) },
                Vel = new FVector3(FFloat.FromInt(2), FFloat.Zero, FFloat.Zero),
            };
            MPhysics.Step(c);
            Assert.That(c.Vel.X, Is.EqualTo(FFloat.FromInt(1)), "2*0.5=1（=阈值，不归零）");
            MPhysics.Step(c);
            Assert.That(c.Vel.X, Is.EqualTo(FFloat.Zero), "1*0.5=0.5<阈值1 → 归零");
        }

        [Test]
        public void None_NoFrictionNoGravity()
        {
            MChar c = new MChar
            {
                Physics = 16,   // N
                Constants = new MConstants { StandFriction = F(1, 2), Yaccel = F(1, 2) },
                Vel = new FVector3(FFloat.FromInt(2), FFloat.FromInt(3), FFloat.Zero),
            };
            MPhysics.Step(c);
            Assert.That(c.Vel.X, Is.EqualTo(FFloat.FromInt(2)), "N 型不改速度");
            Assert.That(c.Vel.Y, Is.EqualTo(FFloat.FromInt(3)));
        }

        [Test]
        public void Integration_AppliesFacingToX()
        {
            MChar c = new MChar
            {
                Physics = 16,
                Constants = new MConstants(),
                Facing = FFloat.FromInt(-1),
                Vel = new FVector3(FFloat.FromInt(2), FFloat.Zero, FFloat.Zero),
            };
            MPhysics.Step(c);
            Assert.That(c.Pos.X, Is.EqualTo(FFloat.FromInt(-2)), "面左时 vel.x 正值使 pos 左移");
            Assert.That(c.OldPos.X, Is.EqualTo(FFloat.Zero), "记录上一帧位置");
        }

        [Test]
        public void PlayerPush_OverlappingGroundedPlayers_AreSeparatedAfterPhysics()
        {
            MBattleEngine engine = TwoPlayerPhysicsEngine();
            MChar left = engine.Chars[0];
            MChar right = engine.Chars[1];
            left.Pos = new FVector3(FFloat.FromInt(-2), FFloat.Zero, FFloat.Zero);
            right.Pos = new FVector3(FFloat.FromInt(2), FFloat.Zero, FFloat.Zero);

            engine.Tick(NoInputs);

            Assert.That(left.Pos.X, Is.LessThanOrEqualTo(FFloat.FromInt(-10)));
            Assert.That(right.Pos.X, Is.GreaterThanOrEqualTo(FFloat.FromInt(10)));
        }

        [Test]
        public void PlayerPush_DisabledOnOnePlayer_DoesNotResolveOverlap()
        {
            MBattleEngine engine = TwoPlayerPhysicsEngine(disableLeftPush: true);
            MChar left = engine.Chars[0];
            MChar right = engine.Chars[1];
            left.Pos = new FVector3(FFloat.FromInt(-2), FFloat.Zero, FFloat.Zero);
            right.Pos = new FVector3(FFloat.FromInt(2), FFloat.Zero, FFloat.Zero);

            engine.Tick(NoInputs);

            Assert.That(left.Pos.X, Is.EqualTo(FFloat.FromInt(-2)));
            Assert.That(right.Pos.X, Is.EqualTo(FFloat.FromInt(2)));
        }

        [Test]
        public void PlayerPush_PosFreezeChar_IsNotPushedAndFlagClearsAfterTick()
        {
            MBattleEngine engine = TwoPlayerPhysicsEngine();
            MChar left = engine.Chars[0];
            MChar right = engine.Chars[1];
            left.Pos = new FVector3(FFloat.FromInt(-2), FFloat.Zero, FFloat.Zero);
            right.Pos = new FVector3(FFloat.FromInt(2), FFloat.Zero, FFloat.Zero);
            left.PosFreeze = true;

            engine.Tick(NoInputs);

            Assert.That(left.Pos.X, Is.EqualTo(FFloat.FromInt(-2)));
            Assert.That(right.Pos.X, Is.EqualTo(FFloat.FromInt(2)));
            Assert.That(left.PosFreeze, Is.False);
        }

        [Test]
        public void PlayerPush_ZeroAndNegativeRuntimeWidths_DoNotInvertPushBounds()
        {
            MBattleEngine engine = TwoPlayerPhysicsEngine(
                leftDataOverride: WidthValueData("-10, -20"),
                rightDataOverride: WidthValueData("-10, -20"));
            MChar left = engine.Chars[0];
            MChar right = engine.Chars[1];
            left.Pos = new FVector3(FFloat.FromInt(-2), FFloat.Zero, FFloat.Zero);
            right.Pos = new FVector3(FFloat.FromInt(2), FFloat.Zero, FFloat.Zero);

            engine.Tick(NoInputs);

            Assert.That(left.WidthPlayerFront, Is.EqualTo(FFloat.Zero));
            Assert.That(left.WidthPlayerBack, Is.LessThan(FFloat.Zero));
            Assert.That(left.WidthPlayerFrontSet, Is.True);
            Assert.That(left.WidthPlayerBackSet, Is.True);
            Assert.That(left.Pos.X, Is.LessThan(FFloat.FromInt(-2)));
            Assert.That(right.Pos.X, Is.GreaterThan(FFloat.FromInt(2)));
        }

        [Test]
        public void PlayerPush_ClampsStageBoundAfterResolvingOverlap()
        {
            MBattleEngine engine = TwoPlayerPhysicsEngine();
            MChar left = engine.Chars[0];
            MChar right = engine.Chars[1];
            left.Pos = new FVector3(FFloat.FromInt(-8), FFloat.Zero, FFloat.Zero);
            right.Pos = new FVector3(FFloat.FromInt(-4), FFloat.Zero, FFloat.Zero);
            engine.Stage.SetSymmetric(10);

            engine.Tick(NoInputs);

            Assert.That(left.Pos.X, Is.EqualTo(FFloat.FromInt(-10)));
            Assert.That(right.Pos.X, Is.LessThanOrEqualTo(FFloat.FromInt(10)));
        }

        [Test]
        public void StageBound_DisabledByScreenBoundController_SkipsStageClamp()
        {
            const string cns =
                "[Statedef 0]\n" +
                "type = S\n" +
                "physics = N\n" +
                "anim = 0\n\n" +
                "[State 0, disable stage bound]\n" +
                "type = ScreenBound\n" +
                "trigger1 = 1\n" +
                "stagebound = 0\n";
            const string air = "[Begin Action 0]\n0,0, 0,0, 1\n";
            MCharData data = MCharLoader.Load(new[] { cns }, cns, null, air, null, "NoStageBound");
            MBattleEngine engine = new MBattleEngine();
            MChar c = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            c.Pos = new FVector3(FFloat.FromInt(500), FFloat.Zero, FFloat.Zero);
            engine.Add(c, data);
            engine.Stage.SetSymmetric(200);

            engine.Tick(new[] { MInput.None });

            Assert.That(c.Pos.X, Is.EqualTo(FFloat.FromInt(500)));
        }

        static MBattleEngine TwoPlayerPhysicsEngine(bool disableLeftPush = false,
            MCharData leftDataOverride = null, MCharData rightDataOverride = null)
        {
            MConstants constants = new MConstants
            {
                SizeGroundBack = FFloat.FromInt(10),
                SizeGroundFront = FFloat.FromInt(10),
            };
            MCharData leftData = leftDataOverride ?? (disableLeftPush ? PlayerPushDisabledData() : new MCharData());
            MCharData rightData = rightDataOverride ?? new MCharData();
            leftData.Constants = constants;
            rightData.Constants = constants;
            MChar left = PushTestChar(0, constants, FFloat.One);
            MChar right = PushTestChar(1, constants, FFloat.MinusOne);
            MBattleEngine engine = new MBattleEngine { EnableDemoAutoTurnFallback = false };
            engine.Add(left, leftData);
            engine.Add(right, rightData);
            engine.LinkPair();
            return engine;
        }

        static MCharData PlayerPushDisabledData()
        {
            const string cns =
                "[Statedef 0]\n" +
                "type = S\n" +
                "physics = N\n" +
                "anim = 0\n\n" +
                "[State 0, disable player push]\n" +
                "type = PlayerPush\n" +
                "trigger1 = 1\n" +
                "value = 0\n";
            const string air = "[Begin Action 0]\n0,0, 0,0, 1\n";
            return MCharLoader.Load(new[] { cns }, cns, null, air, null, "NoPlayerPush");
        }

        static MCharData WidthValueData(string value)
        {
            string cns =
                "[Statedef 0]\n" +
                "type = S\n" +
                "physics = N\n" +
                "anim = 0\n\n" +
                "[State 0, width]\n" +
                "type = Width\n" +
                "trigger1 = 1\n" +
                "value = " + value + "\n";
            const string air = "[Begin Action 0]\n0,0, 0,0, 1\n";
            return MCharLoader.Load(new[] { cns }, cns, null, air, null, "WidthValue");
        }

        static MChar PushTestChar(int id, MConstants constants, FFloat facing)
        {
            return new MChar
            {
                Id = id,
                Constants = constants,
                Physics = 16,
                StateType = 1,
                MoveType = 1,
                Facing = facing,
                WidthPlayerFront = FFloat.FromInt(10),
                WidthPlayerBack = FFloat.FromInt(10),
                PlayerPushEnabled = true,
                PushAffectTeam = 1,
            };
        }
    }
}
