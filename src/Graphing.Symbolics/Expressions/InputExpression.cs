using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record InputExpression(InputExpressionKind Kind, BigRational Constant, string Name, ImmutableArray<InputExpression> Arguments, SourceRange Source)
{
    public static InputExpression Number(BigRational value, SourceRange source) => new(InputExpressionKind.Constant, value, string.Empty, [], source);
    public static InputExpression Variable(string name, SourceRange source) => new(InputExpressionKind.Variable, default, name, [], source);
    public static InputExpression Unary(InputExpressionKind kind, InputExpression argument, SourceRange source) => new(kind, default, string.Empty, [argument], source);
    public static InputExpression Binary(InputExpressionKind kind, InputExpression left, InputExpression right, SourceRange source) => new(kind, default, string.Empty, [left, right], source);
    public static InputExpression Function(string name, ImmutableArray<InputExpression> arguments, SourceRange source) => new(InputExpressionKind.Function, default, name, arguments, source);
}
