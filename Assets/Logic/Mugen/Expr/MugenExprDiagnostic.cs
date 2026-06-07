using System.Collections.Generic;

namespace Lockstep.Mugen.Expr
{
    public enum MugenExprDiagnosticCode
    {
        EmptyExpression,
        UnexpectedToken,
        UnexpectedEnd,
        MissingToken,
        TrailingToken,
        UnterminatedString,
        InvalidNumber,
        UnknownIdentifier,
        UnknownFunction,
        UnknownField,
        InvalidAxis,
    }

    public sealed class MugenExprDiagnostic
    {
        public MugenExprDiagnostic(
            MugenExprDiagnosticCode code,
            int position,
            int length,
            string message)
        {
            Code = code;
            Position = position;
            Length = length;
            Message = message;
        }

        public MugenExprDiagnosticCode Code { get; }
        public int Position { get; }
        public int Length { get; }
        public string Message { get; }

        public override string ToString()
        {
            return Code + " at " + Position + ": " + Message;
        }
    }

    public sealed class MugenExprCompileResult
    {
        readonly IReadOnlyList<MugenExprDiagnostic> _diagnostics;

        internal MugenExprCompileResult(BytecodeExp expression, List<MugenExprDiagnostic> diagnostics)
        {
            Expression = expression;
            _diagnostics = diagnostics.AsReadOnly();
        }

        public BytecodeExp Expression { get; }
        public IReadOnlyList<MugenExprDiagnostic> Diagnostics => _diagnostics;
        public bool Success => _diagnostics.Count == 0;
    }
}
