using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Tests.Mugen.Battle
{
    /// <summary>模块 C/D：引擎硬编码基础动作（actionPrepare）+ ctrl 授予。各转移逐条验收。</summary>
    [TestFixture]
    public sealed class MActionSystemTests
    {
        const int ST_S = 1, ST_C = 2, ST_A = 4;

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
