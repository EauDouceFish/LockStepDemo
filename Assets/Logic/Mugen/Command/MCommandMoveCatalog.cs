using System.Collections.Generic;

namespace Lockstep.Mugen.Command
{
    public sealed class MCommandMoveInfo
    {
        public readonly List<string> CommandNames = new List<string>();
        public readonly List<string> Motions = new List<string>();
        public int? TargetStateNo;
        public string TargetValue = "";

        public string CommandText => string.Join("+", CommandNames.ToArray());
        public string MotionText => string.Join(" + ", Motions.ToArray());
    }

    public static class MCommandMoveCatalog
    {
        // Project-specific: joins parsed CMD commands with C# transition entries for move probes; Ikemen evaluates triggers at runtime.
        public static List<MCommandMoveInfo> Build(
            IReadOnlyList<MCommandDef> commands,
            IReadOnlyList<MCommandTransitionEntry> transitions)
        {
            List<MCommandMoveInfo> result = new List<MCommandMoveInfo>();
            if (commands == null || transitions == null)
            {
                return result;
            }

            for (int i = 0; i < transitions.Count; i++)
            {
                MCommandTransitionEntry entry = transitions[i];
                MCommandMoveInfo info = new MCommandMoveInfo
                {
                    TargetStateNo = entry.TargetStateNo,
                    TargetValue = entry.TargetValue,
                };
                bool complete = true;
                for (int c = 0; c < entry.CommandNames.Count; c++)
                {
                    string name = entry.CommandNames[c];
                    MCommandDef def = First(commands, name);
                    if (def == null)
                    {
                        complete = false;
                        break;
                    }
                    info.CommandNames.Add(name);
                    info.Motions.Add(string.IsNullOrEmpty(def.Motion) ? name : def.Motion);
                }
                if (complete && info.CommandNames.Count > 0)
                {
                    result.Add(info);
                }
            }
            return result;
        }

        // Project-specific: helper resolves a command name in the parsed C# command list; Ikemen stores commands on the player.
        static MCommandDef First(IReadOnlyList<MCommandDef> commands, string name)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Name == name)
                {
                    return commands[i];
                }
            }
            return null;
        }
    }
}
