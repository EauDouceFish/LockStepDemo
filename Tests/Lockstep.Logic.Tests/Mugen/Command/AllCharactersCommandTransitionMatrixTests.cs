using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class AllCharactersCommandTransitionMatrixTests
    {
        static readonly Regex CommandCompare =
            new Regex("\\bcommand\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        public void StatedefMinusOne_BuildsAuditableCommandToTransitionGraph(string directory)
        {
            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            string cmdPath = MugenCharacterPackageTestLoader.Resolve(directory, data.Definition.Files.Cmd);
            Assert.That(cmdPath, Is.Not.Null, Path.GetFileName(directory));

            List<CommandTransitionEntry> transitions = CommandTransitionMatrix.Parse(File.ReadAllText(cmdPath));
            Assert.That(transitions, Is.Not.Empty, Path.GetFileName(directory) + " has no command transitions");

            HashSet<string> definedCommands = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < data.Commands.Count; i++)
            {
                MCommandDef command = data.Commands[i];
                if (command != null && !string.IsNullOrEmpty(command.Name))
                {
                    definedCommands.Add(command.Name);
                }
            }

            HashSet<string> referencedCommands = new HashSet<string>(StringComparer.Ordinal);
            List<string> missingCommands = new List<string>();
            List<string> missingStates = new List<string>();
            for (int i = 0; i < transitions.Count; i++)
            {
                CommandTransitionEntry entry = transitions[i];
                referencedCommands.Add(entry.CommandName);
                if (!definedCommands.Contains(entry.CommandName))
                {
                    missingCommands.Add(entry.Describe() + " references undefined command");
                }
                if (entry.TargetStateNo.HasValue && entry.TargetStateNo.Value >= 0 &&
                    !data.States.ContainsKey(entry.TargetStateNo.Value) &&
                    !data.CommonStates.ContainsKey(entry.TargetStateNo.Value))
                {
                    missingStates.Add(entry.Describe() + " targets missing state");
                }
            }

            int unreferenced = 0;
            foreach (string command in definedCommands)
            {
                if (!referencedCommands.Contains(command) && command != "ai")
                {
                    unreferenced++;
                }
            }

            TestContext.Progress.WriteLine(Path.GetFileName(directory) + ": command-defs=" + definedCommands.Count +
                " transition-edges=" + transitions.Count + " referenced=" + referencedCommands.Count +
                " unreferenced-non-ai=" + unreferenced);

            StringBuilder diagnostics = new StringBuilder();
            Append(diagnostics, "missing-command", missingCommands);
            Append(diagnostics, "missing-state", missingStates);
            if (diagnostics.Length > 0)
            {
                TestContext.Progress.WriteLine(Path.GetFileName(directory) + " transition diagnostics:");
                TestContext.Progress.WriteLine(diagnostics.ToString());
            }
        }

        static void Append(StringBuilder builder, string kind, List<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                builder.AppendLine(kind + ": " + lines[i]);
            }
        }

        internal static List<string> ExtractCommandNames(string expression)
        {
            List<string> names = new List<string>();
            MatchCollection matches = CommandCompare.Matches(expression ?? string.Empty);
            for (int i = 0; i < matches.Count; i++)
            {
                names.Add(matches[i].Groups[1].Value);
            }
            return names;
        }
    }

    internal sealed class CommandTransitionEntry
    {
        public string CommandName;
        public string ControllerType;
        public int ControllerOrdinal;
        public string TargetValue;
        public int? TargetStateNo;

        public string Describe()
        {
            return "#" + ControllerOrdinal + " " + ControllerType + " command=\"" + CommandName +
                "\" value=" + TargetValue;
        }
    }

    internal static class CommandTransitionMatrix
    {
        public static List<CommandTransitionEntry> Parse(string text)
        {
            List<CommandTransitionEntry> result = new List<CommandTransitionEntry>();
            int statedef = int.MinValue;
            bool inController = false;
            string controllerType = null;
            string targetValue = null;
            List<string> triggers = new List<string>();
            int controllerOrdinal = 0;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]).Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line[0] == '[')
                {
                    Flush(result, controllerType, targetValue, triggers, controllerOrdinal);
                    if (inController)
                    {
                        controllerOrdinal++;
                    }
                    inController = false;
                    controllerType = null;
                    targetValue = null;
                    triggers.Clear();

                    string header = Header(line);
                    string lower = header.ToLowerInvariant();
                    if (lower.StartsWith("statedef"))
                    {
                        statedef = ParseTrailingInt(header);
                    }
                    else if ((lower.StartsWith("state ") || lower == "state") && statedef == -1)
                    {
                        inController = true;
                    }
                    continue;
                }

                if (!inController)
                {
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                if (key == "type")
                {
                    controllerType = value.ToLowerInvariant();
                }
                else if (key == "value")
                {
                    targetValue = value;
                }
                else if (key.StartsWith("trigger"))
                {
                    triggers.Add(value);
                }
            }

            Flush(result, controllerType, targetValue, triggers, controllerOrdinal);
            return result;
        }

        static void Flush(List<CommandTransitionEntry> result, string controllerType, string targetValue,
            List<string> triggers, int controllerOrdinal)
        {
            if (controllerType != "changestate" && controllerType != "selfstate")
            {
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                List<string> names = AllCharactersCommandTransitionMatrixTests.ExtractCommandNames(triggers[i]);
                for (int n = 0; n < names.Count; n++)
                {
                    result.Add(new CommandTransitionEntry
                    {
                        CommandName = names[n],
                        ControllerType = controllerType,
                        ControllerOrdinal = controllerOrdinal,
                        TargetValue = targetValue ?? string.Empty,
                        TargetStateNo = ParseLiteralInt(targetValue),
                    });
                }
            }
        }

        static string Header(string line)
        {
            int rb = line.IndexOf(']');
            return (rb > 0 ? line.Substring(1, rb - 1) : line.Substring(1)).Trim();
        }

        static string StripComment(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi) : line;
        }

        static int ParseTrailingInt(string text)
        {
            string[] parts = text.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    return value;
                }
            }
            return int.MinValue;
        }

        static int? ParseLiteralInt(string text)
        {
            if (int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int value))
            {
                return value;
            }
            return null;
        }
    }
}
