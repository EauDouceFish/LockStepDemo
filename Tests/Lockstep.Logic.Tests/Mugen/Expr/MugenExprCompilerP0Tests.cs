using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lockstep.Mugen.Expr;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class MugenExprCompilerP0Tests
    {
        sealed class ProbeContext : IExprContext, IExprVariableContext, IExprTwoArgumentRedirectContext
        {
            readonly int _life;

            public ProbeContext(int life = 0)
            {
                _life = life;
            }

            public int RandomReads { get; private set; }
            public OpCode RedirectOp { get; private set; }
            public int RedirectId { get; private set; }
            public int RedirectIndex { get; private set; }
            public ProbeContext RedirectTarget { get; set; }

            public BytecodeValue ReadTrigger(
                OpCode op,
                byte[] code,
                ref int i,
                List<BytecodeValue> stack)
            {
                switch (op)
                {
                    case OpCode.OC_random:
                        RandomReads++;
                        return BytecodeValue.Int(RandomReads);
                    case OpCode.OC_life:
                        return BytecodeValue.Int(_life);
                    default:
                        return BytecodeValue.Undefined();
                }
            }

            public BytecodeValue ReadVariable(OpCode op, int index)
            {
                switch (op)
                {
                    case OpCode.OC_var: return BytecodeValue.Int(100 + index);
                    case OpCode.OC_sysvar: return BytecodeValue.Int(200 + index);
                    case OpCode.OC_fvar: return BytecodeValue.Int(300 + index);
                    case OpCode.OC_sysfvar: return BytecodeValue.Int(400 + index);
                    default: return BytecodeValue.Undefined();
                }
            }

            public IExprContext Redirect(OpCode op, List<BytecodeValue> stack)
            {
                Assert.Fail("The legacy redirect contract must not be used for target/helper.");
                return null;
            }

            public IExprContext Redirect(
                OpCode op,
                BytecodeValue firstArgument,
                BytecodeValue secondArgument)
            {
                RedirectOp = op;
                RedirectId = firstArgument.ToI();
                RedirectIndex = secondArgument.ToI();
                return RedirectTarget;
            }
        }

        static MugenExprCompileResult Strict(string expression)
        {
            return new MugenExprCompiler().CompileStrict(expression);
        }

        [TestCase("1 2", MugenExprDiagnosticCode.TrailingToken)]
        [TestCase("(1 + 2", MugenExprDiagnosticCode.MissingToken)]
        [TestCase("1 + )", MugenExprDiagnosticCode.UnexpectedToken)]
        [TestCase("unknown_trigger", MugenExprDiagnosticCode.UnknownIdentifier)]
        [TestCase("unknown_function(1)", MugenExprDiagnosticCode.UnknownFunction)]
        [TestCase("pos z", MugenExprDiagnosticCode.InvalidAxis)]
        [TestCase("const(data.not_a_field)", MugenExprDiagnosticCode.UnknownField)]
        [TestCase("\"unterminated", MugenExprDiagnosticCode.UnterminatedString)]
        public void StrictCompilation_ReturnsStructuredErrors(
            string expression,
            MugenExprDiagnosticCode expectedCode)
        {
            MugenExprCompileResult result = Strict(expression);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(expectedCode));
            Assert.That(result.Diagnostics.All(d => d.Position >= 0), Is.True);
        }

        [Test]
        public void ExistingCompileApi_KeepsRecoveryBehavior()
        {
            BytecodeExp expression = new MugenExprCompiler().Compile("unknown_trigger");

            Assert.That(expression, Is.Not.Null);
            Assert.That(expression.Run(null).ToI(), Is.Zero);
        }

        [Test]
        public void BooleanOperators_ShortCircuitRightHandSide()
        {
            MugenExprCompiler compiler = new MugenExprCompiler();
            ProbeContext context = new ProbeContext();

            Assert.That(compiler.Compile("0 && random").Run(context).ToB(), Is.False);
            Assert.That(compiler.Compile("1 || random").Run(context).ToB(), Is.True);
            Assert.That(context.RandomReads, Is.Zero);

            Assert.That(compiler.Compile("1 && random").Run(context).ToI(), Is.EqualTo(1));
            Assert.That(context.RandomReads, Is.EqualTo(1));
        }

        [Test]
        public void BooleanShortCircuit_UsesLongJumpWhenRequired()
        {
            string longRightHandSide = string.Join(" + ", Enumerable.Repeat("1", 100));
            BytecodeExp expression = new MugenExprCompiler()
                .Compile("0 && (" + longRightHandSide + ")");

            Assert.That((OpCode)expression.Code[5], Is.EqualTo(OpCode.OC_jz));
            Assert.That(expression.Run(null).ToB(), Is.False);
        }

        [Test]
        public void Cond_EvaluatesOnlySelectedBranch()
        {
            MugenExprCompiler compiler = new MugenExprCompiler();
            ProbeContext trueContext = new ProbeContext();
            ProbeContext falseContext = new ProbeContext();

            Assert.That(compiler.Compile("cond(1, random, random + random)")
                .Run(trueContext).ToI(), Is.EqualTo(1));
            Assert.That(trueContext.RandomReads, Is.EqualTo(1));

            Assert.That(compiler.Compile("cond(0, random, random + random)")
                .Run(falseContext).ToI(), Is.EqualTo(3));
            Assert.That(falseContext.RandomReads, Is.EqualTo(2));
        }

        [Test]
        public void IfElse_RemainsEagerLikeIkemen()
        {
            ProbeContext context = new ProbeContext();

            Assert.That(new MugenExprCompiler().Compile("ifelse(1, random, random)")
                .Run(context).ToI(), Is.EqualTo(1));
            Assert.That(context.RandomReads, Is.EqualTo(2));
        }

        [Test]
        public void TargetRedirect_PassesIdAndIndexInSourceOrder()
        {
            ProbeContext owner = new ProbeContext { RedirectTarget = new ProbeContext(321) };

            int value = new MugenExprCompiler().Compile("10 + target(7, 2), life")
                .Run(owner).ToI();

            Assert.That(value, Is.EqualTo(331));
            Assert.That(owner.RedirectOp, Is.EqualTo(OpCode.OC_target));
            Assert.That(owner.RedirectId, Is.EqualTo(7));
            Assert.That(owner.RedirectIndex, Is.EqualTo(2));
        }

        [Test]
        public void TargetRedirect_DefaultsIndexToZero()
        {
            ProbeContext owner = new ProbeContext { RedirectTarget = new ProbeContext(99) };

            Assert.That(new MugenExprCompiler().Compile("target(42), life")
                .Run(owner).ToI(), Is.EqualTo(99));
            Assert.That(owner.RedirectId, Is.EqualTo(42));
            Assert.That(owner.RedirectIndex, Is.Zero);
        }

        [Test]
        public void VariableOpcodes_KeepFourIndependentNamespaces()
        {
            ProbeContext context = new ProbeContext();
            BytecodeValue value = new MugenExprCompiler()
                .Compile("var(1) + sysvar(1) + fvar(1) + sysfvar(1)")
                .Run(context);

            Assert.That(value.ToI(), Is.EqualTo(1004));
        }
    }
}
