using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class CommandTransitionCatalogTests
    {
        [Test]
        public void Parse_IgnoresNegativeCommandComparisons()
        {
            string cmd =
                "[Statedef -1]\n" +
                "[State -1, punch]\n" +
                "type = ChangeState\n" +
                "value = 200\n" +
                "triggerall = command = \"x\"\n" +
                "triggerall = command != \"holddown\"\n" +
                "trigger1 = ctrl\n";

            List<MCommandTransitionEntry> entries = MCommandTransitionCatalog.Parse(cmd);

            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].CommandNames, Is.EquivalentTo(new[] { "x" }));
            Assert.That(entries[0].TargetStateNo, Is.EqualTo(200));
        }

        [TestCase("QCF_x", 1000)]
        [TestCase("upper_x", 1100)]
        [TestCase("FF", 100)]
        public void Parse_KfmMotionCommandsExposeExpectedTransitions(string commandName, int targetState)
        {
            string kfmDir = TestAssets.KfmDir();
            string cmdPath = Path.Combine(kfmDir, "kfm.cmd");
            if (!File.Exists(cmdPath))
            {
                Assert.Ignore("KFM cmd is missing.");
            }

            List<MCommandTransitionEntry> entries = MCommandTransitionCatalog.Parse(File.ReadAllText(cmdPath));
            Assert.That(Contains(entries, commandName, targetState), Is.True,
                commandName + " should target state " + targetState);
        }

        static bool Contains(List<MCommandTransitionEntry> entries, string commandName, int targetState)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].TargetStateNo == targetState && entries[i].CommandNames.Contains(commandName))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
