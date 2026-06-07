using System.Collections.Generic;

namespace Lockstep.Mugen.Command
{
    public static class MCommandMoveHelpFormatter
    {
        public static string FormatMotion(string motion)
        {
            if (string.IsNullOrWhiteSpace(motion))
            {
                return "无输入";
            }

            string[] steps = motion.Split(',');
            List<string> labels = new List<string>();
            for (int i = 0; i < steps.Length; i++)
            {
                string step = steps[i].Trim();
                if (step.Length == 0)
                {
                    continue;
                }
                labels.Add(FormatStep(step));
            }
            return labels.Count == 0 ? "无输入" : string.Join(" -> ", labels.ToArray());
        }

        public static string FormatMove(MCommandMoveInfo move)
        {
            if (move == null)
            {
                return "";
            }

            string target = move.TargetStateNo.HasValue ? move.TargetStateNo.Value.ToString() : move.TargetValue;
            return "招式 " + move.CommandText + "：搓法 " + FormatMotion(move.MotionText) + "，进入状态 " + target;
        }

        public static string KeyboardLegend()
        {
            return "键盘：方向键=移动，A/S/D=轻拳x/重拳y/三拳z，Z/X/C=轻脚a/重脚b/三脚c，空格/回车=Start";
        }

        static string FormatStep(string step)
        {
            string[] parts = step.Split('+');
            List<string> labels = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.Length > 0)
                {
                    labels.Add(FormatToken(token));
                }
            }
            return string.Join("+", labels.ToArray());
        }

        static string FormatToken(string token)
        {
            bool release = token.IndexOf('~') >= 0;
            bool hold = token.IndexOf('/') >= 0;
            bool anyDirection = token.IndexOf('$') >= 0;

            string clean = token.Replace("~", "").Replace("/", "").Replace("$", "").Replace(">", "");
            int digit = 0;
            while (digit < clean.Length && char.IsDigit(clean[digit]))
            {
                digit++;
            }
            string holdFrames = digit > 0 ? clean.Substring(0, digit) : "";
            clean = clean.Substring(digit);

            string label = anyDirection ? AnyDirectionLabel(clean) : KeyLabel(clean);
            if (holdFrames.Length > 0)
            {
                label = "按住" + label + holdFrames + "帧";
            }
            else if (hold)
            {
                label = "按住" + label;
            }
            if (release)
            {
                label = "松开" + label;
            }
            return label;
        }

        static string KeyLabel(string key)
        {
            switch (key)
            {
                case "B": return "后";
                case "DB": return "下后";
                case "D": return "下";
                case "DF": return "下前";
                case "F": return "前";
                case "UF": return "上前";
                case "U": return "上";
                case "UB": return "上后";
                case "x": return "轻拳x(A键)";
                case "y": return "重拳y(S键)";
                case "z": return "三拳z(D键)";
                case "a": return "轻脚a(Z键)";
                case "b": return "重脚b(X键)";
                case "c": return "三脚c(C键)";
                case "s": return "Start(空格/回车)";
                default: return key;
            }
        }

        static string AnyDirectionLabel(string key)
        {
            switch (key)
            {
                case "B": return "任意后方向";
                case "D": return "任意下方向";
                case "F": return "任意前方向";
                case "U": return "任意上方向";
                default: return KeyLabel(key);
            }
        }
    }
}
