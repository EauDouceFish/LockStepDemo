using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>M9 物理：位置积分 + 物理类型摩擦(S/C)/重力(A)，移植 Ikemen posUpdate。</summary>
    [TestFixture]
    public sealed class MPhysicsTests
    {
        static FFloat F(int n, int d) => FFloat.FromInt(n) / FFloat.FromInt(d);

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
    }
}
