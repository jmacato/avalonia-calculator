using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class PowerDomainSolver
{
    public static bool TryCompute(SemanticExpression expression, string variable, AngleUnit angleUnit, AnalysisFeatures feature, ResourceBudget budget, out object value, out ImmutableArray<string> parameters)
    {
        ValueTerm term = expression.Value;
        if (angleUnit != AngleUnit.Radians || term.Kind != ValueKind.Power || term.Operands.Length != 2 || !TrigonometricSignSolver.IsPrimitive(term.Operands[0], "sin", variable) || !IntegerPredicateSolver.TrySolvePrimitivePreimage(term.Operands[1], variable, out PrimitiveIntegerPreimage integerPreimage))
        {
            value = null!;
            parameters = [];
            return false;
        }

        budget.Charge(32);
        parameters = feature switch
        {
            AnalysisFeatures.Domain => [expression.DefinedWhen.Canonical, "tan-inverse:atan(n)+k*pi", "negative-base:integer-exponent", "zero-base:positive-exponent"],
            AnalysisFeatures.Zeros => ["nonzero-on-every-certified-domain-component"],
            AnalysisFeatures.YIntercept => ["origin-fails-power-definedness"],
            AnalysisFeatures.HorizontalAsymptotes => ["distinct-periodic-subsequences-at-positive-and-negative-infinity"],
            _ => []
        };
        if (parameters.IsEmpty)
        {
            value = null!;
            return false;
        }

        if (feature == AnalysisFeatures.Zeros)
        {
            value = EmptySet.Instance;
            return true;
        }

        if (feature == AnalysisFeatures.YIntercept)
        {
            value = OptionalValue<ExactReal>.None;
            return true;
        }

        if (feature == AnalysisFeatures.HorizontalAsymptotes)
        {
            value = ImmutableArray<Asymptote>.Empty;
            return true;
        }

        ExactReal halfPi = new AffinePiReal(new BigRational(1, 2), BigRational.Zero);
        ExactReal pi = new AffinePiReal(BigRational.One, BigRational.Zero);
        ExactReal twoPi = new AffinePiReal(new BigRational(2), BigRational.Zero);
        RealSet positiveBaseWithDefinedExponent = TrigonometricSignSolver.PositiveSineWithTangentDefined(halfPi, pi, twoPi);
        if (!LatticeIntersectionSolver.TryRestrictToNegativeSine(integerPreimage, out ImmutableArray<IntegerLatticeSet> integralNegativeBase))
        {
            value = null!;
            parameters = [];
            return false;
        }

        value = RealSets.Union([positiveBaseWithDefinedExponent, .. integralNegativeBase]);
        return true;
    }
}
