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
        enum TokKind { End, Number, FloatNumber, Ident, Op, Str }

        struct Tok
        {
            public TokKind Kind;
            public string Text;
            public int IntValue;
            public FFloat FloatValue;
        }

        List<Tok> _toks;
        int _pos;
        List<byte> _out = new List<byte>();   // 非 readonly：redirect 子表达式编译时临时切换缓冲

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
                if (ch == '"')
                {
                    int start = i + 1;
                    i++;
                    while (i < s.Length && s[i] != '"')
                    {
                        i++;
                    }
                    toks.Add(new Tok { Kind = TokKind.Str, Text = s.Substring(start, i - start) });
                    if (i < s.Length) { i++; }   // 跳过收尾引号
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
                bool ne = IsOp("!=");
                if (!ne && !IsOp("=")) { break; }
                Next();
                // = / != 后接 [ 或 ( → 区间比较：左值已在栈，压 min/max + OC_range_xx（!= 追加 blnot）
                if (IsOp("[") || IsOp("("))
                {
                    EmitRange(ne);
                }
                else
                {
                    ParseGrls();
                    Emit(ne ? OpCode.OC_ne : OpCode.OC_eq);
                }
            }
        }

        // 区间 [min,max] / (min,max] / [min,max) / (min,max)：左操作数已压栈，再压 min、max，发对应 range opcode。
        void EmitRange(bool negate)
        {
            bool incMin = IsOp("[");
            Next();                 // 吃掉 [ 或 (
            ParseBoolOr();          // min
            Expect(",");
            ParseBoolOr();          // max
            bool incMax = IsOp("]");
            if (IsOp("]") || IsOp(")")) { Next(); }
            OpCode rop = incMin
                ? (incMax ? OpCode.OC_range_ii : OpCode.OC_range_ie)
                : (incMax ? OpCode.OC_range_ei : OpCode.OC_range_ee);
            Emit(rop);
            if (negate) { Emit(OpCode.OC_blnot); }
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

            // 带索引 redirect：enemy(n),/enemynear(n), 或省略索引的 enemy,/enemynear,（索引默认 0）。
            // 须在函数调用判断之前，否则 enemy(0) 会被误当作函数 enemy(0)。
            if ((name == "enemy" || name == "enemynear") && (IsOp("(") || IsOp(",")))
            {
                EmitEnemyRedirect(name);
                return;
            }

            // 函数调用：name(...)
            if (IsOp("("))
            {
                ParseFunction(name);
                return;
            }

            // statetype/movetype/prevstatetype 字母枚举比较（自行消费 = / != 与字母列表，是 expValue 级原子布尔）
            if (name == "statetype" || name == "movetype" || name == "prevstatetype")
            {
                EmitTypeCompare(name);
                return;
            }

            // p2statetype/p2movetype：在 p2 上做 statetype/movetype 比较（用 p2 redirect 包裹）
            if (name == "p2statetype" || name == "p2movetype")
            {
                EmitP2TypeCompare(name.Substring(2));
                return;
            }

            // command = "name" / != "name"：发 OC_command + 名字串，运行期查命令是否 active
            if (name == "command")
            {
                EmitCommandCompare();
                return;
            }

            // redirect 前缀：p2,/root,/parent, + 单值子表达式（用 OC_run 包裹保证全程用重定向上下文）
            if ((name == "p2" || name == "root" || name == "parent") && IsOp(","))
            {
                EmitRedirect(name);
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
            // gethitvar(field)：参数是字段名(ident)，发 OC_ex_ + 字段id 字节，运行期从 Ghv 读
            if (name == "gethitvar")
            {
                int fieldId = GetHitVarFieldId(Cur.Kind == TokKind.Ident ? Cur.Text : "");
                if (Cur.Kind == TokKind.Ident) { Next(); }
                // 容错吃掉可能的子字段(如 fall.recover 的 .recover)
                while (!IsOp(")") && Cur.Kind != TokKind.End) { Next(); }
                Expect(")");
                Emit(OpCode.OC_ex_);
                _out.Add((byte)fieldId);
                return;
            }
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

        // statetype/movetype = X[,Y...] / != X[,Y...]：发 OC_statetype/OC_movetype + 1字节类型掩码，
        // 多字母用 OC_blor 串成 OR；!= 末尾追加 OC_blnot。无比较运算符则容错压 0(false)。
        void EmitTypeCompare(string trigger)
        {
            bool negate = IsOp("!=");
            if (negate || IsOp("=")) { Next(); }
            else { EmitInt(0); return; }
            OpCode op = TypeOpcode(trigger);
            bool isMove = trigger == "movetype";
            EmitTypeCheck(op, isMove);
            while (IsOp(","))
            {
                Next();
                EmitTypeCheck(op, isMove);
                Emit(OpCode.OC_blor);
            }
            if (negate) { Emit(OpCode.OC_blnot); }
        }

        static OpCode TypeOpcode(string trigger)
        {
            switch (trigger)
            {
                case "movetype": return OpCode.OC_movetype;
                case "prevstatetype": return OpCode.OC_ex2_;   // 子表标记：MChar 解码为 PrevStateType 比较
                default: return OpCode.OC_statetype;
            }
        }

        // p2statetype/p2movetype：把类型比较编入子缓冲，用 OC_p2 redirect + OC_run 包裹。
        void EmitP2TypeCompare(string baseTrigger)
        {
            List<byte> outer = _out;
            _out = new List<byte>();
            EmitTypeCompare(baseTrigger);
            List<byte> sub = _out;
            _out = outer;

            int runBlockLen = 1 + 4 + sub.Count;
            _out.Add((byte)OpCode.OC_p2);
            AppendI32(runBlockLen);
            _out.Add((byte)OpCode.OC_run);
            AppendI32(sub.Count);
            _out.AddRange(sub);
        }

        // command = "name" / != "name"：发 OC_command + [1字节名长][ASCII 名字]。无比较则压 0。
        void EmitCommandCompare()
        {
            bool negate = IsOp("!=");
            if (negate || IsOp("=")) { Next(); }
            else { EmitInt(0); return; }
            string cmdName = Cur.Kind == TokKind.Str ? Cur.Text : "";
            if (Cur.Kind == TokKind.Str) { Next(); }
            Emit(OpCode.OC_command);
            byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(cmdName);
            _out.Add((byte)(nameBytes.Length > 255 ? 255 : nameBytes.Length));
            for (int k = 0; k < nameBytes.Length && k < 255; k++)
            {
                _out.Add(nameBytes[k]);
            }
            if (negate) { Emit(OpCode.OC_blnot); }
        }

        void EmitTypeCheck(OpCode op, bool isMove)
        {
            int mask = 0;
            if (Cur.Kind == TokKind.Ident && Cur.Text.Length > 0)
            {
                char letter = Cur.Text[0];
                mask = isMove ? MoveTypeMask(letter) : StateTypeMask(letter);
                Next();
            }
            Emit(op);
            _out.Add((byte)mask);
        }

        // StateType 掩码对齐 Ikemen ST_*：S=1 C=2 A=4 L=8
        static int StateTypeMask(char letter)
        {
            switch (letter)
            {
                case 's': return 1;
                case 'c': return 2;
                case 'a': return 4;
                case 'l': return 8;
                default: return 0;
            }
        }

        // MoveType 小码（我方偏离 Ikemen <<15 内部表示，等价）：I=1 H=2 A=4
        static int MoveTypeMask(char letter)
        {
            switch (letter)
            {
                case 'i': return 1;
                case 'h': return 2;
                case 'a': return 4;
                default: return 0;
            }
        }

        // redirect 前缀 p2,/root,/parent,：把后续单值(expValue 级)编入子缓冲，
        // 发 OC_<redirect> + 4字节(OC_run 块长) + OC_run + 4字节(子码长) + 子码。
        void EmitRedirect(string name)
        {
            Next();   // 吃掉 ','
            OpCode op = name == "root" ? OpCode.OC_root : name == "parent" ? OpCode.OC_parent : OpCode.OC_p2;

            List<byte> outer = _out;
            _out = new List<byte>();
            ParseUnary();             // expValue 级单值（含前导一元）
            List<byte> sub = _out;
            _out = outer;

            int runBlockLen = 1 + 4 + sub.Count;   // OC_run(1) + 长度头(4) + 子码
            _out.Add((byte)op);
            AppendI32(runBlockLen);
            _out.Add((byte)OpCode.OC_run);
            AppendI32(sub.Count);
            _out.AddRange(sub);
        }

        // enemy(n),/enemynear(n), <value>：索引在外层上下文求值并压栈（供 Char.Redirect 弹取），
        // 随后发 OC_enemy/OC_enemynear + OC_run 包裹的子值（子值在重定向后的敌人上下文求值）。
        // 对齐 Ikemen compiler.go：索引可选(默认 0)，逗号后接子表达式。
        void EmitEnemyRedirect(string name)
        {
            if (IsOp("("))
            {
                Next();
                ParseBoolOr();      // 索引表达式 → 压栈（外层上下文）
                Expect(")");
            }
            else
            {
                EmitInt(0);         // 省略索引 → 默认 0
            }
            Expect(",");

            OpCode op = name == "enemynear" ? OpCode.OC_enemynear : OpCode.OC_enemy;
            List<byte> outer = _out;
            _out = new List<byte>();
            ParseUnary();           // 重定向后的单值
            List<byte> sub = _out;
            _out = outer;

            int runBlockLen = 1 + 4 + sub.Count;
            _out.Add((byte)op);
            AppendI32(runBlockLen);
            _out.Add((byte)OpCode.OC_run);
            AppendI32(sub.Count);
            _out.AddRange(sub);
        }

        void AppendI32(int v)
        {
            for (int k = 0; k < 4; k++) { _out.Add((byte)(v >> (8 * k))); }
        }

        // gethitvar 字段名 → 字段 id 字节（与 MChar.ReadTrigger 的 OC_ex_ 解码一致）。未知→255。
        static int GetHitVarFieldId(string field)
        {
            switch (field)
            {
                case "xvel": return 0;
                case "yvel": return 1;
                case "zvel": return 2;
                case "hittime": return 3;
                case "slidetime": return 4;
                case "ctrltime": return 5;
                case "hitshaketime": return 6;
                case "damage": return 7;
                case "hitcount": return 8;
                case "fallcount": return 9;
                case "animtype": return 10;
                case "type": return 11;
                case "fall": return 12;
                case "guarded": return 13;
                default: return 255;
            }
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

        // 注：statetype/movetype 不在此表——它们是 expValue 级原子比较(自行消费 = / 字母)，见 ParseIdent。
        static readonly Dictionary<string, OpCode> NoArgTriggers = new Dictionary<string, OpCode>
        {
            ["time"] = OpCode.OC_time, ["statetime"] = OpCode.OC_time,
            ["stateno"] = OpCode.OC_stateno, ["prevstateno"] = OpCode.OC_prevstateno,
            ["ctrl"] = OpCode.OC_ctrl, ["anim"] = OpCode.OC_anim,
            ["life"] = OpCode.OC_life, ["lifemax"] = OpCode.OC_lifemax,
            ["power"] = OpCode.OC_power, ["powermax"] = OpCode.OC_powermax,
            ["alive"] = OpCode.OC_alive, ["facing"] = OpCode.OC_facing,
            ["random"] = OpCode.OC_random, ["gametime"] = OpCode.OC_gametime,
            ["animtime"] = OpCode.OC_animtime, ["animelemno"] = OpCode.OC_animelemno,
            ["id"] = OpCode.OC_id, ["palno"] = OpCode.OC_palno,
            ["hitpausetime"] = OpCode.OC_hitpausetime,
            ["hitcount"] = OpCode.OC_hitcount, ["uniqhitcount"] = OpCode.OC_uniqhitcount,
            ["movecontact"] = OpCode.OC_movecontact, ["movehit"] = OpCode.OC_movehit,
            ["moveguarded"] = OpCode.OC_moveguarded, ["movereversed"] = OpCode.OC_movereversed,
            ["numtarget"] = OpCode.OC_numtarget,
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
