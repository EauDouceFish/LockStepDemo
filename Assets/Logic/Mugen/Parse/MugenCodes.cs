// 共享码表：MUGEN 文本里的字母枚举(statetype/movetype/physics 首字母、攻击类别二字母、命中/守招标志)
// → 引擎内部数值码。原先散落在 MugenCnsParser 各私有方法，抽出集中为单一真相源（去魔数/去重复）。
// 数值约定对齐 M2 编译器与 MChar 比较：StateType S=1/C=2/A=4/L=8/N=16；MoveType I=1/H=2/A=4。
// See Docs/移植方案_Ikemen.md。
using System.Collections.Generic;
using Lockstep.Mugen.Hit;

namespace Lockstep.Mugen.Parse
{
    /// <summary>MUGEN 字母枚举 ↔ 引擎数值码的集中映射（解析期使用，纯静态无状态）。</summary>
    public static class MugenCodes
    {
        /// <summary>statetype 首字母 → 码（S=1/C=2/A=4/L=8）；无法识别返回 -1（=不约束/不改）。</summary>
        public static int StateType(string value)
        {
            switch (FirstLetter(value))
            {
                case 's': return 1;
                case 'c': return 2;
                case 'a': return 4;
                case 'l': return 8;
                default: return -1;
            }
        }

        /// <summary>movetype 首字母 → 码（I=1/H=2/A=4）；无法识别返回 -1。</summary>
        public static int MoveType(string value)
        {
            switch (FirstLetter(value))
            {
                case 'i': return 1;
                case 'h': return 2;
                case 'a': return 4;
                default: return -1;
            }
        }

        /// <summary>physics 首字母 → 码（S=1/C=2/A=4/N=16）；无法识别返回 -1。</summary>
        public static int Physics(string value)
        {
            switch (FirstLetter(value))
            {
                case 's': return 1;
                case 'c': return 2;
                case 'a': return 4;
                case 'n': return 16;
                default: return -1;
            }
        }

        /// <summary>二字母攻击类别(NA/SP/HT/AA…) → <see cref="MAttackType"/> 位标志；未识别返回 0。</summary>
        public static int AttackType(string token)
        {
            switch (token.Trim().ToUpperInvariant())
            {
                case "NA": return (int)MAttackType.NA;
                case "NT": return (int)MAttackType.NT;
                case "NP": return (int)MAttackType.NP;
                case "SA": return (int)MAttackType.SA;
                case "ST": return (int)MAttackType.ST;
                case "SP": return (int)MAttackType.SP;
                case "HA": return (int)MAttackType.HA;
                case "HT": return (int)MAttackType.HT;
                case "HP": return (int)MAttackType.HP;
                case "AA": return (int)MAttackType.AA;
                case "AT": return (int)MAttackType.AT;
                case "AP": return (int)MAttackType.AP;
                default: return 0;
            }
        }

        /// <summary>
        /// attr / HitBy value：格式 `&lt;statetype&gt;, &lt;攻击类别&gt;...`（如 `S, NA` 或 `SCA, AA`）。
        /// 首段是攻方 statetype(本简化版不入 bitmask)，其余 2 字母攻击类别 → 合成 bitmask。
        /// 未写攻击类别 → 全部（容错，HitBy 常用 AA）。
        /// </summary>
        public static int Attr(string value)
        {
            int mask = 0;
            string[] tokens = value.Split(',');
            for (int i = 1; i < tokens.Length; i++)
            {
                mask |= AttackType(tokens[i]);
            }
            return mask != 0 ? mask : (int)MAttackType.All;
        }

        /// <summary>
        /// 命中/守招标志串(hitflag/guardflag, 如 `HLA`/`MA`) → (high, low, air[, down])。
        /// M=High|Low（中段，同时含 H 与 L）。down 仅 hitflag 用，guardflag 调用方忽略 down。
        /// </summary>
        public static void HitFlags(string flags, out bool high, out bool low, out bool air, out bool down)
        {
            string f = flags.ToUpperInvariant();
            high = f.IndexOf('H') >= 0 || f.IndexOf('M') >= 0;
            low = f.IndexOf('L') >= 0 || f.IndexOf('M') >= 0;
            air = f.IndexOf('A') >= 0;
            down = f.IndexOf('D') >= 0;
        }

        /// <summary>取 trim+小写后的首字母（空串返回 '\0'）。供 statetype/movetype/physics 判定。</summary>
        public static char FirstLetter(string value)
        {
            string t = value.Trim().ToLowerInvariant();
            return t.Length > 0 ? t[0] : '\0';
        }
    }
}
