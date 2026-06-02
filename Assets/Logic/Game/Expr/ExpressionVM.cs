using System;
using System.Collections.Generic;
using System.Globalization;
using Lockstep.Math;

namespace Lockstep.Game.Expr
{
    /// <summary>表达式编译/求值异常。</summary>
    public sealed class ExprException : Exception
    {
        public ExprException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 表达式 VM：tokenize → 递归下降 parse → AST → 定点求值。
    /// 支持：整数常量、标识符(trigger)、带参调用(var(n)/abs(x)/ifelse(c,a,b)/min/max)、
    /// 逻辑(|| &amp;&amp; !)、比较(= != &lt; &lt;= &gt; &gt;=)、四则(+ - * / %)、一元负号、括号。
    /// 不硬编码具体 trigger（由 IEvalContext 解析）——能编译任意 CNS 表达式（避免形状捷径）。
    /// 全程 FFloat，无 float、无随机 → 确定性。
    /// </summary>
    public sealed class ExpressionVM : IExpressionVM
    {
        /// <summary>编译一条 MUGEN 表达式为可反复求值的 AST。</summary>
        public IExpr Compile(string expression)
        {
            if (expression == null)
            {
                throw new ExprException("表达式为 null");
            }
            List<Token> tokens = Lex(expression);
            Parser parser = new Parser(tokens);
            IExpr expressionTree = parser.ParseExpression();
            parser.ExpectEnd();
            return expressionTree;
        }

        // ───────────────────── 词法 ─────────────────────

        enum TokenKind { Number, Identifier, Operator, LeftParen, RightParen, Comma, End }

        struct Token
        {
            public TokenKind Kind;
            public string Text;
            public long Number;
        }

        static List<Token> Lex(string source)
        {
            List<Token> tokens = new List<Token>();
            int index = 0;
            while (index < source.Length)
            {
                char current = source[index];
                if (char.IsWhiteSpace(current))
                {
                    index++;
                    continue;
                }
                if (char.IsDigit(current))
                {
                    int start = index;
                    while (index < source.Length && char.IsDigit(source[index]))
                    {
                        index++;
                    }
                    long number = long.Parse(source.Substring(start, index - start), CultureInfo.InvariantCulture);
                    tokens.Add(new Token { Kind = TokenKind.Number, Number = number });
                    continue;
                }
                if (char.IsLetter(current) || current == '_')
                {
                    int start = index;
                    while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_'))
                    {
                        index++;
                    }
                    tokens.Add(new Token { Kind = TokenKind.Identifier, Text = source.Substring(start, index - start) });
                    continue;
                }
                if (current == '(')
                {
                    tokens.Add(new Token { Kind = TokenKind.LeftParen });
                    index++;
                    continue;
                }
                if (current == ')')
                {
                    tokens.Add(new Token { Kind = TokenKind.RightParen });
                    index++;
                    continue;
                }
                if (current == ',')
                {
                    tokens.Add(new Token { Kind = TokenKind.Comma });
                    index++;
                    continue;
                }
                string twoChar = (index + 1 < source.Length) ? source.Substring(index, 2) : null;
                if (twoChar == "!=" || twoChar == "<=" || twoChar == ">=" || twoChar == "&&" || twoChar == "||")
                {
                    tokens.Add(new Token { Kind = TokenKind.Operator, Text = twoChar });
                    index += 2;
                    continue;
                }
                if (current == '=' || current == '<' || current == '>' || current == '!'
                    || current == '+' || current == '-' || current == '*' || current == '/' || current == '%')
                {
                    tokens.Add(new Token { Kind = TokenKind.Operator, Text = current.ToString() });
                    index++;
                    continue;
                }
                throw new ExprException("无法识别的字符: '" + current + "'");
            }
            tokens.Add(new Token { Kind = TokenKind.End });
            return tokens;
        }

        // ───────────────────── 语法（递归下降）─────────────────────

        sealed class Parser
        {
            readonly List<Token> _tokens;
            int _position;

            public Parser(List<Token> tokens)
            {
                _tokens = tokens;
            }

            Token Current
            {
                get { return _tokens[_position]; }
            }

            Token Advance()
            {
                return _tokens[_position++];
            }

            bool IsOperator(string symbol)
            {
                return Current.Kind == TokenKind.Operator && Current.Text == symbol;
            }

            public void ExpectEnd()
            {
                if (Current.Kind != TokenKind.End)
                {
                    throw new ExprException("表达式末尾有多余记号");
                }
            }

            public IExpr ParseExpression()
            {
                return ParseOr();
            }

            IExpr ParseOr()
            {
                IExpr left = ParseAnd();
                while (IsOperator("||"))
                {
                    Advance();
                    IExpr right = ParseAnd();
                    left = new BinaryNode("||", left, right);
                }
                return left;
            }

            IExpr ParseAnd()
            {
                IExpr left = ParseCompare();
                while (IsOperator("&&"))
                {
                    Advance();
                    IExpr right = ParseCompare();
                    left = new BinaryNode("&&", left, right);
                }
                return left;
            }

            IExpr ParseCompare()
            {
                IExpr left = ParseAdd();
                while (IsOperator("=") || IsOperator("!=") || IsOperator("<")
                    || IsOperator("<=") || IsOperator(">") || IsOperator(">="))
                {
                    string symbol = Advance().Text;
                    IExpr right = ParseAdd();
                    left = new BinaryNode(symbol, left, right);
                }
                return left;
            }

            IExpr ParseAdd()
            {
                IExpr left = ParseMultiply();
                while (IsOperator("+") || IsOperator("-"))
                {
                    string symbol = Advance().Text;
                    IExpr right = ParseMultiply();
                    left = new BinaryNode(symbol, left, right);
                }
                return left;
            }

            IExpr ParseMultiply()
            {
                IExpr left = ParseUnary();
                while (IsOperator("*") || IsOperator("/") || IsOperator("%"))
                {
                    string symbol = Advance().Text;
                    IExpr right = ParseUnary();
                    left = new BinaryNode(symbol, left, right);
                }
                return left;
            }

            IExpr ParseUnary()
            {
                if (IsOperator("-"))
                {
                    Advance();
                    return new NegateNode(ParseUnary());
                }
                if (IsOperator("!"))
                {
                    Advance();
                    return new NotNode(ParseUnary());
                }
                return ParsePrimary();
            }

            IExpr ParsePrimary()
            {
                Token token = Current;
                if (token.Kind == TokenKind.Number)
                {
                    Advance();
                    return new ConstNode(FFloat.FromInt((int)token.Number));
                }
                if (token.Kind == TokenKind.Identifier)
                {
                    Advance();
                    if (Current.Kind == TokenKind.LeftParen)
                    {
                        return ParseCall(token.Text);
                    }
                    return new TriggerNode(token.Text);
                }
                if (token.Kind == TokenKind.LeftParen)
                {
                    Advance();
                    IExpr inner = ParseExpression();
                    if (Current.Kind != TokenKind.RightParen)
                    {
                        throw new ExprException("缺少右括号");
                    }
                    Advance();
                    return inner;
                }
                throw new ExprException("意外的记号");
            }

            IExpr ParseCall(string name)
            {
                Advance(); // 吃掉 '('
                List<IExpr> args = new List<IExpr>();
                if (Current.Kind != TokenKind.RightParen)
                {
                    args.Add(ParseExpression());
                    while (Current.Kind == TokenKind.Comma)
                    {
                        Advance();
                        args.Add(ParseExpression());
                    }
                }
                if (Current.Kind != TokenKind.RightParen)
                {
                    throw new ExprException("调用缺少右括号: " + name);
                }
                Advance();
                return new CallNode(name, args.ToArray());
            }
        }

        // ───────────────────── AST 节点 ─────────────────────

        /// <summary>AST 基类：统一 EvalBool（MUGEN 约定非 0 即真），子类只实现 Eval。</summary>
        abstract class ExprNode : IExpr
        {
            public abstract FFloat Eval(IEvalContext context);

            public bool EvalBool(IEvalContext context)
            {
                return Eval(context) != FFloat.Zero;
            }
        }

        sealed class ConstNode : ExprNode
        {
            readonly FFloat _value;

            public ConstNode(FFloat value)
            {
                _value = value;
            }

            public override FFloat Eval(IEvalContext context)
            {
                return _value;
            }
        }

        sealed class TriggerNode : ExprNode
        {
            readonly string _name;

            public TriggerNode(string name)
            {
                _name = name;
            }

            public override FFloat Eval(IEvalContext context)
            {
                if (context.TryGetTrigger(_name, out FFloat value))
                {
                    return value;
                }
                throw new ExprException("未知 trigger: " + _name);
            }
        }

        sealed class NegateNode : ExprNode
        {
            readonly IExpr _inner;

            public NegateNode(IExpr inner)
            {
                _inner = inner;
            }

            public override FFloat Eval(IEvalContext context)
            {
                return -_inner.Eval(context);
            }
        }

        sealed class NotNode : ExprNode
        {
            readonly IExpr _inner;

            public NotNode(IExpr inner)
            {
                _inner = inner;
            }

            public override FFloat Eval(IEvalContext context)
            {
                return _inner.Eval(context) == FFloat.Zero ? FFloat.One : FFloat.Zero;
            }
        }

        sealed class CallNode : ExprNode
        {
            readonly string _name;
            readonly IExpr[] _args;

            public CallNode(string name, IExpr[] args)
            {
                _name = name;
                _args = args;
            }

            public override FFloat Eval(IEvalContext context)
            {
                string lower = _name.ToLowerInvariant();

                // 内建函数（纯、不依赖上下文）
                if (lower == "abs" && _args.Length == 1)
                {
                    FFloat value = _args[0].Eval(context);
                    return value < FFloat.Zero ? -value : value;
                }
                if ((lower == "ifelse" || lower == "cond") && _args.Length == 3)
                {
                    return _args[0].Eval(context) != FFloat.Zero ? _args[1].Eval(context) : _args[2].Eval(context);
                }
                if (lower == "min" && _args.Length == 2)
                {
                    FFloat left = _args[0].Eval(context);
                    FFloat right = _args[1].Eval(context);
                    return left < right ? left : right;
                }
                if (lower == "max" && _args.Length == 2)
                {
                    FFloat left = _args[0].Eval(context);
                    FFloat right = _args[1].Eval(context);
                    return left > right ? left : right;
                }

                // 带参 trigger（var(n)/fvar(n)/const(...) 等）交给上下文
                FFloat[] values = new FFloat[_args.Length];
                for (int i = 0; i < _args.Length; i++)
                {
                    values[i] = _args[i].Eval(context);
                }
                if (context.TryGetFunction(lower, values, out FFloat result))
                {
                    return result;
                }
                throw new ExprException("未知函数/带参 trigger: " + _name);
            }
        }

        sealed class BinaryNode : ExprNode
        {
            readonly string _symbol;
            readonly IExpr _left;
            readonly IExpr _right;

            public BinaryNode(string symbol, IExpr left, IExpr right)
            {
                _symbol = symbol;
                _left = left;
                _right = right;
            }

            public override FFloat Eval(IEvalContext context)
            {
                FFloat left = _left.Eval(context);
                FFloat right = _right.Eval(context);
                switch (_symbol)
                {
                    case "+": return left + right;
                    case "-": return left - right;
                    case "*": return left * right;
                    case "/": return left / right;
                    case "%": return left % right;
                    case "=": return FromBool(left == right);
                    case "!=": return FromBool(left != right);
                    case "<": return FromBool(left < right);
                    case "<=": return FromBool(left <= right);
                    case ">": return FromBool(left > right);
                    case ">=": return FromBool(left >= right);
                    case "&&": return FromBool(left != FFloat.Zero && right != FFloat.Zero);
                    case "||": return FromBool(left != FFloat.Zero || right != FFloat.Zero);
                    default: throw new ExprException("未知运算符: " + _symbol);
                }
            }

            static FFloat FromBool(bool value)
            {
                return value ? FFloat.One : FFloat.Zero;
            }
        }
    }
}
