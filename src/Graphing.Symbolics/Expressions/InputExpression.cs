using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record InputExpression(InputExpressionKind Kind, BigRational Constant, string Name, ImmutableArray<InputExpression> Arguments, SourceRange Source)
{
    public static InputExpression Number(BigRational value, SourceRange source)
    {
        return new InputExpression(InputExpressionKind.Constant, value, string.Empty, [], source);
    }

    public static InputExpression Variable(string name, SourceRange source)
    {
        return new InputExpression(InputExpressionKind.Variable, default, name, [], source);
    }

    public static InputExpression Unary(InputExpressionKind kind, InputExpression argument, SourceRange source)
    {
        return new InputExpression(kind, default, string.Empty, [argument], source);
    }

    public static InputExpression Binary(InputExpressionKind kind, InputExpression left, InputExpression right, SourceRange source)
    {
        return new InputExpression(kind, default, string.Empty, [left, right], source);
    }

    public static InputExpression Function(string name, ImmutableArray<InputExpression> arguments, SourceRange source)
    {
        return new InputExpression(InputExpressionKind.Function, default, name, arguments, source);
    }
}
