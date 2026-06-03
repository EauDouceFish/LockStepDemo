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
            // redirect：op + 4字节小端块长(供 redirect 失败时跳过整块)
            public Emit Rd(OpCode op, int blockLen) { _b.Add((byte)op); for (int k = 0; k < 4; k++) _b.Add((byte)(blockLen >> (8 * k))); return this; }
            // OC_run：4字节小端子块长 + 后续子字节码
            public Emit RunBlock(int subLen) { _b.Add((byte)OpCode.OC_run); for (int k = 0; k < 4; k++) _b.Add((byte)(subLen >> (8 * k))); return this; }
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
        public void SystemVarTriggers_ReadFields()
        {
            MChar c = new MChar
            {
                Id = 13, PalNo = 4, Hitstop = 6,
                HitCount = 3, UniqHitCount = 2,
                MoveContact = 1, MoveHit = 1, MoveGuarded = 0, MoveReversed = 0,
                AnimTime = -5,
            };
            Assert.That(new Emit().Op(OpCode.OC_id).Run(c).ToI(), Is.EqualTo(13), "id");
            Assert.That(new Emit().Op(OpCode.OC_palno).Run(c).ToI(), Is.EqualTo(4), "palno");
            Assert.That(new Emit().Op(OpCode.OC_hitpausetime).Run(c).ToI(), Is.EqualTo(6), "hitpausetime=Hitstop");
            Assert.That(new Emit().Op(OpCode.OC_hitcount).Run(c).ToI(), Is.EqualTo(3), "hitcount");
            Assert.That(new Emit().Op(OpCode.OC_uniqhitcount).Run(c).ToI(), Is.EqualTo(2), "uniqhitcount");
            Assert.That(new Emit().Op(OpCode.OC_movecontact).Run(c).ToI(), Is.EqualTo(1), "movecontact");
            Assert.That(new Emit().Op(OpCode.OC_movehit).Run(c).ToI(), Is.EqualTo(1), "movehit");
            Assert.That(new Emit().Op(OpCode.OC_animtime).Run(c).ToI(), Is.EqualTo(-5), "animtime");
        }

        [Test]
        public void NumTarget_CountsTargets()
        {
            MChar c = new MChar();
            Assert.That(new Emit().Op(OpCode.OC_numtarget).Run(c).ToI(), Is.EqualTo(0));
            c.Targets.Add(new MChar { Id = 1 });
            c.Targets.Add(new MChar { Id = 2 });
            Assert.That(new Emit().Op(OpCode.OC_numtarget).Run(c).ToI(), Is.EqualTo(2));
        }

        // ───────── redirect（M3 补全：root/parent/p2/target 切上下文）─────────

        [Test]
        public void RedirectP2_ReadsOpponentField()
        {
            // p2, life  →  OC_p2 [块长1] OC_life ：p2 有效则后续 OC_life 读对手
            MChar opp = new MChar { Life = 300 };
            MChar c = new MChar { Life = 1000, P2 = opp };
            Assert.That(new Emit().Rd(OpCode.OC_p2, 1).Op(OpCode.OC_life).Run(c).ToI(), Is.EqualTo(300));
        }

        [Test]
        public void RedirectP2_NullIsUndefined()
        {
            // 无 P2 → redirect 失败，压 Undefined 并跳过整块
            MChar c = new MChar { Life = 1000 };
            Assert.IsTrue(new Emit().Rd(OpCode.OC_p2, 1).Op(OpCode.OC_life).Run(c).IsUndefined());
        }

        [Test]
        public void RedirectRoot_CompoundViaRunBlock()
        {
            // root, (life + 100)：OC_root 后用 OC_run 跑子块(life+100)以重定向后的上下文
            // 子块 = OC_life(1) + OC_int(5) + OC_add(1) = 7 字节；root 块 = OC_run(1)+len(4)+子块(7) = 12 字节
            MChar root = new MChar { Life = 500 };
            MChar c = new MChar { Root = root };
            int r = new Emit().Rd(OpCode.OC_root, 12).RunBlock(7).Op(OpCode.OC_life).Int(100).Op(OpCode.OC_add).Run(c).ToI();
            Assert.That(r, Is.EqualTo(600));
        }

        [Test]
        public void RedirectTarget_FirstTarget()
        {
            // target, life：OC_target 弹 id(-1=任意→第一个)；命中目标 Life=222
            MChar tgt = new MChar { Id = 7, Life = 222 };
            MChar c = new MChar();
            c.Targets.Add(tgt);
            int r = new Emit().Int(-1).Rd(OpCode.OC_target, 1).Op(OpCode.OC_life).Run(c).ToI();
            Assert.That(r, Is.EqualTo(222));
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
