using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Independently replays feature claims whose semialgebraic definedness has
/// no interval component.  The actual definedness formula is decomposed and
/// cell-verified before any value at an isolated rational point is evaluated.
/// </summary>
internal static class DegenerateDomainCertificateReplay
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, TheoremProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Theorem != TheoremRule.SemialgebraicCellDecomposition || certificate.Parameters.IsDefault || certificate.Parameters is not ["zero-dimensional-domain", var domainParameter, var valueParameter] || certificate.Feature != certificate.ProvenFeature || !request.Features.HasFlag(certificate.Feature) || !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, certificate.ClaimCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(valueParameter, expression.Value.Canonical, StringComparison.Ordinal) || !TryVerifyDomainAndEvaluate(expression, request.Variable, budget, out RealSet domain, out ImmutableArray<DegenerateDomainCertificateReplayEvaluatedPoint> values) || !string.Equals(domainParameter, domain.Canonical, StringComparison.Ordinal) || !TryReconstruct(certificate.Feature, domain, values, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryVerifyDomainAndEvaluate(SemanticExpression expression, string variable, ResourceBudget budget, out RealSet domain, out ImmutableArray<DegenerateDomainCertificateReplayEvaluatedPoint> values)
    {
        budget.Charge(4);
        if (!PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, variable, budget, out PolynomialFormula formula))
        {
            domain = null!;
            values = [];
            return false;
        }

        CellDecompositionCertificate cells = CellDecomposer.Decompose(formula, budget);
        if (!CellDecomposer.Verify(cells, budget))
        {
            domain = null!;
            values = [];
            return false;
        }

        domain = cells.Result;
        if (domain is EmptySet)
        {
            values = [];
            return true;
        }

        if (domain is not PointSet points)
        {
            values = [];
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<DegenerateDomainCertificateReplayEvaluatedPoint>(points.Points.Length);
        foreach (ExactReal point in points.Points)
        {
            budget.Charge();
            if (point is not RationalReal rational || !TryEvaluateValue(expression.Value, variable, rational.Value, budget, out BigRational result))
            {
                values = [];
                return false;
            }

            builder.Add(new DegenerateDomainCertificateReplayEvaluatedPoint(rational.Value, result));
        }

        values = builder.MoveToImmutable();
        return true;
    }

    private static bool TryReconstruct(AnalysisFeatures feature, RealSet domain, ImmutableArray<DegenerateDomainCertificateReplayEvaluatedPoint> values, out object value)
    {
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = domain;
                return true;
            case AnalysisFeatures.Range:
                value = RealSets.Points(values.Select(static item => (ExactReal)new RationalReal(item.Value)));
                return true;
            case AnalysisFeatures.Parity:
                value = ComputeParity(values);
                return true;
            case AnalysisFeatures.Zeros:
                value = RealSets.Points(values.Where(static item => item.Value.IsZero).Select(static item => (ExactReal)new RationalReal(item.Argument)));
                return true;
            case AnalysisFeatures.YIntercept:
                value = ComputeYIntercept(values);
                return true;
            case AnalysisFeatures.Minima:
            case AnalysisFeatures.Maxima:
            case AnalysisFeatures.InflectionPoints:
                value = ImmutableArray<FeaturePoint>.Empty;
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
            case AnalysisFeatures.HorizontalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.Monotonicity:
                value = ImmutableArray<MonotoneRegion>.Empty;
                return true;
            case AnalysisFeatures.Period:
                value = values.IsEmpty ? new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null) : new Periodicity(PeriodicityKind.NotPeriodic, null);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static FunctionParity ComputeParity(ImmutableArray<DegenerateDomainCertificateReplayEvaluatedPoint> values)
    {
        var reflectedValues = new Dictionary<BigRational, BigRational>();
        foreach (DegenerateDomainCertificateReplayEvaluatedPoint point in values)
        {
            if (!reflectedValues.TryAdd(point.Argument, point.Value))
            {
                return FunctionParity.Neither;
            }
        }

        bool even = true;
        bool odd = true;
        foreach (DegenerateDomainCertificateReplayEvaluatedPoint point in values)
        {
            if (!reflectedValues.TryGetValue(-point.Argument, out BigRational reflected))
            {
                return FunctionParity.Neither;
            }

            even &= reflected == point.Value;
            odd &= reflected == -point.Value;
        }

        return (even, odd) switch
        {
            (true, true) => FunctionParity.Both,
            (true, false) => FunctionParity.Even,
            (false, true) => FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
    }

    private static OptionalValue<ExactReal> ComputeYIntercept(ImmutableArray<DegenerateDomainCertificateReplayEvaluatedPoint> values)
    {
        foreach (DegenerateDomainCertificateReplayEvaluatedPoint point in values)
        {
            if (point.Argument.IsZero)
            {
                return OptionalValue<ExactReal>.Some(new RationalReal(point.Value));
            }
        }

        return OptionalValue<ExactReal>.None;
    }

    private static bool TryEvaluateValue(ValueTerm term, string variable, BigRational argument, ResourceBudget budget, out BigRational value)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                return TryPublish(term.Constant, budget, out value);
            case ValueKind.Variable when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                return TryPublish(argument, budget, out value);
            case ValueKind.Negate:
                if (TryEvaluateValue(term.Operands[0], variable, argument, budget, out BigRational negated))
                {
                    return TryPublish(-negated, budget, out value);
                }

                break;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
            case ValueKind.Divide:
                return TryEvaluateBinary(term, variable, argument, budget, out value);
            case ValueKind.Power:
                return TryEvaluatePower(term, variable, argument, budget, out value);
            case ValueKind.Function:
                return TryEvaluateFunction(term, variable, argument, budget, out value);
        }

        value = default;
        return false;
    }

    private static bool TryEvaluateBinary(ValueTerm term, string variable, BigRational argument, ResourceBudget budget, out BigRational value)
    {
        if (!TryEvaluateValue(term.Operands[0], variable, argument, budget, out BigRational left) || !TryEvaluateValue(term.Operands[1], variable, argument, budget, out BigRational right) || term.Kind == ValueKind.Divide && right.IsZero)
        {
            value = default;
            return false;
        }

        BigRational result = term.Kind switch
        {
            ValueKind.Add => left + right,
            ValueKind.Subtract => left - right,
            ValueKind.Multiply => left * right,
            ValueKind.Divide => left / right,
            _ => throw new ArgumentOutOfRangeException(nameof(term))
        };
        return TryPublish(result, budget, out value);
    }

    private static bool TryEvaluatePower(ValueTerm term, string variable, BigRational argument, ResourceBudget budget, out BigRational value)
    {
        if (!TryEvaluateValue(term.Operands[0], variable, argument, budget, out BigRational basis) || !TryEvaluateValue(term.Operands[1], variable, argument, budget, out BigRational exponent) || !exponent.IsInteger || exponent.Numerator < int.MinValue || exponent.Numerator > int.MaxValue || basis.IsZero && exponent.Sign <= 0)
        {
            value = default;
            return false;
        }

        BigRational result = basis.Pow((int)exponent.Numerator);
        return TryPublish(result, budget, out value);
    }

    private static bool TryEvaluateFunction(ValueTerm term, string variable, BigRational argument, ResourceBudget budget, out BigRational value)
    {
        if (term.Operands.Length != 1 || !TryEvaluateValue(term.Operands[0], variable, argument, budget, out BigRational functionArgument))
        {
            value = default;
            return false;
        }

        switch (term.Name)
        {
            case "abs":
                return TryPublish(functionArgument.Abs(), budget, out value);
            case "sqrt" when BigRational.TrySquareRoot(functionArgument, out BigRational squareRoot):
                return TryPublish(squareRoot, budget, out value);
            case "sin" when functionArgument.IsZero:
            case "tan" when functionArgument.IsZero:
            case "asin" when functionArgument.IsZero:
            case "atan" when functionArgument.IsZero:
                return TryPublish(BigRational.Zero, budget, out value);
            case "cos" when functionArgument.IsZero:
            case "exp" when functionArgument.IsZero:
                return TryPublish(BigRational.One, budget, out value);
            case "log" when functionArgument.IsOne:
            case "ln" when functionArgument.IsOne:
                return TryPublish(BigRational.Zero, budget, out value);
            default:
                value = default;
                return false;
        }
    }

    private static bool TryPublish(BigRational candidate, ResourceBudget budget, out BigRational value)
    {
        budget.CheckCoefficient(candidate);
        value = candidate;
        return true;
    }
}
