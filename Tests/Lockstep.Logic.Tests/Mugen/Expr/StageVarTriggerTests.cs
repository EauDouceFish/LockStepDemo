using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Expr
{
    [TestFixture]
    public sealed class StageVarTriggerTests
    {
        static int Eval(string expression)
        {
            return new MugenExprCompiler().Compile(expression).Run(new MChar()).ToI();
        }

        [Test]
        public void StageVarInfoName_StringCompare_DefaultStageName()
        {
            Assert.That(Eval("StageVar(info.name) = \"caos-4\""), Is.EqualTo(0));
            Assert.That(Eval("StageVar(info.name) != \"caos-4\""), Is.EqualTo(1));
        }
    }
}
