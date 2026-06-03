using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Parse;

namespace Lockstep.Tests.Mugen
{
    /// <summary>修复5-1：const(...) 角色常量 —— 编译 + 读取 + CNS 段解析 + 默认值。</summary>
    [TestFixture]
    public sealed class ConstTests
    {
        static readonly MugenExprCompiler C = new MugenExprCompiler();

        static BytecodeValue Eval(string expr, MChar c)
        {
            return C.Compile(expr).Run(c);
        }

        // ───────── 编译 + 读取 ─────────

        [Test]
        public void Const_ReadsIntAndFloatFields()
        {
            MConstants k = new MConstants { Life = 1200, Attack = 90, WalkFwd = FFloat.FromInt(2) };
            MChar c = new MChar { Constants = k };
            Assert.That(Eval("const(data.life)", c).ToI(), Is.EqualTo(1200), "整数常量");
            Assert.That(Eval("const(data.attack)", c).ToI(), Is.EqualTo(90));
            Assert.That(Eval("const(velocity.walk.fwd.x)", c).ToF().Raw, Is.EqualTo(FFloat.FromInt(2).Raw), "定点常量");
        }

        [Test]
        public void Const_UsableInExpression()
        {
            MConstants k = new MConstants { Attack = 100 };
            MChar c = new MChar { Constants = k };
            // const 参与算术/比较
            Assert.That(Eval("const(data.attack) * 2", c).ToI(), Is.EqualTo(200));
            Assert.That(Eval("const(data.attack) >= 100", c).ToB(), Is.True);
        }

        [Test]
        public void Const_NoConstants_ReturnsZero()
        {
            MChar c = new MChar { Constants = null };
            Assert.That(Eval("const(data.life)", c).ToI(), Is.EqualTo(0), "无常量集 → 0（容错）");
        }

        [Test]
        public void Const_UnknownField_ReturnsZero()
        {
            MChar c = new MChar { Constants = new MConstants { Life = 999 } };
            Assert.That(Eval("const(data.nonexistent.field)", c).ToI(), Is.EqualTo(0), "未知字段 → 0");
        }

        // ───────── 默认值（对齐 Ikemen init）─────────

        [Test]
        public void Defaults_MatchIkemen()
        {
            MConstants k = new MConstants();
            Assert.That(k.Life, Is.EqualTo(1000));
            Assert.That(k.Power, Is.EqualTo(3000));
            Assert.That(k.Attack, Is.EqualTo(100));
            Assert.That(k.Defence, Is.EqualTo(100));
            Assert.That(k.SizeHeight.Raw, Is.EqualTo(FFloat.FromInt(60).Raw));
            Assert.That(k.HeadPosY.Raw, Is.EqualTo(FFloat.FromInt(-90).Raw));
        }

        // ───────── CNS 段解析 ─────────

        [Test]
        public void ConstParser_ParsesAllSections()
        {
            string cns =
                "[Data]\nlife = 1200\nattack = 110\ndefence = 95\nfall.defence_up = 40\nairjuggle = 12\n" +
                "[Size]\nground.back = 15\nground.front = 16\nheight = 60\nhead.pos = -5, -90\n" +
                "[Velocity]\nwalk.fwd = 2.4\nwalk.back = -2.2\nrun.fwd = 4.6, 0\njump.neu = 0,-8.4\njump.fwd = 2.5\n" +
                "[Movement]\nyaccel = .44\nstand.friction = .85\nairjump.num = 1\n";
            MConstants k = MugenConstParser.Parse(cns);

            Assert.That(k.Life, Is.EqualTo(1200));
            Assert.That(k.Attack, Is.EqualTo(110));
            Assert.That(k.Defence, Is.EqualTo(95));
            Assert.That(k.FallDefenceUp, Is.EqualTo(40));
            Assert.That(k.Airjuggle, Is.EqualTo(12));
            Assert.That(k.SizeGroundBack.Raw, Is.EqualTo(FFloat.FromInt(15).Raw));
            Assert.That(k.SizeGroundFront.Raw, Is.EqualTo(FFloat.FromInt(16).Raw));
            Assert.That(k.HeadPosX.Raw, Is.EqualTo(FFloat.FromInt(-5).Raw));
            Assert.That(k.HeadPosY.Raw, Is.EqualTo(FFloat.FromInt(-90).Raw));
            Assert.That(k.AirjumpNum, Is.EqualTo(1));
        }

        [Test]
        public void ConstParser_JumpNeuY_MapsToJumpY()
        {
            // 关键映射：const(velocity.jump.y) = jump.neu 的 y 分量（对齐 Ikemen）
            string cns = "[Velocity]\njump.neu = 0,-8.4\n";
            MConstants k = MugenConstParser.Parse(cns);
            MChar c = new MChar { Constants = k };

            FFloat expected = FFloat.FromInt(0) - (FFloat.FromInt(84) / FFloat.FromInt(10));   // -8.4
            Assert.That(c.Constants.JumpY.Raw, Is.EqualTo(expected.Raw), "jump.neu.y → JumpY");
            Assert.That(Eval("const(velocity.jump.y)", c).ToF().Raw, Is.EqualTo(expected.Raw));
            Assert.That(Eval("const(velocity.jump.neu.x)", c).ToF().Raw, Is.EqualTo(FFloat.Zero.Raw));
        }

        [Test]
        public void ConstParser_NegativeAndDecimalValues()
        {
            string cns = "[Velocity]\nwalk.back = -2.2\nrun.back = -4.5,-3.8\n";
            MConstants k = MugenConstParser.Parse(cns);

            FFloat walkBack = FFloat.Zero - (FFloat.FromInt(22) / FFloat.FromInt(10));   // -2.2
            FFloat runBackY = FFloat.Zero - (FFloat.FromInt(38) / FFloat.FromInt(10));   // -3.8
            Assert.That(k.WalkBack.Raw, Is.EqualTo(walkBack.Raw), "负小数");
            Assert.That(k.RunBackY.Raw, Is.EqualTo(runBackY.Raw), "逗号对的第二项");
        }

        [Test]
        public void ConstParser_OmittedFields_KeepDefaults()
        {
            string cns = "[Data]\nlife = 1500\n";   // 只给 life
            MConstants k = MugenConstParser.Parse(cns);
            Assert.That(k.Life, Is.EqualTo(1500), "给出的覆盖");
            Assert.That(k.Power, Is.EqualTo(3000), "未给的保留默认");
            Assert.That(k.Attack, Is.EqualTo(100));
        }
    }
}
