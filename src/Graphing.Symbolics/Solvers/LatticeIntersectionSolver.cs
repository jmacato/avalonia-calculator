using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class LatticeIntersectionSolver
{
    public static bool TryRestrictToNegativeSine(PrimitiveIntegerPreimage preimage, out ImmutableArray<IntegerLatticeSet> result)
    {
        if (preimage.Primitive != "tan" || preimage.PrincipalInverse != "arctan(n)")
        {
            result = [];
            return false;
        }

        result = [new IntegerLatticeSet("arctan(n)+2mπ", ["m", "n"], ["m,n∈ℤ", "n<0"]), new IntegerLatticeSet("arctan(n)+(2m+1)π", ["m", "n"], ["m,n∈ℤ", "n>0"])];
        return true;
    }
}
