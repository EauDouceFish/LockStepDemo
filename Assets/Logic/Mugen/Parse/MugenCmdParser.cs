// Behavior-faithful CMD parser for the ported Mugen engine.
// 解析 .cmd 的 [Command] 段(name/command/time/buffer.time) → MCommandDef 列表。
// [Remap]/状态控制器段在此忽略(状态走 CnsParser)；[Defaults] 先扫描后应用到所有命令。
using System.Collections.Generic;
using System.Globalization;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Parse
{
    public static class MugenCmdParser
    {
        public static List<MCommandDef> Parse(string text)
        {
            List<MCommandDef> result = new List<MCommandDef>();

            int defaultTime = 15;
            int defaultBufferTime = 1;
            ReadDefaults(text, ref defaultTime, ref defaultBufferTime);

            bool inCommand = false;
            string name = null;
            string command = null;
            int time = 15;
            int bufferTime = 1;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string raw in lines)
            {
                string line = StripComment(raw).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '[')
                {
                    // 收尾上一条
                    if (inCommand && name != null && command != null)
                    {
                        result.Add(MCommandParser.Parse(name, command, time, bufferTime));
                    }
                    inCommand = false;
                    name = null;
                    command = null;
                    time = defaultTime;
                    bufferTime = defaultBufferTime;

                    int rb = line.IndexOf(']');
                    string header = (rb > 0 ? line.Substring(1, rb - 1) : line.Substring(1)).Trim().ToLowerInvariant();
                    if (header.StartsWith("command"))
                    {
                        inCommand = true;
                    }
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();

                if (!inCommand)
                {
                    continue;
                }
                switch (key)
                {
                    case "name": name = Unquote(val); break;
                    case "command": command = val; break;
                    case "time": time = ParseInt(val, defaultTime); break;
                    case "buffer.time": bufferTime = System.Math.Max(1, ParseInt(val, defaultBufferTime)); break;
                }
            }

            if (inCommand && name != null && command != null)
            {
                result.Add(MCommandParser.Parse(name, command, time, bufferTime));
            }
            return result;
        }

        static void ReadDefaults(string text, ref int defaultTime, ref int defaultBufferTime)
        {
            bool inDefaults = false;
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string raw in lines)
            {
                string line = StripComment(raw).Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line[0] == '[')
                {
                    int rb = line.IndexOf(']');
                    string header = (rb > 0 ? line.Substring(1, rb - 1) : line.Substring(1)).Trim();
                    inDefaults = string.Equals(header, "defaults", System.StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inDefaults)
                {
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();
                if (key == "command.time")
                {
                    defaultTime = ParseInt(val, defaultTime);
                }
                else if (key == "command.buffer.time")
                {
                    defaultBufferTime = System.Math.Max(1, ParseInt(val, defaultBufferTime));
                }
            }
        }

        static string Unquote(string v)
        {
            v = v.Trim();
            if (v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"')
            {
                return v.Substring(1, v.Length - 2);
            }
            return v;
        }

        static int ParseInt(string v, int def)
        {
            return int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : def;
        }

        static string StripComment(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi) : line;
        }
    }
}
