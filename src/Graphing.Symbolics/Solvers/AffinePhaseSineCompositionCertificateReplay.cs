using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Independent replay for sqrt/log(sin(a*x+b)) certificates.  It binds the
/// exact source guard, repeats affine extraction and angle-unit normalization,
/// and reconstructs the published claim without invoking the producer
/// extractor or proof kernel.
/// </summary>
internal static class AffinePhaseSineCompositionCertificateReplay
{
    private const string Rule = "exact-affine-phase-sine-composition-v1";
    private const string Parameter = "m";

    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        AffinePhaseSineCompositionProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Rule != Rule ||
            certificate.Feature != certificate.ProvenFeature ||
            !CertificateFeatureBinding.IsSingleRequested(request, certificate.Feature) ||
            !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) ||
            !string.Equals(
                certificate.DefinednessCanonical,
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal) ||
            request.AngleUnit is not (AngleUnit.Radians or AngleUnit.Degrees or AngleUnit.Grads) ||
            request.AngleUnit != certificate.AngleUnit ||
            !TryExtractPattern(
                request,
                expression,
                budget,
                out AffinePhaseSineCompositionPattern? pattern) ||
            pattern.OuterKind != certificate.OuterKind ||
            pattern.Frequency != certificate.Frequency ||
            pattern.PhasePiCoefficient != certificate.PhasePiCoefficient ||
            pattern.PhaseConstant != certificate.PhaseConstant ||
            pattern.InnerSign != certificate.InnerSign ||
            !string.Equals(pattern.Canonical, certificate.PatternCanonical, StringComparison.Ordinal) ||
            !TryReconstructClaim(
                pattern,
                request.AngleUnit,
                certificate.Feature,
                budget,
                out object? expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryExtractPattern(
        AnalysisRequest request,
        SemanticExpression expression,
        ResourceBudget budget,
        out AffinePhaseSineCompositionPattern pattern)
    {
        budget.Charge(8);
        if (expression.Value is not
            {
                Kind: ValueKind.Function,
                Name: "sqrt" or "ln" or "log",
                Operands:
                [
                    {
                        Kind: ValueKind.Function,
                        Name: "sin",
                        Operands: [var argument]
                    }
                ]
            } outer ||
            !ExactRationalExtractor.TryExtract(
                argument,
                request.Variable,
                budget,
                out ExactRationalExtraction extraction) ||
            !extraction.DomainExclusions.IsEmpty ||
            !extraction.Denominator.IsConstant ||
            extraction.Numerator.Degree != 1)
        {
            pattern = null!;
            return false;
        }

        ExactScalar denominator = extraction.Denominator[0];
        if (denominator.IsZero)
        {
            pattern = null!;
            return false;
        }

        ExactScalar reciprocal = denominator.Reciprocal(budget);
        ExactScalar frequencyScalar = extraction.Numerator[1].Multiply(reciprocal, budget);
        ExactScalar phaseScalar = extraction.Numerator[0].Multiply(reciprocal, budget);
        if (frequencyScalar.RationalValue is not { IsZero: false } frequency ||
            !TryDecomposeAffinePi(
                phaseScalar,
                out BigRational phasePiCoefficient,
                out BigRational phaseConstant))
        {
            pattern = null!;
            return false;
        }

        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phasePiCoefficient = -phasePiCoefficient;
            phaseConstant = -phaseConstant;
            AddHalfTurn(
                request.AngleUnit,
                ref phasePiCoefficient,
                ref phaseConstant);
        }

        NormalizeFullTurns(
            request.AngleUnit,
            ref phasePiCoefficient,
            ref phaseConstant);

        AffinePhaseSineOuterKind outerKind = outer.Name switch
        {
            "sqrt" => AffinePhaseSineOuterKind.SquareRoot,
            "ln" => AffinePhaseSineOuterKind.NaturalLogarithm,
            "log" => AffinePhaseSineOuterKind.CommonLogarithm,
            _ => throw new InvalidOperationException()
        };
        pattern = new AffinePhaseSineCompositionPattern(
            outerKind,
            frequency,
            phasePiCoefficient,
            phaseConstant,
            1);

        if (!DefinednessMatches(expression, pattern))
        {
            pattern = null!;
            return false;
        }

        budget.CheckCoefficient(pattern.Frequency);
        budget.CheckCoefficient(pattern.PhasePiCoefficient);
        budget.CheckCoefficient(pattern.PhaseConstant);
        return true;
    }

    private static bool DefinednessMatches(
        SemanticExpression expression,
        AffinePhaseSineCompositionPattern pattern)
    {
        ValueTerm sine = expression.Value.Operands[0];
        ValueTerm zero = new(
            -1,
            ValueKind.Constant,
            BigRational.Zero,
            string.Empty,
            [],
            "q:0");
        Comparison comparison = pattern.IsSquareRoot
            ? Comparison.GreaterOrEqual
            : Comparison.Greater;
        Formula expected = Formula.Compare(sine, comparison, zero);
        return string.Equals(
            expression.DefinedWhen.Canonical,
            expected.Canonical,
            StringComparison.Ordinal);
    }

    private static bool TryDecomposeAffinePi(
        ExactScalar phase,
        out BigRational piCoefficient,
        out BigRational constant)
    {
        switch (phase.Value)
        {
            case RationalReal rational:
                piCoefficient = BigRational.Zero;
                constant = rational.Value;
                return true;
            case AffinePiReal affine:
                piCoefficient = affine.PiCoefficient;
                constant = affine.Constant;
                return true;
            default:
                piCoefficient = default;
                constant = default;
                return false;
        }
    }

    private static void AddHalfTurn(
        AngleUnit angleUnit,
        ref BigRational piCoefficient,
        ref BigRational constant)
    {
        switch (angleUnit)
        {
            case AngleUnit.Radians:
                piCoefficient += BigRational.One;
                break;
            case AngleUnit.Degrees:
                constant += new BigRational(180);
                break;
            case AngleUnit.Grads:
                constant += new BigRational(200);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(angleUnit));
        }
    }

    private static void NormalizeFullTurns(
        AngleUnit angleUnit,
        ref BigRational piCoefficient,
        ref BigRational constant)
    {
        switch (angleUnit)
        {
            case AngleUnit.Radians:
                piCoefficient = PositiveModulo(piCoefficient, new BigRational(2));
                break;
            case AngleUnit.Degrees:
                constant = PositiveModulo(constant, new BigRational(360));
                break;
            case AngleUnit.Grads:
                constant = PositiveModulo(constant, new BigRational(400));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(angleUnit));
        }
    }

    private static bool TryReconstructClaim(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge(4);
        if (feature == AnalysisFeatures.YIntercept)
        {
            OptionalValue<ExactReal> intercept = YIntercept(
                pattern,
                angleUnit,
                out bool known);
            value = intercept;
            return known;
        }

        value = feature switch
        {
            AnalysisFeatures.Domain => Domain(pattern, angleUnit),
            AnalysisFeatures.Range => Range(pattern),
            AnalysisFeatures.Parity => Parity(pattern, angleUnit),
            AnalysisFeatures.Zeros => Zeros(pattern, angleUnit),
            AnalysisFeatures.Minima => Minima(pattern, angleUnit),
            AnalysisFeatures.Maxima => Maxima(pattern, angleUnit),
            AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => VerticalAsymptotes(pattern, angleUnit),
            AnalysisFeatures.HorizontalAsymptotes or
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => MonotonicityRegions(pattern, angleUnit),
            AnalysisFeatures.Period => Period(pattern, angleUnit),
            _ => null!
        };
        return value is not null;
    }

    private static PeriodicIntervalSet Domain(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        BigRational start = pattern.InnerSign > 0
            ? BigRational.Zero
            : BigRational.One;
        return PeriodicIntervals(
            pattern,
            angleUnit,
            start,
            pattern.IsSquareRoot,
            start + BigRational.One,
            pattern.IsSquareRoot,
            new BigRational(2));
    }

    private static IntervalSet Range(AffinePhaseSineCompositionPattern pattern)
    {
        ExactReal zero = Rational(BigRational.Zero);
        return pattern.IsSquareRoot
            ? new IntervalSet(
                RealBound.Finite(zero),
                true,
                RealBound.Finite(Rational(BigRational.One)),
                true)
            : new IntervalSet(
                RealBound.NegativeInfinity,
                false,
                RealBound.Finite(zero),
                true);
    }

    private static FunctionParity Parity(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        return TryPhaseFraction(pattern, angleUnit, out BigRational fraction) &&
               (fraction - new BigRational(1, 2)).IsInteger
            ? FunctionParity.Even
            : FunctionParity.Neither;
    }

    private static PeriodicPointSet Zeros(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        BigRational angle = pattern.IsSquareRoot
            ? BigRational.Zero
            : pattern.InnerSign > 0
                ? new BigRational(1, 2)
                : new BigRational(3, 2);
        BigRational step = pattern.IsSquareRoot
            ? BigRational.One
            : new BigRational(2);
        return new PeriodicPointSet(
            SolveAngle(pattern, Angle(angleUnit, angle)),
            Step(pattern, angleUnit, step),
            Parameter,
            IntegerConstraint.All(Parameter));
    }

    private static OptionalValue<ExactReal> YIntercept(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit,
        out bool known)
    {
        if (!TryPhaseFraction(pattern, angleUnit, out BigRational fraction))
        {
            known = false;
            return default;
        }

        BigRational reduced = PositiveModulo(fraction, new BigRational(2));
        int rawSign = reduced.IsZero || reduced.IsOne
            ? 0
            : reduced < BigRational.One
                ? 1
                : -1;
        int valueSign = pattern.InnerSign * rawSign;
        if (valueSign < 0 || valueSign == 0 && !pattern.IsSquareRoot)
        {
            known = true;
            return OptionalValue<ExactReal>.None;
        }

        if (valueSign == 0)
        {
            known = true;
            return OptionalValue<ExactReal>.Some(Rational(BigRational.Zero));
        }

        ExactReal inner = ExactSineAtPhase(pattern, fraction);
        if (inner is RationalReal { Value.IsOne: true })
        {
            known = true;
            return OptionalValue<ExactReal>.Some(Rational(
                pattern.IsSquareRoot ? BigRational.One : BigRational.Zero));
        }

        known = true;
        return OptionalValue<ExactReal>.Some(new FunctionReal(
            pattern.OuterFunction,
            [inner]));
    }

    private static ExactReal ExactSineAtPhase(
        AffinePhaseSineCompositionPattern pattern,
        BigRational phaseFraction)
    {
        BigRational quarterTurns = phaseFraction * new BigRational(2);
        if (quarterTurns.IsInteger)
        {
            int residue = (int)(quarterTurns.Numerator % 4);
            if (residue < 0)
            {
                residue += 4;
            }

            BigRational special = residue switch
            {
                1 => BigRational.One,
                3 => BigRational.MinusOne,
                _ => BigRational.Zero
            };
            return Rational(pattern.InnerSign * special);
        }

        ExactReal sine = new FunctionReal(
            "sin",
            [new AffinePiReal(phaseFraction, BigRational.Zero)]);
        return pattern.InnerSign > 0 ? sine : ExactRealArithmetic.Negate(sine);
    }

    private static ImmutableArray<FeaturePoint> Minima(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        if (!pattern.IsSquareRoot)
        {
            return [];
        }

        BigRational start = pattern.InnerSign > 0
            ? BigRational.Zero
            : BigRational.One;
        ExactReal period = Step(pattern, angleUnit, new BigRational(2));
        return
        [
            PeriodicFeature(pattern, angleUnit, start, period, Rational(BigRational.Zero)),
            PeriodicFeature(
                pattern,
                angleUnit,
                start + BigRational.One,
                period,
                Rational(BigRational.Zero))
        ];
    }

    private static ImmutableArray<FeaturePoint> Maxima(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        BigRational angle = pattern.InnerSign > 0
            ? new BigRational(1, 2)
            : new BigRational(3, 2);
        ExactReal y = Rational(pattern.IsSquareRoot
            ? BigRational.One
            : BigRational.Zero);
        return
        [
            PeriodicFeature(
                pattern,
                angleUnit,
                angle,
                Step(pattern, angleUnit, new BigRational(2)),
                y)
        ];
    }

    private static ImmutableArray<Asymptote> VerticalAsymptotes(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.IsSquareRoot)
        {
            return [];
        }

        return
        [
            new Asymptote(
                AsymptoteOrientation.Vertical,
                new PeriodicReal(
                    SolveAngle(pattern, Angle(angleUnit, BigRational.Zero)),
                    Step(pattern, angleUnit, BigRational.One),
                    Parameter,
                    IntegerConstraint.All(Parameter)),
                null,
                null)
        ];
    }

    private static ImmutableArray<MonotoneRegion> MonotonicityRegions(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        BigRational start = pattern.InnerSign > 0
            ? BigRational.Zero
            : BigRational.One;
        return
        [
            new MonotoneRegion(
                PeriodicIntervals(
                    pattern,
                    angleUnit,
                    start,
                    false,
                    start + new BigRational(1, 2),
                    false,
                    new BigRational(2)),
                Monotonicity.Increasing),
            new MonotoneRegion(
                PeriodicIntervals(
                    pattern,
                    angleUnit,
                    start + new BigRational(1, 2),
                    false,
                    start + BigRational.One,
                    false,
                    new BigRational(2)),
                Monotonicity.Decreasing)
        ];
    }

    private static Periodicity Period(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        return new Periodicity(
            PeriodicityKind.PeriodicWithFundamentalPeriod,
            Step(pattern, angleUnit, new BigRational(2)));
    }

    private static ConstantYFeaturePoint PeriodicFeature(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit,
        BigRational angle,
        ExactReal period,
        ExactReal y)
    {
        return new ConstantYFeaturePoint(
            new PeriodicReal(
                SolveAngle(pattern, Angle(angleUnit, angle)),
                period,
                Parameter,
                IntegerConstraint.All(Parameter)),
            y);
    }

    private static PeriodicIntervalSet PeriodicIntervals(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit,
        BigRational lower,
        bool includesLower,
        BigRational upper,
        bool includesUpper,
        BigRational period)
    {
        return new PeriodicIntervalSet(
            Step(pattern, angleUnit, period),
            Parameter,
            IntegerConstraint.All(Parameter),
            [
                new PeriodicInterval(
                    SolveAngle(pattern, Angle(angleUnit, lower)),
                    includesLower,
                    SolveAngle(pattern, Angle(angleUnit, upper)),
                    includesUpper)
            ]);
    }

    private static ExactReal SolveAngle(
        AffinePhaseSineCompositionPattern pattern,
        ExactReal angle)
    {
        return ExactRealArithmetic.Scale(
            ExactRealArithmetic.Subtract(angle, pattern.Phase),
            pattern.Frequency.Reciprocal());
    }

    private static ExactReal Step(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit,
        BigRational fraction)
    {
        return ExactRealArithmetic.Scale(
            Angle(angleUnit, fraction),
            pattern.Frequency.Reciprocal());
    }

    private static bool TryPhaseFraction(
        AffinePhaseSineCompositionPattern pattern,
        AngleUnit angleUnit,
        out BigRational fraction)
    {
        switch (angleUnit)
        {
            case AngleUnit.Radians when pattern.PhaseConstant.IsZero:
                fraction = pattern.PhasePiCoefficient;
                return true;
            case AngleUnit.Degrees when pattern.PhasePiCoefficient.IsZero:
                fraction = pattern.PhaseConstant / new BigRational(180);
                return true;
            case AngleUnit.Grads when pattern.PhasePiCoefficient.IsZero:
                fraction = pattern.PhaseConstant / new BigRational(200);
                return true;
            default:
                fraction = default;
                return false;
        }
    }

    private static BigRational PositiveModulo(BigRational value, BigRational modulus)
    {
        BigRational quotientValue = value / modulus;
        ExactInteger quotient = quotientValue.Numerator / quotientValue.Denominator;
        BigRational remainder = value - new BigRational(quotient) * modulus;
        return remainder.Sign < 0 ? remainder + modulus : remainder;
    }

    private static ExactReal Angle(AngleUnit unit, BigRational piFraction)
    {
        return ExactAngleArithmetic.PiFraction(unit, piFraction);
    }

    private static RationalReal Rational(BigRational value)
    {
        return new RationalReal(value);
    }
}
