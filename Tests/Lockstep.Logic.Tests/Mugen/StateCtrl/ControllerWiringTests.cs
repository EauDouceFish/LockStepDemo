using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.StateCtrl
{
    /// <summary>
    /// Tier B 写态控制器接线到 MChar 字段（Claude 接 Codex 已捕获参数的控制器 Run()）：
    /// Pause/SuperPause→SetPause/SetSuperPause、PosFreeze、Width、PlayerPush、ScreenBound、HitOverride。
    /// </summary>
    [TestFixture]
    public sealed class ControllerWiringTests
    {
        static BytecodeExp E(string expr)
        {
            return new MugenExprCompiler().Compile(expr);
        }

        static MChar CharWithPause()
        {
            return new MChar { Id = 0, Pause = new MPauseState(), CommandList = new Lockstep.Mugen.Command.MCommandList() };
        }

        [Test]
        public void Pause_WritesBufferAndMovetime()
        {
            MChar c = CharWithPause();
            new PauseController { Time = E("10"), MoveTime = E("3") }.Run(c);
            Assert.That(c.Pause.PauseTimeBuffer, Is.EqualTo(~10), "Pause 写 buffer = ^pausetime");
            Assert.That(c.PauseMovetime, Is.EqualTo(3));
        }

        [Test]
        public void SuperPause_WritesBufferMovetimeUnhittable()
        {
            MChar c = CharWithPause();
            new SuperPauseController { Time = E("20"), MoveTime = E("20"), Unhittable = E("1") }.Run(c);
            Assert.That(c.Pause.SuperTimeBuffer, Is.EqualTo(~20));
            Assert.That(c.SuperMovetime, Is.EqualTo(20));
            Assert.That(c.UnhittableTime, Is.EqualTo(21), "unhittable 延 1 帧 = pausetime + 1");
        }

        [Test]
        public void SuperPause_DefaultsTime30()
        {
            MChar c = CharWithPause();
            new SuperPauseController().Run(c);   // 无参 → t=30
            Assert.That(c.Pause.SuperTimeBuffer, Is.EqualTo(~30));
        }

        [Test]
        public void PosFreeze_SetsFlag()
        {
            MChar c = new MChar();
            new PosFreezeController().Run(c);   // 无参 value 默认 true
            Assert.That(c.PosFreeze, Is.True);
        }

        [Test]
        public void Width_ValueSetsPlayerAndEdge()
        {
            MChar c = new MChar();
            new WidthController { Value = new[] { E("20"), E("18") } }.Run(c);
            Assert.That(c.WidthPlayerFront.ToFloat(), Is.EqualTo(20f).Within(0.01f));
            Assert.That(c.WidthPlayerBack.ToFloat(), Is.EqualTo(18f).Within(0.01f));
            Assert.That(c.WidthEdgeFront.ToFloat(), Is.EqualTo(20f).Within(0.01f));
        }

        [Test]
        public void PlayerPush_SetsFields()
        {
            MChar c = new MChar();
            new PlayerPushController { Value = E("0"), Priority = E("3") }.Run(c);
            Assert.That(c.PlayerPushEnabled, Is.False);
            Assert.That(c.PushPriority, Is.EqualTo(3));
        }

        [Test]
        public void ScreenBound_SetsFields()
        {
            MChar c = new MChar();
            new ScreenBoundController { Value = E("1"), MoveCamera = new[] { E("1"), E("0") } }.Run(c);
            Assert.That(c.ScreenBoundEnabled, Is.True);
            Assert.That(c.ScreenBoundMoveCameraX, Is.True);
            Assert.That(c.ScreenBoundMoveCameraY, Is.False);
        }

        [Test]
        public void HitOverride_WritesSlot()
        {
            MChar c = new MChar();
            new HitOverrideController { Attr = 0x12, Slot = E("2"), StateNo = E("1300"), Time = E("60") }.Run(c);
            Assert.That(c.HitOverrides[2].Attr, Is.EqualTo(0x12));
            Assert.That(c.HitOverrides[2].StateNo, Is.EqualTo(1300));
            Assert.That(c.HitOverrides[2].Time, Is.EqualTo(60));
            Assert.That(c.HitOverrides[2].Active, Is.True);
        }
    }
}
