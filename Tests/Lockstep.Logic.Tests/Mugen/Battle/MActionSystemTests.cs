using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen.Battle
{
    /// <summary>模块 C/D：引擎硬编码基础动作（actionPrepare）+ ctrl 授予。各转移逐条验收。</summary>
    [TestFixture]
    public sealed class MActionSystemTests
    {
        const int ST_S = 1, ST_C = 2, ST_A = 4;
        const int MT_A = 4;

        // 构造一个受控、有控制权、面右的角色，处于指定 statetype/stateno。
        static MChar MakePlayer(int stateType, int stateNo)
        {
            return new MChar
            {
                KeyCtrl = true,
                Ctrl = true,
                StateType = stateType,
                StateNo = stateNo,
                Facing = FFloat.One,
                Constants = new MConstants(),
                Input = new MInputBuffer(),
                PendingStateNo = -1,
            };
        }

        [Test]
        public void HoldForward_Standing_TransitionsToWalk()
        {
            MChar c = MakePlayer(ST_S, 0);
            c.Input.Update(MInput.Right, facingRight: true);   // Fb>0
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(20), "立持前 → 走(20)");
        }

        [Test]
        public void HoldBack_Standing_TransitionsToWalk()
        {
            MChar c = MakePlayer(ST_S, 0);
            c.Input.Update(MInput.Left, facingRight: true);   // Bb>0
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(20), "立持后(无敌人靠近) → 走(20)");
        }

        [Test]
        public void HoldBack_InGuardDist_TransitionsToStandGuard()
        {
            MChar c = MakePlayer(ST_S, 0);
            MChar enemy = MakePlayer(ST_S, 200);
            c.Pos = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            enemy.Pos = new FVector3(FFloat.FromInt(60), FFloat.Zero, FFloat.Zero);
            enemy.MoveType = 4;
            enemy.AttackDistX = FFloat.FromInt(100);
            c.P2 = enemy;
            c.Input.Update(MInput.Left, facingRight: true);

            MActionSystem.Prepare(c);

            Assert.That(c.PendingStateNo, Is.EqualTo(120));
            Assert.That(c.Guarding, Is.True);
        }

        [Test]
        public void HoldBack_OutsideGuardDist_StillWalksBack()
        {
            MChar c = MakePlayer(ST_S, 0);
            MChar enemy = MakePlayer(ST_S, 200);
            c.Pos = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            enemy.Pos = new FVector3(FFloat.FromInt(160), FFloat.Zero, FFloat.Zero);
            enemy.MoveType = 4;
            enemy.AttackDistX = FFloat.FromInt(100);
            c.P2 = enemy;
            c.Input.Update(MInput.Left, facingRight: true);

            MActionSystem.Prepare(c);

            Assert.That(c.PendingStateNo, Is.EqualTo(20));
            Assert.That(c.Guarding, Is.False);
        }

        [Test]
        public void Guarding_RecomputesEachFrame_AndClearsWhenInputReleased()
        {
            MChar c = MakePlayer(ST_S, 0);
            MChar enemy = MakePlayer(ST_S, 200);
            c.Pos = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            enemy.Pos = new FVector3(FFloat.FromInt(60), FFloat.Zero, FFloat.Zero);
            enemy.MoveType = 4;
            enemy.AttackDistX = FFloat.FromInt(100);
            c.P2 = enemy;
            c.Input.Update(MInput.Left, facingRight: true);
            MActionSystem.Prepare(c);
            Assert.That(c.Guarding, Is.True);

            c.Input.Update(MInput.None, facingRight: true);
            c.PendingStateNo = -1;
            MActionSystem.Prepare(c);

            Assert.That(c.PendingStateNo, Is.EqualTo(-1));
            Assert.That(c.Guarding, Is.False);
        }

        [TestCase(120)]
        [TestCase(130)]
        [TestCase(131)]
        [TestCase(132)]
        [TestCase(140)]
        [TestCase(150)]
        [TestCase(151)]
        [TestCase(152)]
        [TestCase(153)]
        [TestCase(154)]
        [TestCase(155)]
        public void GuardState_RemainsGuarding_WhenControlIsHeldByState(int stateNo)
        {
            MChar c = MakePlayer(ST_S, stateNo);
            c.Ctrl = false;
            c.Input.Update(MInput.None, facingRight: true);

            MActionSystem.Prepare(c);

            Assert.That(c.Guarding, Is.True);
        }

        [Test]
        public void AutoTurn_GroundedControlled_FacesOpponent()
        {
            MChar c = MakePlayer(ST_S, 0);
            MChar enemy = MakePlayer(ST_S, 0);
            c.Pos = new FVector3(FFloat.FromInt(20), FFloat.Zero, FFloat.Zero);
            c.Facing = FFloat.One;
            enemy.Pos = new FVector3(FFloat.FromInt(-20), FFloat.Zero, FFloat.Zero);
            c.P2 = enemy;

            MActionSystem.AutoTurn(c);

            Assert.That(c.Facing, Is.EqualTo(FFloat.MinusOne), "对手在左侧时自动面左");
        }

        [Test]
        public void AutoTurn_RespectsNoAutoTurnAndControl()
        {
            MChar c = MakePlayer(ST_S, 0);
            MChar enemy = MakePlayer(ST_S, 0);
            c.Pos = FVector3.Zero;
            c.Facing = FFloat.One;
            enemy.Pos = new FVector3(FFloat.FromInt(-20), FFloat.Zero, FFloat.Zero);
            c.P2 = enemy;
            c.AssertFlags = (int)MAssertFlag.NoAutoTurn;

            MActionSystem.AutoTurn(c);

            Assert.That(c.Facing, Is.EqualTo(FFloat.One), "NoAutoTurn 断言时不翻身");

            c.AssertFlags = 0;
            c.Ctrl = false;
            MActionSystem.AutoTurn(c);

            Assert.That(c.Facing, Is.EqualTo(FFloat.One), "无控制权时不翻身");
        }

        [Test]
        public void Tick_AutoTurn_DoesNotReinterpretCurrentFrameInput()
        {
            MChar c = MakePlayer(ST_S, 0);
            MChar enemy = MakePlayer(ST_S, 0);
            c.Pos = new FVector3(FFloat.FromInt(20), FFloat.Zero, FFloat.Zero);
            c.Facing = FFloat.One;
            enemy.Pos = new FVector3(FFloat.FromInt(-20), FFloat.Zero, FFloat.Zero);
            enemy.Facing = FFloat.MinusOne;

            MBattleEngine engine = new MBattleEngine();
            engine.Add(c, new MCharData());
            engine.Add(enemy, new MCharData());
            engine.LinkPair();

            engine.Tick(new[] { MInput.Right, MInput.None });

            Assert.That(c.Input.Fb, Is.EqualTo(1), "本帧输入仍按翻身前面右解释：右=前");
            Assert.That(c.Input.Bb, Is.LessThan(0), "本帧不应被翻身后面左解释成后");
            Assert.That(c.Facing, Is.EqualTo(FFloat.MinusOne), "输入缓冲之后才应用 demo 自动转身");
        }

        [Test]
        public void Tick_AutoTurnAfterPhysics_PreservesCurrentFrameWorldWalkDirection()
        {
            MChar c = MakePlayer(ST_S, 20);
            MChar enemy = MakePlayer(ST_S, 0);
            c.Pos = new FVector3(FFloat.FromInt(20), FFloat.Zero, FFloat.Zero);
            c.Facing = FFloat.One;
            c.Vel = new FVector3(FFloat.One, FFloat.Zero, FFloat.Zero);
            enemy.Pos = new FVector3(FFloat.FromInt(-20), FFloat.Zero, FFloat.Zero);
            enemy.Facing = FFloat.MinusOne;

            MBattleEngine engine = new MBattleEngine();
            engine.Add(c, new MCharData());
            engine.Add(enemy, new MCharData());
            engine.LinkPair();

            FFloat beforeX = c.Pos.X;
            engine.Tick(new[] { MInput.Right, MInput.None });

            Assert.That(c.Pos.X, Is.GreaterThan(beforeX),
                "crossing frame should keep walking toward world right before demo auto-turn affects next frame");
            Assert.That(c.Facing, Is.EqualTo(FFloat.MinusOne),
                "demo auto-turn should still face the opponent after physics for the next frame");
        }

        [Test]
        public void Tick_AutoTurn_RespectsNoAutoTurnAssertedThisFrame()
        {
            MChar c = MakePlayer(ST_S, 0);
            MChar enemy = MakePlayer(ST_S, 0);
            c.Pos = new FVector3(FFloat.FromInt(20), FFloat.Zero, FFloat.Zero);
            c.Facing = FFloat.One;
            enemy.Pos = new FVector3(FFloat.FromInt(-20), FFloat.Zero, FFloat.Zero);

            MCharData data = new MCharData();
            MStateDef state = new MStateDef { No = 0, StateType = ST_S, MoveType = 1 };
            state.Controllers.Add(new AssertSpecialController { Flags = (int)MAssertFlag.NoAutoTurn });
            data.States[0] = state;

            MBattleEngine engine = new MBattleEngine();
            engine.Add(c, data);
            engine.Add(enemy, new MCharData());
            engine.LinkPair();

            engine.Tick(new[] { MInput.None, MInput.None });

            Assert.That(c.Facing, Is.EqualTo(FFloat.One), "本帧状态机断言 noautoturn 后，demo fallback 不应翻身");
        }

        [Test]
        public void Tick_AutoTurn_CanBeDisabledForStrictFidelity()
        {
            MChar c = MakePlayer(ST_S, 0);
            MChar enemy = MakePlayer(ST_S, 0);
            c.Pos = new FVector3(FFloat.FromInt(20), FFloat.Zero, FFloat.Zero);
            c.Facing = FFloat.One;
            enemy.Pos = new FVector3(FFloat.FromInt(-20), FFloat.Zero, FFloat.Zero);

            MBattleEngine engine = new MBattleEngine { EnableDemoAutoTurnFallback = false };
            engine.Add(c, new MCharData());
            engine.Add(enemy, new MCharData());
            engine.LinkPair();

            engine.Tick(new[] { MInput.None, MInput.None });

            Assert.That(c.Facing, Is.EqualTo(FFloat.One), "严格对齐模式可关闭 demo 自动转身");
        }

        [Test]
        public void HoldDown_Standing_TransitionsToCrouchAndZeroesVelX()
        {
            MChar c = MakePlayer(ST_S, 0);
            c.Vel = new FVector3(FFloat.FromInt(5), FFloat.Zero, FFloat.Zero);
            c.Input.Update(MInput.Down, facingRight: true);   // Db>0
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(10), "立持下 → 立转蹲(10)");
            Assert.That(c.Vel.X.Raw, Is.EqualTo(0), "转蹲清 vel.x（非 run 态）");
        }

        [Test]
        public void NoDown_Crouching_TransitionsToStand()
        {
            MChar c = MakePlayer(ST_C, 11);
            c.Input.Update(MInput.None, facingRight: true);   // Db<=0
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(12), "蹲松下 → 蹲转立(12)");
        }

        [Test]
        public void HoldUp_Standing_TransitionsToJumpStart()
        {
            MChar c = MakePlayer(ST_S, 0);
            c.Input.Update(MInput.Up, facingRight: true);     // Ub>0
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(40), "立持上 → 跳跃起始(40)");
        }

        [Test]
        public void Walking_NoDirection_BrakesToStand()
        {
            MChar c = MakePlayer(ST_S, 20);
            c.Input.Update(MInput.None, facingRight: true);   // Fb<0, Bb<0 → 相等
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(0), "走中无方向 → 刹车回站立(0)");
        }

        [Test]
        public void NoJumpAsserted_HoldUp_DoesNotJump()
        {
            MChar c = MakePlayer(ST_S, 0);
            c.AssertFlags = (int)MAssertFlag.NoJump;
            c.Input.Update(MInput.Up, facingRight: true);
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(-1), "断言 nojump → 不跳");
        }

        [Test]
        public void NoControl_HoldForward_DoesNothing()
        {
            MChar c = MakePlayer(ST_S, 0);
            c.Ctrl = false;
            c.Input.Update(MInput.Right, facingRight: true);
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(-1), "无 ctrl → 不走");
        }

        [TestCase(true, ST_S, MInput.Right, TestName = "AttackCtrlTrue_HoldForward_DoesNotWalk")]
        [TestCase(true, ST_S, MInput.Down, TestName = "AttackCtrlTrue_HoldDown_DoesNotCrouch")]
        [TestCase(true, ST_C, MInput.None, TestName = "AttackCtrlTrue_CrouchRelease_DoesNotStand")]
        [TestCase(false, ST_S, MInput.Right, TestName = "AttackCtrlFalse_HoldForward_DoesNotWalk")]
        [TestCase(false, ST_S, MInput.Down, TestName = "AttackCtrlFalse_HoldDown_DoesNotCrouch")]
        public void AttackMoveType_DoesNotQueueGroundedHardcodedBasics(bool ctrl, int stateType, MInput input)
        {
            MChar c = MakePlayer(stateType, 200);
            c.MoveType = MT_A;
            c.Ctrl = ctrl;
            c.Input.Update(input, facingRight: true);

            MActionSystem.Prepare(c);

            Assert.That(c.PendingStateNo, Is.EqualTo(-1),
                "attacking states must recover through authored state logic, not hardcoded basics");
        }

        [Test]
        public void AttackMoveType_HoldBackInGuardDist_DoesNotStartStandGuard()
        {
            MChar c = MakePlayer(ST_S, 200);
            MChar enemy = MakePlayer(ST_S, 210);
            c.MoveType = MT_A;
            c.Pos = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            enemy.Pos = new FVector3(FFloat.FromInt(60), FFloat.Zero, FFloat.Zero);
            enemy.MoveType = MT_A;
            enemy.AttackDistX = FFloat.FromInt(100);
            c.P2 = enemy;
            c.Input.Update(MInput.Left, facingRight: true);

            MActionSystem.Prepare(c);

            Assert.That(c.PendingStateNo, Is.EqualTo(-1));
            Assert.That(c.Guarding, Is.False);
        }

        [Test]
        public void AttackMoveType_WalkNumber_DoesNotHardcodedBrake()
        {
            MChar c = MakePlayer(ST_S, 20);
            c.MoveType = MT_A;
            c.Input.Update(MInput.None, facingRight: true);

            MActionSystem.Prepare(c);

            Assert.That(c.PendingStateNo, Is.EqualTo(-1));
        }

        [Test]
        public void NotKeyCtrl_HoldForward_DoesNothing()
        {
            MChar c = MakePlayer(ST_S, 0);
            c.KeyCtrl = false;
            c.Input.Update(MInput.Right, facingRight: true);
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(-1), "非玩家控制 → 引擎硬编码键不生效");
        }

        [Test]
        public void Grounded_ResetsAirJumpCount()
        {
            MChar c = MakePlayer(ST_S, 0);
            c.AirJumpCount = 2;
            c.Input.Update(MInput.None, facingRight: true);
            MActionSystem.Prepare(c);
            Assert.That(c.AirJumpCount, Is.EqualTo(0), "落地(非空中) → 清空跳计数");
        }

        [Test]
        public void AirJump_FreshUpEdge_WithinHeightAndCount_Transitions()
        {
            MChar c = MakePlayer(ST_A, 50);
            c.Constants.AirjumpNum = 1;
            c.Constants.AirjumpHeight = FFloat.FromInt(35);
            c.Pos = new FVector3(FFloat.Zero, FFloat.FromInt(-60), FFloat.Zero);   // 高于 -35（更负=更高）
            c.Time = 5;
            c.Input.Update(MInput.None, facingRight: true);
            c.Input.Update(MInput.Up, facingRight: true);       // Ub==1 边沿
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(45), "空中持上边沿 + 在高度内 + 次数足 → 空跳(45)");
            Assert.That(c.AirJumpCount, Is.EqualTo(1), "空跳计数 +1");
        }

        [Test]
        public void AirJump_ExceedCount_DoesNotTransition()
        {
            MChar c = MakePlayer(ST_A, 50);
            c.Constants.AirjumpNum = 1;
            c.Constants.AirjumpHeight = FFloat.FromInt(35);
            c.Pos = new FVector3(FFloat.Zero, FFloat.FromInt(-60), FFloat.Zero);
            c.Time = 5;
            c.AirJumpCount = 1;   // 已用满
            c.Input.Update(MInput.None, facingRight: true);
            c.Input.Update(MInput.Up, facingRight: true);
            MActionSystem.Prepare(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(-1), "空跳次数耗尽 → 不再空跳");
        }

        [Test]
        public void LandCheck_AirborneFalling_TouchesGround_TransitionsToLand()
        {
            MChar c = MakePlayer(ST_A, 50);
            c.Physics = 4;   // PHYS_A
            c.Vel = new FVector3(FFloat.Zero, FFloat.FromInt(3), FFloat.Zero);   // vel.y>0 下落
            c.Pos = new FVector3(FFloat.Zero, FFloat.FromInt(1), FFloat.Zero);   // pos.y>=0 触地
            MActionSystem.LandCheck(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(52), "空中下落触地 → 落地态(52)");
        }

        [Test]
        public void LandCheck_AirborneRising_DoesNotLand()
        {
            MChar c = MakePlayer(ST_A, 50);
            c.Physics = 4;
            c.Vel = new FVector3(FFloat.Zero, FFloat.FromInt(-5), FFloat.Zero);   // vel.y<0 上升
            c.Pos = new FVector3(FFloat.Zero, FFloat.FromInt(-30), FFloat.Zero);
            MActionSystem.LandCheck(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(-1), "上升中不落地");
        }

        [Test]
        public void LandCheck_AirborneAboveGround_DoesNotLand()
        {
            MChar c = MakePlayer(ST_A, 50);
            c.Physics = 4;
            c.Vel = new FVector3(FFloat.Zero, FFloat.FromInt(3), FFloat.Zero);   // 下落但还在空中
            c.Pos = new FVector3(FFloat.Zero, FFloat.FromInt(-40), FFloat.Zero);
            MActionSystem.LandCheck(c);
            Assert.That(c.PendingStateNo, Is.EqualTo(-1), "未触地不落地");
        }

        [Test]
        public void StartRound_GrantsCtrlAndKeyCtrl()
        {
            MChar c = new MChar { Ctrl = false, KeyCtrl = false, Constants = new MConstants() };
            MBattleEngine engine = new MBattleEngine();
            MCharData data = new MCharData();
            engine.Add(c, data);
            engine.StartRound();
            Assert.That(c.Ctrl, Is.True, "回合开始授予 ctrl");
            Assert.That(c.KeyCtrl, Is.True, "回合开始授予按键控制");
        }
    }
}
