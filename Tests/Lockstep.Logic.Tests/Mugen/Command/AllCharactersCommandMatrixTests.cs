using System.Collections.Generic;
using System.IO;
using System.Text;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class AllCharactersCommandMatrixTests
    {
        static IEnumerable<string> CharacterDirectories()
        {
            string root = TestAssets.MugenSourceDir();
            if (!Directory.Exists(root)) { yield break; }
            foreach (string directory in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(directory);
                if (name.StartsWith("_")) { continue; }
                if (MugenCharacterPackageTestLoader.PickMainDef(directory) != null) { yield return directory; }
            }
        }

        [TestCaseSource(nameof(CharacterDirectories))]
        public void EveryParsedCommandDefinition_CanBeActivatedBySynthesizedInput(string directory)
        {
            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            Assert.That(data.Commands, Is.Not.Null.And.Not.Empty, Path.GetFileName(directory));

            List<string> failures = new List<string>();
            List<string> timeCompressed = new List<string>();
            for (int i = 0; i < data.Commands.Count; i++)
            {
                MCommandDef command = data.Commands[i];
                if (command == null || string.IsNullOrEmpty(command.Name) || command.Steps.Count == 0)
                {
                    continue;
                }

                List<MInput> sequence = CommandInputMatrix.BuildSequence(command, facingRight: true);
                if (!CanActivate(command, sequence))
                {
                    if (IsTimeCompressedAiCommand(command))
                    {
                        timeCompressed.Add("#" + i + " " + command.Name + " steps=" + command.Steps.Count +
                            " time=" + command.Time);
                        continue;
                    }
                    failures.Add("#" + i + " " + command.Name + " steps=" + command.Steps.Count +
                        " frames=" + sequence.Count);
                }
            }

            TestContext.Progress.WriteLine(Path.GetFileName(directory) + ": commands=" + data.Commands.Count +
                " time-compressed-ai=" + timeCompressed.Count + " failures=" + failures.Count);
            for (int i = 0; i < timeCompressed.Count; i++)
            {
                TestContext.Progress.WriteLine(Path.GetFileName(directory) + ": classified " + timeCompressed[i]);
            }
            if (failures.Count > 0)
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine(Path.GetFileName(directory) + " command activation failures:");
                for (int i = 0; i < failures.Count; i++)
                {
                    builder.AppendLine(failures[i]);
                }
                Assert.Fail(builder.ToString());
            }
        }

        static bool CanActivate(MCommandDef command, List<MInput> sequence)
        {
            MCommandList list = new MCommandList();
            list.Commands.Add(command);
            for (int i = 0; i < sequence.Count; i++)
            {
                list.Update(sequence[i], facingRight: true);
                if (list.IsActive(command.Name))
                {
                    return true;
                }
            }
            return false;
        }

        static bool IsTimeCompressedAiCommand(MCommandDef command)
        {
            return command.Name == "ai" && command.Time <= 1 && command.Steps.Count > 1;
        }
    }

    internal static class CommandInputMatrix
    {
        public static List<MInput> BuildSequence(MCommandDef command, bool facingRight)
        {
            List<MInput> frames = new List<MInput>();
            frames.Add(MInput.None);
            frames.Add(MInput.None);
            for (int i = 0; i < command.Steps.Count; i++)
            {
                AppendStep(frames, command.Steps[i], facingRight);
            }
            frames.Add(MInput.None);
            return frames;
        }

        static void AppendStep(List<MInput> frames, MCommandStep step, bool facingRight)
        {
            List<MCommandKey> keys = SelectedKeys(step);
            MInput preReleaseHold = MInput.None;
            int holdFrames = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                if (!keys[i].Release)
                {
                    continue;
                }

                preReleaseHold |= KeyBits(keys[i], facingRight);
                int required = keys[i].ChargeTime > 0 ? keys[i].ChargeTime : 1;
                if (required > holdFrames)
                {
                    holdFrames = required;
                }
            }

            for (int i = 0; i < holdFrames; i++)
            {
                frames.Add(preReleaseHold);
            }

            MInput current = MInput.None;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].Release)
                {
                    continue;
                }
                current |= KeyBits(keys[i], facingRight);
            }
            frames.Add(current);
        }

        static List<MCommandKey> SelectedKeys(MCommandStep step)
        {
            List<MCommandKey> keys = new List<MCommandKey>();
            if (!step.OrLogic)
            {
                for (int i = 0; i < step.Keys.Count; i++)
                {
                    keys.Add(step.Keys[i]);
                }
                return keys;
            }

            if (step.Keys.Count > 0)
            {
                keys.Add(step.Keys[0]);
            }
            return keys;
        }

        static MInput KeyBits(MCommandKey key, bool facingRight)
        {
            if (key.IsNeutral)
            {
                return MInput.None;
            }

            MInput result = key.Bits;
            if (key.IsBack)
            {
                result |= facingRight ? MInput.Left : MInput.Right;
            }
            if (key.IsFwd)
            {
                result |= facingRight ? MInput.Right : MInput.Left;
            }
            return result;
        }
    }
}
