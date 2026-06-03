using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;

namespace Lockstep.Tests.Mugen
{
    /// <summary>修复1：gethitvar(...) 触发器编译 + 从 Ghv 读取（gethit 状态读受击反应）。</summary>
    [TestFixture]
    public sealed class GetHitVarTriggerTests
    {
        static readonly MugenExprCompiler C = new MugenExprCompiler();

        [Test]
        public void GetHitVar_IntFields()
        {
            MChar c = new MChar();
            c.Ghv.HitTime = 14;
            c.Ghv.SlideTime = 5;
            c.Ghv.Damage = 80;
            c.Ghv.AnimType = 1;
            Assert.That(C.Compile("gethitvar(hittime)").Run(c).ToI(), Is.EqualTo(14));
            Assert.That(C.Compile("gethitvar(slidetime)").Run(c).ToI(), Is.EqualTo(5));
            Assert.That(C.Compile("gethitvar(damage)").Run(c).ToI(), Is.EqualTo(80));
            Assert.That(C.Compile("gethitvar(animtype)").Run(c).ToI(), Is.EqualTo(1));
        }

        [Test]
        public void GetHitVar_FloatFields()
        {
            MChar c = new MChar();
            c.Ghv.XVel = FFloat.FromInt(-4);
            c.Ghv.YVel = FFloat.FromInt(7) / FFloat.FromInt(2);
            BytecodeValue x = C.Compile("gethitvar(xvel)").Run(c);
            Assert.That(x.Type, Is.EqualTo(ValueType.Float));
            Assert.That(x.ToF().Raw, Is.EqualTo(FFloat.FromInt(-4).Raw));
            Assert.That(C.Compile("gethitvar(yvel)").Run(c).ToF().Raw,
                Is.EqualTo((FFloat.FromInt(7) / FFloat.FromInt(2)).Raw));
        }

        [Test]
        public void GetHitVar_BoolFall()
        {
            MChar c = new MChar();
            c.Ghv.Fall = true;
            Assert.IsTrue(C.Compile("gethitvar(fall)").Run(c).ToB());
            c.Ghv.Fall = false;
            Assert.IsFalse(C.Compile("gethitvar(fall)").Run(c).ToB());
        }

        [Test]
        public void GetHitVar_UsableInTriggerExpression()
        {
            // gethit 状态典型用法：gethitvar(hittime) <= 0 判断硬直结束
            MChar c = new MChar();
            c.Ghv.HitTime = 0;
            Assert.IsTrue(C.Compile("gethitvar(hittime) <= 0").Run(c).ToB());
            c.Ghv.HitTime = 5;
            Assert.IsFalse(C.Compile("gethitvar(hittime) <= 0").Run(c).ToB());
        }
    }
}
