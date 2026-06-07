using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class MoveTestRunnerTests
    {
        [Test]
        public void CommandInputSynthesizer_ActivatesKfmPunchCommand()
        {
            MCharData data = LoadKfm();
            MCommandDef command = FirstCommand(data, "x");
            List<MInput> sequence = MCommandInputSynthesizer.BuildSequence(command, facingRight: true);
            MCommandList list = new MCommandList();
            list.Commands.Add(command);

            bool active = false;
            for (int i = 0; i < sequence.Count; i++)
            {
                list.Update(sequence[i], facingRight: true);
                active |= list.IsActive("x");
            }

            Assert.That(active, Is.True);
        }

        [Test]
        public void MoveTestRunner_UsesSnapshotAndClassifiesPassedMove()
        {
            MCharData data = LoadKfm();
            MMoveTestResult result = MMoveTestRunner.Run(
                data,
                new[] { FirstCommand(data, "x") },
                200,
                new[] { new MMovePrerequisiteProfile { Name = "standing-close", Distance = 40, ActorPower = -1 } });

            Assert.That(result.Status, Is.EqualTo(MMoveTestStatus.Passed));
            Assert.That(result.UsedSnapshot, Is.True);
            Assert.That(result.Deterministic, Is.True);
            Assert.That(result.CommandMatched, Is.True);
            Assert.That(result.StateEntered, Is.True);
            Assert.That(result.FirstHash, Is.EqualTo(result.ReplayHash));
        }

        [Test]
        public void MoveTestRunner_ClassifiesTransitionFailureAfterCommandMatch()
        {
            MCharData data = LoadKfm();
            MMoveTestResult result = MMoveTestRunner.Run(
                data,
                new[] { FirstCommand(data, "x") },
                9999,
                new[] { new MMovePrerequisiteProfile { Name = "standing-close", Distance = 40, ActorPower = -1 } });

            Assert.That(result.Status, Is.EqualTo(MMoveTestStatus.TransitionFailure));
            Assert.That(result.CommandMatched, Is.True);
            Assert.That(result.StateEntered, Is.False);
            Assert.That(result.UsedSnapshot, Is.True);
        }

        static MCharData LoadKfm()
        {
            string directory = TestAssets.KfmDir();
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("KFM素材缺失，跳过。");
            }
            return MugenCharacterPackageTestLoader.Load(directory);
        }

        static MCommandDef FirstCommand(MCharData data, string name)
        {
            for (int i = 0; i < data.Commands.Count; i++)
            {
                if (data.Commands[i].Name == name)
                {
                    return data.Commands[i];
                }
            }
            Assert.Fail("Missing command " + name);
            return null;
        }
    }
}
