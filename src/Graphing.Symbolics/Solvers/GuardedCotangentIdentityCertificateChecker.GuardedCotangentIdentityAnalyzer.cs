using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class GuardedCotangentIdentityCertificateChecker
{
    private const string Rule = "pythagorean-identity-over-centered-tangent";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, GuardedCotangentIdentityProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (!HasValidEnvelope(request, expression, certificate, claim) || !TryCreateReplayContext(expression, request.Variable, request.AngleUnit, budget, out GuardedCotangentIdentityCertificateCheckerGuardedCotangentReplayContext? context) || !string.Equals(certificate.NumeratorCanonical, context.Numerator.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DenominatorCanonical, context.Denominator.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.PatternCanonical, context.PatternCanonical, StringComparison.Ordinal) || !string.Equals(certificate.DomainCanonical, context.Domain.Canonical, StringComparison.Ordinal) || !TryReconstructClaim(context, certificate.Feature, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool HasValidEnvelope(AnalysisRequest request, SemanticExpression expression, GuardedCotangentIdentityProofCertificate certificate, string claim) => certificate.Feature == certificate.ProvenFeature && IsSingleFeature(certificate.Feature) && request.Features.HasFlag(certificate.Feature) && string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) && string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) && string.Equals(certificate.Claim, claim, StringComparison.Ordinal) && string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) && string.Equals(certificate.Rule, Rule, StringComparison.Ordinal) && string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal);
    private static bool TryCreateReplayContext(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out GuardedCotangentIdentityCertificateCheckerGuardedCotangentReplayContext context)
    {
        budget.Charge(8);
        if (expression.Value is not { Kind: ValueKind.Divide, Operands: [var numeratorValue, var denominatorValue] } || expression.SourceOperands is not [var numerator, var denominator] || numerator.Value.Id != numeratorValue.Id || denominator.Value.Id != denominatorValue.Id || !IsEverywhereDefinedIdentity(numerator, variable, budget) || !TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(denominator.Value, variable, budget, out AffineTrigPattern tangent) || tangent.Function != "tan" || tangent.Amplitude.IsZero || !tangent.Phase.IsZero || !tangent.Shift.IsZero || !HasExactTangentGuard(denominator.DefinedWhen, tangent, variable, budget) || !HasExactDivisionGuards(expression.DefinedWhen, denominator.Value, tangent, variable, budget))
        {
            context = null!;
            return false;
        }

        ExactScalar cotangentScale = tangent.Amplitude.Reciprocal(budget);
        ExactReal punctureStep = ScaleAngle(PiAngle(angleUnit, new BigRational(1, 2)), tangent.Frequency.Reciprocal(), budget);
        var domain = new PeriodicIntervalSet(punctureStep, "m", IntegerConstraint.All("m"), [new PeriodicInterval(new RationalReal(BigRational.Zero), false, punctureStep, false)]);
        ExactReal functionPeriod = ScaleAngle(PiAngle(angleUnit, BigRational.One), tangent.Frequency.Reciprocal(), budget);
        context = new GuardedCotangentIdentityCertificateCheckerGuardedCotangentReplayContext(numerator, denominator, tangent, cotangentScale, domain, functionPeriod);
        return true;
    }

    private static bool IsEverywhereDefinedIdentity(SemanticExpression numerator, string variable, ResourceBudget budget)
    {
        if (!ExactFormulaVerifier.IsAlwaysTrue(numerator.DefinedWhen, budget) || !HalfAngleReducer.TryReduce(numerator.Value, variable, budget, out RationalFunction reduced))
        {
            return false;
        }

        return reduced.Numerator.Subtract(reduced.Denominator, budget).IsZero;
    }

    private static bool HasExactTangentGuard(Formula definedWhen, AffineTrigPattern tangent, string variable, ResourceBudget budget) => ExactFormulaVerifier.MatchesSingleGuard(definedWhen, operand => MatchesCosineGuard(operand, tangent, variable, budget), budget);
    private static bool HasExactDivisionGuards(Formula definedWhen, ValueTerm denominator, AffineTrigPattern tangent, string variable, ResourceBudget budget)
    {
        budget.Charge();
        bool foundCosineGuard = false;
        bool foundDenominatorGuard = false;
        IEnumerable<Formula> operands = definedWhen is JunctionFormula { IsConjunction: true } conjunction ? conjunction.Operands : [definedWhen];
        foreach (Formula operand in operands)
        {
            budget.Charge();
            if (MatchesCosineGuard(operand, tangent, variable, budget))
            {
                if (foundCosineGuard)
                {
                    return false;
                }

                foundCosineGuard = true;
                continue;
            }

            if (MatchesDenominatorNonzeroGuard(operand, denominator))
            {
                if (foundDenominatorGuard)
                {
                    return false;
                }

                foundDenominatorGuard = true;
                continue;
            }

            if (!ExactFormulaVerifier.IsAlwaysTrue(operand, budget))
            {
                return false;
            }
        }

        return foundCosineGuard && foundDenominatorGuard;
    }

    private static bool MatchesCosineGuard(Formula formula, AffineTrigPattern tangent, string variable, ResourceBudget budget)
    {
        if (formula is not ComparisonFormula { Comparison: Comparison.NotEqual, Left: { Kind: ValueKind.Function, Name: "cos", Operands: [var argument] }, Right: { Kind: ValueKind.Constant, Constant.IsZero: true } } || !RationalFunctionExtractor.TryExtract(argument, variable, budget, out RationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || extraction.Function.Denominator.Degree != 0 || extraction.Function.Numerator.Degree != 1)
        {
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        if (denominator.IsZero)
        {
            return false;
        }

        BigRational frequency = extraction.Function.Numerator[1] / denominator;
        BigRational phase = extraction.Function.Numerator[0] / denominator;
        budget.CheckCoefficient(frequency);
        budget.CheckCoefficient(phase);
        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phase = -phase;
        }

        return frequency == tangent.Frequency && phase == tangent.Phase;
    }

    private static bool MatchesDenominatorNonzeroGuard(Formula formula, ValueTerm denominator) => formula is ComparisonFormula { Comparison: Comparison.NotEqual, Left: var candidate, Right: { Kind: ValueKind.Constant, Constant.IsZero: true } } && candidate.Id == denominator.Id;
    private static bool TryReconstructClaim(GuardedCotangentIdentityCertificateCheckerGuardedCotangentReplayContext context, AnalysisFeatures feature, out object value)
    {
        var zero = new RationalReal(BigRational.Zero);
        value = feature switch
        {
            AnalysisFeatures.Domain => context.Domain,
            AnalysisFeatures.Range => RealSets.Union(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(zero), false), new IntervalSet(RealBound.Finite(zero), false, RealBound.PositiveInfinity, false)),
            AnalysisFeatures.Parity => FunctionParity.Odd,
            AnalysisFeatures.Zeros => EmptySet.Instance,
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Vertical, new PeriodicReal(zero, context.FunctionPeriod, "m", IntegerConstraint.All("m")), null, null)),
            AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(context.Domain, context.CotangentScale.Sign > 0 ? Monotonicity.Decreasing : Monotonicity.Increasing)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, context.FunctionPeriod),
            _ => null!
        };
        return value is not null;
    }

    private static ExactReal PiAngle(AngleUnit unit, BigRational fraction) => unit switch
    {
        AngleUnit.Radians => new AffinePiReal(fraction, BigRational.Zero),
        AngleUnit.Degrees => new RationalReal(new BigRational(180) * fraction),
        AngleUnit.Grads => new RationalReal(new BigRational(200) * fraction),
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };
    private static ExactReal ScaleAngle(ExactReal value, BigRational scale, ResourceBudget budget)
    {
        budget.CheckCoefficient(scale);
        budget.Charge();
        ExactReal result = ExactRealArithmetic.Scale(value, scale);
        if (result is AffinePiReal affine)
        {
            budget.CheckCoefficient(affine.PiCoefficient);
            budget.CheckCoefficient(affine.Constant);
        }
        else if (result is RationalReal rational)
        {
            budget.CheckCoefficient(rational.Value);
        }

        return result;
    }

    private static bool IsSingleFeature(AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 && (value & (value - 1)) == 0 && (feature & AnalysisFeatures.All) == feature;
    }
}
