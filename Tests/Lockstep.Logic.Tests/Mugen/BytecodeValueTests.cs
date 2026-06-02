using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Expr;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M0：BytecodeValue 定点版——类型转换 + 向零截断（对齐 Ikemen int32(float)）。</summary>
    [TestFixture]
    public sealed class BytecodeValueTests
    {
        static FFloat F(int num, int den)
        {
            return FFloat.FromInt(num) / FFloat.FromInt(den);
        }

        [Test]
        public void Int_Conversions()
        {
            BytecodeValue v = BytecodeValue.Int(42);
            Assert.That(v.Type, Is.EqualTo(ValueType.Int));
            Assert.That(v.ToI(), Is.EqualTo(42));
            Assert.That(v.ToF().Raw, Is.EqualTo(FFloat.FromInt(42).Raw));
            Assert.IsTrue(v.ToB());
            Assert.IsFalse(BytecodeValue.Int(0).ToB());
        }

        [Test]
        public void Float_TruncatesTowardZero()
        {
            // 3.5 → 3
            Assert.That(BytecodeValue.Float(F(7, 2)).ToI(), Is.EqualTo(3));
            // -3.5 → -3（向零截断；floor 会得 -4）
            Assert.That(BytecodeValue.Float(F(-7, 2)).ToI(), Is.EqualTo(-3));
            // -2.0 整数 → -2
            Assert.That(BytecodeValue.Float(FFloat.FromInt(-2)).ToI(), Is.EqualTo(-2));
            // 0.9 → 0, -0.9 → 0
            Assert.That(BytecodeValue.Float(F(9, 10)).ToI(), Is.EqualTo(0));
            Assert.That(BytecodeValue.Float(F(-9, 10)).ToI(), Is.EqualTo(0));
        }

        [Test]
        public void Float_ToFAndToB()
        {
            BytecodeValue v = BytecodeValue.Float(F(7, 2));
            Assert.That(v.ToF().Raw, Is.EqualTo(F(7, 2).Raw));
            Assert.IsTrue(v.ToB());
            Assert.IsFalse(BytecodeValue.Float(FFloat.Zero).ToB());
        }

        [Test]
        public void Bool_Conversions()
        {
            Assert.IsTrue(BytecodeValue.Bool(true).ToB());
            Assert.That(BytecodeValue.Bool(true).ToI(), Is.EqualTo(1));
            Assert.That(BytecodeValue.Bool(false).ToI(), Is.EqualTo(0));
            Assert.That(BytecodeValue.Bool(true).Type, Is.EqualTo(ValueType.Bool));
        }

        [Test]
        public void Undefined_IsZeroAndFalse()
        {
            BytecodeValue u = BytecodeValue.Undefined();
            Assert.IsTrue(u.IsUndefined());
            Assert.That(u.ToI(), Is.EqualTo(0));
            Assert.That(u.ToF().Raw, Is.EqualTo(FFloat.Zero.Raw));
            Assert.IsFalse(u.ToB());
        }

        [Test]
        public void None_IsNone()
        {
            Assert.IsTrue(BytecodeValue.None().IsNone());
            Assert.IsFalse(BytecodeValue.Int(1).IsNone());
        }
    }
}
