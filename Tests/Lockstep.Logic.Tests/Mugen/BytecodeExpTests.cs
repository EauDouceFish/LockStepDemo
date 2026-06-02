using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Expr;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M1：字节码 VM 执行器——手搓程序跑算术/比较/三目/区间/常量编码。</summary>
    [TestFixture]
    public sealed class BytecodeExpTests
    {
        // 简易字节程序构造器（编码与 BytecodeExp.Run 约定一致；M2 编译器将产出同样编码）
        sealed class Emit
        {
            readonly List<byte> _b = new List<byte>();
            public Emit Int(int v) { _b.Add((byte)OpCode.OC_int); for (int k = 0; k < 4; k++) _b.Add((byte)(v >> (8 * k))); return this; }
            public Emit Int8(sbyte v) { _b.Add((byte)OpCode.OC_int8); _b.Add((byte)v); return this; }
            public Emit Float(FFloat v) { _b.Add((byte)OpCode.OC_float); long r = v.Raw; for (int k = 0; k < 8; k++) _b.Add((byte)(r >> (8 * k))); return this; }
            public Emit Op(OpCode op) { _b.Add((byte)op); return this; }
            public BytecodeValue Run() { return new BytecodeExp(_b.ToArray()).Run(null); }
        }

        static FFloat F(int num, int den) => FFloat.FromInt(num) / FFloat.FromInt(den);

        [Test]
        public void Arithmetic_Precedence_ViaStack()
        {
            // (2+3)*4 = 20
            BytecodeValue r = new Emit().Int(2).Int(3).Op(OpCode.OC_add).Int(4).Op(OpCode.OC_mul).Run();
            Assert.That(r.ToI(), Is.EqualTo(20));
        }

        [Test]
        public void IntDivision_Truncates()
        {
            Assert.That(new Emit().Int(7).Int(2).Op(OpCode.OC_div).Run().ToI(), Is.EqualTo(3));
        }

        [Test]
        public void Float_Add()
        {
            // 1.5 + 2.5 = 4.0
            BytecodeValue r = new Emit().Float(F(3, 2)).Float(F(5, 2)).Op(OpCode.OC_add).Run();
            Assert.That(r.Type, Is.EqualTo(ValueType.Float));
            Assert.That(r.ToF().Raw, Is.EqualTo(FFloat.FromInt(4).Raw));
        }

        [Test]
        public void Comparison()
        {
            Assert.IsTrue(new Emit().Int(5).Int(3).Op(OpCode.OC_gt).Run().ToB());
            Assert.IsFalse(new Emit().Int(5).Int(3).Op(OpCode.OC_lt).Run().ToB());
        }

        [Test]
        public void IfElse_PicksByCondition()
        {
            // cond=1 → trueVal(10)
            Assert.That(new Emit().Int(1).Int(10).Int(20).Op(OpCode.OC_ifelse).Run().ToI(), Is.EqualTo(10));
            // cond=0 → falseVal(20)
            Assert.That(new Emit().Int(0).Int(10).Int(20).Op(OpCode.OC_ifelse).Run().ToI(), Is.EqualTo(20));
        }

        [Test]
        public void Range_Inclusive()
        {
            // 3 in [2,5]
            Assert.IsTrue(new Emit().Int(3).Int(2).Int(5).Op(OpCode.OC_range_ii).Run().ToB());
            // 5 not in (2,5)
            Assert.IsFalse(new Emit().Int(5).Int(2).Int(5).Op(OpCode.OC_range_ee).Run().ToB());
        }

        [Test]
        public void Int8_Encoding()
        {
            Assert.That(new Emit().Int8(5).Int8(3).Op(OpCode.OC_sub).Run().ToI(), Is.EqualTo(2));
            Assert.That(new Emit().Int8(-7).Op(OpCode.OC_abs).Run().ToI(), Is.EqualTo(7));
        }

        [Test]
        public void TriggerOpcode_WithoutContext_IsUndefined()
        {
            // OC_time 依赖 Char，无 context → Undefined（M3 接入后才有值）
            Assert.IsTrue(new Emit().Op(OpCode.OC_time).Run().IsUndefined());
        }
    }
}
