using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingImpl;

internal static class SymbolicsAdapter
{
    public static InputExpression Translate(AstNode node)
    {
        SourceRange source = new(node.Span.Start, node.Span.Length);
        ImmutableArray<InputExpression> arguments = node.Children
            .Select(Translate)
            .ToImmutableArray();
        return node.Kind switch
        {
            AstKind.Number => InputExpression.Number(
                BigRational.Parse(node.Number.ToString()),
                source),
            AstKind.Variable => InputExpression.Variable(node.Name, source),
            AstKind.Negate => InputExpression.Unary(
                InputExpressionKind.Negate,
                arguments[0],
                source),
            AstKind.Add => Binary(InputExpressionKind.Add, arguments, source),
            AstKind.Subtract => Binary(InputExpressionKind.Subtract, arguments, source),
            AstKind.Multiply => Binary(InputExpressionKind.Multiply, arguments, source),
            AstKind.Divide => Binary(InputExpressionKind.Divide, arguments, source),
            AstKind.Power => Binary(InputExpressionKind.Power, arguments, source),
            AstKind.Function => InputExpression.Function(node.Name, arguments, source),
            _ => throw new ArgumentOutOfRangeException(nameof(node))
        };
    }

    public static AnalysisFeatures Translate(PerformAnalysisType requested)
    {
        AnalysisFeatures features = AnalysisFeatures.None;
        if (requested.HasFlag(PerformAnalysisType.Domain))
        {
            features |= AnalysisFeatures.Domain;
        }

        if (requested.HasFlag(PerformAnalysisType.Range))
        {
            features |= AnalysisFeatures.Range;
        }

        if (requested.HasFlag(PerformAnalysisType.Parity))
        {
            features |= AnalysisFeatures.Parity;
        }

        if (requested.HasFlag(PerformAnalysisType.InterceptionPointsWithXAndYAxis))
        {
            features |= AnalysisFeatures.Zeros | AnalysisFeatures.YIntercept;
        }

        if (requested.HasFlag(PerformAnalysisType.CriticalPoints))
        {
            features |= AnalysisFeatures.Minima |
                        AnalysisFeatures.Maxima |
                        AnalysisFeatures.InflectionPoints;
        }

        if (requested.HasFlag(PerformAnalysisType.Asymptotes))
        {
            features |= AnalysisFeatures.VerticalAsymptotes |
                        AnalysisFeatures.HorizontalAsymptotes |
                        AnalysisFeatures.ObliqueAsymptotes;
        }

        if (requested.HasFlag(PerformAnalysisType.Monotonicity))
        {
            features |= AnalysisFeatures.Monotonicity;
        }

        if (requested.HasFlag(PerformAnalysisType.Period))
        {
            features |= AnalysisFeatures.Period;
        }

        return features;
    }

    public static AngleUnit Translate(EvalTrigUnitMode mode)
    {
        return mode switch
        {
            EvalTrigUnitMode.Degrees => AngleUnit.Degrees,
            EvalTrigUnitMode.Grads => AngleUnit.Grads,
            _ => AngleUnit.Radians
        };
    }

    private static InputExpression Binary(
        InputExpressionKind kind,
        ImmutableArray<InputExpression> arguments,
        SourceRange source)
    {
        return InputExpression.Binary(kind, arguments[0], arguments[1], source);
    }
}
