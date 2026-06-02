// Behavior-faithful reimplementation of Ikemen GO's expression compiler (MIT, (c) 2016 Suehiro et al.).
// Source: src/compiler.go (CharCompiler.expBoolOr..expValue 优先级链)。
// 编译器是编译期变换：兼容性取决于(1)运算符语义[已在 BytecodeOps 忠实移植] 与(2)优先级+trigger 映射[此处照搬]。
// 故采用按 Ikemen 优先级链重写的递归下降，产出与 M1 执行器约定一致的字节码，差分测试兜底。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Math;

namespace Lockstep.Mugen.Expr
{
    /// <summary>把 MUGEN 触发条件表达式字符串编译成 <see cref="BytecodeExp"/>。</summary>
    public sealed class MugenExprCompiler
    {
        enum TokKind { End, Number, FloatNumber, Ident, Op }

        struct Tok
        {
            public TokKind Kind;
            public string Text;
            public int IntValue;
            public FFloat FloatValue;
        }

        List<Tok> _toks;
        int _pos;
        readonly List<byte> _out = new List<byte>();

        public BytecodeExp Compile(string expr)
        {
            _toks = Tokenize(expr);
            _pos = 0;
            _out.Clear();
            ParseBoolOr();
            return new BytecodeExp(_out.ToArray());
        }

        // ───────── 词法 ─────────
        static List<Tok> Tokenize(string s)
        {
            List<Tok> toks = new List<Tok>();
            int i = 0;
            while (i < s.Length)
            {
                char ch = s[i];
                if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n')
                {
                    i++;
                    continue;
                }
                if (char.IsDigit(ch) || (ch == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                {
                    int start = i;
                    bool isFloat = false;
                    while (i < s.Length && char.IsDigit(s[i]))
                    {
                        i++;
                    }
                    if (i < s.Length && s[i] == '.')
                    {
                        isFloat = true;
                        i++;
                        while (i < s.Length && char.IsDigit(s[i]))
                        {
                            i++;
                        }
                    }
                    string num = s.Substring(start, i - start);
                    if (isFloat)
                    {
                        toks.Add(new Tok { Kind = TokKind.FloatNumber, Text = num, FloatValue = ParseFixed(num) });
                    }
                    else
                    {
                        int.TryParse(num, out int iv);
                        toks.Add(new Tok { Kind = TokKind.Number, Text = num, IntValue = iv });
                    }
                    continue;
                }
                if (char.IsLetter(ch) || ch == '_')
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                    {
                        i++;
                    }
                    toks.Add(new Tok { Kind = TokKind.Ident, Text = s.Substring(start, i - start).ToLowerInvariant() });
                    continue;
                }
                // 多字符运算符优先
                string two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                if (two == "**" || two == "!=" || two == ">=" || two == "<=" || two == "&&" || two == "||" || two == "^^" || two == ":=")
                {
                    toks.Add(new Tok { Kind = TokKind.Op, Text = two });
                    i += 2;
                    continue;
                }
                toks.Add(new Tok { Kind = TokKind.Op, Text = ch.ToString() });
                i++;
            }
            toks.Add(new Tok { Kind = TokKind.End, Text = "" });
            return toks;
        }

        // 文本定点解析（导入期允许）：整数部分 + 小数分子÷10^位数（一次除法，使 0.5/0.25 等精确）。
        static FFloat ParseFixed(string num)
        {
            int dot = num.IndexOf('.');
            string intPart = dot < 0 ? num : num.Substring(0, dot);
            string fracPart = dot < 0 ? "" : num.Substring(dot + 1);
            FFloat result = intPart.Length > 0 ? FFloat.FromInt(int.Parse(intPart)) : FFloat.Zero;
            if (fracPart.Length > 0)
            {
                if (fracPart.Length > 9)
                {
                    fracPart = fracPart.Substring(0, 9);   // 限位避免 10^k 溢出 int
                }
                int denom = 1;
                for (int k = 0; k < fracPart.Length; k++)
                {
                    denom *= 10;
                }
                result += FFloat.FromInt(int.Parse(fracPart)) / FFloat.FromInt(denom);
            }
            return result;
        }

        // ───────── 词法游标 ─────────
        Tok Cur => _toks[_pos];
        bool IsOp(string op) => Cur.Kind == TokKind.Op && Cur.Text == op;
        void Next() => _pos++;

        // ───────── 优先级链（低→高，照搬 Ikemen）─────────
        void ParseBoolOr()
        {
            ParseBoolXor();
            while (IsOp("||")) { Next(); ParseBoolXor(); Emit(OpCode.OC_blor); }
        }
        void ParseBoolXor()
        {
            ParseBoolAnd();
            while (IsOp("^^")) { Next(); ParseBoolAnd(); Emit(OpCode.OC_blxor); }
        }
        void ParseBoolAnd()
        {
            ParseOr();
            while (IsOp("&&")) { Next(); ParseOr(); Emit(OpCode.OC_bland); }
        }
        void ParseOr()
        {
            ParseXor();
            while (IsOp("|")) { Next(); ParseXor(); Emit(OpCode.OC_or); }
        }
        void ParseXor()
        {
            ParseAnd();
            while (IsOp("^")) { Next(); ParseAnd(); Emit(OpCode.OC_xor); }
        }
        void ParseAnd()
        {
            ParseEqne();
            while (IsOp("&")) { Next(); ParseEqne(); Emit(OpCode.OC_and); }
        }
        void ParseEqne()
        {
            ParseGrls();
            while (true)
            {
                if (IsOp("=")) { Next(); ParseGrls(); Emit(OpCode.OC_eq); }
                else if (IsOp("!=")) { Next(); ParseGrls(); Emit(OpCode.OC_ne); }
                else break;
            }
        }
        void ParseGrls()
        {
            ParseAddSub();
            while (true)
            {
                if (IsOp(">=")) { Next(); ParseAddSub(); Emit(OpCode.OC_ge); }
                else if (IsOp("<=")) { Next(); ParseAddSub(); Emit(OpCode.OC_le); }
                else if (IsOp(">")) { Next(); ParseAddSub(); Emit(OpCode.OC_gt); }
                else if (IsOp("<")) { Next(); ParseAddSub(); Emit(OpCode.OC_lt); }
                else break;
            }
        }
        void ParseAddSub()
        {
            ParseMulDiv();
            while (true)
            {
                if (IsOp("+")) { Next(); ParseMulDiv(); Emit(OpCode.OC_add); }
                else if (IsOp("-")) { Next(); ParseMulDiv(); Emit(OpCode.OC_sub); }
                else break;
            }
        }
        void ParseMulDiv()
        {
            ParsePow();
            while (true)
            {
                if (IsOp("*")) { Next(); ParsePow(); Emit(OpCode.OC_mul); }
                else if (IsOp("/")) { Next(); ParsePow(); Emit(OpCode.OC_div); }
                else if (IsOp("%")) { Next(); ParsePow(); Emit(OpCode.OC_mod); }
                else break;
            }
        }
        void ParsePow()
        {
            ParseUnary();
            // ** 右结合
            if (IsOp("**")) { Next(); ParsePow(); Emit(OpCode.OC_pow); }
        }
        void ParseUnary()
        {
            if (IsOp("-")) { Next(); ParseUnary(); Emit(OpCode.OC_neg); return; }
            if (IsOp("!")) { Next(); ParseUnary(); Emit(OpCode.OC_blnot); return; }
            if (IsOp("~")) { Next(); ParseUnary(); Emit(OpCode.OC_not); return; }
            ParseValue();
        }

        // ───────── 值：字面量 / 括号 / 函数 / trigger ─────────
        void ParseValue()
        {
            if (Cur.Kind == TokKind.Number)
            {
                EmitInt(Cur.IntValue);
                Next();
                return;
            }
            if (Cur.Kind == TokKind.FloatNumber)
            {
                EmitFloat(Cur.FloatValue);
                Next();
                return;
            }
            if (IsOp("("))
            {
                Next();
                ParseBoolOr();
                if (IsOp(")")) { Next(); }
                return;
            }
            if (Cur.Kind == TokKind.Ident)
            {
                ParseIdent();
                return;
            }
            // 无法识别 → 压 0（容错，详见诚实边界）
            EmitInt(0);
            if (Cur.Kind != TokKind.End) { Next(); }
        }

        void ParseIdent()
        {
            string name = Cur.Text;
            Next();

            // 函数调用：name(...)
            if (IsOp("("))
            {
                ParseFunction(name);
                return;
            }

            // 轴 trigger：pos x / vel y / screenpos x / hitvel z
            if ((name == "pos" || name == "vel" || name == "screenpos" || name == "hitvel") && Cur.Kind == TokKind.Ident)
            {
                string axis = Cur.Text;
                Next();
                Emit(AxisOpcode(name, axis));
                return;
            }

            // 无参 trigger
            if (NoArgTriggers.TryGetValue(name, out OpCode op))
            {
                Emit(op);
                return;
            }

            // 未知标识符 → 压 0（容错；statetype 字母枚举/redirect/command 待补）
            EmitInt(0);
        }

        void ParseFunction(string name)
        {
            Next(); // 吃掉 '('
            // var(n) / fvar(n)：压 index 再发 opcode
            if (name == "var" || name == "fvar" || name == "sysvar" || name == "sysfvar")
            {
                ParseBoolOr();
                Expect(")");
                Emit(name == "fvar" || name == "sysfvar" ? OpCode.OC_fvar : OpCode.OC_var);
                return;
            }
            if (name == "ifelse" || name == "cond")
            {
                ParseBoolOr(); Expect(",");   // cond
                ParseBoolOr(); Expect(",");   // trueVal
                ParseBoolOr(); Expect(")");   // falseVal
                Emit(OpCode.OC_ifelse);
                return;
            }
            if (name == "log")
            {
                ParseBoolOr(); Expect(",");
                ParseBoolOr(); Expect(")");
                Emit(OpCode.OC_log);
                return;
            }
            // 单参函数
            ParseBoolOr();
            Expect(")");
            if (UnaryFuncs.TryGetValue(name, out OpCode op))
            {
                Emit(op);
            }
            // 未知单参函数：保留参数值（不发 opcode）
        }

        void Expect(string op)
        {
            if (IsOp(op)) { Next(); }
        }

        static OpCode AxisOpcode(string name, string axis)
        {
            switch (name)
            {
                case "pos": return axis == "y" ? OpCode.OC_pos_y : OpCode.OC_pos_x;
                case "vel": return axis == "y" ? OpCode.OC_vel_y : axis == "z" ? OpCode.OC_vel_z : OpCode.OC_vel_x;
                case "screenpos": return axis == "y" ? OpCode.OC_screenpos_y : OpCode.OC_screenpos_x;
                case "hitvel": return axis == "y" ? OpCode.OC_hitvel_y : axis == "z" ? OpCode.OC_hitvel_z : OpCode.OC_hitvel_x;
                default: return OpCode.OC_pos_x;
            }
        }

        static readonly Dictionary<string, OpCode> NoArgTriggers = new Dictionary<string, OpCode>
        {
            ["time"] = OpCode.OC_time, ["statetime"] = OpCode.OC_time,
            ["stateno"] = OpCode.OC_stateno, ["prevstateno"] = OpCode.OC_prevstateno,
            ["statetype"] = OpCode.OC_statetype, ["movetype"] = OpCode.OC_movetype,
            ["ctrl"] = OpCode.OC_ctrl, ["anim"] = OpCode.OC_anim,
            ["life"] = OpCode.OC_life, ["lifemax"] = OpCode.OC_lifemax,
            ["power"] = OpCode.OC_power, ["powermax"] = OpCode.OC_powermax,
            ["alive"] = OpCode.OC_alive, ["facing"] = OpCode.OC_facing,
            ["random"] = OpCode.OC_random, ["gametime"] = OpCode.OC_gametime,
            ["animtime"] = OpCode.OC_animtime, ["animelemno"] = OpCode.OC_animelemno,
            ["stateno"] = OpCode.OC_stateno, ["id"] = OpCode.OC_id,
        };

        static readonly Dictionary<string, OpCode> UnaryFuncs = new Dictionary<string, OpCode>
        {
            ["abs"] = OpCode.OC_abs, ["exp"] = OpCode.OC_exp, ["ln"] = OpCode.OC_ln,
            ["sin"] = OpCode.OC_sin, ["cos"] = OpCode.OC_cos, ["tan"] = OpCode.OC_tan,
            ["acos"] = OpCode.OC_acos, ["asin"] = OpCode.OC_asin, ["atan"] = OpCode.OC_atan,
            ["floor"] = OpCode.OC_floor, ["ceil"] = OpCode.OC_ceil,
        };

        // ───────── 字节码发射（编码与 BytecodeExp.Run 一致）─────────
        void Emit(OpCode op) => _out.Add((byte)op);

        void EmitInt(int v)
        {
            _out.Add((byte)OpCode.OC_int);
            for (int k = 0; k < 4; k++) { _out.Add((byte)(v >> (8 * k))); }
        }

        void EmitFloat(FFloat v)
        {
            _out.Add((byte)OpCode.OC_float);
            long raw = v.Raw;
            for (int k = 0; k < 8; k++) { _out.Add((byte)(raw >> (8 * k))); }
        }
    }
}
