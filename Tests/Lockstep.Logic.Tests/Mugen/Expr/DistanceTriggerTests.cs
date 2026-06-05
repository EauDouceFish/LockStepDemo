using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Expr
{
    /// <summary>
    /// p2dist / p2bodydist 距离触发器（p2dist 用量 946、p2bodydist 123）。此前未特判 → 落到压 0。
    /// p2dist X = facing*(p2.x - self.x)（前为正）；p2dist Y = p2.y - self.y（不翻向）；
    /// p2bodydist X = p2dist X 减双方前缘半宽（MUGEN 边到边）。对齐 Ikemen char.go distX/bodyDistX。
    /// </summary>
    [TestFixture]
    public sealed class DistanceTriggerTests
    {
        static MChar MakeChar(int x, int y, int facing, int frontWidth)
        {
            return new MChar
            {
                Pos = new FVector3(FFloat.FromInt(x), FFloat.FromInt(y), FFloat.Zero),
                Facing = FFloat.FromInt(facing),
                Constants = new MConstants { SizeGroundFront = FFloat.FromInt(frontWidth) },
            };
        }

        static MChar Pair(int selfX, int selfFacing, int p2X, int p2Y = 0, int selfFront = 16, int p2Front = 16)
        {
            MChar self = MakeChar(selfX, 0, selfFacing, selfFront);
            MChar p2 = MakeChar(p2X, p2Y, -selfFacing, p2Front);
            self.P2 = p2;
            return self;
        }

        static float Eval(string expr, MChar c)
        {
            return new MugenExprCompiler().Compile(expr).Run(c).ToF().ToFloat();
        }

        [Test]
        public void P2DistX_FacingRight_PositiveWhenEnemyInFront()
        {
            // self@100 面右(+1)，p2@180 → p2dist X = +1*(180-100) = 80。
            MChar c = Pair(selfX: 100, selfFacing: 1, p2X: 180);
            Assert.That(Eval("p2dist X", c), Is.EqualTo(80f).Within(0.01f));
        }

        [Test]
        public void P2DistX_FacingLeft_FrontIsNegativeWorldX()
        {
            // self@100 面左(-1)，p2@40（世界左侧=面前）→ p2dist X = -1*(40-100) = +60。
            MChar c = Pair(selfX: 100, selfFacing: -1, p2X: 40);
            Assert.That(Eval("p2dist X", c), Is.EqualTo(60f).Within(0.01f));
        }

        [Test]
        public void P2DistX_EnemyBehind_Negative()
        {
            // self@100 面右，p2@60（身后）→ p2dist X = +1*(60-100) = -40。
            MChar c = Pair(selfX: 100, selfFacing: 1, p2X: 60);
            Assert.That(Eval("p2dist X", c), Is.EqualTo(-40f).Within(0.01f));
        }

        [Test]
        public void P2DistY_NotFacingFlipped()
        {
            // self y=0，p2 y=-30（上方，MUGEN 上为负）→ p2dist Y = -30 - 0 = -30（不随朝向翻）。
            MChar c = Pair(selfX: 100, selfFacing: -1, p2X: 200, p2Y: -30);
            Assert.That(Eval("p2dist Y", c), Is.EqualTo(-30f).Within(0.01f));
        }

        [Test]
        public void P2BodyDistX_SubtractsBothFrontWidths()
        {
            // p2dist X = 80；前宽各 16 → p2bodydist X = 80 - 16 - 16 = 48。
            MChar c = Pair(selfX: 100, selfFacing: 1, p2X: 180, selfFront: 16, p2Front: 16);
            Assert.That(Eval("p2bodydist X", c), Is.EqualTo(48f).Within(0.01f));
        }

        [Test]
        public void Distance_NoEnemy_ReturnsUndefinedCoercedToZero_NoCrash()
        {
            // 无 P2（退化场景；1v1 实战 P2 恒存在）：p2dist 返回 undefined，强制取数 → 0，不崩。
            MChar lonely = MakeChar(100, 0, 1, 16);   // 无 P2
            Assert.That(Eval("p2dist X", lonely), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Eval("p2bodydist X", lonely), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void P2BodyDistX_InComparison_TypicalProximityCheck()
        {
            // 典型用法：p2bodydist X < 30。距 48 → 不成立(0)。
            MChar far = Pair(selfX: 100, selfFacing: 1, p2X: 180);
            Assert.That(Eval("p2bodydist X < 30", far), Is.EqualTo(0f).Within(0.01f));
            // 贴近：p2@120 → p2dist 20，bodydist 20-32=-12 < 30 成立(1)。
            MChar near = Pair(selfX: 100, selfFacing: 1, p2X: 120);
            Assert.That(Eval("p2bodydist X < 30", near), Is.EqualTo(1f).Within(0.01f));
        }
    }
}
