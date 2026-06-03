using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Expr;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M1：表达式值运算——类型提升/除零/整除/幂/比较/区间/取整，对齐 Ikemen 语义。</summary>
    [TestFixture]
    public sealed class BytecodeOpsTests
    {
        static BytecodeValue I(int i) => BytecodeValue.Int(i);
        static BytecodeValue Ff(int num, int den) => BytecodeValue.Float(FFloat.FromInt(num) / FFloat.FromInt(den));

        [Test]
        public void Add_TypePromotion()
        {
            BytecodeValue ii = BytecodeOps.Add(I(2), I(3));
            Assert.That(ii.Type, Is.EqualTo(ValueType.Int));
            Assert.That(ii.ToI(), Is.EqualTo(5));

            BytecodeValue iflt = BytecodeOps.Add(I(2), Ff(1, 2));   // 2 + 0.5
            Assert.That(iflt.Type, Is.EqualTo(ValueType.Float));
            Assert.That(iflt.ToF().Raw, Is.EqualTo((FFloat.FromInt(5) / FFloat.FromInt(2)).Raw));
        }

        [Test]
        public void Div_IntTruncates_FloatExact_ZeroUndefined()
        {
            Assert.That(BytecodeOps.Div(I(7), I(2)).ToI(), Is.EqualTo(3));        // 整数除法截断
            Assert.That(BytecodeOps.Div(Ff(7, 1), I(2)).ToF().Raw,
                Is.EqualTo((FFloat.FromInt(7) / FFloat.FromInt(2)).Raw));          // 浮点 3.5
            Assert.IsTrue(BytecodeOps.Div(I(5), I(0)).IsUndefined());             // 除零
        }

        [Test]
        public void Mod_And_Pow()
        {
            Assert.That(BytecodeOps.Mod(I(7), I(3)).ToI(), Is.EqualTo(1));
            Assert.IsTrue(BytecodeOps.Mod(I(7), I(0)).IsUndefined());
            Assert.That(BytecodeOps.Pow(I(2), I(10)).ToI(), Is.EqualTo(1024));    // 整数快速幂
            FFloat sqrt2 = BytecodeOps.Pow(Ff(2, 1), Ff(1, 2)).ToF();             // 2^0.5 ≈ 1.414
            Assert.That(sqrt2 > FFloat.FromInt(141) / FFloat.FromInt(100), Is.True);
            Assert.That(sqrt2 < FFloat.FromInt(142) / FFloat.FromInt(100), Is.True);
        }

        [Test]
        public void Comparisons()
        {
            Assert.IsTrue(BytecodeOps.Gt(I(5), I(3)).ToB());
            Assert.IsFalse(BytecodeOps.Lt(I(5), I(3)).ToB());
            Assert.IsTrue(BytecodeOps.Eq(I(4), I(4)).ToB());
            Assert.IsTrue(BytecodeOps.Ge(Ff(5, 2), Ff(5, 2)).ToB());
            Assert.IsTrue(BytecodeOps.Ne(I(1), I(2)).ToB());
        }

        [Test]
        public void Logical_And_Bitwise()
        {
            Assert.IsTrue(BytecodeOps.BlAnd(I(1), I(1)).ToB());
            Assert.IsFalse(BytecodeOps.BlAnd(I(1), I(0)).ToB());
            Assert.IsTrue(BytecodeOps.BlOr(I(0), I(1)).ToB());
            Assert.IsTrue(BytecodeOps.BlXor(I(1), I(0)).ToB());
            Assert.That(BytecodeOps.And(I(6), I(3)).ToI(), Is.EqualTo(2));   // 0b110 & 0b011 = 0b010
            Assert.That(BytecodeOps.Or(I(6), I(1)).ToI(), Is.EqualTo(7));
            Assert.That(BytecodeOps.Xor(I(6), I(3)).ToI(), Is.EqualTo(5));
        }

        [Test]
        public void Range_InclusiveExclusive()
        {
            // [2,5]
            Assert.IsTrue(BytecodeOps.Range(I(2), I(2), I(5), true, true).ToB());
            Assert.IsTrue(BytecodeOps.Range(I(5), I(2), I(5), true, true).ToB());
            Assert.IsFalse(BytecodeOps.Range(I(1), I(2), I(5), true, true).ToB());
            // (2,5)
            Assert.IsFalse(BytecodeOps.Range(I(2), I(2), I(5), false, false).ToB());
            Assert.IsTrue(BytecodeOps.Range(I(3), I(2), I(5), false, false).ToB());
            // undefined 透传
            Assert.IsTrue(BytecodeOps.Range(BytecodeValue.Undefined(), I(2), I(5), true, true).IsUndefined());
        }

        [Test]
        public void FloorCeil_OnlyAffectFloat()
        {
            Assert.That(BytecodeOps.Floor(Ff(37, 10)).ToI(), Is.EqualTo(3));    // 3.7→3
            Assert.That(BytecodeOps.Ceil(Ff(32, 10)).ToI(), Is.EqualTo(4));     // 3.2→4
            Assert.That(BytecodeOps.Floor(Ff(-3, 2)).ToI(), Is.EqualTo(-2));    // -1.5→-2 (floor)
            BytecodeValue intVal = BytecodeOps.Floor(I(7));                      // 整数原样
            Assert.That(intVal.Type, Is.EqualTo(ValueType.Int));
            Assert.That(intVal.ToI(), Is.EqualTo(7));
        }

        [Test]
        public void Unary_Neg_Abs_Not()
        {
            Assert.That(BytecodeOps.Neg(I(5)).ToI(), Is.EqualTo(-5));
            Assert.That(BytecodeOps.Abs(I(-9)).ToI(), Is.EqualTo(9));
            Assert.That(BytecodeOps.Abs(Ff(-7, 2)).ToF().Raw, Is.EqualTo((FFloat.FromInt(7) / FFloat.FromInt(2)).Raw));
            Assert.That(BytecodeOps.Not(I(0)).ToI(), Is.EqualTo(-1));           // ~0 = -1
            Assert.IsFalse(BytecodeOps.BlNot(I(1)).ToB());
        }

        [Test]
        public void Trig_Smoke()
        {
            // cos(0)=1, sin(0)=0（容差，定点近似）
            Assert.That(BytecodeOps.Cos(BytecodeValue.Float(FFloat.Zero)).ToF().Raw,
                Is.EqualTo(FFloat.One.Raw).Within(FFloat.Epsilon.Raw * 64));
            Assert.That(BytecodeOps.Sin(BytecodeValue.Float(FFloat.Zero)).ToF().Raw,
                Is.EqualTo(0L).Within(FFloat.Epsilon.Raw * 64));
        }
    }
}
