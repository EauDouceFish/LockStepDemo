using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockstep.View
{
    public static class MugenChineseText
    {
        static readonly Dictionary<string, string> CharacterNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ananzi", "安南齐" },
                { "Animus", "灵魂使" },
                { "Final", "终焉" },
                { "Hashi", "哈希" },
                { "Janos", "亚诺斯" },
                { "kfm", "功夫男" },
                { "Kung Fu Man", "功夫男" },
                { "Maxine", "玛克辛" },
                { "Noroko", "诺罗子" },
                { "Peketo", "佩克托" },
                { "Shar-Makai", "沙尔魔界" },
                { "Terrarian", "泰拉瑞亚人" },
                { "Gustavo", "古斯塔沃" },
            };

        static UnityEngine.Font _font;

        public static UnityEngine.Font Font()
        {
            if (_font != null)
            {
                return _font;
            }

            string[] candidates =
            {
                "Noto Sans CJK SC",
                "Noto Sans SC",
                "Droid Sans Fallback",
                "Microsoft YaHei",
                "SimHei",
                "PingFang SC",
                "Arial Unicode MS",
                "Arial",
            };

            try
            {
                _font = UnityEngine.Font.CreateDynamicFontFromOSFont(candidates, 32);
            }
            catch
            {
                _font = null;
            }

            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
            }
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<UnityEngine.Font>("Arial.ttf");
            }
            return _font;
        }

        public static string CharacterName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
            {
                return "";
            }
            return CharacterNames.TryGetValue(rawName, out string name) ? name : rawName;
        }

        public static string MatchMode(MugenMatchMode mode)
        {
            switch (mode)
            {
                case MugenMatchMode.LocalVersus: return "本地双人";
                case MugenMatchMode.NetKcp: return "在线匹配";
                default: return "人机对战";
            }
        }
    }
}
