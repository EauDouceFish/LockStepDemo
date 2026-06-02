using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Lockstep.Game.Data;

namespace Lockstep.Import.Cmd
{
    /// <summary>
    /// MUGEN .cmd → CommandData[] 解析器（[Command] 块部分）。纯文本、无 Unity。
    /// 方向用大写(B/DB/D/DF/F/UF/U/UB)、按钮用小写(a/b/c/x/y/z/s)区分；修饰符(~ / $ &gt; +)
    /// v1 先剥离取基础符号（精确匹配语义留待 CommandSystem / T3.1）。
    /// .cmd 里的 [Statedef -1] 指令→状态切换由 CNS 解析器处理，本类只管 [Command] 定义。
    /// </summary>
    public static class CmdParser
    {
        const int DefaultTime = 15;
        const int DefaultBufferTime = 1;

        /// <summary>读取并解析 .cmd 文件，返回全部指令定义。</summary>
        public static List<CommandData> ParseFile(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        /// <summary>解析 .cmd 文本，返回 [Command] 块定义的指令。</summary>
        public static List<CommandData> Parse(string text)
        {
            List<CommandData> result = new List<CommandData>();

            bool inCommand = false;
            string name = null;
            List<InputSymbol> motion = new List<InputSymbol>();
            int time = DefaultTime;
            int bufferTime = DefaultBufferTime;
            bool hasCommand = false;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines)
            {
                string line = StripComment(rawLine).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '[')
                {
                    FlushCommand(result, inCommand, hasCommand, name, motion, time, bufferTime);
                    inCommand = line.ToLowerInvariant().StartsWith("[command]");
                    name = null;
                    motion = new List<InputSymbol>();
                    time = DefaultTime;
                    bufferTime = DefaultBufferTime;
                    hasCommand = false;
                    continue;
                }

                if (!inCommand)
                {
                    continue;
                }

                int equalsAt = line.IndexOf('=');
                if (equalsAt < 0)
                {
                    continue;
                }
                string key = line.Substring(0, equalsAt).Trim().ToLowerInvariant();
                string value = line.Substring(equalsAt + 1).Trim();

                switch (key)
                {
                    case "name":
                        name = Unquote(value);
                        break;
                    case "command":
                        motion = ParseMotion(value);
                        hasCommand = true;
                        break;
                    case "time":
                        time = ParseIntOr(value, DefaultTime);
                        break;
                    case "buffer.time":
                        bufferTime = ParseIntOr(value, DefaultBufferTime);
                        break;
                }
            }

            FlushCommand(result, inCommand, hasCommand, name, motion, time, bufferTime);
            return result;
        }

        static void FlushCommand(List<CommandData> result, bool inCommand, bool hasCommand,
            string name, List<InputSymbol> motion, int time, int bufferTime)
        {
            if (inCommand && hasCommand && name != null)
            {
                result.Add(new CommandData
                {
                    Name = name,
                    Motion = motion.ToArray(),
                    TimeWindow = time,
                    BufferTime = bufferTime,
                });
            }
        }

        static List<InputSymbol> ParseMotion(string value)
        {
            List<InputSymbol> motion = new List<InputSymbol>();
            string[] parts = value.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (TryMapSymbol(parts[i], out InputSymbol symbol))
                {
                    motion.Add(symbol);
                }
            }
            return motion;
        }

        static bool TryMapSymbol(string token, out InputSymbol symbol)
        {
            symbol = InputSymbol.Neutral;
            string cleaned = token.Trim().TrimStart('~', '/', '$', '>', ' ');
            int plus = cleaned.IndexOf('+');
            if (plus >= 0)
            {
                cleaned = cleaned.Substring(0, plus);   // v1：组合键取首个
            }
            cleaned = cleaned.Trim();
            if (cleaned.Length == 0)
            {
                return false;
            }

            switch (cleaned)
            {
                // 方向（大写）
                case "B": symbol = InputSymbol.Back; return true;
                case "DB": symbol = InputSymbol.DownBack; return true;
                case "D": symbol = InputSymbol.Down; return true;
                case "DF": symbol = InputSymbol.DownFwd; return true;
                case "F": symbol = InputSymbol.Fwd; return true;
                case "UF": symbol = InputSymbol.UpFwd; return true;
                case "U": symbol = InputSymbol.Up; return true;
                case "UB": symbol = InputSymbol.UpBack; return true;
                // 按钮（小写）
                case "a": symbol = InputSymbol.BtnA; return true;
                case "b": symbol = InputSymbol.BtnB; return true;
                case "c": symbol = InputSymbol.BtnC; return true;
                case "x": symbol = InputSymbol.BtnX; return true;
                case "y": symbol = InputSymbol.BtnY; return true;
                case "z": symbol = InputSymbol.BtnZ; return true;
                case "s": symbol = InputSymbol.BtnStart; return true;
                default: return false;
            }
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

        static int ParseIntOr(string value, int fallback)
        {
            return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }
    }
}
