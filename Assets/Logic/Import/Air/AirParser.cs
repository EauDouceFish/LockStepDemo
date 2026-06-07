using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Lockstep.Game.Data;
using Lockstep.Math;

namespace Lockstep.Import.Air
{
    /// <summary>MUGEN AIR parser. Produces immutable-style animation definitions without Unity types.</summary>
    public static class AirParser
    {
        const int MaxCopyDepth = 8;

        sealed class RawAction
        {
            public AnimData Data;
            public int CopyAction = -1;
            public bool HasCopyAction;
            public bool Terminated;
            public RawAction Next;
        }

        public static List<AnimData> ParseFile(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public static List<AnimData> Parse(string text)
        {
            List<RawAction> parsed = ParseRaw(text ?? string.Empty);
            Dictionary<int, RawAction> firstById = new Dictionary<int, RawAction>();
            List<RawAction> unique = new List<RawAction>();
            for (int i = 0; i < parsed.Count; i++)
            {
                if (!firstById.ContainsKey(parsed[i].Data.Id))
                {
                    firstById.Add(parsed[i].Data.Id, parsed[i]);
                    unique.Add(parsed[i]);
                }
            }

            List<AnimData> result = new List<AnimData>();
            for (int i = 0; i < unique.Count; i++)
            {
                AnimData resolved = Resolve(unique[i], firstById, new HashSet<RawAction>(), 0);
                if (resolved != null)
                {
                    result.Add(resolved);
                }
            }
            return result;
        }

        public static Dictionary<int, AnimData> ToDictionary(List<AnimData> anims)
        {
            Dictionary<int, AnimData> map = new Dictionary<int, AnimData>();
            for (int i = 0; i < anims.Count; i++)
            {
                if (!map.ContainsKey(anims[i].Id))
                {
                    map.Add(anims[i].Id, anims[i]);
                }
            }
            return map;
        }

        static List<RawAction> ParseRaw(string text)
        {
            List<RawAction> result = new List<RawAction>();
            RawAction current = null;
            List<AnimFrame> frames = null;
            int loopStart = 0;

            List<ClsnBox> default1 = new List<ClsnBox>();
            List<ClsnBox> default2 = new List<ClsnBox>();
            List<ClsnBox> pending1 = new List<ClsnBox>();
            List<ClsnBox> pending2 = new List<ClsnBox>();
            bool pendingSet1 = false;
            bool pendingSet2 = false;
            bool currentDefault1 = false;
            bool currentDefault2 = false;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = StripComment(lines[lineIndex]).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                int actionId;
                if (TryParseActionId(line, out actionId))
                {
                    Flush(result, ref current, ref frames, ref loopStart);
                    current = new RawAction { Data = new AnimData { Id = actionId } };
                    frames = new List<AnimFrame>();
                    ResetCollision(default1, default2, pending1, pending2,
                        ref pendingSet1, ref pendingSet2, ref currentDefault1, ref currentDefault2);
                    continue;
                }
                if (line[0] == '[')
                {
                    Flush(result, ref current, ref frames, ref loopStart);
                    ResetCollision(default1, default2, pending1, pending2,
                        ref pendingSet1, ref pendingSet2, ref currentDefault1, ref currentDefault2);
                    continue;
                }
                if (current == null || current.Terminated)
                {
                    continue;
                }

                string lower = line.ToLowerInvariant();
                if (lower.StartsWith("copy action ", StringComparison.Ordinal))
                {
                    int copyId;
                    string number = line.Substring(12).Trim();
                    if (int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out copyId)
                        && copyId >= 0)
                    {
                        current.CopyAction = copyId;
                        current.HasCopyAction = true;
                    }
                    current.Terminated = true;
                    continue;
                }
                if (lower.StartsWith("loopstart", StringComparison.Ordinal))
                {
                    loopStart = frames.Count;
                    continue;
                }
                if (lower.StartsWith("clsn", StringComparison.Ordinal) && line.Contains(":"))
                {
                    bool isDefault = lower.Contains("default");
                    bool isClsn1 = lower.Contains("clsn1");
                    if (isClsn1)
                    {
                        currentDefault1 = isDefault;
                        (isDefault ? default1 : pending1).Clear();
                        if (!isDefault)
                        {
                            pendingSet1 = true;
                        }
                    }
                    else
                    {
                        currentDefault2 = isDefault;
                        (isDefault ? default2 : pending2).Clear();
                        if (!isDefault)
                        {
                            pendingSet2 = true;
                        }
                    }
                    continue;
                }
                if (lower.StartsWith("clsn", StringComparison.Ordinal)
                    && line.Contains("[") && line.Contains("="))
                {
                    ClsnBox box = ParseBox(line);
                    bool isClsn1 = lower.StartsWith("clsn1", StringComparison.Ordinal);
                    if (isClsn1)
                    {
                        (currentDefault1 ? default1 : pending1).Add(box);
                    }
                    else
                    {
                        (currentDefault2 ? default2 : pending2).Add(box);
                    }
                    continue;
                }
                if (line[0] == '-' || char.IsDigit(line[0]))
                {
                    AnimFrame frame = ParseFrame(line);
                    frame.Clsn1 = (pendingSet1 ? pending1 : default1).ToArray();
                    frame.Clsn2 = (pendingSet2 ? pending2 : default2).ToArray();
                    frames.Add(frame);
                    pending1.Clear();
                    pending2.Clear();
                    pendingSet1 = false;
                    pendingSet2 = false;
                }
            }

            Flush(result, ref current, ref frames, ref loopStart);
            for (int i = 0; i + 1 < result.Count; i++)
            {
                result[i].Next = result[i + 1];
            }
            return result;
        }

        static AnimData Resolve(RawAction action, Dictionary<int, RawAction> firstById,
            HashSet<RawAction> chain, int depth)
        {
            if (!chain.Add(action))
            {
                return null;
            }

            RawAction target = null;
            if (action.HasCopyAction)
            {
                if (depth >= MaxCopyDepth || !firstById.TryGetValue(action.CopyAction, out target)
                    || ReferenceEquals(action, target))
                {
                    chain.Remove(action);
                    return null;
                }
            }
            else if (action.Data.Frames.Length == 0 && action.Next != null)
            {
                RawAction canonical;
                target = firstById.TryGetValue(action.Next.Data.Id, out canonical) ? canonical : action.Next;
            }

            AnimData result;
            if (target == null)
            {
                result = action.Data;
            }
            else
            {
                AnimData resolvedTarget = Resolve(target, firstById, chain, depth + 1);
                result = resolvedTarget == null ? null : CloneWithId(resolvedTarget, action.Data.Id);
            }
            chain.Remove(action);
            return result;
        }

        static AnimData CloneWithId(AnimData source, int id)
        {
            return new AnimData
            {
                Id = id,
                Frames = (AnimFrame[])source.Frames.Clone(),
                LoopStart = source.LoopStart,
            };
        }

        static void Flush(List<RawAction> result, ref RawAction current,
            ref List<AnimFrame> frames, ref int loopStart)
        {
            if (current != null)
            {
                current.Data.Frames = frames.ToArray();
                current.Data.LoopStart = loopStart;
                result.Add(current);
            }
            current = null;
            frames = null;
            loopStart = 0;
        }

        static void ResetCollision(List<ClsnBox> default1, List<ClsnBox> default2,
            List<ClsnBox> pending1, List<ClsnBox> pending2, ref bool pendingSet1,
            ref bool pendingSet2, ref bool currentDefault1, ref bool currentDefault2)
        {
            default1.Clear();
            default2.Clear();
            pending1.Clear();
            pending2.Clear();
            pendingSet1 = false;
            pendingSet2 = false;
            currentDefault1 = false;
            currentDefault2 = false;
        }

        static string StripComment(string line)
        {
            int semicolon = line.IndexOf(';');
            return semicolon >= 0 ? line.Substring(0, semicolon) : line;
        }

        static bool TryParseActionId(string line, out int id)
        {
            id = 0;
            if (line.Length < 3 || line[0] != '[')
            {
                return false;
            }
            int closing = line.IndexOf(']');
            if (closing < 0)
            {
                return false;
            }
            string inner = line.Substring(1, closing - 1).Trim();
            const string prefix = "begin action";
            if (!inner.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string number = inner.Substring(prefix.Length).Trim();
            return number.Length > 0
                && int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }

        static ClsnBox ParseBox(string line)
        {
            int equals = line.IndexOf('=');
            string[] values = line.Substring(equals + 1).Split(',');
            if (values.Length < 4)
            {
                throw new FormatException("AIR collision box requires four coordinates");
            }
            return new ClsnBox(
                FFloat.FromInt(ParseInt(values[0])),
                FFloat.FromInt(ParseInt(values[1])),
                FFloat.FromInt(ParseInt(values[2])),
                FFloat.FromInt(ParseInt(values[3])));
        }

        static AnimFrame ParseFrame(string line)
        {
            string[] parts = line.Split(',');
            if (parts.Length < 5)
            {
                throw new FormatException("AIR frame requires at least five fields");
            }
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
                if (flip.Contains("H")) frame.Flip |= FlipFlags.Horizontal;
                if (flip.Contains("V")) frame.Flip |= FlipFlags.Vertical;
            }
            return frame;
        }

        static int ParseInt(string text)
        {
            return int.Parse(text.Trim(), CultureInfo.InvariantCulture);
        }
    }
}
