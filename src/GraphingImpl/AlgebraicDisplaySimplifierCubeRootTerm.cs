using Graphing.Symbolics;

namespace GraphingImpl;

internal readonly record struct AlgebraicDisplaySimplifierCubeRootTerm(BigRational RationalValue, BigRational Coefficient, string? Argument)
{
    public bool IsRadical => Argument is not null;

    public static AlgebraicDisplaySimplifierCubeRootTerm Rational(BigRational value)
    {
        return new AlgebraicDisplaySimplifierCubeRootTerm(value, BigRational.Zero, null);
    }

    public static AlgebraicDisplaySimplifierCubeRootTerm Radical(BigRational coefficient, string argument)
    {
        return new AlgebraicDisplaySimplifierCubeRootTerm(BigRational.Zero, coefficient, argument);
    }
}
