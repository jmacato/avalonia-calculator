using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class PeriodicPunctureFacts
{
    public static bool TryCreate(ExactReal period, ExactReal lower, ExactReal upper, ResourceBudget budget, out bool symmetric)
    {
        budget.Charge();
        if (!TryCoordinate(period, out BigRational periodCoordinate, out PeriodicPunctureFactsCoordinateBasis basis) || !TryCoordinate(lower, out BigRational lowerCoordinate, out PeriodicPunctureFactsCoordinateBasis lowerBasis) || !TryCoordinate(upper, out BigRational upperCoordinate, out PeriodicPunctureFactsCoordinateBasis upperBasis) || basis != lowerBasis || basis != upperBasis || periodCoordinate.Sign <= 0 || upperCoordinate - lowerCoordinate != periodCoordinate)
        {
            symmetric = false;
            return false;
        }

        // The domain is R minus the lattice lower + k*period. It is symmetric
        // exactly when reflecting that lattice yields the same lattice.
        symmetric = ((new BigRational(2) * lowerCoordinate) / periodCoordinate).IsInteger;
        return true;
    }

    private static bool TryCoordinate(ExactReal value, out BigRational coordinate, out PeriodicPunctureFactsCoordinateBasis basis)
    {
        switch (value)
        {
            case RationalReal rational:
                coordinate = rational.Value;
                basis = PeriodicPunctureFactsCoordinateBasis.Rational;
                return true;
            case AffinePiReal { Constant.IsZero: true } affine:
                coordinate = affine.PiCoefficient;
                basis = PeriodicPunctureFactsCoordinateBasis.Pi;
                return true;
            default:
                coordinate = default;
                basis = default;
                return false;
        }
    }
}
