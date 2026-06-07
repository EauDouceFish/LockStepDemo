// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/input.go (ReadCommandSymbols)。把 CMD 命令串解析为 MCommandDef。
// 支持：方向 B/F/U/D + 对角 DB/DF/UB/UF；按钮 a/b/c/x/y/z/s；修饰 '~'(释放,带数字=蓄力)、'/'(按住)、'$'(4way)；
//       步内 '+' (同时按 AND) 或 '|' (任一 OR，二者不混用)；'>' (禁止中间无关输入变化)。
using System.Collections.Generic;

namespace Lockstep.Mugen.Command
{
    public static class MCommandParser
    {
        /// <summary>解析命令动作串(如 "~D, DF, F, x")为步骤序列。</summary>
        public static MCommandDef Parse(string name, string motion, int time = 15, int bufferTime = 1)
        {
            MCommandDef def = new MCommandDef { Name = name, Time = time, BufferTime = bufferTime };
            if (string.IsNullOrWhiteSpace(motion))
            {
                return def;
            }
            string[] stepStrs = motion.Split(',');
            for (int s = 0; s < stepStrs.Length; s++)
            {
                string raw = stepStrs[s].Trim();
                if (raw.Length == 0)
                {
                    continue;
                }
                MCommandStep step = new MCommandStep();
                // '|' OR（任一键满足）优先于 '+' AND（全部满足）；两者不混用
                string[] parts;
                if (raw.IndexOf('|') >= 0)
                {
                    step.OrLogic = true;
                    parts = raw.Split('|');
                }
                else
                {
                    parts = raw.Split('+');
                }
                for (int k = 0; k < parts.Length; k++)
                {
                    string token = parts[k].Trim();
                    if (token.Length > 0)
                    {
                        step.Keys.Add(ParseKey(token, ref step.Greater));
                    }
                }
                if (step.Keys.Count > 0)
                {
                    def.Steps.Add(step);
                }
            }
            return def;
        }

        static MCommandKey ParseKey(string token, ref bool greater)
        {
            MCommandKey key = new MCommandKey();
            int i = 0;
            // 前缀修饰
            while (i < token.Length)
            {
                char ch = token[i];
                if (ch == '>')
                {
                    greater = true;
                    i++;
                }
                else if (ch == '~' || ch == '/')
                {
                    key.Release |= ch == '~';
                    key.Hold |= ch == '/';
                    i++;
                    int digitsStart = i;
                    while (i < token.Length && char.IsDigit(token[i]))
                    {
                        i++;
                    }
                    if (i > digitsStart)
                    {
                        key.ChargeTime = int.Parse(token.Substring(digitsStart, i - digitsStart));
                        // Ikemen/MUGEN 的 ~N 是“至少按住 N 帧后释放”，不是持续按住。
                    }
                }
                else if (ch == '$')
                {
                    key.Dollar = true;
                    i++;
                }
                else
                {
                    break;
                }
            }
            // MUGEN 大小写敏感：方向大写 B/F/U/D/DB/DF/UB/UF；按钮小写 a/b/c/x/y/z/s。
            string body = token.Substring(i).Trim();
            ApplyKeyBody(ref key, body);
            return key;
        }

        static void ApplyKeyBody(ref MCommandKey key, string body)
        {
            switch (body)
            {
                case "B": key.IsBack = true; return;
                case "F": key.IsFwd = true; return;
                case "L": key.Bits = MInput.Left; return;
                case "R": key.Bits = MInput.Right; return;
                case "N": key.IsNeutral = true; return;
                case "U": key.Bits = MInput.Up; return;
                case "D": key.Bits = MInput.Down; return;
                case "DB": key.Bits = MInput.Down; key.IsBack = true; return;
                case "DF": key.Bits = MInput.Down; key.IsFwd = true; return;
                case "UB": key.Bits = MInput.Up; key.IsBack = true; return;
                case "UF": key.Bits = MInput.Up; key.IsFwd = true; return;
            }
            key.IsButton = true;
            switch (body.ToLowerInvariant())
            {
                case "a": key.Bits = MInput.A; break;
                case "b": key.Bits = MInput.B; break;
                case "c": key.Bits = MInput.C; break;
                case "x": key.Bits = MInput.X; break;
                case "y": key.Bits = MInput.Y; break;
                case "z": key.Bits = MInput.Z; break;
                case "s": key.Bits = MInput.S; break;
                default: key.Bits = MInput.None; break;
            }
        }
    }
}
