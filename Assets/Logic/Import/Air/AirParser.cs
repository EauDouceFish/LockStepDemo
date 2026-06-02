using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Lockstep.Game.Data;
using Lockstep.Math;

namespace Lockstep.Import.Air
{
    /// <summary>
    /// MUGEN .air → AnimData 解析器。纯文本、整数坐标（像素），无 float、无 Unity → dotnet 可测。
    /// 处理：[Begin Action N]、Clsn1/Clsn2（Default 持久 / 非 Default 仅作用下一帧）、帧行
    /// (group,image,offx,offy,duration[,flip])、LoopStart、注释(;)。坐标按 MUGEN 原生（高度上为负）。
    /// </summary>
    public static class AirParser
    {
        /// <summary>读取并解析 .air 文件，返回按出现顺序排列的全部动画。</summary>
        public static List<AnimData> ParseFile(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        /// <summary>解析 .air 文本内容为动画列表。</summary>
        public static List<AnimData> Parse(string text)
        {
            List<AnimData> result = new List<AnimData>();

            AnimData current = null;
            List<AnimFrame> frames = null;
            int loopStart = 0;

            // Clsn 状态
            List<ClsnBox> default1 = new List<ClsnBox>();
            List<ClsnBox> default2 = new List<ClsnBox>();
            List<ClsnBox> pending1 = new List<ClsnBox>();
            List<ClsnBox> pending2 = new List<ClsnBox>();
            bool pendingSet1 = false;
            bool pendingSet2 = false;
            bool curDefault1 = false;
            bool curDefault2 = false;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines)
            {
                string line = StripComment(rawLine).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                string lower = line.ToLowerInvariant();

                // ── 新 action ──
                if (line[0] == '[')
                {
                    FlushAction(result, ref current, ref frames, ref loopStart);
                    int id = ParseActionId(line);
                    current = new AnimData { Id = id };
                    frames = new List<AnimFrame>();
                    loopStart = 0;
                    default1.Clear();
                    default2.Clear();
                    pending1.Clear();
                    pending2.Clear();
                    pendingSet1 = false;
                    pendingSet2 = false;
                    continue;
                }

                if (current == null)
                {
                    continue; // action 之前的杂项行
                }

                // ── LoopStart ──
                if (lower.StartsWith("loopstart"))
                {
                    loopStart = frames.Count;
                    continue;
                }

                // ── Clsn 头 (含 ':') ──
                if (lower.StartsWith("clsn") && line.Contains(":"))
                {
                    bool isDefault = lower.Contains("default");
                    int which = lower.Contains("clsn1") ? 1 : 2;
                    if (which == 1)
                    {
                        curDefault1 = isDefault;
                        if (isDefault)
                        {
                            default1.Clear();
                        }
                        else
                        {
                            pending1.Clear();
                            pendingSet1 = true;
                        }
                    }
                    else
                    {
                        curDefault2 = isDefault;
                        if (isDefault)
                        {
                            default2.Clear();
                        }
                        else
                        {
                            pending2.Clear();
                            pendingSet2 = true;
                        }
                    }
                    continue;
                }

                // ── Clsn 框行 (含 '[' 和 '=') ──
                if (lower.StartsWith("clsn") && line.Contains("[") && line.Contains("="))
                {
                    int which = lower.StartsWith("clsn1") ? 1 : 2;
                    ClsnBox box = ParseBox(line);
                    if (which == 1)
                    {
                        (curDefault1 ? default1 : pending1).Add(box);
                    }
                    else
                    {
                        (curDefault2 ? default2 : pending2).Add(box);
                    }
                    continue;
                }

                // ── 帧行 (以数字/负号开头) ──
                if (line[0] == '-' || (line[0] >= '0' && line[0] <= '9'))
                {
                    AnimFrame frame = ParseFrame(line);
                    frame.Clsn1 = (pendingSet1 ? pending1 : default1).ToArray();
                    frame.Clsn2 = (pendingSet2 ? pending2 : default2).ToArray();
                    frames.Add(frame);

                    pending1.Clear();
                    pending2.Clear();
                    pendingSet1 = false;
                    pendingSet2 = false;
                    continue;
                }
            }

            FlushAction(result, ref current, ref frames, ref loopStart);
            return result;
        }

        /// <summary>把动画列表转成"动画号 → AnimData"字典，便于按 Anim 号查。</summary>
        public static Dictionary<int, AnimData> ToDictionary(List<AnimData> anims)
        {
            Dictionary<int, AnimData> map = new Dictionary<int, AnimData>();
            for (int i = 0; i < anims.Count; i++)
            {
                map[anims[i].Id] = anims[i];
            }
            return map;
        }

        // ───────────────────── helpers ─────────────────────

        static void FlushAction(List<AnimData> result, ref AnimData current, ref List<AnimFrame> frames, ref int loopStart)
        {
            if (current != null)
            {
                current.Frames = frames.ToArray();
                current.LoopStart = loopStart;
                result.Add(current);
            }
            current = null;
            frames = null;
            loopStart = 0;
        }

        static string StripComment(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi) : line;
        }

        static int ParseActionId(string line)
        {
            // "[Begin Action 0]" → 0
            int rb = line.IndexOf(']');
            string inner = (rb > 0 ? line.Substring(1, rb - 1) : line.Substring(1)).Trim();
            int lastSpace = inner.LastIndexOf(' ');
            string num = lastSpace >= 0 ? inner.Substring(lastSpace + 1) : inner;
            return ParseInt(num);
        }

        static ClsnBox ParseBox(string line)
        {
            int equalsAt = line.IndexOf('=');
            string[] coords = line.Substring(equalsAt + 1).Split(',');
            return new ClsnBox(
                FFloat.FromInt(ParseInt(coords[0])),
                FFloat.FromInt(ParseInt(coords[1])),
                FFloat.FromInt(ParseInt(coords[2])),
                FFloat.FromInt(ParseInt(coords[3])));
        }

        static AnimFrame ParseFrame(string line)
        {
            string[] parts = line.Split(',');
            AnimFrame frame = new AnimFrame
            {
                SpriteGroup = ParseInt(parts[0]),
                SpriteImage = ParseInt(parts[1]),
                OffX = FFloat.FromInt(ParseInt(parts[2])),
                OffY = FFloat.FromInt(ParseInt(parts[3])),
                Duration = ParseInt(parts[4]),
                Flip = FlipFlags.None,
            };
            if (parts.Length >= 6)
            {
                string flip = parts[5].Trim().ToUpperInvariant();
                if (flip.Contains("H"))
                {
                    frame.Flip |= FlipFlags.Horizontal;
                }
                if (flip.Contains("V"))
                {
                    frame.Flip |= FlipFlags.Vertical;
                }
            }
            return frame;
        }

        static int ParseInt(string text)
        {
            return int.Parse(text.Trim(), CultureInfo.InvariantCulture);
        }
    }
}
