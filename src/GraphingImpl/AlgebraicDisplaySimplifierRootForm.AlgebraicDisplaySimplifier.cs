using System.Globalization;
using Graphing.Symbolics;

namespace GraphingImpl;

internal readonly record struct AlgebraicDisplaySimplifierRootForm(bool IsDirect, AlgebraicDisplaySimplifierQuadraticValue DirectValue, AlgebraicDisplaySimplifierQuadraticValue Square, int RootSign)
{
    public static AlgebraicDisplaySimplifierRootForm Direct(AlgebraicDisplaySimplifierQuadraticValue value) => new(true, value, default, 0);
    public static AlgebraicDisplaySimplifierRootForm Extension(AlgebraicDisplaySimplifierQuadraticValue square, int sign) => new(false, default, square, sign);
}
