using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class CommandInputDisplayTests
    {
        [Test]
        public void Format_NoneShowsNone()
        {
            Assert.That(MInputDisplayFormatter.Format(MInput.None), Is.EqualTo("None"));
        }

        [Test]
        public void Format_CombinesDirectionAndMugenButtonNames()
        {
            Assert.That(MInputDisplayFormatter.Format(MInput.Down | MInput.Right | MInput.X),
                Is.EqualTo("D + R + x"));
        }

        [Test]
        public void Format_UsesMugenSixButtonOrder()
        {
            Assert.That(MInputDisplayFormatter.Format(MInput.X | MInput.Y | MInput.Z | MInput.A | MInput.B | MInput.C),
                Is.EqualTo("x + y + z + a + b + c"));
        }
    }
}
