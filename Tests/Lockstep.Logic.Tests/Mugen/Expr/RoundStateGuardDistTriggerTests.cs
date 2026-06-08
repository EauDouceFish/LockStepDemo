using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Expr
{
    /// <summary>
    /// roundstate（用量 929，大量招式 triggerall=roundstate=2 门控）与 inguarddist（守招判定入口）。
    /// 此前未特判 → 落到压 0。roundstate 读 MChar.RoundState（默认 2=Fight）；
    /// inguarddist=对手攻击态且水平距在守备范围内。
    /// </summary>
    [TestFixture]
    public sealed class RoundStateGuardDistTriggerTests
    {
        static int Eval(string expr, MChar c)
        {
            return new MugenExprCompiler().Compile(expr).Run(c).ToI();
        }

        [Test]
        public void RoundState_DefaultsToFight2()
        {
            MChar c = new MChar();
            Assert.That(Eval("roundstate", c), Is.EqualTo(2), "默认 Fight=2");
            Assert.That(Eval("roundstate = 2", c), Is.EqualTo(1), "roundstate=2 门控通过");
        }

        [Test]
        public void RoundState_ReadsField()
        {
            MChar c = new MChar { RoundState = 3 };
            Assert.That(Eval("roundstate", c), Is.EqualTo(3));
            Assert.That(Eval("roundstate = 2", c), Is.EqualTo(0), "非战斗相 → roundstate=2 不通过");
        }

        static MChar SelfWithEnemy(int selfX, int p2X, int p2MoveType)
        {
            MChar self = new MChar { Pos = new FVector3(FFloat.FromInt(selfX), FFloat.Zero, FFloat.Zero) };
            self.P2 = new MChar
            {
                Pos = new FVector3(FFloat.FromInt(p2X), FFloat.Zero, FFloat.Zero),
                MoveType = p2MoveType,
            };
            return self;
        }

        [Test]
        public void InGuardDist_EnemyAttackingAndClose_True()
        {
            // P2 攻击态(MoveType=A=4)，距 50 < 160 → 在守备范围。
            MChar c = SelfWithEnemy(selfX: 100, p2X: 150, p2MoveType: 4);
            Assert.That(Eval("inguarddist", c), Is.EqualTo(1));
        }

        [Test]
        public void InGuardDist_EnemyNotAttacking_False()
        {
            // P2 站立态(MoveType=I=1)，即便很近也不算守备触发。
            MChar c = SelfWithEnemy(selfX: 100, p2X: 150, p2MoveType: 1);
            Assert.That(Eval("inguarddist", c), Is.EqualTo(0));
        }

        [Test]
        public void InGuardDist_EnemyAttackingButFar_False()
        {
            // P2 攻击态但距 300 > 160 → 不在守备范围。
            MChar c = SelfWithEnemy(selfX: 100, p2X: 400, p2MoveType: 4);
            Assert.That(Eval("inguarddist", c), Is.EqualTo(0));
        }

        [Test]
        public void InGuardDist_UsesEnemyAttackDist()
        {
            MChar c = SelfWithEnemy(selfX: 100, p2X: 220, p2MoveType: 4);
            c.P2.AttackDistX = FFloat.FromInt(100);
            Assert.That(Eval("inguarddist", c), Is.EqualTo(0), "distance 120 is outside attackdist 100");

            c.P2.AttackDistX = FFloat.FromInt(130);
            Assert.That(Eval("inguarddist", c), Is.EqualTo(1), "distance 120 is inside attackdist 130");
        }

        [Test]
        public void InGuardDist_NoEnemy_False()
        {
            MChar lonely = new MChar { Pos = new FVector3(FFloat.FromInt(100), FFloat.Zero, FFloat.Zero) };
            Assert.That(Eval("inguarddist", lonely), Is.EqualTo(0));
        }
    }
}
