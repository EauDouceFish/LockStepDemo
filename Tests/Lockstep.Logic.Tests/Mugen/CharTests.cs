using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M3：Char 骨架——trigger 读取接入表达式 VM + Clone/WriteHash 回滚支持。</summary>
    [TestFixture]
    public sealed class CharTests
    {
        sealed class Emit
        {
            readonly List<byte> _b = new List<byte>();
            public Emit Int(int v) { _b.Add((byte)OpCode.OC_int); for (int k = 0; k < 4; k++) _b.Add((byte)(v >> (8 * k))); return this; }
            public Emit Op(OpCode op) { _b.Add((byte)op); return this; }
            public BytecodeValue Run(MChar c) => new BytecodeExp(_b.ToArray()).Run(c);
        }

        [Test]
        public void TimeTrigger_ReadsCharTime()
        {
            MChar c = new MChar { Time = 5 };
            Assert.That(new Emit().Op(OpCode.OC_time).Run(c).ToI(), Is.EqualTo(5));
        }

        [Test]
        public void TimeComparison_EndToEnd()
        {
            // 表达式 "time >= 2"
            MChar c = new MChar { Time = 5 };
            Assert.IsTrue(new Emit().Op(OpCode.OC_time).Int(2).Op(OpCode.OC_ge).Run(c).ToB());
            c.Time = 1;
            Assert.IsFalse(new Emit().Op(OpCode.OC_time).Int(2).Op(OpCode.OC_ge).Run(c).ToB());
        }

        [Test]
        public void Var_ReadsIntVars()
        {
            MChar c = new MChar();
            c.IntVars[3] = 42;
            // var(3)
            Assert.That(new Emit().Int(3).Op(OpCode.OC_var).Run(c).ToI(), Is.EqualTo(42));
            // 未设的 var(7) → 0
            Assert.That(new Emit().Int(7).Op(OpCode.OC_var).Run(c).ToI(), Is.EqualTo(0));
        }

        [Test]
        public void PosX_ReadsFloat()
        {
            MChar c = new MChar { Pos = new FVector3(FFloat.FromInt(5) / FFloat.FromInt(2), FFloat.Zero, FFloat.Zero) };
            BytecodeValue r = new Emit().Op(OpCode.OC_pos_x).Run(c);
            Assert.That(r.Type, Is.EqualTo(ValueType.Float));
            Assert.That(r.ToF().Raw, Is.EqualTo((FFloat.FromInt(5) / FFloat.FromInt(2)).Raw));
        }

        [Test]
        public void Alive_FromLife()
        {
            Assert.IsTrue(new Emit().Op(OpCode.OC_alive).Run(new MChar { Life = 100 }).ToB());
            Assert.IsFalse(new Emit().Op(OpCode.OC_alive).Run(new MChar { Life = 0 }).ToB());
        }

        [Test]
        public void UnwiredTrigger_IsUndefined()
        {
            // OC_gametime 尚未接入 → Undefined（增量补全）
            Assert.IsTrue(new Emit().Op(OpCode.OC_gametime).Run(new MChar()).IsUndefined());
        }

        [Test]
        public void Clone_IsDeepCopy()
        {
            MChar c = new MChar { StateNo = 200, Life = 500 };
            c.IntVars[1] = 9;
            MChar clone = c.Clone();
            c.StateNo = 0;
            c.IntVars[1] = 99;

            Assert.That(clone.StateNo, Is.EqualTo(200));
            Assert.That(clone.IntVars[1], Is.EqualTo(9), "IntVars 应深拷");
        }

        static ulong HashOf(MChar c)
        {
            Hash64 h = new Hash64();
            c.WriteHash(ref h);
            return h.Value;
        }

        [Test]
        public void WriteHash_StableAndSensitive()
        {
            MChar a = new MChar { StateNo = 200, Time = 3, Life = 500 };
            MChar b = new MChar { StateNo = 200, Time = 3, Life = 500 };
            Assert.That(HashOf(a), Is.EqualTo(HashOf(b)), "相同状态哈希相同");

            b.Time = 4;
            Assert.That(HashOf(a), Is.Not.EqualTo(HashOf(b)), "状态变化哈希应变");
        }
    }
}
