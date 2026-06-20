using System;
using System.Collections.Generic;
using System.IO;

namespace Lockstep.View
{
    /// <summary>
    /// 解析角色 .def 的 [Files] 段，按 MUGEN 约定取 sprite/anim/cmd/cns 等所指文件。
    /// 解决"一个角色文件夹里有多个 .sff/.def"的歧义（如 kfm 有 ending/intro/kfm.sff，
    /// 不能靠字母序乱猜）。主 def 优先取 &lt;文件夹名&gt;.def，否则取含 cmd= 的（排除纯故事板 def）。
    /// </summary>
    public static class MugenDef
    {
        public static string SpritePath(string folder)
        {
            return ResolveFile(folder, "sprite", "*.sff");
        }

        public static string AnimPath(string folder)
        {
            return ResolveFile(folder, "anim", "*.air");
        }

        public static string CmdPath(string folder)
        {
            return ResolveFile(folder, "cmd", "*.cmd");
        }

        /// <summary>角色常量 cns（[Files] cns=）；找不到回退首个 .cns。</summary>
        public static string ConstantsPath(string folder)
        {
            return ResolveFile(folder, "cns", "*.cns");
        }

        /// <summary>角色状态文件集合：常量 cns + st/st0..stN（状态常分散其中），去重，仅存在者。</summary>
        public static List<string> StatePaths(string folder)
        {
            List<string> list = new List<string>();
            string cns = ConstantsPath(folder);
            if (cns != null && File.Exists(cns))
            {
                list.Add(cns);
            }
            string defPath = MainDef(folder);
            if (defPath != null)
            {
                string[] keys = { "st", "st0", "st1", "st2", "st3", "st4", "st5", "st6", "st7" };
                for (int i = 0; i < keys.Length; i++)
                {
                    string fileName = ReadFilesEntry(defPath, keys[i]);
                    if (fileName == null)
                    {
                        continue;
                    }
                    string full = Path.Combine(folder, fileName);
                    if (File.Exists(full) && !list.Contains(full))
                    {
                        list.Add(full);
                    }
                }
            }
            return list;
        }

        static string ResolveFile(string folder, string key, string fallbackPattern)
        {
            string defPath = MainDef(folder);
            if (defPath != null)
            {
                string fileName = ReadFilesEntry(defPath, key);
                if (fileName != null)
                {
                    string full = Path.Combine(folder, fileName);
                    if (File.Exists(full))
                    {
                        return full;
                    }
                }
            }
            return MugenAssetPaths.FirstFile(folder, fallbackPattern);
        }

        static string MainDef(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }
            string[] defs = Directory.GetFiles(folder, "*.def");
            Array.Sort(defs, StringComparer.OrdinalIgnoreCase);
            if (defs.Length == 0)
            {
                return null;
            }
            string folderName = Path.GetFileName(folder);
            // 1) <文件夹名>.def
            foreach (string def in defs)
            {
                if (Path.GetFileNameWithoutExtension(def).Equals(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    return def;
                }
            }
            // 2) 含 cmd= 的 def（角色主文件；intro/ending 故事板没有 cmd）
            foreach (string def in defs)
            {
                if (ReadFilesEntry(def, "cmd") != null)
                {
                    return def;
                }
            }
            // 3) 兜底第一个
            return defs[0];
        }

        // 读 [Files] 段里 `key = value`（忽略大小写、去 ; 注释、去引号）。找不到返回 null。
        static string ReadFilesEntry(string defPath, string key)
        {
            bool inFiles = false;
            foreach (string rawLine in File.ReadAllLines(defPath))
            {
                string line = rawLine;
                int comment = line.IndexOf(';');
                if (comment >= 0)
                {
                    line = line.Substring(0, comment);
                }
                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line[0] == '[')
                {
                    inFiles = line.Replace(" ", string.Empty).Equals("[files]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inFiles)
                {
                    continue;
                }
                int equals = line.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }
                string name = line.Substring(0, equals).Trim();
                if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(equals + 1).Trim().Trim('"');
                }
            }
            return null;
        }
    }
}
