using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Lockstep.Mugen.Command
{
    public sealed class MCommandTransitionEntry
    {
        public readonly List<string> CommandNames = new List<string>();
        public string ControllerType = "";
        public int ControllerOrdinal;
        public int TriggerGroup;
        public string TargetValue = "";
        public int? TargetStateNo;

        public string Describe()
        {
            return "#" + ControllerOrdinal + "/trigger" + TriggerGroup + " " + ControllerType +
                   " commands=\"" + string.Join("+", CommandNames.ToArray()) + "\" value=" + TargetValue;
        }
    }

    public static class MCommandTransitionCatalog
    {
        static readonly Regex PositiveCommandCompare =
            new Regex("\\bcommand\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<MCommandTransitionEntry> Parse(string text)
        {
            List<MCommandTransitionEntry> result = new List<MCommandTransitionEntry>();
            int statedef = int.MinValue;
            bool inController = false;
            string controllerType = null;
            string targetValue = null;
            List<string> triggerAll = new List<string>();
            SortedDictionary<int, List<string>> triggerGroups = new SortedDictionary<int, List<string>>();
            int controllerOrdinal = 0;

            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]).Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line[0] == '[')
                {
                    Flush(result, controllerType, targetValue, triggerAll, triggerGroups, controllerOrdinal);
                    if (inController)
                    {
                        controllerOrdinal++;
                    }
                    inController = false;
                    controllerType = null;
                    targetValue = null;
                    triggerAll.Clear();
                    triggerGroups.Clear();

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
                else if (key == "triggerall")
                {
                    triggerAll.Add(value);
                }
                else if (key.StartsWith("trigger") &&
                         int.TryParse(key.Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture,
                             out int groupNo))
                {
                    if (!triggerGroups.TryGetValue(groupNo, out List<string> group))
                    {
                        group = new List<string>();
                        triggerGroups[groupNo] = group;
                    }
                    group.Add(value);
                }
            }

            Flush(result, controllerType, targetValue, triggerAll, triggerGroups, controllerOrdinal);
            return result;
        }

        public static List<string> ExtractPositiveCommandNames(string expression)
        {
            List<string> names = new List<string>();
            MatchCollection matches = PositiveCommandCompare.Matches(expression ?? string.Empty);
            for (int i = 0; i < matches.Count; i++)
            {
                names.Add(matches[i].Groups[1].Value);
            }
            return names;
        }

        static void Flush(List<MCommandTransitionEntry> result, string controllerType, string targetValue,
            List<string> triggerAll, SortedDictionary<int, List<string>> triggerGroups, int controllerOrdinal)
        {
            if (controllerType != "changestate" && controllerType != "selfstate")
            {
                return;
            }

            if (triggerGroups.Count == 0)
            {
                AddEntry(result, controllerType, targetValue, triggerAll, null, 0, controllerOrdinal);
                return;
            }

            foreach (KeyValuePair<int, List<string>> group in triggerGroups)
            {
                AddEntry(result, controllerType, targetValue, triggerAll, group.Value, group.Key, controllerOrdinal);
            }
        }

        static void AddEntry(List<MCommandTransitionEntry> result, string controllerType, string targetValue,
            List<string> triggerAll, List<string> triggerGroup, int groupNo, int controllerOrdinal)
        {
            List<string> names = new List<string>();
            AppendCommandNames(names, triggerAll);
            AppendCommandNames(names, triggerGroup);
            if (names.Count == 0)
            {
                return;
            }

            result.Add(new MCommandTransitionEntry
            {
                ControllerType = controllerType,
                ControllerOrdinal = controllerOrdinal,
                TriggerGroup = groupNo,
                TargetValue = targetValue ?? string.Empty,
                TargetStateNo = ParseLiteralInt(targetValue),
            });
            result[result.Count - 1].CommandNames.AddRange(names);
        }

        static void AppendCommandNames(List<string> result, List<string> expressions)
        {
            if (expressions == null)
            {
                return;
            }

            for (int i = 0; i < expressions.Count; i++)
            {
                List<string> names = ExtractPositiveCommandNames(expressions[i]);
                for (int n = 0; n < names.Count; n++)
                {
                    if (!result.Contains(names[n]))
                    {
                        result.Add(names[n]);
                    }
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
