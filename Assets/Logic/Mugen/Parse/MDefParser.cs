// MUGEN .def [Files] 段解析（移植 Ikemen char.go loadName/[Files] 读取的文件名约定）。
// 只取文件名（逻辑层不做 IO）；由表现层/测试读文件内容后交给 MCharLoader。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;

namespace Lockstep.Mugen.Parse
{
    /// <summary>.def [Files] 段里各资源的文件名。st 可有多个(st/st1/st2…)，合并为状态文件列表。</summary>
    public sealed class MDefFiles
    {
        public string Cmd;          // 命令集 .cmd
        public string Cns;          // 常量 .cns（const 取值来源）
        public List<string> St = new List<string>();   // 角色状态 .cns（st, st1, st2…）
        public string StCommon;     // 公共状态 common1.cns
        public string Sprite;       // .sff（表现层）
        public string Anim;         // .air
    }

    /// <summary>解析 .def 文本，提取 [Files] 段文件名。容错：缺段返回空字段。</summary>
    public static class MDefParser
    {
        public static MDefFiles ParseFiles(string text)
        {
            MDefFiles files = new MDefFiles();
            bool inFiles = false;

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
                    string header = (rb > 0 ? line.Substring(1, rb - 1) : line.Substring(1)).Trim().ToLowerInvariant();
                    inFiles = header == "files";
                    continue;
                }
                if (!inFiles)
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
                AssignFile(files, key, val);
            }
            return files;
        }

        static void AssignFile(MDefFiles files, string key, string val)
        {
            switch (key)
            {
                case "cmd": files.Cmd = val; break;
                case "cns": files.Cns = val; break;
                case "stcommon": files.StCommon = val; break;
                case "sprite": files.Sprite = val; break;
                case "anim": files.Anim = val; break;
                default:
                    // st / st1 / st2 … → 状态文件列表（按出现顺序）。
                    if (key == "st" || (key.StartsWith("st") && key.Length > 2 && IsDigits(key.Substring(2))))
                    {
                        files.St.Add(val);
                    }
                    break;
            }
        }

        static bool IsDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                {
                    return false;
                }
            }
            return s.Length > 0;
        }

        static string StripComment(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi) : line;
        }
    }
}
