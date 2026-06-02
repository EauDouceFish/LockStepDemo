using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Game.Expr;
using Lockstep.Math;

namespace Lockstep.Tests
{
    /// <summary>T0.4：表达式 VM 最小实现验收（常量 / Time / 比较 / 四则 / 括号）。</summary>
    [TestFixture]
    public sealed class ExpressionVmTests
    {
        readonly ExpressionVM _vm = new ExpressionVM();

        sealed class FakeContext : IEvalContext
        {
            readonly Dictionary<string, FFloat> _map;

            public FakeContext(Dictionary<string, FFloat> map)
            {
                _map = map;
            }

            public bool TryGetTrigger(string name, out FFloat value)
            {
                // MUGEN trigger 名大小写不敏感
                return _map.TryGetValue(name.ToLowerInvariant(), out value);
            }

            public bool TryGetFunction(string name, FFloat[] args, out FFloat value)
            {
                // 测试桩：var(n) 返回 n*10，便于断言
                if (name == "var" && args.Length == 1)
                {
                    value = args[0] * FFloat.FromInt(10);
                    return true;
                }
                value = FFloat.Zero;
                return false;
            }
        }

        static IEvalContext WithTime(int time)
        {
            return new FakeContext(new Dictionary<string, FFloat> { ["time"] = FFloat.FromInt(time) });
        }

        [Test]
        public void Constant_Evaluates()
        {
            Assert.That(_vm.Compile("5").Eval(WithTime(0)).Raw, Is.EqualTo(FFloat.FromInt(5).Raw));
        }

        [Test]
        public void TimeEqualsZero_TrueAtFrame0_FalseLater()
        {
            IExpr expr = _vm.Compile("Time = 0");
            Assert.IsTrue(expr.EvalBool(WithTime(0)));
            Assert.IsFalse(expr.EvalBool(WithTime(5)));
        }

        [Test]
        public void TimeGreaterEqualThree()
        {
            IExpr expr = _vm.Compile("Time >= 3");
            Assert.IsFalse(expr.EvalBool(WithTime(2)));
            Assert.IsTrue(expr.EvalBool(WithTime(3)));
        }

        [Test]
        public void ArithmeticPrecedence_AndParens()
        {
            Assert.That(_vm.Compile("2 + 3 * 4").Eval(WithTime(0)).Raw, Is.EqualTo(FFloat.FromInt(14).Raw));
            Assert.That(_vm.Compile("(2 + 3) * 4").Eval(WithTime(0)).Raw, Is.EqualTo(FFloat.FromInt(20).Raw));
        }

        [Test]
        public void UnknownTrigger_Throws()
        {
            IExpr expr = _vm.Compile("NoSuchTrigger");
            Assert.Throws<ExprException>(() => expr.Eval(WithTime(0)));
        }

        [Test]
        public void LogicalAndOr_Combine()
        {
            Assert.IsTrue(_vm.Compile("Time >= 0 && Time < 10").EvalBool(WithTime(3)));
            Assert.IsFalse(_vm.Compile("Time > 5 && Time < 10").EvalBool(WithTime(3)));
            Assert.IsTrue(_vm.Compile("Time = 0 || Time = 3").EvalBool(WithTime(3)));
        }

        [Test]
        public void LogicalNot()
        {
            Assert.IsTrue(_vm.Compile("!(Time = 5)").EvalBool(WithTime(3)));
            Assert.IsFalse(_vm.Compile("!(Time = 3)").EvalBool(WithTime(3)));
        }

        [Test]
        public void Precedence_OrLowerThanAnd()
        {
            // 0 && 1 || 1 → (0&&1)||1 → 1
            Assert.IsTrue(_vm.Compile("0 && 1 || 1").EvalBool(WithTime(0)));
            // 1 || 1 && 0 → 1 || (1&&0) → 1
            Assert.IsTrue(_vm.Compile("1 || 1 && 0").EvalBool(WithTime(0)));
        }

        [Test]
        public void BuiltinFunctions()
        {
            Assert.That(_vm.Compile("abs(0 - 7)").Eval(WithTime(0)).Raw, Is.EqualTo(FFloat.FromInt(7).Raw));
            Assert.That(_vm.Compile("ifelse(1, 4, 9)").Eval(WithTime(0)).Raw, Is.EqualTo(FFloat.FromInt(4).Raw));
            Assert.That(_vm.Compile("ifelse(0, 4, 9)").Eval(WithTime(0)).Raw, Is.EqualTo(FFloat.FromInt(9).Raw));
            Assert.That(_vm.Compile("min(3, 8)").Eval(WithTime(0)).Raw, Is.EqualTo(FFloat.FromInt(3).Raw));
            Assert.That(_vm.Compile("max(3, 8)").Eval(WithTime(0)).Raw, Is.EqualTo(FFloat.FromInt(8).Raw));
        }

        [Test]
        public void ParameterizedTrigger_GoesToContext()
        {
            // FakeContext: var(n) = n*10
            Assert.That(_vm.Compile("var(5)").Eval(WithTime(0)).Raw, Is.EqualTo(FFloat.FromInt(50).Raw));
        }
    }
}
