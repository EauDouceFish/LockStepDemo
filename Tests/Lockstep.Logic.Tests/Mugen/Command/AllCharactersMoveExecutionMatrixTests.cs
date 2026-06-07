using System;
using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class AllCharactersMoveExecutionMatrixTests
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
        public void LiteralCommandTransitions_AreExecutionProbedFromFreshSessions(string directory)
        {
            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            string cmdPath = MugenCharacterPackageTestLoader.Resolve(directory, data.Definition.Files.Cmd);
            Assert.That(cmdPath, Is.Not.Null, Path.GetFileName(directory));

            Dictionary<string, List<MCommandDef>> commandsByName = CommandsByName(data.Commands);
            List<CommandTransitionEntry> transitions = CommandTransitionMatrix.Parse(File.ReadAllText(cmdPath));

            int probed = 0;
            int reached = 0;
            int skipped = 0;
            List<string> misses = new List<string>();
            for (int i = 0; i < transitions.Count; i++)
            {
                CommandTransitionEntry entry = transitions[i];
                if (!IsProbeable(data, commandsByName, entry))
                {
                    skipped++;
                    continue;
                }

                probed++;
                MMoveTestResult result = Probe(data, commandsByName, entry);
                if (result.Status == MMoveTestStatus.Passed)
                {
                    reached++;
                }
                else if (misses.Count < 40)
                {
                    misses.Add(entry.Describe() + " status=" + result.Status +
                        " profile=" + result.ProfileName + " final=" + result.FinalStateNo);
                }
            }

            TestContext.Progress.WriteLine(Path.GetFileName(directory) + ": probed=" + probed +
                " reached=" + reached + " skipped=" + skipped + " missed=" + (probed - reached));
            for (int i = 0; i < misses.Count; i++)
            {
                TestContext.Progress.WriteLine(Path.GetFileName(directory) + " move-probe-miss: " + misses[i]);
            }

            Assert.That(probed, Is.GreaterThan(0), Path.GetFileName(directory) + " has no probeable transitions");
        }

        static bool IsProbeable(MCharData data, Dictionary<string, List<MCommandDef>> commandsByName,
            CommandTransitionEntry entry)
        {
            if (!entry.TargetStateNo.HasValue || entry.TargetStateNo.Value < 0)
            {
                return false;
            }
            if (!data.States.ContainsKey(entry.TargetStateNo.Value) &&
                !data.CommonStates.ContainsKey(entry.TargetStateNo.Value))
            {
                return false;
            }
            for (int i = 0; i < entry.CommandNames.Count; i++)
            {
                if (entry.CommandNames[i] == "ai" || !commandsByName.ContainsKey(entry.CommandNames[i]))
                {
                    return false;
                }
            }
            return true;
        }

        static Dictionary<string, List<MCommandDef>> CommandsByName(List<MCommandDef> commands)
        {
            Dictionary<string, List<MCommandDef>> result =
                new Dictionary<string, List<MCommandDef>>(StringComparer.Ordinal);
            for (int i = 0; i < commands.Count; i++)
            {
                MCommandDef command = commands[i];
                if (command == null || string.IsNullOrEmpty(command.Name) || command.Steps.Count == 0)
                {
                    continue;
                }
                if (!result.TryGetValue(command.Name, out List<MCommandDef> list))
                {
                    list = new List<MCommandDef>();
                    result[command.Name] = list;
                }
                list.Add(command);
            }
            return result;
        }

        static MMoveTestResult Probe(MCharData data, Dictionary<string, List<MCommandDef>> commandsByName,
            CommandTransitionEntry entry)
        {
            List<MCommandDef> commandDefinitions = SelectFirstDefinitions(commandsByName, entry.CommandNames);
            if (commandDefinitions.Count == 0)
            {
                return new MMoveTestResult
                {
                    Status = MMoveTestStatus.UnreachablePrerequisite,
                    TargetStateNo = entry.TargetStateNo.HasValue ? entry.TargetStateNo.Value : -1,
                };
            }

            return MMoveTestRunner.Run(data, commandDefinitions, entry.TargetStateNo.Value);
        }

        static List<MCommandDef> SelectFirstDefinitions(Dictionary<string, List<MCommandDef>> commandsByName,
            List<string> commandNames)
        {
            List<MCommandDef> result = new List<MCommandDef>();
            for (int i = 0; i < commandNames.Count; i++)
            {
                if (commandsByName.TryGetValue(commandNames[i], out List<MCommandDef> definitions) &&
                    definitions.Count > 0)
                {
                    result.Add(definitions[0]);
                }
            }
            return result;
        }

    }
}
