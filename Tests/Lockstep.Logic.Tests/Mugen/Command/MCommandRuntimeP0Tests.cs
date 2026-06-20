using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class MCommandRuntimeP0Tests
    {
        const MInput A = MInput.A;
        const MInput R = MInput.Right;

        [Test]
        public void DefaultsApplyToCommandsBeforeAndAfterDefaultsSection()
        {
            string text =
                "[Command]\nname = \"first\"\ncommand = a\n" +
                "[Defaults]\ncommand.time = 30\ncommand.buffer.time = 4\n" +
                "[Command]\nname = \"override\"\ncommand = b\ntime = 7\nbuffer.time = 2\n";

            System.Collections.Generic.List<MCommandDef> commands = MugenCmdParser.Parse(text);

            Assert.That(commands[0].Time, Is.EqualTo(30));
            Assert.That(commands[0].BufferTime, Is.EqualTo(4));
            Assert.That(commands[1].Time, Is.EqualTo(7));
            Assert.That(commands[1].BufferTime, Is.EqualTo(2));
        }

        [Test]
        public void BufferTimeZeroClampsToOneFrame()
        {
            string text =
                "[Defaults]\ncommand.buffer.time = 0\n" +
                "[Command]\nname = \"tap\"\ncommand = a\nbuffer.time = 0\n";

            System.Collections.Generic.List<MCommandDef> commands = MugenCmdParser.Parse(text);
            MCommandList list = new MCommandList();
            list.Commands.Add(commands[0]);

            Assert.That(commands[0].BufferTime, Is.EqualTo(1));
            list.Update(A, true);
            Assert.That(list.IsActive("tap"), Is.True);
            list.Update(MInput.None, true);
            Assert.That(list.IsActive("tap"), Is.False);
        }

        [Test]
        public void ReleaseChargeRequiresReleaseAfterRequiredHoldTime()
        {
            MCommandList list = List("release", "~3a");

            list.Update(A, true);
            list.Update(A, true);
            list.Update(A, true);
            Assert.That(list.IsActive("release"), Is.False);

            list.Update(MInput.None, true);
            Assert.That(list.IsActive("release"), Is.True);
        }

        [Test]
        public void ReleaseChargeRejectsEarlyRelease()
        {
            MCommandList list = List("release", "~3a");

            list.Update(A, true);
            list.Update(A, true);
            list.Update(MInput.None, true);

            Assert.That(list.IsActive("release"), Is.False);
        }

        [Test]
        public void DirectionToButtonCanCompleteInOneFrame()
        {
            MCommandList list = List("same-frame", "F, a");

            list.Update(R | A, true);

            Assert.That(list.IsActive("same-frame"), Is.True);
        }

        [Test]
        public void ParserExpandsRepeatedDirectionIntoReleaseRetap()
        {
            MCommandDef command = MCommandParser.Parse("dash", "F, F");

            Assert.That(command.Steps.Count, Is.EqualTo(3));
            Assert.That(command.Steps[1].Greater, Is.True);
            Assert.That(command.Steps[1].Keys[0].Release, Is.True);
            Assert.That(command.Steps[2].Greater, Is.True);

            MCommandList list = new MCommandList();
            list.Commands.Add(command);
            list.Update(R, true);
            list.Update(MInput.None, true);
            list.Update(R, true);

            Assert.That(list.IsActive("dash"), Is.True);
        }

        [Test]
        public void GreaterAllowsWaitFramesWithoutInputChanges()
        {
            MCommandList list = List("strict", "a, >b");

            list.Update(A, true);
            list.Update(A, true);
            list.Update(A | MInput.B, true);

            Assert.That(list.IsActive("strict"), Is.True);
        }

        [Test]
        public void GreaterRejectsUnrelatedInputChange()
        {
            MCommandList list = List("strict", "a, >b");

            list.Update(A, true);
            list.Update(A | MInput.C, true);
            list.Update(A | MInput.B | MInput.C, true);

            Assert.That(list.IsActive("strict"), Is.False);
        }

        static MCommandList List(string name, string motion)
        {
            MCommandList list = new MCommandList();
            list.Commands.Add(MCommandParser.Parse(name, motion));
            return list;
        }
    }
}
