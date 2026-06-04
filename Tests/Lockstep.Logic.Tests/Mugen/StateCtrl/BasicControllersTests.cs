using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M5 第一批：基础 StateController（vel/pos/anim/ctrl/var/statetype）行为 + Ikemen 朝向语义。</summary>
    [TestFixture]
    public sealed class BasicControllersTests
    {
        static readonly MugenExprCompiler C = new MugenExprCompiler();
        static BytecodeExp E(string expr) => C.Compile(expr);
        static FFloat F(int num, int den) => FFloat.FromInt(num) / FFloat.FromInt(den);

        [Test]
        public void VelSet_SetsRawVelocity_NoFacing()
        {
            // VelSet 存原始值，不乘 facing（facing 在物理积分时应用）
            MChar c = new MChar { Facing = -FFloat.One };
            new VelSetController { X = E("2.5"), Y = E("0 - 3") }.Run(c);
            Assert.That(c.Vel.X.Raw, Is.EqualTo(F(5, 2).Raw), "vel.x=2.5 原值(不乘 facing)");
            Assert.That(c.Vel.Y.Raw, Is.EqualTo(FFloat.FromInt(-3).Raw));
        }

        [Test]
        public void VelAdd_Accumulates()
        {
            MChar c = new MChar { Vel = new FVector3(FFloat.FromInt(1), FFloat.FromInt(2), FFloat.Zero) };
            new VelAddController { X = E("3"), Y = E("0 - 5") }.Run(c);
            Assert.That(c.Vel.X.Raw, Is.EqualTo(FFloat.FromInt(4).Raw));
            Assert.That(c.Vel.Y.Raw, Is.EqualTo(FFloat.FromInt(-3).Raw));
        }

        [Test]
        public void PosSet_IsAbsolute_NoFacing()
        {
            // PosSet 绝对坐标（对齐 Ikemen setPosX，不乘 facing）
            MChar c = new MChar { Facing = -FFloat.One, Pos = new FVector3(FFloat.FromInt(99), FFloat.Zero, FFloat.Zero) };
            new PosSetController { X = E("10"), Y = E("20") }.Run(c);
            Assert.That(c.Pos.X.Raw, Is.EqualTo(FFloat.FromInt(10).Raw), "facing=-1 也设绝对 10");
            Assert.That(c.Pos.Y.Raw, Is.EqualTo(FFloat.FromInt(20).Raw));
        }

        [Test]
        public void PosAdd_XIsFacingRelative_YAbsolute()
        {
            // PosAdd：pos.x += x*facing（对齐 Ikemen addX），pos.y += y（绝对）
            MChar right = new MChar { Facing = FFloat.One, Pos = new FVector3(FFloat.FromInt(100), FFloat.FromInt(50), FFloat.Zero) };
            new PosAddController { X = E("5"), Y = E("3") }.Run(right);
            Assert.That(right.Pos.X.Raw, Is.EqualTo(FFloat.FromInt(105).Raw), "朝右 +5");
            Assert.That(right.Pos.Y.Raw, Is.EqualTo(FFloat.FromInt(53).Raw));

            MChar left = new MChar { Facing = -FFloat.One, Pos = new FVector3(FFloat.FromInt(100), FFloat.Zero, FFloat.Zero) };
            new PosAddController { X = E("5") }.Run(left);
            Assert.That(left.Pos.X.Raw, Is.EqualTo(FFloat.FromInt(95).Raw), "朝左 +5*(-1) → 后退到 95");
        }

        [Test]
        public void ChangeAnim_SetsAnimAndPrev()
        {
            MChar c = new MChar { AnimNo = 0 };
            new ChangeAnimController { Value = E("200") }.Run(c);
            Assert.That(c.AnimNo, Is.EqualTo(200));
            Assert.That(c.PrevAnimNo, Is.EqualTo(0), "记录前一动画号");
        }

        // 切到不存在的动画号：保留当前动画（对齐 Ikemen changeAnimEx a==nil → return）。
        // 这是 KFM"跳起浮空"bug 的根因——借用的 common1 切到 KFM 没有的 anim 44，曾把动画运行态冻死。
        static System.Collections.Generic.Dictionary<int, Lockstep.Mugen.Anim.MAnimData> AnimTableWith(params int[] nos)
        {
            System.Collections.Generic.Dictionary<int, Lockstep.Mugen.Anim.MAnimData> table =
                new System.Collections.Generic.Dictionary<int, Lockstep.Mugen.Anim.MAnimData>();
            for (int k = 0; k < nos.Length; k++)
            {
                table[nos[k]] = new Lockstep.Mugen.Anim.MAnimData { No = nos[k], Frames = new Lockstep.Mugen.Anim.MAnimFrame[1] };
            }
            return table;
        }

        [Test]
        public void ChangeAnim_ToMissingAnim_IsNoOp_WhenTablePresent()
        {
            MChar c = new MChar { AnimNo = 41, AnimTable = AnimTableWith(41, 43) };
            new ChangeAnimController { Value = E("44") }.Run(c);   // 44 不在表中
            Assert.That(c.AnimNo, Is.EqualTo(41), "切到不存在的动画应 no-op，保留 41（不冻结）");
        }

        [Test]
        public void ChangeAnim_ToExistingAnim_Switches_WhenTablePresent()
        {
            MChar c = new MChar { AnimNo = 41, AnimTable = AnimTableWith(41, 43, 44) };
            new ChangeAnimController { Value = E("44") }.Run(c);
            Assert.That(c.AnimNo, Is.EqualTo(44), "存在则正常切换");
            Assert.That(c.PrevAnimNo, Is.EqualTo(41));
        }

        [Test]
        public void CtrlSet_SetsControl()
        {
            MChar c = new MChar { Ctrl = false };
            new CtrlSetController { Value = E("1") }.Run(c);
            Assert.IsTrue(c.Ctrl);
            new CtrlSetController { Value = E("0") }.Run(c);
            Assert.IsFalse(c.Ctrl);
        }

        [Test]
        public void VarSet_IntAndFloat()
        {
            MChar c = new MChar();
            new VarSetController { Index = 3, IsFloat = false, Value = E("42") }.Run(c);
            Assert.That(c.IntVars[3], Is.EqualTo(42));
            new VarSetController { Index = 1, IsFloat = true, Value = E("1.5") }.Run(c);
            Assert.That(c.FloatVars[1].Raw, Is.EqualTo(F(3, 2).Raw));
        }

        [Test]
        public void VarAdd_AccumulatesFromZero()
        {
            MChar c = new MChar();
            new VarAddController { Index = 5, Value = E("10") }.Run(c);
            new VarAddController { Index = 5, Value = E("7") }.Run(c);
            Assert.That(c.IntVars[5], Is.EqualTo(17), "未设变量按 0 起算累加");
        }

        [Test]
        public void VarSet_RoundTripsThroughTrigger()
        {
            // VarSet 写入后，var(n) trigger 应读到同值（控制器↔表达式 VM 闭环）
            MChar c = new MChar();
            new VarSetController { Index = 2, Value = E("88") }.Run(c);
            Assert.That(C.Compile("var(2)").Run(c).ToI(), Is.EqualTo(88));
        }

        [Test]
        public void StateTypeSet_ChangesTypes()
        {
            MChar c = new MChar { StateType = 1, MoveType = 1, Ctrl = true };
            // 改为 air(4) + attack(4) + ctrl=0
            new StateTypeSetController { StateType = 4, MoveType = 4, CtrlExpr = E("0") }.Run(c);
            Assert.That(c.StateType, Is.EqualTo(4));
            Assert.That(c.MoveType, Is.EqualTo(4));
            Assert.IsFalse(c.Ctrl);
            // statetype = A 经编译应为真
            Assert.IsTrue(C.Compile("statetype = A").Run(c).ToB());
        }

        [Test]
        public void Null_DoesNothing()
        {
            MChar c = new MChar { Life = 100, StateNo = 5 };
            bool changed = new NullController().Run(c);
            Assert.IsFalse(changed);
            Assert.That(c.Life, Is.EqualTo(100));
            Assert.That(c.StateNo, Is.EqualTo(5));
        }
    }
}
