using NUnit.Framework;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;

namespace Lockstep.Tests.Mugen
{
    /// <summary>修复3：p2statetype / prevstatetype 字母枚举编译与求值。</summary>
    [TestFixture]
    public sealed class P2StateTypeTests
    {
        static readonly MugenExprCompiler C = new MugenExprCompiler();

        [Test]
        public void P2StateType_RedirectsAndCompares()
        {
            MChar p2 = new MChar { StateType = 4 };   // 对手在空中 A
            MChar c = new MChar { StateType = 1, P2 = p2 };

            Assert.IsTrue(C.Compile("p2statetype = A").Run(c).ToB(), "p2 在 A");
            Assert.IsFalse(C.Compile("p2statetype = S").Run(c).ToB(), "p2 不在 S");
        }

        [Test]
        public void P2StateType_NoP2_IsUndefinedFalse()
        {
            MChar c = new MChar { StateType = 1 };   // 无 P2
            Assert.IsFalse(C.Compile("p2statetype = S").Run(c).ToB(), "无 p2 → redirect 失败为假");
        }

        [Test]
        public void PrevStateType_ReadsPrevious()
        {
            MChar c = new MChar { StateType = 2, PrevStateType = 1 };   // 当前蹲，上一状态站
            Assert.IsTrue(C.Compile("prevstatetype = S").Run(c).ToB(), "上一状态是 S");
            Assert.IsFalse(C.Compile("prevstatetype = C").Run(c).ToB());
            Assert.IsTrue(C.Compile("statetype = C").Run(c).ToB(), "当前是 C 不受影响");
        }
    }
}
