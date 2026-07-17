using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class IntegerPredicateSolver
{
    public static bool TrySolvePrimitivePreimage(ValueTerm expression, string variable, out PrimitiveIntegerPreimage preimage)
    {
        if (!TrigonometricSignSolver.IsPrimitive(expression, "tan", variable))
        {
            preimage = null!;
            return false;
        }

        preimage = new PrimitiveIntegerPreimage("tan", "arctan(n)", new AffinePiReal(BigRational.One, BigRational.Zero), "n", "k");
        return true;
    }
}
