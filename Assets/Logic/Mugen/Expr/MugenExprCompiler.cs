// Behavior-faithful reimplementation of Ikemen GO's expression compiler (MIT, (c) 2016 Suehiro et al.).
// Source: src/compiler.go (CharCompiler.expBoolOr..expValue 优先级链)。
// 编译器是编译期变换：兼容性取决于(1)运算符语义[已在 BytecodeOps 忠实移植] 与(2)优先级+trigger 映射[此处照搬]。
// 故采用按 Ikemen 优先级链重写的递归下降，产出与 M1 执行器约定一致的字节码，差分测试兜底。
// See Docs/移植方案_Ikemen.md.
using System;
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
            public int Position;
            public int Length;
        }

        List<Tok> _toks;
        int _pos;
        List<byte> _out = new List<byte>();   // 非 readonly：redirect 子表达式编译时临时切换缓冲
        List<MugenExprDiagnostic> _diagnostics;

        public BytecodeExp Compile(string expr)
        {
            return CompileCore(expr).Expression;
        }

        /// <summary>
        /// Compiles with strict, structured diagnostics. Existing <see cref="Compile"/> callers keep
        /// their recovery behavior; import/audit code should require <see cref="MugenExprCompileResult.Success"/>.
        /// </summary>
        public MugenExprCompileResult CompileStrict(string expr)
        {
            return CompileCore(expr);
        }

        MugenExprCompileResult CompileCore(string expr)
        {
            expr = expr ?? string.Empty;
            _diagnostics = new List<MugenExprDiagnostic>();
            _toks = Tokenize(expr, _diagnostics);
            _pos = 0;
            _out.Clear();
            if (Cur.Kind == TokKind.End)
            {
                Error(MugenExprDiagnosticCode.EmptyExpression, Cur, "Expression is empty.");
            }
            ParseBoolOr();
            if (Cur.Kind != TokKind.End)
            {
                Error(MugenExprDiagnosticCode.TrailingToken, Cur,
                    "Unexpected trailing token '" + Cur.Text + "'.");
            }
            return new MugenExprCompileResult(new BytecodeExp(_out.ToArray()), _diagnostics);
        }

        // ───────── 词法 ─────────
        static List<Tok> Tokenize(string s, List<MugenExprDiagnostic> diagnostics)
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
                    bool terminated = i < s.Length;
                    toks.Add(new Tok
                    {
                        Kind = TokKind.Str,
                        Text = s.Substring(start, i - start),
                        Position = start - 1,
                        Length = i - start + (terminated ? 2 : 1),
                    });
                    if (terminated)
                    {
                        i++;
                    }
                    else
                    {
                        diagnostics.Add(new MugenExprDiagnostic(
                            MugenExprDiagnosticCode.UnterminatedString,
                            start - 1,
                            s.Length - start + 1,
                            "String literal is not terminated."));
                    }
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
                        FFloat floatValue = FFloat.Zero;
                        try
                        {
                            floatValue = ParseFixed(num);
                        }
                        catch (OverflowException)
                        {
                            diagnostics.Add(new MugenExprDiagnostic(
                                MugenExprDiagnosticCode.InvalidNumber,
                                start,
                                i - start,
                                "Fixed-point literal is outside the supported range."));
                        }
                        toks.Add(new Tok
                        {
                            Kind = TokKind.FloatNumber,
                            Text = num,
                            FloatValue = floatValue,
                            Position = start,
                            Length = i - start,
                        });
                    }
                    else
                    {
                        if (!int.TryParse(num, out int iv))
                        {
                            diagnostics.Add(new MugenExprDiagnostic(
                                MugenExprDiagnosticCode.InvalidNumber,
                                start,
                                i - start,
                                "Integer literal is outside the supported Int32 range."));
                        }
                        toks.Add(new Tok
                        {
                            Kind = TokKind.Number,
                            Text = num,
                            IntValue = iv,
                            Position = start,
                            Length = i - start,
                        });
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
                    toks.Add(new Tok
                    {
                        Kind = TokKind.Ident,
                        Text = s.Substring(start, i - start).ToLowerInvariant(),
                        Position = start,
                        Length = i - start,
                    });
                    continue;
                }
                // 多字符运算符优先
                string two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                if (two == "**" || two == "!=" || two == ">=" || two == "<=" || two == "&&" || two == "||" || two == "^^" || two == ":=")
                {
                    toks.Add(new Tok { Kind = TokKind.Op, Text = two, Position = i, Length = 2 });
                    i += 2;
                    continue;
                }
                toks.Add(new Tok { Kind = TokKind.Op, Text = ch.ToString(), Position = i, Length = 1 });
                i++;
            }
            toks.Add(new Tok { Kind = TokKind.End, Text = "", Position = s.Length, Length = 0 });
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

        void Error(MugenExprDiagnosticCode code, Tok token, string message)
        {
            _diagnostics.Add(new MugenExprDiagnostic(
                code,
                token.Position,
                token.Length,
                message));
        }

        // ───────── 优先级链（低→高，照搬 Ikemen）─────────
        void ParseBoolOr()
        {
            ParseBoolXor();
            while (IsOp("||"))
            {
                Next();
                List<byte> right = Capture(ParseBoolXor);
                EmitShortCircuit(OpCode.OC_jnz8, OpCode.OC_jnz, right);
            }
        }
        void ParseBoolXor()
        {
            ParseBoolAnd();
            while (IsOp("^^")) { Next(); ParseBoolAnd(); Emit(OpCode.OC_blxor); }
        }
        void ParseBoolAnd()
        {
            ParseOr();
            while (IsOp("&&"))
            {
                Next();
                List<byte> right = Capture(ParseOr);
                EmitShortCircuit(OpCode.OC_jz8, OpCode.OC_jz, right);
            }
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
                Expect(")");
                return;
            }
            if (Cur.Kind == TokKind.Ident)
            {
                ParseIdent();
                return;
            }
            Error(Cur.Kind == TokKind.End
                    ? MugenExprDiagnosticCode.UnexpectedEnd
                    : MugenExprDiagnosticCode.UnexpectedToken,
                Cur,
                Cur.Kind == TokKind.End
                    ? "Expected an expression value."
                    : "Unexpected token '" + Cur.Text + "'.");
            EmitInt(0);
            if (Cur.Kind != TokKind.End) { Next(); }
        }

        void ParseIdent()
        {
            Tok identifier = Cur;
            string name = Cur.Text;
            Next();

            // 带索引 redirect：enemy(n),/enemynear(n), 或省略索引的 enemy,/enemynear,（索引默认 0）。
            // 须在函数调用判断之前，否则 enemy(0) 会被误当作函数 enemy(0)。
            if ((name == "enemy" || name == "enemynear") && (IsOp("(") || IsOp(",")))
            {
                EmitEnemyRedirect(name);
                return;
            }

            if (name == "partner" && (IsOp("(") || IsOp(",")))
            {
                EmitIndexedRedirect(name);
                return;
            }

            if ((name == "helper" || name == "target") && (IsOp("(") || IsOp(",")))
            {
                EmitTwoArgRedirect(name);
                return;
            }

            // R-ENT entity count predicates must run before function parsing,
            // otherwise numhelper(5) is consumed as an unknown function.
            if (name == "numhelper" || name == "numproj" || name == "numexplod" || name == "ishelper")
            {
                if (IsOp("("))
                {
                    Next();
                    ParseBoolOr();
                    Expect(")");
                }
                else
                {
                    EmitInt(-1);
                }
                Emit(name == "numhelper" ? OpCode.OC_numhelper
                    : name == "numproj" ? OpCode.OC_numproj
                    : name == "numexplod" ? OpCode.OC_numexplod : OpCode.OC_ishelper);
                return;
            }

            // Projectile contact aliases. Time variants are regular unary functions;
            // old boolean forms compile to Proj*Time(id) >= 0.
            if (name == "projhit" || name == "projcontact" || name == "projguarded")
            {
                EmitProjectileContactAlias(name);
                return;
            }

            // 函数调用：name(...)
            if (IsOp("("))
            {
                ParseFunction(name, identifier);
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

            // name/p1name = "x"（自身）、p2name/enemyname = "x"（对手 P2）：角色名字符串比较。
            // 用量大（name 897/p2name 275）。p1name 等同 name；1v1 中 p2name/enemyname 走 P2 redirect。
            if (name == "name" || name == "p1name")
            {
                EmitNameCompare();
                return;
            }
            if (name == "p2name" || name == "enemyname")
            {
                EmitRedirectedNameCompare();
                return;
            }

            // animelem = n：动画到达元素 n 的「首帧」触发（AnimElemNo==n && AnimElemTime==0），
            // 是 expValue 级原子布尔（自行消费 = / != 与操作数），对齐 MUGEN animelem 触发器语义。
            // 大量 HitDef/取消用 `trigger1 = animelem = N` 计时——此前 animelem 未特判落到压 0 → 恒假 → 招式哑火。
            if (name == "animelem")
            {
                EmitAnimElemCompare();
                return;
            }

            // redirect 前缀：p2,/root,/parent, + 单值子表达式（用 OC_run 包裹保证全程用重定向上下文）
            if ((name == "p2" || name == "root" || name == "parent" || name == "stateowner") && IsOp(","))
            {
                EmitRedirect(name);
                return;
            }

            // 轴 trigger：pos x / vel y / screenpos x / hitvel z / p2dist x / p2bodydist y
            if ((name == "pos" || name == "vel" || name == "screenpos" || name == "hitvel"
                 || name == "p2dist" || name == "p2bodydist") && Cur.Kind == TokKind.Ident)
            {
                Tok axisToken = Cur;
                string axis = Cur.Text;
                Next();
                if (!IsValidAxis(name, axis))
                {
                    Error(MugenExprDiagnosticCode.InvalidAxis, axisToken,
                        "Axis '" + axis + "' is not valid for " + name + ".");
                }
                Emit(AxisOpcode(name, axis));
                return;
            }

            // 无参 trigger
            if (NoArgTriggers.TryGetValue(name, out OpCode op))
            {
                Emit(op);
                return;
            }

            Error(MugenExprDiagnosticCode.UnknownIdentifier, identifier,
                "Unknown identifier '" + name + "'.");
            EmitInt(0);
        }

        void ParseFunction(string name, Tok identifier)
        {
            Next(); // 吃掉 '('
            // const(field)：参数是点分字段名(如 data.life / velocity.walk.fwd.x)，发 OC_const_ + 字段id 字节。
            if (name == "const")
            {
                Tok fieldToken = Cur;
                string field = ReadDottedName();
                Expect(")");
                Emit(OpCode.OC_const_);
                MConstId fieldId = ConstFieldId(field);
                if (fieldId == MConstId.Unknown)
                {
                    Error(MugenExprDiagnosticCode.UnknownField, fieldToken,
                        "Unknown const field '" + field + "'.");
                }
                _out.Add((byte)fieldId);
                return;
            }
            // gethitvar(field)：参数是点分字段名(如 fall.yvel)，发 OC_ex_ + 字段id 字节，运行期从 Ghv 读
            if (name == "gethitvar")
            {
                Tok fieldToken = Cur;
                string field = ReadDottedName();
                Expect(")");
                Emit(OpCode.OC_ex_);
                int fieldId = GetHitVarFieldId(field);
                if (fieldId == 255)
                {
                    Error(MugenExprDiagnosticCode.UnknownField, fieldToken,
                        "Unknown gethitvar field '" + field + "'.");
                }
                _out.Add((byte)fieldId);
                return;
            }
            if (name == "jugglepoints")
            {
                ParseBoolOr();
                Expect(")");
                Emit(OpCode.OC_jugglepoints);
                return;
            }
            // var(n) / fvar(n)：压 index 再发 opcode
            if (name == "var" || name == "fvar" || name == "sysvar" || name == "sysfvar")
            {
                ParseBoolOr();
                Expect(")");
                Emit(name == "var" ? OpCode.OC_var
                    : name == "fvar" ? OpCode.OC_fvar
                    : name == "sysvar" ? OpCode.OC_sysvar
                    : OpCode.OC_sysfvar);
                return;
            }
            if (name == "ifelse")
            {
                ParseBoolOr(); Expect(",");   // cond
                ParseBoolOr(); Expect(",");   // trueVal
                ParseBoolOr(); Expect(")");   // falseVal
                Emit(OpCode.OC_ifelse);
                return;
            }
            if (name == "cond")
            {
                ParseBoolOr();
                Expect(",");
                List<byte> trueValue = Capture(ParseBoolOr);
                Expect(",");
                List<byte> falseValue = Capture(ParseBoolOr);
                Expect(")");
                EmitConditional(trueValue, falseValue);
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
                return;
            }
            Error(MugenExprDiagnosticCode.UnknownFunction, identifier,
                "Unknown function '" + name + "'.");
        }

        void EmitProjectileContactAlias(string name)
        {
            if (IsOp("("))
            {
                Next();
                ParseBoolOr();
                Expect(")");
            }
            else
            {
                EmitInt(-1);
            }

            Emit(name == "projhit" ? OpCode.OC_projhittime
                : name == "projguarded" ? OpCode.OC_projguardedtime : OpCode.OC_projcontacttime);
            EmitInt(0);
            Emit(OpCode.OC_ge);
        }

        void Expect(string op)
        {
            if (IsOp(op))
            {
                Next();
                return;
            }
            Error(MugenExprDiagnosticCode.MissingToken, Cur,
                "Expected '" + op + "' before "
                + (Cur.Kind == TokKind.End ? "the end of the expression." : "'" + Cur.Text + "'."));
        }

        List<byte> Capture(Action parser)
        {
            List<byte> outer = _out;
            _out = new List<byte>();
            parser();
            List<byte> captured = _out;
            _out = outer;
            return captured;
        }

        void EmitShortCircuit(OpCode shortJump, OpCode longJump, List<byte> right)
        {
            AppendJump(shortJump, longJump, right.Count + 1);
            Emit(OpCode.OC_pop);
            _out.AddRange(right);
        }

        void EmitConditional(List<byte> trueValue, List<byte> falseValue)
        {
            List<byte> falsePath = new List<byte>(falseValue.Count + 1);
            falsePath.Add((byte)OpCode.OC_pop);
            falsePath.AddRange(falseValue);

            List<byte> truePath = new List<byte>(trueValue.Count + 6);
            truePath.Add((byte)OpCode.OC_pop);
            truePath.AddRange(trueValue);
            AppendJump(truePath, OpCode.OC_jmp8, OpCode.OC_jmp, falsePath.Count);

            AppendJump(OpCode.OC_jz8, OpCode.OC_jz, truePath.Count);
            _out.AddRange(truePath);
            _out.AddRange(falsePath);
        }

        void AppendJump(OpCode shortJump, OpCode longJump, int skippedByteCount)
        {
            AppendJump(_out, shortJump, longJump, skippedByteCount);
        }

        static void AppendJump(
            List<byte> output,
            OpCode shortJump,
            OpCode longJump,
            int skippedByteCount)
        {
            if (skippedByteCount <= byte.MaxValue)
            {
                output.Add((byte)shortJump);
                output.Add((byte)skippedByteCount);
                return;
            }

            output.Add((byte)longJump);
            for (int k = 0; k < 4; k++)
            {
                output.Add((byte)(skippedByteCount >> (8 * k)));
            }
        }

        // 读点分字段名：ident(.ident)*（标识符已小写）。用于 const(data.fall.defence_up) 等。
        string ReadDottedName()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            while (Cur.Kind == TokKind.Ident || IsOp("."))
            {
                if (IsOp("."))
                {
                    sb.Append('.');
                }
                else
                {
                    sb.Append(Cur.Text);
                }
                Next();
            }
            return sb.ToString();
        }

        // const(...) 字段名 → MConstId（与 MConstants.Read 解码一致）。未知 → Unknown(读出 0)。
        static MConstId ConstFieldId(string field)
        {
            return ConstFields.TryGetValue(field, out MConstId id) ? id : MConstId.Unknown;
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

        // animelem = n / != n：首帧触发比较。无 =/!= 时（如 animelem >= n）退化为发当前元素号，交外层比较。
        // 第二参形式 `animelem = n, >= m`（= 元素 n 且其已播 >= m tick）暂不支持，罕见，待 animelemtime(n) 一并补。
        void EmitAnimElemCompare()
        {
            bool negate = IsOp("!=");
            if (negate || IsOp("="))
            {
                Next();
                ParseAddSub();                 // 目标元素号 n（算术级，不吞外层比较/逻辑运算）
                Emit(OpCode.OC_animelem);      // 弹 n → bool(AnimElemNo==n && AnimElemTime==0)
                if (negate)
                {
                    Emit(OpCode.OC_blnot);
                }
            }
            else
            {
                Emit(OpCode.OC_animelemno);    // 无比较运算符：返回当前元素号，交外层处理 >= / <= 等
            }
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

        // name = "x" / != "x"：发 OC_name + [1字节名长][ASCII 名字]，运行期与角色 Name 精确比较。无比较则压 0。
        void EmitNameCompare()
        {
            bool negate = IsOp("!=");
            if (negate || IsOp("=")) { Next(); }
            else { EmitInt(0); return; }
            string wanted = Cur.Kind == TokKind.Str ? Cur.Text : "";
            if (Cur.Kind == TokKind.Str) { Next(); }
            Emit(OpCode.OC_name);
            byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(wanted);
            _out.Add((byte)(nameBytes.Length > 255 ? 255 : nameBytes.Length));
            for (int k = 0; k < nameBytes.Length && k < 255; k++)
            {
                _out.Add(nameBytes[k]);
            }
            if (negate) { Emit(OpCode.OC_blnot); }
        }

        // p2name/enemyname = "x"：把名字比较编入子缓冲，用 OC_p2 redirect + OC_run 包裹（对齐 p2statetype 套路）。
        void EmitRedirectedNameCompare()
        {
            List<byte> outer = _out;
            _out = new List<byte>();
            EmitNameCompare();
            List<byte> sub = _out;
            _out = outer;

            int runBlockLen = 1 + 4 + sub.Count;
            _out.Add((byte)OpCode.OC_p2);
            AppendI32(runBlockLen);
            _out.Add((byte)OpCode.OC_run);
            AppendI32(sub.Count);
            _out.AddRange(sub);
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
            OpCode op = name == "root" ? OpCode.OC_root
                : name == "parent" ? OpCode.OC_parent
                : name == "stateowner" ? OpCode.OC_stateowner
                : OpCode.OC_p2;

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

        void EmitIndexedRedirect(string name)
        {
            if (IsOp("("))
            {
                Next();
                ParseBoolOr();
                Expect(")");
            }
            else
            {
                EmitInt(0);
            }
            Expect(",");

            OpCode op = name == "partner" ? OpCode.OC_partner : OpCode.OC_enemy;
            List<byte> outer = _out;
            _out = new List<byte>();
            ParseUnary();
            List<byte> sub = _out;
            _out = outer;

            int runBlockLen = 1 + 4 + sub.Count;
            _out.Add((byte)op);
            AppendI32(runBlockLen);
            _out.Add((byte)OpCode.OC_run);
            AppendI32(sub.Count);
            _out.AddRange(sub);
        }

        void EmitTwoArgRedirect(string name)
        {
            if (IsOp("("))
            {
                Next();
                ParseBoolOr();
                if (IsOp(","))
                {
                    Next();
                    ParseBoolOr();
                }
                else
                {
                    EmitInt(0);
                }
                Expect(")");
            }
            else
            {
                EmitInt(-1);
                EmitInt(0);
            }
            Expect(",");

            OpCode op = name == "target" ? OpCode.OC_target : OpCode.OC_helper;
            List<byte> outer = _out;
            _out = new List<byte>();
            ParseUnary();
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
                case "groundtype": return 14;
                case "airtype": return 15;
                case "yaccel": return 16;
                case "fall.yvel": case "fall.yvelocity": return 17;
                case "fall.xvel": case "fall.xvelocity": return 18;
                case "fall.recovertime": return 19;
                case "fall.recover": return 20;
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
                case "p2dist": return axis == "y" ? OpCode.OC_p2dist_y : OpCode.OC_p2dist_x;
                case "p2bodydist": return axis == "y" ? OpCode.OC_p2bodydist_y : OpCode.OC_p2bodydist_x;
                default: return OpCode.OC_pos_x;
            }
        }

        static bool IsValidAxis(string name, string axis)
        {
            if (axis == "x" || axis == "y")
            {
                return true;
            }
            return axis == "z" && (name == "vel" || name == "hitvel");
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
            ["numtarget"] = OpCode.OC_numtarget, ["roundstate"] = OpCode.OC_roundstate,
            ["inguarddist"] = OpCode.OC_inguarddist,
            // 注：ishelper/numhelper/numproj/numexplod 在 ParseIdent 特判（统一弹 id 参数，无参压 -1=全部），不在此表。
            // 受击触发器（common1 5000-5160 用）
            ["hitshakeover"] = OpCode.OC_hitshakeover, ["hitover"] = OpCode.OC_hitover,
            ["hitfall"] = OpCode.OC_hitfall, ["canrecover"] = OpCode.OC_canrecover,
        };

        // const(...) 字段名 → MConstId。名字照搬 Ikemen compiler.go 的 const token（真实角色用到的子集）。
        static readonly Dictionary<string, MConstId> ConstFields = new Dictionary<string, MConstId>
        {
            ["data.life"] = MConstId.DataLife,
            ["data.power"] = MConstId.DataPower,
            ["data.attack"] = MConstId.DataAttack,
            ["data.defence"] = MConstId.DataDefence,
            ["data.fall.defence_up"] = MConstId.DataFallDefenceUp,
            ["data.liedown.time"] = MConstId.DataLiedownTime,
            ["data.airjuggle"] = MConstId.DataAirjuggle,
            ["size.ground.back"] = MConstId.SizeGroundBack,
            ["size.ground.front"] = MConstId.SizeGroundFront,
            ["size.air.back"] = MConstId.SizeAirBack,
            ["size.air.front"] = MConstId.SizeAirFront,
            ["size.height"] = MConstId.SizeHeight,
            ["size.head.pos.x"] = MConstId.SizeHeadPosX,
            ["size.head.pos.y"] = MConstId.SizeHeadPosY,
            ["size.mid.pos.x"] = MConstId.SizeMidPosX,
            ["size.mid.pos.y"] = MConstId.SizeMidPosY,
            ["velocity.walk.fwd.x"] = MConstId.VelWalkFwd,
            ["velocity.walk.back.x"] = MConstId.VelWalkBack,
            ["velocity.run.fwd.x"] = MConstId.VelRunFwdX,
            ["velocity.run.fwd.y"] = MConstId.VelRunFwdY,
            ["velocity.run.back.x"] = MConstId.VelRunBackX,
            ["velocity.run.back.y"] = MConstId.VelRunBackY,
            ["velocity.jump.neu.x"] = MConstId.VelJumpNeuX,
            ["velocity.jump.y"] = MConstId.VelJumpY,
            ["velocity.jump.back.x"] = MConstId.VelJumpBack,
            ["velocity.jump.fwd.x"] = MConstId.VelJumpFwd,
            ["velocity.runjump.fwd.x"] = MConstId.VelRunjumpFwdX,
            ["velocity.runjump.back.x"] = MConstId.VelRunjumpBackX,
            ["velocity.runjump.back.y"] = MConstId.VelRunjumpBackY,
            ["velocity.airjump.neu.x"] = MConstId.VelAirjumpNeuX,
            ["velocity.airjump.y"] = MConstId.VelAirjumpY,
            ["velocity.airjump.back.x"] = MConstId.VelAirjumpBack,
            ["velocity.airjump.fwd.x"] = MConstId.VelAirjumpFwd,
            ["movement.yaccel"] = MConstId.MoveYaccel,
            ["movement.stand.friction"] = MConstId.MoveStandFriction,
            ["movement.crouch.friction"] = MConstId.MoveCrouchFriction,
            ["movement.airjump.num"] = MConstId.MoveAirjumpNum,
            ["movement.airjump.height"] = MConstId.MoveAirjumpHeight,
        };

        static readonly Dictionary<string, OpCode> UnaryFuncs = new Dictionary<string, OpCode>
        {
            ["abs"] = OpCode.OC_abs, ["exp"] = OpCode.OC_exp, ["ln"] = OpCode.OC_ln,
            ["sin"] = OpCode.OC_sin, ["cos"] = OpCode.OC_cos, ["tan"] = OpCode.OC_tan,
            ["acos"] = OpCode.OC_acos, ["asin"] = OpCode.OC_asin, ["atan"] = OpCode.OC_atan,
            ["floor"] = OpCode.OC_floor, ["ceil"] = OpCode.OC_ceil,
            // animexist(n)/selfanimexist(n)：求值参数 n 后发 trigger opcode，运行期由 MChar 查动画表。
            // 对齐 Ikemen compiler.go:1824/3588（参数压栈 + OC_animexist/OC_selfanimexist）。
            ["animexist"] = OpCode.OC_animexist, ["selfanimexist"] = OpCode.OC_selfanimexist,
            // animelemtime(n)：求值元素号 n 后发 opcode，运行期 MChar 算「自元素 n 起已播 tick」。
            ["animelemtime"] = OpCode.OC_animelemtime,
            ["projcontacttime"] = OpCode.OC_projcontacttime,
            ["projhittime"] = OpCode.OC_projhittime,
            ["projguardedtime"] = OpCode.OC_projguardedtime,
            ["projcanceltime"] = OpCode.OC_projcanceltime,
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
