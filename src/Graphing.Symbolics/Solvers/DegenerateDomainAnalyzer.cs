using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class DegenerateDomainAnalyzer
{
    public static bool TryCompute(
        SemanticExpression expression,
        string variable,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value,
        out ImmutableArray<string> parameters)
    {
        if (!DomainSolver.TrySolve(
                expression,
                variable,
                budget,
                out RealSet domain,
                out _) ||
            domain is not (EmptySet or PointSet))
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
                if (point is not RationalReal rational ||
                    !ExactTermEvaluator.TryEvaluate(
                        expression.Value,
                        variable,
                        rational.Value,
                        budget,
                        out BigRational result))
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
            AnalysisFeatures.Range => RealSets.Points(
                values.Select(static item => (ExactReal)new RationalReal(item.Y))),
            AnalysisFeatures.Parity => Parity(values),
            AnalysisFeatures.Zeros => RealSets.Points(
                values
                    .Where(static item => item.Y.IsZero)
                    .Select(static item => (ExactReal)new RationalReal(item.X))),
            AnalysisFeatures.YIntercept => Intercept(values),
            AnalysisFeatures.Minima or
            AnalysisFeatures.Maxima or
            AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or
            AnalysisFeatures.HorizontalAsymptotes or
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray<MonotoneRegion>.Empty,
            AnalysisFeatures.Period => values.IsEmpty
                ? new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null)
                : new Periodicity(PeriodicityKind.NotPeriodic, null),
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

    private static OptionalValue<ExactReal> Intercept(
        ImmutableArray<(BigRational X, BigRational Y)> values)
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

internal static class ExactTermEvaluator
{
    public static bool TryEvaluate(
        ValueTerm term,
        string variable,
        BigRational variableValue,
        ResourceBudget budget,
        out BigRational value)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                value = term.Constant;
                return true;
            case ValueKind.Variable when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                value = variableValue;
                return true;
            case ValueKind.Negate:
                if (TryEvaluate(term.Operands[0], variable, variableValue, budget, out BigRational negated))
                {
                    value = -negated;
                    return true;
                }

                break;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
            case ValueKind.Divide:
                if (TryEvaluate(term.Operands[0], variable, variableValue, budget, out BigRational left) &&
                    TryEvaluate(term.Operands[1], variable, variableValue, budget, out BigRational right) &&
                    (term.Kind != ValueKind.Divide || !right.IsZero))
                {
                    value = term.Kind switch
                    {
                        ValueKind.Add => left + right,
                        ValueKind.Subtract => left - right,
                        ValueKind.Multiply => left * right,
                        ValueKind.Divide => left / right,
                        _ => throw new InvalidOperationException()
                    };
                    return true;
                }

                break;
            case ValueKind.Power:
                if (TryEvaluate(term.Operands[0], variable, variableValue, budget, out BigRational basis) &&
                    TryEvaluate(term.Operands[1], variable, variableValue, budget, out BigRational exponent) &&
                    exponent.IsInteger &&
                    exponent.Numerator >= int.MinValue &&
                    exponent.Numerator <= int.MaxValue &&
                    (!basis.IsZero || exponent.Sign > 0))
                {
                    value = basis.Pow((int)exponent.Numerator);
                    return true;
                }

                break;
            case ValueKind.Function:
                if (TryFunction(term, variable, variableValue, budget, out value))
                {
                    return true;
                }

                break;
        }

        value = default;
        return false;
    }

    private static bool TryFunction(
        ValueTerm term,
        string variable,
        BigRational variableValue,
        ResourceBudget budget,
        out BigRational value)
    {
        if (term.Operands.Length != 1 ||
            !TryEvaluate(term.Operands[0], variable, variableValue, budget, out BigRational argument))
        {
            value = default;
            return false;
        }

        switch (term.Name)
        {
            case "abs":
                value = argument.Abs();
                return true;
            case "sqrt" when BigRational.TrySquareRoot(argument, out BigRational squareRoot):
                value = squareRoot;
                return true;
            case "sin" when argument.IsZero:
            case "tan" when argument.IsZero:
            case "asin" when argument.IsZero:
            case "atan" when argument.IsZero:
                value = BigRational.Zero;
                return true;
            case "cos" when argument.IsZero:
            case "exp" when argument.IsZero:
                value = BigRational.One;
                return true;
            case "log" when argument.IsOne:
            case "ln" when argument.IsOne:
                value = BigRational.Zero;
                return true;
            default:
                value = default;
                return false;
        }
    }
}
