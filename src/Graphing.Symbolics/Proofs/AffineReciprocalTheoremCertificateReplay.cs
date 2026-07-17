using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Independent claim reconstruction for guarded affine cotangent, secant,
/// and cosecant theorem certificates.
/// </summary>
internal static class AffineReciprocalTheoremCertificateReplay
{
    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Theorem != TheoremRule.AffineReciprocalTrigonometric ||
            certificate.Feature != certificate.ProvenFeature ||
            !IsSingleFeature(certificate.Feature) ||
            !request.Features.HasFlag(certificate.Feature) ||
            !string.Equals(
                certificate.Subject,
                expression.Value.Canonical,
                StringComparison.Ordinal) ||
            !string.Equals(
                certificate.SubjectCanonical,
                expression.Value.Canonical,
                StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) ||
            !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) ||
            certificate.Parameters.Length != 4 ||
            !AffineReciprocalTrigonometricAnalyzer.TryGetPattern(
                expression.Value,
                request.Variable,
                budget,
                out AffineReciprocalTrigPattern pattern) ||
            !string.Equals(certificate.Parameters[0], pattern.Canonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Parameters[1], request.AngleUnit.ToString(), StringComparison.Ordinal) ||
            !string.Equals(
                certificate.Parameters[2],
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal) ||
            !string.Equals(
                certificate.Parameters[3],
                "guarded-sine-cosine-ratio",
                StringComparison.Ordinal))
        {
            return false;
        }

        RealSet domain = BuildDomain(pattern, request.AngleUnit, budget);
        if (!HasExactDenominatorGuard(
                expression.DefinedWhen,
                pattern,
                request.Variable,
                budget) ||
            !TryReconstruct(
                pattern,
                request.AngleUnit,
                certificate.Feature,
                domain,
                budget,
                out object expected))
        {
            return false;
        }

        return string.Equals(
            ClaimCanonical.ForObject(expected),
            claim,
            StringComparison.Ordinal);
    }

    private static bool HasExactDenominatorGuard(
        Formula definedWhen,
        AffineReciprocalTrigPattern pattern,
        string variable,
        ResourceBudget budget) =>
        ExactFormulaVerifier.MatchesSingleGuard(
            definedWhen,
            operand => MatchesDenominatorGuard(
                operand,
                pattern,
                variable,
                budget),
            budget);

    private static bool MatchesDenominatorGuard(
        Formula formula,
        AffineReciprocalTrigPattern pattern,
        string variable,
        ResourceBudget budget)
    {
        if (formula is not ComparisonFormula
            {
                Comparison: Comparison.NotEqual,
                Left: var guardedDenominator,
                Right:
                {
                    Kind: ValueKind.Constant,
                    Constant.IsZero: true
                }
            } ||
            !TryStripExactScalar(
                guardedDenominator,
                budget,
                out ValueTerm primitive,
                out ExactScalar scale) ||
            scale.IsZero ||
            primitive is not
            {
                Kind: ValueKind.Function,
                Name: "sin" or "cos",
                Operands: [var argument]
            } ||
            !string.Equals(
                primitive.Name,
                pattern.Function == "sec" ? "cos" : "sin",
                StringComparison.Ordinal) ||
            !RationalFunctionExtractor.TryExtract(
                argument,
                variable,
                budget,
                out RationalExtraction extraction) ||
            !extraction.DomainExclusions.IsEmpty ||
            extraction.Function.Denominator.Degree != 0 ||
            extraction.Function.Numerator.Degree != 1)
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

        return frequency == pattern.Frequency && phase == pattern.Phase;
    }

    private static bool TryStripExactScalar(
        ValueTerm source,
        ResourceBudget budget,
        out ValueTerm core,
        out ExactScalar scale)
    {
        budget.Charge();
        core = source;
        scale = ExactScalar.One;
        bool changed;
        do
        {
            changed = false;
            while (core.Kind == ValueKind.Negate)
            {
                core = core.Operands[0];
                scale = scale.Negate();
                changed = true;
                budget.Charge();
            }

            if (core.Kind == ValueKind.Multiply &&
                ExactScalar.TryCreate(core.Operands[0], budget, out ExactScalar left))
            {
                scale = scale.Multiply(left, budget);
                core = core.Operands[1];
                changed = true;
            }
            else if (core.Kind == ValueKind.Multiply &&
                     ExactScalar.TryCreate(core.Operands[1], budget, out ExactScalar right))
            {
                scale = scale.Multiply(right, budget);
                core = core.Operands[0];
                changed = true;
            }
            else if (core.Kind == ValueKind.Divide &&
                     ExactScalar.TryCreate(core.Operands[1], budget, out ExactScalar divisor) &&
                     !divisor.IsZero)
            {
                scale = scale.Multiply(divisor.Reciprocal(budget), budget);
                core = core.Operands[0];
                changed = true;
            }
        }
        while (changed);

        return true;
    }

    private static bool TryReconstruct(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        RealSet domain,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = domain;
                return true;
            case AnalysisFeatures.Range:
                value = BuildRange(pattern);
                return true;
            case AnalysisFeatures.Parity:
                value = BuildParity(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.Zeros:
                if (!pattern.Shift.IsZero)
                {
                    value = null!;
                    return false;
                }

                value = BuildZeros(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.YIntercept:
                return TryBuildYIntercept(pattern, angleUnit, budget, out value);
            case AnalysisFeatures.Minima:
                value = BuildExtrema(pattern, angleUnit, minimum: true, budget);
                return true;
            case AnalysisFeatures.Maxima:
                value = BuildExtrema(pattern, angleUnit, minimum: false, budget);
                return true;
            case AnalysisFeatures.InflectionPoints:
                value = BuildInflections(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
                value = BuildVerticalAsymptotes(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.Monotonicity:
                value = BuildMonotonicity(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.Period:
                value = BuildPeriod(pattern, angleUnit, budget);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static Graphing.Symbolics.PeriodicIntervalSet BuildDomain(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        budget.Charge();
        bool denominatorIsCosine = pattern.Function == "sec";
        BigRational lower = denominatorIsCosine
            ? new BigRational(-1, 2)
            : BigRational.MinusOne;
        BigRational upper = denominatorIsCosine
            ? new BigRational(1, 2)
            : BigRational.Zero;
        ExactReal period = Scale(
            Angle(angleUnit, BigRational.One),
            pattern.Frequency.Reciprocal(),
            budget);
        return new PeriodicIntervalSet(
            period,
            "m",
            IntegerConstraint.All("m"),
            [
                new PeriodicInterval(
                    Solve(pattern, Angle(angleUnit, lower), budget),
                    false,
                    Solve(pattern, Angle(angleUnit, upper), budget),
                    false)
            ]);
    }

    private static RealSet BuildRange(AffineReciprocalTrigPattern pattern)
    {
        if (pattern.Function == "cot")
        {
            return AllRealSet.Instance;
        }

        ExactScalar magnitude = pattern.Amplitude.Abs();
        ExactReal lower = ExactRealArithmetic.AddRational(
            magnitude.Negate().Value,
            pattern.Shift);
        ExactReal upper = ExactRealArithmetic.AddRational(
            magnitude.Value,
            pattern.Shift);
        return RealSets.Union(
            new IntervalSet(
                RealBound.NegativeInfinity,
                false,
                RealBound.Finite(lower),
                true),
            new IntervalSet(
                RealBound.Finite(upper),
                true,
                RealBound.PositiveInfinity,
                false));
    }

    private static FunctionParity BuildParity(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        BigRational? phase = PhaseInPi(pattern.Phase, angleUnit, budget);
        if (phase is null)
        {
            return FunctionParity.Neither;
        }

        bool odd;
        bool even;
        switch (pattern.Function)
        {
            case "cot":
                odd = (phase.Value * new BigRational(2)).IsInteger;
                even = false;
                break;
            case "sec":
                even = phase.Value.IsInteger;
                odd = (phase.Value - new BigRational(1, 2)).IsInteger;
                break;
            case "csc":
                odd = phase.Value.IsInteger;
                even = (phase.Value - new BigRational(1, 2)).IsInteger;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pattern));
        }

        if (even)
        {
            return FunctionParity.Even;
        }

        return odd && pattern.Shift.IsZero
            ? FunctionParity.Odd
            : FunctionParity.Neither;
    }

    private static RealSet BuildZeros(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        if (pattern.Function is "sec" or "csc")
        {
            return EmptySet.Instance;
        }

        return new PeriodicPointSet(
            Solve(
                pattern,
                Angle(angleUnit, new BigRational(1, 2)),
                budget),
            Scale(
                Angle(angleUnit, BigRational.One),
                pattern.Frequency.Reciprocal(),
                budget),
            "m",
            IntegerConstraint.All("m"));
    }

    private static bool TryBuildYIntercept(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        BigRational? phase = PhaseInPi(pattern.Phase, angleUnit, budget);
        bool sineDenominator = pattern.Function is "cot" or "csc";
        if (phase is { } piPhase &&
            (sineDenominator
                ? piPhase.IsInteger
                : (piPhase - new BigRational(1, 2)).IsInteger))
        {
            value = OptionalValue<ExactReal>.None;
            return true;
        }

        ExactReal primitive;
        if (phase is { } exactPhase &&
            TryQuarterTurns(exactPhase, out BigRational sine, out BigRational cosine))
        {
            BigRational reciprocal = pattern.Function switch
            {
                "cot" => cosine / sine,
                "sec" => cosine.Reciprocal(),
                "csc" => sine.Reciprocal(),
                _ => throw new ArgumentOutOfRangeException(nameof(pattern))
            };
            primitive = new RationalReal(reciprocal);
        }
        else
        {
            primitive = new FunctionReal(
                pattern.Function,
                [
                    ExactAngleArithmetic.ToRadians(
                        new RationalReal(pattern.Phase),
                        angleUnit)
                ]);
        }

        ExactReal scaled = ExactRealArithmetic.Multiply(
            pattern.Amplitude.Value,
            primitive);
        value = OptionalValue<ExactReal>.Some(
            ExactRealArithmetic.AddRational(scaled, pattern.Shift));
        return true;
    }

    private static ImmutableArray<FeaturePoint> BuildExtrema(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        bool minimum,
        ResourceBudget budget)
    {
        if (pattern.Function == "cot")
        {
            return [];
        }

        bool positiveCore = minimum == (pattern.Amplitude.Sign > 0);
        BigRational fraction = pattern.Function switch
        {
            "sec" when positiveCore => BigRational.Zero,
            "sec" => BigRational.One,
            "csc" when positiveCore => new BigRational(1, 2),
            "csc" => new BigRational(3, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
        ExactReal y = ExactRealArithmetic.AddRational(
            positiveCore ? pattern.Amplitude.Value : pattern.Amplitude.Negate().Value,
            pattern.Shift);
        return
        [
            new ConstantYFeaturePoint(
                new PeriodicReal(
                    Solve(pattern, Angle(angleUnit, fraction), budget),
                    Scale(
                        Angle(angleUnit, new BigRational(2)),
                        pattern.Frequency.Reciprocal(),
                        budget),
                    "m",
                    IntegerConstraint.All("m")),
                y)
        ];
    }

    private static ImmutableArray<FeaturePoint> BuildInflections(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        if (pattern.Function != "cot")
        {
            return [];
        }

        return
        [
            new ConstantYFeaturePoint(
                new PeriodicReal(
                    Solve(
                        pattern,
                        Angle(angleUnit, new BigRational(1, 2)),
                        budget),
                    Scale(
                        Angle(angleUnit, BigRational.One),
                        pattern.Frequency.Reciprocal(),
                        budget),
                    "m",
                    IntegerConstraint.All("m")),
                new RationalReal(pattern.Shift))
        ];
    }

    private static ImmutableArray<Asymptote> BuildVerticalAsymptotes(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        BigRational fraction = pattern.Function == "sec"
            ? new BigRational(1, 2)
            : BigRational.Zero;
        return
        [
            new Asymptote(
                AsymptoteOrientation.Vertical,
                new PeriodicReal(
                    Solve(pattern, Angle(angleUnit, fraction), budget),
                    Scale(
                        Angle(angleUnit, BigRational.One),
                        pattern.Frequency.Reciprocal(),
                        budget),
                    "m",
                    IntegerConstraint.All("m")),
                null,
                null)
        ];
    }

    private static ImmutableArray<MonotoneRegion> BuildMonotonicity(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        if (pattern.Function == "cot")
        {
            return
            [
                Region(
                    pattern,
                    angleUnit,
                    BigRational.Zero,
                    BigRational.One,
                    BigRational.One,
                    pattern.Amplitude.Sign > 0
                        ? Monotonicity.Decreasing
                        : Monotonicity.Increasing,
                    budget)
            ];
        }

        (BigRational Lower, BigRational Upper, bool Increasing)[] cells =
            pattern.Function == "sec"
                ?
                [
                    (new BigRational(1, 2), BigRational.One, true),
                    (new BigRational(3, 2), new BigRational(2), false),
                    (BigRational.One, new BigRational(3, 2), false),
                    (new BigRational(2), new BigRational(5, 2), true)
                ]
                :
                [
                    (BigRational.Zero, new BigRational(1, 2), false),
                    (new BigRational(1, 2), BigRational.One, true),
                    (BigRational.One, new BigRational(3, 2), true),
                    (new BigRational(3, 2), new BigRational(2), false)
                ];
        var regions = ImmutableArray.CreateBuilder<MonotoneRegion>(cells.Length);
        foreach ((BigRational lower, BigRational upper, bool coreIncreasing) in cells)
        {
            bool increasing = pattern.Amplitude.Sign > 0
                ? coreIncreasing
                : !coreIncreasing;
            regions.Add(Region(
                pattern,
                angleUnit,
                lower,
                upper,
                new BigRational(2),
                increasing ? Monotonicity.Increasing : Monotonicity.Decreasing,
                budget));
        }

        return regions.MoveToImmutable();
    }

    private static MonotoneRegion Region(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        BigRational lower,
        BigRational upper,
        BigRational periodFraction,
        Monotonicity direction,
        ResourceBudget budget) =>
        new(
            new PeriodicIntervalSet(
                Scale(
                    Angle(angleUnit, periodFraction),
                    pattern.Frequency.Reciprocal(),
                    budget),
                "m",
                IntegerConstraint.All("m"),
                [
                    new PeriodicInterval(
                        Solve(pattern, Angle(angleUnit, lower), budget),
                        false,
                        Solve(pattern, Angle(angleUnit, upper), budget),
                        false)
                ]),
            direction);

    private static Periodicity BuildPeriod(
        AffineReciprocalTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        BigRational fraction = pattern.Function == "cot"
            ? BigRational.One
            : new BigRational(2);
        return new Periodicity(
            PeriodicityKind.PeriodicWithFundamentalPeriod,
            Scale(
                Angle(angleUnit, fraction),
                pattern.Frequency.Reciprocal(),
                budget));
    }

    private static BigRational? PhaseInPi(
        BigRational phase,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        budget.CheckCoefficient(phase);
        if (angleUnit == AngleUnit.Radians)
        {
            return phase.IsZero ? BigRational.Zero : null;
        }

        BigRational halfTurn = angleUnit == AngleUnit.Degrees
            ? new BigRational(180)
            : new BigRational(200);
        BigRational result = phase / halfTurn;
        budget.CheckCoefficient(result);
        return result;
    }

    private static bool TryQuarterTurns(
        BigRational phaseInPi,
        out BigRational sine,
        out BigRational cosine)
    {
        BigRational turns = phaseInPi * new BigRational(2);
        if (!turns.IsInteger)
        {
            sine = default;
            cosine = default;
            return false;
        }

        int residue = (int)(turns.Numerator % 4);
        if (residue < 0)
        {
            residue += 4;
        }

        sine = residue switch
        {
            1 => BigRational.One,
            3 => BigRational.MinusOne,
            _ => BigRational.Zero
        };
        cosine = residue switch
        {
            0 => BigRational.One,
            2 => BigRational.MinusOne,
            _ => BigRational.Zero
        };
        return true;
    }

    private static ExactReal Solve(
        AffineReciprocalTrigPattern pattern,
        ExactReal angle,
        ResourceBudget budget)
    {
        budget.Charge();
        ExactReal shifted = ExactRealArithmetic.AddRational(angle, -pattern.Phase);
        return Scale(shifted, pattern.Frequency.Reciprocal(), budget);
    }

    private static ExactReal Scale(
        ExactReal value,
        BigRational factor,
        ResourceBudget budget)
    {
        budget.CheckCoefficient(factor);
        ExactReal result = ExactRealArithmetic.Scale(value, factor);
        budget.Charge();
        return result;
    }

    private static ExactReal Angle(AngleUnit unit, BigRational fraction) =>
        ExactAngleArithmetic.PiFraction(unit, fraction);

    private static bool IsSingleFeature(AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 &&
               (value & (value - 1)) == 0 &&
               (feature & AnalysisFeatures.All) == feature;
    }
}
