using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Expr
{
    /// <summary>
    /// name/p1name（自身）与 p2name/enemyname（对手）字符串比较（name 用量 897、p2name 275）。
    /// 此前未特判 → 落到压 0。与角色 Name 精确比较；p2name 走 P2 redirect。
    /// </summary>
    [TestFixture]
    public sealed class NameTriggerTests
    {
        static int Eval(string expr, MChar c)
        {
            return new MugenExprCompiler().Compile(expr).Run(c).ToI();
        }

        [Test]
        public void Name_Equals_MatchesSelf()
        {
            MChar c = new MChar { Name = "Kung Fu Man" };
            Assert.That(Eval("name = \"Kung Fu Man\"", c), Is.EqualTo(1));
            Assert.That(Eval("name = \"Terrarian\"", c), Is.EqualTo(0));
        }

        [Test]
        public void Name_NotEquals_Negates()
        {
            MChar c = new MChar { Name = "KFM" };
            Assert.That(Eval("name != \"Terrarian\"", c), Is.EqualTo(1));
            Assert.That(Eval("name != \"KFM\"", c), Is.EqualTo(0));
        }

        [Test]
        public void P1Name_IsAliasForName()
        {
            MChar c = new MChar { Name = "KFM" };
            Assert.That(Eval("p1name = \"KFM\"", c), Is.EqualTo(1));
        }

        [Test]
        public void P2Name_ComparesEnemyName()
        {
            MChar self = new MChar { Name = "KFM" };
            self.P2 = new MChar { Name = "Terrarian" };
            Assert.That(Eval("p2name = \"Terrarian\"", self), Is.EqualTo(1), "p2name 读对手名");
            Assert.That(Eval("p2name = \"KFM\"", self), Is.EqualTo(0), "不是对手名 → 假");
        }

        [Test]
        public void EnemyName_IsAliasForP2Name()
        {
            MChar self = new MChar { Name = "KFM" };
            self.P2 = new MChar { Name = "Noroko" };
            Assert.That(Eval("enemyname = \"Noroko\"", self), Is.EqualTo(1));
        }

        [Test]
        public void P2Name_NoEnemy_DoesNotMatch()
        {
            MChar lonely = new MChar { Name = "KFM" };   // 无 P2 → redirect null → 整块跳过（不成立）
            Assert.That(Eval("p2name = \"KFM\"", lonely), Is.EqualTo(0));
        }

        [Test]
        public void Name_CaseSensitive()
        {
            MChar c = new MChar { Name = "KFM" };
            Assert.That(Eval("name = \"kfm\"", c), Is.EqualTo(0), "名字比较大小写敏感");
        }
    }
}
