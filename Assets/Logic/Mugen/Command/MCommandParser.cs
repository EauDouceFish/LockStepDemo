// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/input.go (ReadCommandSymbols)。把 CMD 命令串解析为 MCommandDef。
// 支持：方向 B/F/U/D + 对角 DB/DF/UB/UF；按钮 a/b/c/x/y/z/s；修饰 '~'(释放,带数字=蓄力)、'/'(按住)、'$'(4way)；
//       步内 '+' (同时按 AND)；步前 '>' (严格相邻)。'|'(OR) 暂不支持(降级为 +)。See Docs/移植方案_Ikemen.md.
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
                if (raw[0] == '>')
                {
                    step.Greater = true;
                    raw = raw.Substring(1).Trim();
                }
                string[] parts = raw.Split('+');   // AND（'|' OR 暂降级）
                for (int k = 0; k < parts.Length; k++)
                {
                    string token = parts[k].Trim();
                    if (token.Length > 0)
                    {
                        step.Keys.Add(ParseKey(token));
                    }
                }
                if (step.Keys.Count > 0)
                {
                    def.Steps.Add(step);
                }
            }
            return def;
        }

        static MCommandKey ParseKey(string token)
        {
            MCommandKey key = new MCommandKey();
            int i = 0;
            // 前缀修饰
            while (i < token.Length)
            {
                char ch = token[i];
                if (ch == '~')
                {
                    key.Release = true;
                    i++;
                    int digitsStart = i;
                    while (i < token.Length && char.IsDigit(token[i]))
                    {
                        i++;
                    }
                    if (i > digitsStart)
                    {
                        key.ChargeTime = int.Parse(token.Substring(digitsStart, i - digitsStart));
                        key.Release = false;   // 带数字 = 蓄力(按住 N 帧)，非释放
                    }
                }
                else if (ch == '/')
                {
                    key.Hold = true;
                    i++;
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
