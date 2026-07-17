using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class AffineSquareLogCertificateChecker
{
    private const string Rule = "base-ten-log-affine-square-v1";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, AffineSquareLogProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge(8);
        if (!HasValidEnvelope(request, expression, certificate, claim) || !TryCreateContext(expression, request.Variable, budget, out AffineSquareLogCertificateCheckerAffineSquareLogReplayContext? context) || context.Slope != certificate.Slope || context.Intercept != certificate.Intercept || !string.Equals(context.PatternCanonical, certificate.PatternCanonical, StringComparison.Ordinal) || !TryReconstructClaim(context, certificate.Feature, budget, out object? expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool HasValidEnvelope(AnalysisRequest request, SemanticExpression expression, AffineSquareLogProofCertificate certificate, string claim) => certificate.Rule == Rule && IsSingleFeature(certificate.Feature) && certificate.Feature == certificate.ProvenFeature && request.Features.HasFlag(certificate.Feature) && string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) && string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) && string.Equals(certificate.Claim, claim, StringComparison.Ordinal) && string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal);
    private static bool TryCreateContext(SemanticExpression expression, string variable, ResourceBudget budget, out AffineSquareLogCertificateCheckerAffineSquareLogReplayContext context)
    {
        if (!TryGetStructuralOperands(expression, out _, out ValueTerm affine) || !RationalFunctionExtractor.TryExtract(affine, variable, budget, out RationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || extraction.Function.Denominator.Degree != 0 || extraction.Function.Numerator.Degree != 1)
        {
            context = null!;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        if (denominator.IsZero)
        {
            context = null!;
            return false;
        }

        BigRational slope = extraction.Function.Numerator[1] / denominator;
        BigRational intercept = extraction.Function.Numerator[0] / denominator;
        if (slope.IsZero)
        {
            context = null!;
            return false;
        }

        budget.CheckCoefficient(slope);
        budget.CheckCoefficient(intercept);
        BigRational center = -intercept / slope;
        budget.CheckCoefficient(center);
        context = new AffineSquareLogCertificateCheckerAffineSquareLogReplayContext(slope, intercept, center);
        return HasExactDomain(expression.DefinedWhen, variable, Domain(context), budget);
    }

    private static bool TryGetStructuralOperands(SemanticExpression expression, out ValueTerm square, out ValueTerm affine)
    {
        if (expression.Value is not { Kind: ValueKind.Function, Name: "log", Operands: [{ Kind: ValueKind.Power, Operands: [var candidateAffine, { Kind: ValueKind.Constant, Constant.IsInteger: true } exponent] } candidateSquare] } || exponent.Constant.Numerator != 2 || expression.SourceOperands is not [var squareExpression] || !ReferenceEquals(squareExpression.Value, candidateSquare) || squareExpression.SourceOperands is not [var affineExpression, var exponentExpression] || !ReferenceEquals(affineExpression.Value, candidateAffine) || !ReferenceEquals(exponentExpression.Value, exponent) || exponentExpression.DefinedWhen is not BooleanFormula { Value: true })
        {
            square = null!;
            affine = null!;
            return false;
        }

        square = candidateSquare;
        affine = candidateAffine;
        return true;
    }

    private static bool HasExactDomain(Formula definedWhen, string variable, RealSet expected, ResourceBudget budget)
    {
        if (!PolynomialFormulaConverter.TryConvert(definedWhen, variable, budget, out PolynomialFormula formula))
        {
            return false;
        }

        CellDecompositionCertificate cells = CellDecomposer.Decompose(formula, budget);
        return string.Equals(cells.Result.Canonical, expected.Canonical, StringComparison.Ordinal);
    }

    private static bool TryReconstructClaim(AffineSquareLogCertificateCheckerAffineSquareLogReplayContext context, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge(4);
        ExactReal center = Rational(context.Center);
        value = feature switch
        {
            AnalysisFeatures.Domain => Domain(context),
            AnalysisFeatures.Range => AllRealSet.Instance,
            AnalysisFeatures.Parity => context.Intercept.IsZero ? FunctionParity.Even : FunctionParity.Neither,
            AnalysisFeatures.Zeros => Zeros(context, budget),
            AnalysisFeatures.YIntercept => YIntercept(context, budget),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Vertical, new SingletonReal(center), null, null)),
            AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => MonotonicityRegions(context),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static RealSet Domain(AffineSquareLogCertificateCheckerAffineSquareLogReplayContext context)
    {
        ExactReal center = Rational(context.Center);
        return RealSets.Union(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(center), false), new IntervalSet(RealBound.Finite(center), false, RealBound.PositiveInfinity, false));
    }

    private static Graphing.Symbolics.PointSet Zeros(AffineSquareLogCertificateCheckerAffineSquareLogReplayContext context, ResourceBudget budget)
    {
        BigRational first = (-context.Intercept - BigRational.One) / context.Slope;
        BigRational second = (-context.Intercept + BigRational.One) / context.Slope;
        budget.CheckCoefficient(first);
        budget.CheckCoefficient(second);
        return new PointSet(new[] { first, second }.Order().Select(static point => (ExactReal)Rational(point)).ToImmutableArray());
    }

    private static OptionalValue<ExactReal> YIntercept(AffineSquareLogCertificateCheckerAffineSquareLogReplayContext context, ResourceBudget budget)
    {
        if (context.Intercept.IsZero)
        {
            return OptionalValue<ExactReal>.None;
        }

        BigRational argument = context.Intercept.Pow(2);
        budget.CheckCoefficient(argument);
        return OptionalValue<ExactReal>.Some(BaseTenLog(argument, budget));
    }

    private static ImmutableArray<MonotoneRegion> MonotonicityRegions(AffineSquareLogCertificateCheckerAffineSquareLogReplayContext context)
    {
        ExactReal center = Rational(context.Center);
        return [new MonotoneRegion(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(center), false), Monotonicity.Decreasing), new MonotoneRegion(new IntervalSet(RealBound.Finite(center), false, RealBound.PositiveInfinity, false), Monotonicity.Increasing)];
    }

    private static ExactReal BaseTenLog(BigRational positive, ResourceBudget budget)
    {
        if (TryPowerOfTen(positive.Numerator, budget, out int numeratorPower) && TryPowerOfTen(positive.Denominator, budget, out int denominatorPower))
        {
            return Rational(new BigRational(numeratorPower - denominatorPower));
        }

        return new FunctionReal("log", [Rational(positive)]);
    }

    private static bool TryPowerOfTen(ExactInteger value, ResourceBudget budget, out int power)
    {
        if (value.Sign <= 0)
        {
            power = default;
            return false;
        }

        power = 0;
        while (value > ExactInteger.One)
        {
            budget.Charge();
            value = ExactInteger.DivRem(value, 10, out ExactInteger remainder);
            if (!remainder.IsZero)
            {
                power = default;
                return false;
            }

            power++;
        }

        return true;
    }

    private static bool IsSingleFeature(AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 && (value & (value - 1)) == 0 && (feature & AnalysisFeatures.All) == feature;
    }

    private static RationalReal Rational(BigRational value) => new(value);
}
