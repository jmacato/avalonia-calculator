using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Proves the affine reciprocal-trigonometric fragment after cotangent,
/// secant, and cosecant have been lowered to domain-guarded sine/cosine
/// ratios. Recognition is structural and also accepts the equivalent ratios
/// written by the user; no display string participates in the proof.
/// </summary>
internal static class AffineReciprocalTrigonometricAnalyzer
{
    public static bool TryCompute(SemanticExpression expression, string variable, AngleUnit angleUnit, AnalysisFeatures feature, ResourceBudget budget, out object value, out ImmutableArray<string> parameters)
    {
        budget.Charge();
        if (!TryGetPattern(expression.Value, variable, budget, out AffineReciprocalTrigPattern pattern))
        {
            value = null!;
            parameters = [];
            return false;
        }

        RealSet domain = Domain(pattern, angleUnit, budget);
        if (!TrigonometricAndLatticeAnalyzer.DomainMatches(expression, variable, angleUnit, domain, budget))
        {
            value = null!;
            parameters = [];
            return false;
        }

        if (feature == AnalysisFeatures.Zeros && !pattern.Shift.IsZero)
        {
            // The exact inverse-image theorem for shifted reciprocal
            // functions is deliberately left to the general MTP projection
            // backend. Never turn an unsupported inverse into an empty set.
            value = null!;
            parameters = [];
            return false;
        }

        OptionalValue<ExactReal> intercept = default;
        if (feature == AnalysisFeatures.YIntercept && !TryYIntercept(pattern, angleUnit, budget, out intercept))
        {
            value = null!;
            parameters = [];
            return false;
        }

        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => Range(pattern),
            AnalysisFeatures.Parity => Parity(pattern, angleUnit, budget),
            AnalysisFeatures.Zeros => Zeros(pattern, angleUnit, budget),
            AnalysisFeatures.YIntercept => intercept,
            AnalysisFeatures.Minima => Extrema(pattern, angleUnit, minimum: true, budget),
            AnalysisFeatures.Maxima => Extrema(pattern, angleUnit, minimum: false, budget),
            AnalysisFeatures.InflectionPoints => Inflections(pattern, angleUnit, budget),
            AnalysisFeatures.VerticalAsymptotes => VerticalAsymptotes(pattern, angleUnit, budget),
            AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => MonotonicityRegions(pattern, angleUnit, budget),
            AnalysisFeatures.Period => Period(pattern, angleUnit, budget),
            _ => null!
        };
        if (value is null)
        {
            parameters = [];
            return false;
        }

        parameters = [pattern.Canonical, angleUnit.ToString(), expression.DefinedWhen.Canonical, "guarded-sine-cosine-ratio"];
        return true;
    }

    internal static RealSet NonzeroDomain(AffineTrigPattern primitive, AngleUnit angleUnit, ResourceBudget budget)
    {
        budget.Charge();
        BigRational lowerFraction = primitive.Function switch
        {
            "sin" => BigRational.MinusOne,
            "cos" => new BigRational(-1, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(primitive))
        };
        BigRational upperFraction = primitive.Function == "sin" ? BigRational.Zero : new BigRational(1, 2);
        ExactReal period = ScaleAngle(Angle(angleUnit, BigRational.One), primitive.Frequency.Reciprocal(), budget);
        return new PeriodicIntervalSet(period, "m", IntegerConstraint.All("m"), [new PeriodicInterval(SolveAngle(primitive.Frequency, primitive.Phase, Angle(angleUnit, lowerFraction), budget), false, SolveAngle(primitive.Frequency, primitive.Phase, Angle(angleUnit, upperFraction), budget), false)]);
    }

    internal static bool TryGetPattern(ValueTerm term, string variable, ResourceBudget budget, out AffineReciprocalTrigPattern pattern)
    {
        budget.Charge();
        BigRational shift = BigRational.Zero;
        ValueTerm core = term;
        if (term.Kind is ValueKind.Add or ValueKind.Subtract)
        {
            if (TryRationalConstant(term.Operands[1], out BigRational right))
            {
                core = term.Operands[0];
                shift = term.Kind == ValueKind.Add ? right : -right;
            }
            else if (term.Kind == ValueKind.Add && TryRationalConstant(term.Operands[0], out BigRational left))
            {
                core = term.Operands[1];
                shift = left;
            }
        }

        if (!TryStripExactScalar(core, budget, out ValueTerm ratio, out ExactScalar amplitude) || !TryRatio(ratio, budget, out string function, out ValueTerm argument, out ExactScalar ratioScale))
        {
            pattern = null!;
            return false;
        }

        amplitude = amplitude.Multiply(ratioScale, budget);
        if (amplitude.IsZero || !RationalFunctionExtractor.TryExtract(argument, variable, budget, out RationalExtraction extraction) || extraction.Function.Denominator.Degree != 0 || extraction.Function.Numerator.Degree != 1)
        {
            pattern = null!;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        BigRational frequency = extraction.Function.Numerator[1] / denominator;
        BigRational phase = extraction.Function.Numerator[0] / denominator;
        budget.CheckCoefficient(frequency);
        budget.CheckCoefficient(phase);
        budget.CheckCoefficient(shift);
        if (frequency.IsZero)
        {
            pattern = null!;
            return false;
        }

        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phase = -phase;
            if (function is "cot" or "csc")
            {
                amplitude = amplitude.Negate();
            }
        }

        pattern = new AffineReciprocalTrigPattern(function, amplitude, frequency, phase, shift, ratio.Canonical);
        return true;
    }

    private static bool TryRatio(ValueTerm term, ResourceBudget budget, out string function, out ValueTerm argument, out ExactScalar scale)
    {
        if (term.Kind != ValueKind.Divide || term.Operands.Length != 2 || !TryStripExactScalar(term.Operands[1], budget, out ValueTerm denominator, out ExactScalar denominatorScale) || denominatorScale.IsZero || !TryPrimitive(denominator, out string denominatorFunction, out ValueTerm denominatorArgument))
        {
            function = string.Empty;
            argument = null!;
            scale = default;
            return false;
        }

        ExactScalar reciprocalDenominatorScale = denominatorScale.Reciprocal(budget);
        if (TryStripExactScalar(term.Operands[0], budget, out ValueTerm numerator, out ExactScalar numeratorScale) && TryPrimitive(numerator, out string numeratorFunction, out ValueTerm numeratorArgument) && string.Equals(numeratorArgument.Canonical, denominatorArgument.Canonical, StringComparison.Ordinal) && numeratorFunction == "cos" && denominatorFunction == "sin")
        {
            function = "cot";
            argument = denominatorArgument;
            scale = numeratorScale.Multiply(reciprocalDenominatorScale, budget);
            return !scale.IsZero;
        }

        if (!ExactScalar.TryCreate(term.Operands[0], budget, out ExactScalar scalarNumerator) || scalarNumerator.IsZero)
        {
            function = string.Empty;
            argument = null!;
            scale = default;
            return false;
        }

        function = denominatorFunction switch
        {
            "cos" => "sec",
            "sin" => "csc",
            _ => throw new InvalidOperationException("Only sine and cosine ratios are normalized.")
        };
        argument = denominatorArgument;
        scale = scalarNumerator.Multiply(reciprocalDenominatorScale, budget);
        return !scale.IsZero;
    }

    private static bool TryStripExactScalar(ValueTerm source, ResourceBudget budget, out ValueTerm core, out ExactScalar scale)
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

            if (core.Kind == ValueKind.Multiply && ExactScalar.TryCreate(core.Operands[0], budget, out ExactScalar left))
            {
                scale = scale.Multiply(left, budget);
                core = core.Operands[1];
                changed = true;
            }
            else if (core.Kind == ValueKind.Multiply && ExactScalar.TryCreate(core.Operands[1], budget, out ExactScalar right))
            {
                scale = scale.Multiply(right, budget);
                core = core.Operands[0];
                changed = true;
            }
            else if (core.Kind == ValueKind.Divide && ExactScalar.TryCreate(core.Operands[1], budget, out ExactScalar divisor) && !divisor.IsZero)
            {
                scale = scale.Multiply(divisor.Reciprocal(budget), budget);
                core = core.Operands[0];
                changed = true;
            }
        }
        while (changed);
        return true;
    }

    private static bool TryPrimitive(ValueTerm term, out string function, out ValueTerm argument)
    {
        if (term is { Kind: ValueKind.Function, Name: "sin" or "cos", Operands.Length: 1 })
        {
            function = term.Name;
            argument = term.Operands[0];
            return true;
        }

        function = string.Empty;
        argument = null!;
        return false;
    }

    internal static RealSet Domain(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        string denominator = pattern.Function == "sec" ? "cos" : "sin";
        var primitive = new AffineTrigPattern(denominator, ExactScalar.One, pattern.Frequency, pattern.Phase, BigRational.Zero);
        return NonzeroDomain(primitive, angleUnit, budget);
    }

    private static RealSet Range(AffineReciprocalTrigPattern pattern)
    {
        if (pattern.Function == "cot")
        {
            return AllRealSet.Instance;
        }

        ExactScalar magnitude = pattern.Amplitude.Abs();
        ExactReal negativeBoundary = ExactRealArithmetic.AddRational(magnitude.Negate().Value, pattern.Shift);
        ExactReal positiveBoundary = ExactRealArithmetic.AddRational(magnitude.Value, pattern.Shift);
        return RealSets.Union(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(negativeBoundary), true), new IntervalSet(RealBound.Finite(positiveBoundary), true, RealBound.PositiveInfinity, false));
    }

    private static FunctionParity Parity(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        BigRational? phaseInPi = PhaseInPi(pattern.Phase, angleUnit, budget);
        if (phaseInPi is null)
        {
            return FunctionParity.Neither;
        }

        bool coreOdd;
        bool coreEven;
        BigRational phase = phaseInPi.Value;
        switch (pattern.Function)
        {
            case "cot":
                coreOdd = (phase * 2).IsInteger;
                coreEven = false;
                break;
            case "sec":
                coreEven = phase.IsInteger;
                coreOdd = (phase - new BigRational(1, 2)).IsInteger;
                break;
            case "csc":
                coreOdd = phase.IsInteger;
                coreEven = (phase - new BigRational(1, 2)).IsInteger;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pattern));
        }

        if (coreEven)
        {
            return FunctionParity.Even;
        }

        return coreOdd && pattern.Shift.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
    }

    private static RealSet Zeros(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        if (pattern.Function is "sec" or "csc")
        {
            return EmptySet.Instance;
        }

        ExactReal offset = SolveAngle(pattern.Frequency, pattern.Phase, Angle(angleUnit, new BigRational(1, 2)), budget);
        ExactReal period = ScaleAngle(Angle(angleUnit, BigRational.One), pattern.Frequency.Reciprocal(), budget);
        return new PeriodicPointSet(offset, period, "m", IntegerConstraint.All("m"));
    }

    private static bool TryYIntercept(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget, out OptionalValue<ExactReal> intercept)
    {
        BigRational? phaseInPi = PhaseInPi(pattern.Phase, angleUnit, budget);
        bool denominatorIsSine = pattern.Function is "cot" or "csc";
        if (phaseInPi is { } piPhase && (denominatorIsSine ? piPhase.IsInteger : (piPhase - new BigRational(1, 2)).IsInteger))
        {
            intercept = OptionalValue<ExactReal>.None;
            return true;
        }

        ExactReal primitive;
        if (phaseInPi is { } exactPhase && TryQuarterTurnValues(exactPhase, out BigRational sine, out BigRational cosine))
        {
            BigRational reciprocalValue = pattern.Function switch
            {
                "cot" => cosine / sine,
                "sec" => cosine.Reciprocal(),
                "csc" => sine.Reciprocal(),
                _ => throw new ArgumentOutOfRangeException(nameof(pattern))
            };
            primitive = new RationalReal(reciprocalValue);
        }
        else
        {
            primitive = new FunctionReal(pattern.Function, [ExactAngleArithmetic.ToRadians(new RationalReal(pattern.Phase), angleUnit)]);
        }

        ExactReal scaled = ExactRealArithmetic.Multiply(pattern.Amplitude.Value, primitive);
        intercept = OptionalValue<ExactReal>.Some(ExactRealArithmetic.AddRational(scaled, pattern.Shift));
        return true;
    }

    private static ImmutableArray<FeaturePoint> Extrema(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, bool minimum, ResourceBudget budget)
    {
        if (pattern.Function == "cot")
        {
            return [];
        }

        bool choosePositiveCore = minimum == (pattern.Amplitude.Sign > 0);
        BigRational angleFraction = pattern.Function switch
        {
            "sec" when choosePositiveCore => BigRational.Zero,
            "sec" => BigRational.One,
            "csc" when choosePositiveCore => new BigRational(1, 2),
            "csc" => new BigRational(3, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
        ExactReal y = ExactRealArithmetic.AddRational(choosePositiveCore ? pattern.Amplitude.Value : pattern.Amplitude.Negate().Value, pattern.Shift);
        return [new ConstantYFeaturePoint(new PeriodicReal(SolveAngle(pattern.Frequency, pattern.Phase, Angle(angleUnit, angleFraction), budget), ScaleAngle(Angle(angleUnit, new BigRational(2)), pattern.Frequency.Reciprocal(), budget), "m", IntegerConstraint.All("m")), y)];
    }

    private static ImmutableArray<FeaturePoint> Inflections(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        if (pattern.Function != "cot")
        {
            return [];
        }

        return [new ConstantYFeaturePoint(new PeriodicReal(SolveAngle(pattern.Frequency, pattern.Phase, Angle(angleUnit, new BigRational(1, 2)), budget), ScaleAngle(Angle(angleUnit, BigRational.One), pattern.Frequency.Reciprocal(), budget), "m", IntegerConstraint.All("m")), new RationalReal(pattern.Shift))];
    }

    private static ImmutableArray<Asymptote> VerticalAsymptotes(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        BigRational poleFraction = pattern.Function == "sec" ? new BigRational(1, 2) : BigRational.Zero;
        return [new Asymptote(AsymptoteOrientation.Vertical, new PeriodicReal(SolveAngle(pattern.Frequency, pattern.Phase, Angle(angleUnit, poleFraction), budget), ScaleAngle(Angle(angleUnit, BigRational.One), pattern.Frequency.Reciprocal(), budget), "m", IntegerConstraint.All("m")), null, null)];
    }

    private static ImmutableArray<MonotoneRegion> MonotonicityRegions(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        if (pattern.Function == "cot")
        {
            return [Region(pattern, angleUnit, BigRational.Zero, BigRational.One, BigRational.One, pattern.Amplitude.Sign > 0 ? Graphing.Symbolics.Monotonicity.Decreasing : Graphing.Symbolics.Monotonicity.Increasing, budget)];
        }

        (BigRational Lower, BigRational Upper, bool Increasing)[] cells = pattern.Function == "sec" ? [(new BigRational(1, 2), BigRational.One, true), (new BigRational(3, 2), new BigRational(2), false), (BigRational.One, new BigRational(3, 2), false), (new BigRational(2), new BigRational(5, 2), true)] : [(BigRational.Zero, new BigRational(1, 2), false), (new BigRational(1, 2), BigRational.One, true), (BigRational.One, new BigRational(3, 2), true), (new BigRational(3, 2), new BigRational(2), false)];
        var regions = ImmutableArray.CreateBuilder<MonotoneRegion>(cells.Length);
        foreach ((BigRational lower, BigRational upper, bool coreIncreasing) in cells)
        {
            bool increasing = pattern.Amplitude.Sign > 0 ? coreIncreasing : !coreIncreasing;
            regions.Add(Region(pattern, angleUnit, lower, upper, new BigRational(2), increasing ? Graphing.Symbolics.Monotonicity.Increasing : Graphing.Symbolics.Monotonicity.Decreasing, budget));
        }

        return regions.MoveToImmutable();
    }

    private static MonotoneRegion Region(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, BigRational lowerFraction, BigRational upperFraction, BigRational periodFraction, Monotonicity direction, ResourceBudget budget) => new(new PeriodicIntervalSet(ScaleAngle(Angle(angleUnit, periodFraction), pattern.Frequency.Reciprocal(), budget), "m", IntegerConstraint.All("m"), [new PeriodicInterval(SolveAngle(pattern.Frequency, pattern.Phase, Angle(angleUnit, lowerFraction), budget), false, SolveAngle(pattern.Frequency, pattern.Phase, Angle(angleUnit, upperFraction), budget), false)]), direction);
    private static Periodicity Period(AffineReciprocalTrigPattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        BigRational fraction = pattern.Function == "cot" ? BigRational.One : new BigRational(2);
        return new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, ScaleAngle(Angle(angleUnit, fraction), pattern.Frequency.Reciprocal(), budget));
    }

    private static BigRational? PhaseInPi(BigRational phase, AngleUnit angleUnit, ResourceBudget budget)
    {
        budget.CheckCoefficient(phase);
        if (angleUnit == AngleUnit.Radians)
        {
            // A nonzero rational cannot be a nonzero rational multiple of pi.
            return phase.IsZero ? BigRational.Zero : null;
        }

        BigRational halfTurn = angleUnit switch
        {
            AngleUnit.Degrees => new BigRational(180),
            AngleUnit.Grads => new BigRational(200),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnit))
        };
        BigRational result = phase / halfTurn;
        budget.CheckCoefficient(result);
        return result;
    }

    private static bool TryQuarterTurnValues(BigRational phaseInPi, out BigRational sine, out BigRational cosine)
    {
        BigRational quarterIndex = phaseInPi * 2;
        if (!quarterIndex.IsInteger)
        {
            sine = default;
            cosine = default;
            return false;
        }

        ExactInteger residue = quarterIndex.Numerator % 4;
        if (residue.Sign < 0)
        {
            residue += 4;
        }

        (sine, cosine) = (int)residue switch
        {
            0 => (BigRational.Zero, BigRational.One),
            1 => (BigRational.One, BigRational.Zero),
            2 => (BigRational.Zero, BigRational.MinusOne),
            3 => (BigRational.MinusOne, BigRational.Zero),
            _ => throw new InvalidOperationException("Modulo four produced an invalid residue.")
        };
        return true;
    }

    private static ExactReal SolveAngle(BigRational frequency, BigRational phase, ExactReal angle, ResourceBudget budget)
    {
        BigRational inverseFrequency = frequency.Reciprocal();
        BigRational offset = -phase / frequency;
        budget.CheckCoefficient(inverseFrequency);
        budget.CheckCoefficient(offset);
        return ExactRealArithmetic.AddRational(ScaleAngle(angle, inverseFrequency, budget), offset);
    }

    private static ExactReal ScaleAngle(ExactReal value, BigRational scale, ResourceBudget budget)
    {
        budget.CheckCoefficient(scale);
        ExactReal result = ExactRealArithmetic.Scale(value, scale);
        if (result is AffinePiReal affine)
        {
            budget.CheckCoefficient(affine.PiCoefficient);
            budget.CheckCoefficient(affine.Constant);
        }

        return result;
    }

    private static ExactReal Angle(AngleUnit unit, BigRational piFraction) => ExactAngleArithmetic.PiFraction(unit, piFraction);
    private static bool TryRationalConstant(ValueTerm term, out BigRational value)
    {
        if (term.Kind == ValueKind.Constant)
        {
            value = term.Constant;
            return true;
        }

        value = default;
        return false;
    }
}
