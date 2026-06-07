using System;
using System.Collections.Generic;

namespace Lockstep.Mugen.Parse
{
    /// <summary>Files referenced by a character DEF. Paths are kept unresolved so the logic layer performs no IO.</summary>
    public sealed class MDefFiles
    {
        public string Cmd;
        public string Cns;
        public readonly List<string> St = new List<string>();
        public string StCommon;
        public string Sprite;
        public string Anim;
        public string Sound;
        public readonly Dictionary<int, string> Palettes = new Dictionary<int, string>();
    }

    /// <summary>Character metadata that affects import and runtime interpretation.</summary>
    public sealed class MCharacterDefinition
    {
        public string Name = "";
        public string DisplayName = "";
        public int LocalCoordWidth = 320;
        public int LocalCoordHeight = 240;
        public readonly List<int> PaletteDefaults = new List<int>();
        public MDefFiles Files = new MDefFiles();

        public bool UsesDefaultLocalCoord => LocalCoordWidth == 320 && LocalCoordHeight == 240;
    }

    /// <summary>Pure parser for the [Info] and [Files] portions of a character DEF.</summary>
    public static class MDefParser
    {
        public static MDefFiles ParseFiles(string text)
        {
            return Parse(text).Files;
        }

        public static MCharacterDefinition Parse(string text)
        {
            MCharacterDefinition definition = new MCharacterDefinition();
            string section = "";
            string[] lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string raw in lines)
            {
                string line = StripComment(raw).Trim();
                if (line.Length == 0) { continue; }
                if (line[0] == '[')
                {
                    int rb = line.IndexOf(']');
                    section = (rb > 0 ? line.Substring(1, rb - 1) : line.Substring(1)).Trim().ToLowerInvariant();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq < 0) { continue; }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = Unquote(line.Substring(eq + 1).Trim());
                if (section == "files") { AssignFile(definition.Files, key, value); }
                else if (section == "info") { AssignInfo(definition, key, value); }
            }
            return definition;
        }

        static void AssignInfo(MCharacterDefinition definition, string key, string value)
        {
            switch (key)
            {
                case "name": definition.Name = value; break;
                case "displayname": definition.DisplayName = value; break;
                case "localcoord":
                    int[] coord = ParseInts(value);
                    if (coord.Length > 0 && coord[0] > 0) { definition.LocalCoordWidth = coord[0]; }
                    if (coord.Length > 1 && coord[1] > 0) { definition.LocalCoordHeight = coord[1]; }
                    break;
                case "pal.defaults":
                    definition.PaletteDefaults.Clear();
                    definition.PaletteDefaults.AddRange(ParseInts(value));
                    break;
            }
        }

        static void AssignFile(MDefFiles files, string key, string value)
        {
            switch (key)
            {
                case "cmd": files.Cmd = value; break;
                case "cns": files.Cns = value; break;
                case "stcommon": files.StCommon = value; break;
                case "sprite": files.Sprite = value; break;
                case "anim": files.Anim = value; break;
                case "sound": files.Sound = value; break;
                default:
                    if (key == "st" || (key.StartsWith("st", StringComparison.Ordinal) && IsDigits(key.Substring(2))))
                    {
                        files.St.Add(value);
                    }
                    else if (key.StartsWith("pal", StringComparison.Ordinal) &&
                             int.TryParse(key.Substring(3), out int paletteNo) && paletteNo > 0)
                    {
                        files.Palettes[paletteNo] = value;
                    }
                    break;
            }
        }

        static int[] ParseInts(string value)
        {
            string[] parts = value.Split(',');
            List<int> values = new List<int>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int parsed)) { values.Add(parsed); }
            }
            return values.ToArray();
        }

        static bool IsDigits(string value)
        {
            if (value.Length == 0) { return false; }
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] < '0' || value[i] > '9') { return false; }
            }
            return true;
        }

        static string StripComment(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi) : line;
        }

        static string Unquote(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2);
            }
            return value;
        }
    }
}
