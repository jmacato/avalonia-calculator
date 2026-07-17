using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class DegenerateDomainAnalyzer
{
    public static bool TryCompute(SemanticExpression expression, string variable, AnalysisFeatures feature, ResourceBudget budget, out object value, out ImmutableArray<string> parameters)
    {
        if (!DomainSolver.TrySolve(expression, variable, budget, out RealSet domain, out _) || domain is not (EmptySet or PointSet))
        {
            value = null!;
            parameters = [];
            return false;
        }

        ImmutableArray<(BigRational X, BigRational Y)> values;
        if (domain is EmptySet)
        {
            values = [];
        }
        else
        {
            var builder = ImmutableArray.CreateBuilder<(BigRational X, BigRational Y)>();
            foreach (ExactReal point in ((PointSet)domain).Points)
            {
                if (point is not RationalReal rational || !ExactTermEvaluator.TryEvaluate(expression.Value, variable, rational.Value, budget, out BigRational result))
                {
                    value = null!;
                    parameters = [];
                    return false;
                }

                builder.Add((rational.Value, result));
            }

            values = builder.ToImmutable();
        }

        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => RealSets.Points(values.Select(static item => (ExactReal)new RationalReal(item.Y))),
            AnalysisFeatures.Parity => Parity(values),
            AnalysisFeatures.Zeros => RealSets.Points(values.Where(static item => item.Y.IsZero).Select(static item => (ExactReal)new RationalReal(item.X))),
            AnalysisFeatures.YIntercept => Intercept(values),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray<MonotoneRegion>.Empty,
            AnalysisFeatures.Period => values.IsEmpty ? new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null) : new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        parameters = ["zero-dimensional-domain", domain.Canonical, expression.Value.Canonical];
        return value is not null;
    }

    private static FunctionParity Parity(ImmutableArray<(BigRational X, BigRational Y)> values)
    {
        var map = values.ToDictionary(static item => item.X, static item => item.Y);
        bool even = true;
        bool odd = true;
        foreach ((BigRational x, BigRational y) in values)
        {
            if (!map.TryGetValue(-x, out BigRational reflected))
            {
                return FunctionParity.Neither;
            }

            even &= reflected == y;
            odd &= reflected == -y;
        }

        return (even, odd) switch
        {
            (true, true) => FunctionParity.Both,
            (true, false) => FunctionParity.Even,
            (false, true) => FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
    }

    private static OptionalValue<ExactReal> Intercept(ImmutableArray<(BigRational X, BigRational Y)> values)
    {
        foreach ((BigRational x, BigRational y) in values)
        {
            if (x.IsZero)
            {
                return OptionalValue<ExactReal>.Some(new RationalReal(y));
            }
        }

        return OptionalValue<ExactReal>.None;
    }
}
