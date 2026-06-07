using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class CommandMoveCatalogTests
    {
        [Test]
        public void Build_PreservesOriginalMotionTextForMoveHelp()
        {
            string cmd =
                "[Command]\n" +
                "name = \"QCF_x\"\n" +
                "command = ~D, DF, F, x\n" +
                "[Statedef -1]\n" +
                "[State -1, palm]\n" +
                "type = ChangeState\n" +
                "value = 1000\n" +
                "trigger1 = command = \"QCF_x\"\n";

            List<MCommandDef> commands = Lockstep.Mugen.Parse.MugenCmdParser.Parse(cmd);
            List<MCommandTransitionEntry> transitions = MCommandTransitionCatalog.Parse(cmd);
            List<MCommandMoveInfo> catalog = MCommandMoveCatalog.Build(commands, transitions);

            Assert.That(catalog, Has.Count.EqualTo(1));
            Assert.That(catalog[0].CommandText, Is.EqualTo("QCF_x"));
            Assert.That(catalog[0].MotionText, Is.EqualTo("~D, DF, F, x"));
            Assert.That(catalog[0].TargetStateNo, Is.EqualTo(1000));
        }

        [Test]
        public void Build_KfmMoveHelpIncludesKnownMotionAndTarget()
        {
            string kfmDir = TestAssets.KfmDir();
            string cmdPath = Path.Combine(kfmDir, "kfm.cmd");
            if (!File.Exists(cmdPath))
            {
                Assert.Ignore("KFM cmd is missing.");
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(kfmDir);
            string cmdText = File.ReadAllText(cmdPath);
            List<MCommandMoveInfo> catalog =
                MCommandMoveCatalog.Build(data.Commands, MCommandTransitionCatalog.Parse(cmdText));

            MCommandMoveInfo qcf = Find(catalog, "QCF_x", 1000);
            Assert.That(qcf, Is.Not.Null);
            Assert.That(qcf.MotionText, Is.EqualTo("~D, DF, F, x"));
            Assert.That(MCommandMoveHelpFormatter.FormatMove(qcf),
                Is.EqualTo("招式 QCF_x：搓法 松开下 -> 下前 -> 前 -> 轻拳x(A键)，进入状态 1000"));
        }

        [Test]
        public void HelpFormatter_ExplainsKeyboardButtonsForBeginners()
        {
            Assert.That(MCommandMoveHelpFormatter.KeyboardLegend(),
                Does.Contain("A/S/D=轻拳x/重拳y/三拳z"));
            Assert.That(MCommandMoveHelpFormatter.KeyboardLegend(),
                Does.Contain("Z/X/C=轻脚a/重脚b/三脚c"));
        }

        [Test]
        public void HelpFormatter_ExplainsChargeHoldAndSimultaneousButtons()
        {
            Assert.That(MCommandMoveHelpFormatter.FormatMotion("~30$B, F, x+y"),
                Is.EqualTo("松开按住任意后方向30帧 -> 前 -> 轻拳x(A键)+重拳y(S键)"));
        }

        static MCommandMoveInfo Find(List<MCommandMoveInfo> catalog, string commandName, int targetState)
        {
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i].TargetStateNo == targetState && catalog[i].CommandNames.Contains(commandName))
                {
                    return catalog[i];
                }
            }
            return null;
        }
    }
}
