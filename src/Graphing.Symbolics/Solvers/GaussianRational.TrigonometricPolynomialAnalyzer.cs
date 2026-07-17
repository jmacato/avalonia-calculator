using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal readonly record struct GaussianRational(BigRational Real, BigRational Imaginary)
{
    public bool IsZero => Real.IsZero && Imaginary.IsZero;

    public static GaussianRational operator +(GaussianRational left, GaussianRational right) => new(left.Real + right.Real, left.Imaginary + right.Imaginary);
    public static GaussianRational operator -(GaussianRational left, GaussianRational right) => new(left.Real - right.Real, left.Imaginary - right.Imaginary);
    public static GaussianRational operator -(GaussianRational value) => new(-value.Real, -value.Imaginary);
    public static GaussianRational operator *(GaussianRational left, GaussianRational right) => new((left.Real * right.Real) - (left.Imaginary * right.Imaginary), (left.Real * right.Imaginary) + (left.Imaginary * right.Real));
}
