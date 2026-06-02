using System;
using System.Collections.Generic;
using System.Globalization;
using Lockstep.Math;

namespace Lockstep.Game.Expr
{
    /// <summary>表达式编译/求值异常。</summary>
    public sealed class ExprException : Exception
    {
        public ExprException(string message) : base(message) { }
    }

    /// <summary>
    /// 最小但"形状正确"的表达式 VM：tokenize → 递归下降 parse → AST → 定点求值。
    /// v1 支持：整数常量、标识符(trigger)、比较(= != &lt; &lt;= &gt; &gt;=)、四则(+ - * / %)、一元负号、括号。
    /// 扩展方式：加 trigger = 在 IEvalContext 多解析一个名字；加运算/函数 = 加 AST 节点。
    /// 故意不硬编码任何具体 trigger —— 保证能编译任意 CNS 表达式（避免"形状捷径"烂尾，见架构设计 §12）。
    /// 全程 FFloat，无 float、无随机 → 确定性。
    /// </summary>
    public sealed class ExpressionVM : IExpressionVM
    {
        public IExpr Compile(string expression)
        {
            if (expression == null)
            {
                throw new ExprException("表达式为 null");
            }
            List<Token> tokens = Lex(expression);
            Parser parser = new Parser(tokens);
            IExpr expr = parser.ParseExpression();
            parser.ExpectEnd();
            return expr;
        }

        // ───────────────────── 词法 ─────────────────────

        enum TokKind { Number, Ident, Op, LParen, RParen, End }

        struct Token
        {
            public TokKind Kind;
            public string Text;
            public long Number;
        }

        static List<Token> Lex(string s)
        {
            List<Token> tokens = new List<Token>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }
                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < s.Length && char.IsDigit(s[i]))
                    {
                        i++;
                    }
                    long n = long.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
                    tokens.Add(new Token { Kind = TokKind.Number, Number = n });
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                    {
                        i++;
                    }
                    tokens.Add(new Token { Kind = TokKind.Ident, Text = s.Substring(start, i - start) });
                    continue;
                }
                if (c == '(')
                {
                    tokens.Add(new Token { Kind = TokKind.LParen });
                    i++;
                    continue;
                }
                if (c == ')')
                {
                    tokens.Add(new Token { Kind = TokKind.RParen });
                    i++;
                    continue;
                }
                string two = (i + 1 < s.Length) ? s.Substring(i, 2) : null;
                if (two == "!=" || two == "<=" || two == ">=")
                {
                    tokens.Add(new Token { Kind = TokKind.Op, Text = two });
                    i += 2;
                    continue;
                }
                if (c == '=' || c == '<' || c == '>' || c == '+' || c == '-' || c == '*' || c == '/' || c == '%')
                {
                    tokens.Add(new Token { Kind = TokKind.Op, Text = c.ToString() });
                    i++;
                    continue;
                }
                throw new ExprException("无法识别的字符: '" + c + "'");
            }
            tokens.Add(new Token { Kind = TokKind.End });
            return tokens;
        }

        // ───────────────────── 语法（递归下降）─────────────────────

        sealed class Parser
        {
            readonly List<Token> _toks;
            int _pos;

            public Parser(List<Token> toks)
            {
                _toks = toks;
            }

            Token Cur
            {
                get { return _toks[_pos]; }
            }

            Token Next()
            {
                return _toks[_pos++];
            }

            bool IsOp(string op)
            {
                return Cur.Kind == TokKind.Op && Cur.Text == op;
            }

            public void ExpectEnd()
            {
                if (Cur.Kind != TokKind.End)
                {
                    throw new ExprException("表达式末尾有多余记号");
                }
            }

            public IExpr ParseExpression()
            {
                return ParseCompare();
            }

            IExpr ParseCompare()
            {
                IExpr left = ParseAdd();
                while (IsOp("=") || IsOp("!=") || IsOp("<") || IsOp("<=") || IsOp(">") || IsOp(">="))
                {
                    string op = Next().Text;
                    IExpr right = ParseAdd();
                    left = new BinaryNode(op, left, right);
                }
                return left;
            }

            IExpr ParseAdd()
            {
                IExpr left = ParseMul();
                while (IsOp("+") || IsOp("-"))
                {
                    string op = Next().Text;
                    IExpr right = ParseMul();
                    left = new BinaryNode(op, left, right);
                }
                return left;
            }

            IExpr ParseMul()
            {
                IExpr left = ParseUnary();
                while (IsOp("*") || IsOp("/") || IsOp("%"))
                {
                    string op = Next().Text;
                    IExpr right = ParseUnary();
                    left = new BinaryNode(op, left, right);
                }
                return left;
            }

            IExpr ParseUnary()
            {
                if (IsOp("-"))
                {
                    Next();
                    return new UnaryNegNode(ParseUnary());
                }
                return ParsePrimary();
            }

            IExpr ParsePrimary()
            {
                Token t = Cur;
                if (t.Kind == TokKind.Number)
                {
                    Next();
                    return new ConstNode(FFloat.FromInt((int)t.Number));
                }
                if (t.Kind == TokKind.Ident)
                {
                    Next();
                    return new TriggerNode(t.Text);
                }
                if (t.Kind == TokKind.LParen)
                {
                    Next();
                    IExpr inner = ParseExpression();
                    if (Cur.Kind != TokKind.RParen)
                    {
                        throw new ExprException("缺少右括号");
                    }
                    Next();
                    return inner;
                }
                throw new ExprException("意外的记号");
            }
        }

        // ───────────────────── AST 节点 ─────────────────────

        sealed class ConstNode : IExpr
        {
            readonly FFloat _v;

            public ConstNode(FFloat v)
            {
                _v = v;
            }

            public FFloat Eval(IEvalContext ctx)
            {
                return _v;
            }

            public bool EvalBool(IEvalContext ctx)
            {
                return _v != FFloat.Zero;
            }
        }

        sealed class TriggerNode : IExpr
        {
            readonly string _name;

            public TriggerNode(string name)
            {
                _name = name;
            }

            public FFloat Eval(IEvalContext ctx)
            {
                if (ctx.TryGetTrigger(_name, out FFloat v))
                {
                    return v;
                }
                throw new ExprException("未知 trigger: " + _name);
            }

            public bool EvalBool(IEvalContext ctx)
            {
                return Eval(ctx) != FFloat.Zero;
            }
        }

        sealed class UnaryNegNode : IExpr
        {
            readonly IExpr _inner;

            public UnaryNegNode(IExpr inner)
            {
                _inner = inner;
            }

            public FFloat Eval(IEvalContext ctx)
            {
                return -_inner.Eval(ctx);
            }

            public bool EvalBool(IEvalContext ctx)
            {
                return Eval(ctx) != FFloat.Zero;
            }
        }

        sealed class BinaryNode : IExpr
        {
            readonly string _op;
            readonly IExpr _left;
            readonly IExpr _right;

            public BinaryNode(string op, IExpr left, IExpr right)
            {
                _op = op;
                _left = left;
                _right = right;
            }

            public FFloat Eval(IEvalContext ctx)
            {
                FFloat a = _left.Eval(ctx);
                FFloat b = _right.Eval(ctx);
                switch (_op)
                {
                    case "+": return a + b;
                    case "-": return a - b;
                    case "*": return a * b;
                    case "/": return a / b;
                    case "%": return a % b;
                    case "=": return Bool(a == b);
                    case "!=": return Bool(a != b);
                    case "<": return Bool(a < b);
                    case "<=": return Bool(a <= b);
                    case ">": return Bool(a > b);
                    case ">=": return Bool(a >= b);
                    default: throw new ExprException("未知运算符: " + _op);
                }
            }

            public bool EvalBool(IEvalContext ctx)
            {
                return Eval(ctx) != FFloat.Zero;
            }

            static FFloat Bool(bool b)
            {
                return b ? FFloat.One : FFloat.Zero;
            }
        }
    }
}
