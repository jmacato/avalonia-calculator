using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal static class ExactRealCanonical
{
    public static string Format(ExactReal value) => value switch
    {
        RationalReal rational => "q:" + rational.Value,
        AlgebraicReal algebraic => $"alg:{algebraic.Polynomial.Canonical}:{algebraic.IsolatingInterval.Lower}:{algebraic.IsolatingInterval.Upper}:{algebraic.RootIndex}:{string.Join(',', algebraic.ThomEncoding)}",
        AffinePiReal affinePi => $"pi:{affinePi.PiCoefficient}:{affinePi.Constant}",
        NamedReal named => "named:" + named.Name,
        FunctionReal function => $"fn:{function.Function}({string.Join(',', function.Arguments.Select(Format))})",
        AlgebraicImageReal image => $"image:{image.Function.Numerator.Canonical}/{image.Function.Denominator.Canonical}@{Format(image.Argument)}",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    public static string SortKey(ExactReal value) => value switch
    {
        RationalReal rational => "0:" + rational.Value,
        AlgebraicReal algebraic => "1:" + algebraic.IsolatingInterval.Lower,
        _ => "2:" + Format(value)
    };
}
