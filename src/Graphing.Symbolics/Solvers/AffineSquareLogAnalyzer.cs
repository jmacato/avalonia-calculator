using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class AffineSquareLogAnalyzer
{
    internal const string Rule = "base-ten-log-affine-square-v1";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!TryCompute(request, expression, feature, budget, out object? value, out AffineSquareLogPattern? pattern) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new AffineSquareLogProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), pattern.Slope, pattern.Intercept, pattern.Canonical, expression.DefinedWhen.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    private static bool TryCompute(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out object value, out AffineSquareLogPattern pattern)
    {
        budget.Charge(8);
        if (!TryExtractPattern(expression.Value, request.Variable, budget, out pattern))
        {
            value = null!;
            return false;
        }

        budget.CheckCoefficient(pattern.Slope);
        budget.CheckCoefficient(pattern.Intercept);
        RealSet domain = Domain(pattern);
        if (!TrigonometricAndLatticeAnalyzer.DomainMatches(expression, request.Variable, request.AngleUnit, domain, budget))
        {
            value = null!;
            return false;
        }

        return TryComputeFeature(pattern, feature, domain, budget, out value);
    }

    private static bool TryExtractPattern(ValueTerm term, string variable, ResourceBudget budget, out AffineSquareLogPattern pattern)
    {
        if (term is not { Kind: ValueKind.Function, Name: "log", Operands: [{ Kind: ValueKind.Power, Operands: [var affine, { Kind: ValueKind.Constant, Constant.IsInteger: true } exponent] }] } || exponent.Constant.Numerator != 2 || !RationalFunctionExtractor.TryExtract(affine, variable, budget, out RationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || extraction.Function.Denominator.Degree != 0 || extraction.Function.Numerator.Degree != 1)
        {
            pattern = null!;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        BigRational slope = extraction.Function.Numerator[1] / denominator;
        if (slope.IsZero)
        {
            pattern = null!;
            return false;
        }

        BigRational intercept = extraction.Function.Numerator[0] / denominator;
        pattern = new AffineSquareLogPattern(slope, intercept);
        return true;
    }

    private static RealSet Domain(AffineSquareLogPattern pattern)
    {
        ExactReal center = Rational(pattern.Center);
        return RealSets.Union(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(center), false), new IntervalSet(RealBound.Finite(center), false, RealBound.PositiveInfinity, false));
    }

    private static bool TryComputeFeature(AffineSquareLogPattern pattern, AnalysisFeatures feature, RealSet domain, ResourceBudget budget, out object value)
    {
        ExactReal center = Rational(pattern.Center);
        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => AllRealSet.Instance,
            AnalysisFeatures.Parity => pattern.Intercept.IsZero ? FunctionParity.Even : FunctionParity.Neither,
            AnalysisFeatures.Zeros => Zeros(pattern, budget),
            AnalysisFeatures.YIntercept => YIntercept(pattern, budget),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Vertical, new SingletonReal(center), null, null)),
            AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(center), false), Monotonicity.Decreasing), new MonotoneRegion(new IntervalSet(RealBound.Finite(center), false, RealBound.PositiveInfinity, false), Monotonicity.Increasing)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static Graphing.Symbolics.PointSet Zeros(AffineSquareLogPattern pattern, ResourceBudget budget)
    {
        BigRational first = (-pattern.Intercept - BigRational.One) / pattern.Slope;
        BigRational second = (-pattern.Intercept + BigRational.One) / pattern.Slope;
        budget.CheckCoefficient(first);
        budget.CheckCoefficient(second);
        return new PointSet(new[] { first, second }.Order().Select(static point => (ExactReal)Rational(point)).ToImmutableArray());
    }

    private static OptionalValue<ExactReal> YIntercept(AffineSquareLogPattern pattern, ResourceBudget budget)
    {
        if (pattern.Intercept.IsZero)
        {
            return OptionalValue<ExactReal>.None;
        }

        BigRational argument = pattern.Intercept.Pow(2);
        budget.CheckCoefficient(argument);
        return OptionalValue<ExactReal>.Some(BaseTenLog(argument, budget));
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

    private static RationalReal Rational(BigRational value) => new(value);
}
