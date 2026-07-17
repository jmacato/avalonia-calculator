using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Checker-owned replay for the lower or upper envelope of two total exact
/// rational-affine source operands.  Extraction, totality, regularity, kink,
/// tail, and feature derivations are independent of the producer context and
/// theorem dispatcher.
/// </summary>
internal static class AffineMinMaxCertificateReplay
{
    private const string ExpectedRule = "exact-total-rational-affine-minmax-envelope";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, AffineMinMaxProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != certificate.ProvenFeature || !IsSingleFeature(certificate.Feature) || !request.Features.HasFlag(certificate.Feature) || !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, certificate.ClaimCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.Rule, ExpectedRule, StringComparison.Ordinal) || !TryExtractEnvelope(expression, request.Variable, budget, out AffineMinMaxCertificateReplayReplayEnvelope envelope) || !MatchesCertificate(expression, envelope, certificate) || !TryReconstruct(envelope, certificate.Feature, budget, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryExtractEnvelope(SemanticExpression expression, string variable, ResourceBudget budget, out AffineMinMaxCertificateReplayReplayEnvelope envelope)
    {
        budget.Charge();
        if (expression.Value is not { Kind: ValueKind.Function, Name: "min" or "max", Operands: [var firstValue, var secondValue] } function || expression.SourceOperands is not [var first, var second] || !expression.RewriteHistory.IsEmpty || first.Value.Id != firstValue.Id || second.Value.Id != secondValue.Id || !TryExtractTotalLine(first, variable, budget, out AffineMinMaxCertificateReplayReplayLine firstLine) || !TryExtractTotalLine(second, variable, budget, out AffineMinMaxCertificateReplayReplayLine secondLine) || !MatchesPropagatedConditions(expression, first, second))
        {
            envelope = null!;
            return false;
        }

        string[] branches = [firstLine.Canonical, secondLine.Canonical];
        Array.Sort(branches, StringComparer.Ordinal);
        string pattern = $"rational-affine-{function.Name}[{branches[0]};{branches[1]}]";
        AffineMinMaxCertificateReplayReplayModel model = BuildModel(function.Name == "min", firstLine, secondLine, budget);
        envelope = new AffineMinMaxCertificateReplayReplayEnvelope(function.Name, first, second, firstLine, secondLine, pattern, model);
        return true;
    }

    private static bool TryExtractTotalLine(SemanticExpression expression, string variable, ResourceBudget budget, out AffineMinMaxCertificateReplayReplayLine line)
    {
        if (!TryExtractRational(expression.Value, variable, budget, out AffineMinMaxCertificateReplayReplayRational rational) || rational.HasVariableExclusion || !rational.Function.Denominator.IsConstant || rational.Function.Denominator[0].IsZero || rational.Function.Numerator.Degree > 1 || !IsAllReal(expression.DefinedWhen, variable, budget))
        {
            line = default;
            return false;
        }

        BigRational reciprocal = rational.Function.Denominator[0].Reciprocal();
        BigRational slope = rational.Function.Numerator[1] * reciprocal;
        BigRational intercept = rational.Function.Numerator[0] * reciprocal;
        budget.CheckCoefficient(slope);
        budget.CheckCoefficient(intercept);
        line = new AffineMinMaxCertificateReplayReplayLine(slope, intercept);
        return true;
    }

    private static bool IsAllReal(Formula formula, string variable, ResourceBudget budget)
    {
        budget.Charge();
        if (ExactFormulaVerifier.IsAlwaysTrue(formula, budget))
        {
            return true;
        }

        if (!PolynomialFormulaConverter.TryConvert(formula, variable, budget, out PolynomialFormula polynomialFormula))
        {
            return false;
        }

        CellDecompositionCertificate cells = CellDecomposer.Decompose(polynomialFormula, budget);
        return CellDecomposer.Verify(cells, budget) && cells.Result is AllRealSet;
    }

    private static bool MatchesPropagatedConditions(SemanticExpression expression, SemanticExpression first, SemanticExpression second)
    {
        Formula expectedDefined = Formula.And(first.DefinedWhen, second.DefinedWhen, Formula.Predicate(ExactPredicate.FunctionIsDefined, expression.Value));
        Formula expectedContinuous = Formula.And(first.ContinuousWhen, second.ContinuousWhen, Formula.Predicate(ExactPredicate.FunctionIsContinuous, expression.Value));
        Formula expectedDifferentiable = Formula.And(first.DifferentiableWhen, second.DifferentiableWhen, Formula.Predicate(ExactPredicate.FunctionIsDifferentiable, expression.Value));
        return SameFormula(expression.DefinedWhen, expectedDefined) && SameFormula(expression.ContinuousWhen, expectedContinuous) && SameFormula(expression.DifferentiableWhen, expectedDifferentiable);
    }

    private static AffineMinMaxCertificateReplayReplayModel BuildModel(bool isMinimum, AffineMinMaxCertificateReplayReplayLine first, AffineMinMaxCertificateReplayReplayLine second, ResourceBudget budget)
    {
        budget.Charge();
        BigRational slopeDifference = first.Slope - second.Slope;
        budget.CheckCoefficient(slopeDifference);
        if (slopeDifference.IsZero)
        {
            AffineMinMaxCertificateReplayReplayLine selected = SelectParallelLine(isMinimum, first, second);
            return new AffineMinMaxCertificateReplayReplayModel(isMinimum, false, selected, selected, selected, BigRational.Zero, selected.Intercept);
        }

        BigRational intersectionNumerator = second.Intercept - first.Intercept;
        budget.CheckCoefficient(intersectionNumerator);
        BigRational kinkX = intersectionNumerator / slopeDifference;
        budget.CheckCoefficient(kinkX);
        BigRational scaledKinkX = first.Slope * kinkX;
        budget.CheckCoefficient(scaledKinkX);
        BigRational kinkY = scaledKinkX + first.Intercept;
        budget.CheckCoefficient(kinkY);
        bool firstOnLeft = isMinimum ? slopeDifference.Sign > 0 : slopeDifference.Sign < 0;
        AffineMinMaxCertificateReplayReplayLine left = firstOnLeft ? first : second;
        AffineMinMaxCertificateReplayReplayLine right = firstOnLeft ? second : first;
        return new AffineMinMaxCertificateReplayReplayModel(isMinimum, true, default, left, right, kinkX, kinkY);
    }

    private static AffineMinMaxCertificateReplayReplayLine SelectParallelLine(bool isMinimum, AffineMinMaxCertificateReplayReplayLine first, AffineMinMaxCertificateReplayReplayLine second)
    {
        if (first.Intercept == second.Intercept)
        {
            return first;
        }

        bool selectFirst = isMinimum ? first.Intercept < second.Intercept : first.Intercept > second.Intercept;
        return selectFirst ? first : second;
    }

    private static bool MatchesCertificate(SemanticExpression expression, AffineMinMaxCertificateReplayReplayEnvelope envelope, AffineMinMaxProofCertificate certificate) => string.Equals(certificate.Function, envelope.Function, StringComparison.Ordinal) && string.Equals(certificate.FirstOperandCanonical, envelope.FirstOperand.Value.Canonical, StringComparison.Ordinal) && string.Equals(certificate.SecondOperandCanonical, envelope.SecondOperand.Value.Canonical, StringComparison.Ordinal) && string.Equals(certificate.FirstDefinednessCanonical, envelope.FirstOperand.DefinedWhen.Canonical, StringComparison.Ordinal) && string.Equals(certificate.SecondDefinednessCanonical, envelope.SecondOperand.DefinedWhen.Canonical, StringComparison.Ordinal) && string.Equals(certificate.PatternCanonical, envelope.PatternCanonical, StringComparison.Ordinal) && certificate.FirstSlope == envelope.FirstLine.Slope && certificate.FirstIntercept == envelope.FirstLine.Intercept && certificate.SecondSlope == envelope.SecondLine.Slope && certificate.SecondIntercept == envelope.SecondLine.Intercept && string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal);
    private static bool TryReconstruct(AffineMinMaxCertificateReplayReplayEnvelope envelope, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge();
        AffineMinMaxCertificateReplayReplayModel model = envelope.Model;
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = AllRealSet.Instance;
                return true;
            case AnalysisFeatures.Range:
                value = Range(model);
                return true;
            case AnalysisFeatures.Parity:
                value = Parity(envelope, model);
                return true;
            case AnalysisFeatures.Zeros:
                value = Zeros(model, budget);
                return true;
            case AnalysisFeatures.YIntercept:
                value = OptionalValue<ExactReal>.Some(Rational(model.IsMinimum ? Min(envelope.FirstLine.Intercept, envelope.SecondLine.Intercept) : Max(envelope.FirstLine.Intercept, envelope.SecondLine.Intercept)));
                return true;
            case AnalysisFeatures.Minima:
                value = Extrema(model, minimum: true);
                return true;
            case AnalysisFeatures.Maxima:
                value = Extrema(model, minimum: false);
                return true;
            case AnalysisFeatures.InflectionPoints:
                value = ImmutableArray<FeaturePoint>.Empty;
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
                value = Asymptotes(model, AsymptoteOrientation.Horizontal);
                return true;
            case AnalysisFeatures.ObliqueAsymptotes:
                value = Asymptotes(model, AsymptoteOrientation.Oblique);
                return true;
            case AnalysisFeatures.Monotonicity:
                value = MonotonicityRegions(model);
                return true;
            case AnalysisFeatures.Period:
                value = new Periodicity(!model.HasKink && model.EffectiveLine.Slope.IsZero ? PeriodicityKind.PeriodicWithoutFundamentalPeriod : PeriodicityKind.NotPeriodic, null);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static RealSet Range(AffineMinMaxCertificateReplayReplayModel model)
    {
        if (!model.HasKink)
        {
            return model.EffectiveLine.Slope.IsZero ? RealSets.Points([Rational(model.EffectiveLine.Intercept)]) : AllRealSet.Instance;
        }

        bool unboundedBelow = model.LeftTail.Slope.Sign > 0 || model.RightTail.Slope.Sign < 0;
        bool unboundedAbove = model.LeftTail.Slope.Sign < 0 || model.RightTail.Slope.Sign > 0;
        if (unboundedBelow && unboundedAbove)
        {
            return AllRealSet.Instance;
        }

        ExactReal kinkY = Rational(model.KinkY);
        if (unboundedBelow)
        {
            return new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(kinkY), true);
        }

        if (unboundedAbove)
        {
            return new IntervalSet(RealBound.Finite(kinkY), true, RealBound.PositiveInfinity, false);
        }

        return RealSets.Points([kinkY]);
    }

    private static RealSet Zeros(AffineMinMaxCertificateReplayReplayModel model, ResourceBudget budget)
    {
        if (!model.HasKink)
        {
            return AffineZeros(model.EffectiveLine, budget);
        }

        IntervalSet? zeroInterval = null;
        var roots = new List<BigRational>(2);
        AddSide(model.LeftTail, left: true);
        AddSide(model.RightTail, left: false);
        if (zeroInterval is not null)
        {
            return zeroInterval;
        }

        ImmutableArray<ExactReal> orderedRoots = roots.Distinct().Order().Select(static root => (ExactReal)Rational(root)).ToImmutableArray();
        return orderedRoots.IsEmpty ? EmptySet.Instance : new PointSet(orderedRoots);
        void AddSide(AffineMinMaxCertificateReplayReplayLine line, bool left)
        {
            budget.Charge();
            if (line.Slope.IsZero)
            {
                if (model.KinkY.IsZero)
                {
                    ExactReal kink = Rational(model.KinkX);
                    zeroInterval = left ? new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(kink), true) : new IntervalSet(RealBound.Finite(kink), true, RealBound.PositiveInfinity, false);
                }

                return;
            }

            BigRational root = -line.Intercept / line.Slope;
            budget.CheckCoefficient(root);
            if (left ? root <= model.KinkX : root >= model.KinkX)
            {
                roots.Add(root);
            }
        }
    }

    private static RealSet AffineZeros(AffineMinMaxCertificateReplayReplayLine line, ResourceBudget budget)
    {
        if (line.Slope.IsZero)
        {
            return line.Intercept.IsZero ? AllRealSet.Instance : EmptySet.Instance;
        }

        BigRational root = -line.Intercept / line.Slope;
        budget.CheckCoefficient(root);
        return RealSets.Points([Rational(root)]);
    }

    private static FunctionParity Parity(AffineMinMaxCertificateReplayReplayEnvelope envelope, AffineMinMaxCertificateReplayReplayModel model)
    {
        if (!model.HasKink)
        {
            AffineMinMaxCertificateReplayReplayLine line = model.EffectiveLine;
            if (line.Slope.IsZero)
            {
                return line.Intercept.IsZero ? FunctionParity.Both : FunctionParity.Even;
            }

            return line.Intercept.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
        }

        return envelope.FirstLine.Slope == -envelope.SecondLine.Slope && envelope.FirstLine.Intercept == envelope.SecondLine.Intercept ? FunctionParity.Even : FunctionParity.Neither;
    }

    private static ImmutableArray<FeaturePoint> Extrema(AffineMinMaxCertificateReplayReplayModel model, bool minimum)
    {
        if (!model.HasKink)
        {
            return [];
        }

        bool strictMinimum = model.LeftTail.Slope.Sign < 0 && model.RightTail.Slope.Sign > 0;
        bool strictMaximum = model.LeftTail.Slope.Sign > 0 && model.RightTail.Slope.Sign < 0;
        if (minimum ? !strictMinimum : !strictMaximum)
        {
            return [];
        }

        return [new ConstantYFeaturePoint(new SingletonReal(Rational(model.KinkX)), Rational(model.KinkY))];
    }

    private static ImmutableArray<Asymptote> Asymptotes(AffineMinMaxCertificateReplayReplayModel model, AsymptoteOrientation orientation)
    {
        ImmutableArray<AffineMinMaxCertificateReplayReplayLine> tails = model.HasKink ? [model.RightTail, model.LeftTail] : [model.EffectiveLine];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = ImmutableArray.CreateBuilder<Asymptote>();
        foreach (AffineMinMaxCertificateReplayReplayLine tail in tails)
        {
            bool horizontal = tail.Slope.IsZero;
            if ((orientation == AsymptoteOrientation.Horizontal) != horizontal || !seen.Add(tail.Canonical))
            {
                continue;
            }

            ExactReal intercept = Rational(tail.Intercept);
            result.Add(horizontal ? new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(intercept), null, intercept) : new Asymptote(AsymptoteOrientation.Oblique, new SingletonReal(intercept), Rational(tail.Slope), intercept));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<MonotoneRegion> MonotonicityRegions(AffineMinMaxCertificateReplayReplayModel model)
    {
        if (!model.HasKink)
        {
            return [new MonotoneRegion(AllRealSet.Instance, Direction(model.EffectiveLine.Slope))];
        }

        int leftSign = model.LeftTail.Slope.Sign;
        int rightSign = model.RightTail.Slope.Sign;
        if (leftSign == rightSign)
        {
            return [new MonotoneRegion(AllRealSet.Instance, Direction(model.LeftTail.Slope))];
        }

        ExactReal kink = Rational(model.KinkX);
        return [new MonotoneRegion(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(kink), false), Direction(model.LeftTail.Slope)), new MonotoneRegion(new IntervalSet(RealBound.Finite(kink), false, RealBound.PositiveInfinity, false), Direction(model.RightTail.Slope))];
    }

    private static bool TryExtractRational(ValueTerm term, string variable, ResourceBudget budget, out AffineMinMaxCertificateReplayReplayRational rational)
    {
        budget.Charge();
        switch (term)
        {
            case { Kind: ValueKind.Constant }:
                budget.CheckCoefficient(term.Constant);
                rational = new AffineMinMaxCertificateReplayReplayRational(RationalFunction.Constant(term.Constant, budget), false);
                return true;
            case { Kind: ValueKind.Variable } when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                rational = new AffineMinMaxCertificateReplayReplayRational(RationalFunction.Variable, false);
                return true;
            case { Kind: ValueKind.Negate, Operands: [var operand] }:
                if (TryExtractRational(operand, variable, budget, out AffineMinMaxCertificateReplayReplayRational negated))
                {
                    rational = new AffineMinMaxCertificateReplayReplayRational(negated.Function.Negate(budget), negated.HasVariableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide, Operands: [var leftTerm, var rightTerm] }:
                if (TryExtractRational(leftTerm, variable, budget, out AffineMinMaxCertificateReplayReplayRational left) && TryExtractRational(rightTerm, variable, budget, out AffineMinMaxCertificateReplayReplayRational right) && (term.Kind != ValueKind.Divide || !right.Function.Numerator.IsZero))
                {
                    bool variableExclusion = left.HasVariableExclusion || right.HasVariableExclusion || term.Kind == ValueKind.Divide && !right.Function.Numerator.IsConstant;
                    RationalFunction function = term.Kind switch
                    {
                        ValueKind.Add => left.Function.Add(right.Function, budget),
                        ValueKind.Subtract => left.Function.Subtract(right.Function, budget),
                        ValueKind.Multiply => left.Function.Multiply(right.Function, budget),
                        ValueKind.Divide => left.Function.Divide(right.Function, budget),
                        _ => throw new ArgumentOutOfRangeException(nameof(term))
                    };
                    rational = new AffineMinMaxCertificateReplayReplayRational(function, variableExclusion);
                    return true;
                }

                break;
            case { Kind: ValueKind.Power, Operands: [var basisTerm, { Kind: ValueKind.Constant, Constant: { IsInteger: true } exponent }] } when exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue:
                if (TryExtractRational(basisTerm, variable, budget, out AffineMinMaxCertificateReplayReplayRational basis) && (exponent.Sign > 0 || !basis.Function.Numerator.IsZero))
                {
                    bool variableExclusion = basis.HasVariableExclusion || exponent.Sign <= 0 && !basis.Function.Numerator.IsConstant;
                    rational = new AffineMinMaxCertificateReplayReplayRational(basis.Function.Pow((int)exponent.Numerator, budget), variableExclusion);
                    return true;
                }

                break;
        }

        rational = default;
        return false;
    }

    private static Monotonicity Direction(BigRational slope) => slope.Sign switch
    {
        > 0 => Monotonicity.Increasing,
        < 0 => Monotonicity.Decreasing,
        _ => Monotonicity.Constant
    };
    private static bool IsSingleFeature(AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 && (value & (value - 1)) == 0 && (feature & AnalysisFeatures.All) == feature;
    }

    private static BigRational Min(BigRational first, BigRational second) => first <= second ? first : second;
    private static BigRational Max(BigRational first, BigRational second) => first >= second ? first : second;
    private static bool SameFormula(Formula actual, Formula expected) => string.Equals(actual.Canonical, expected.Canonical, StringComparison.Ordinal);
    private static RationalReal Rational(BigRational value) => new(value);
}
