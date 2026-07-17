using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class AbsoluteCompositionAnalyzer
{
    private const string CertificateRule = "exact-absolute-affine-composition";
    private const string Parameter = "m";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!TryCompute(request, expression, feature, budget, out object? value, out AbsoluteCompositionPattern? pattern) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new AbsoluteCompositionProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), pattern.Form, pattern.Canonical, CertificateRule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    private static bool TryCompute(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out object value, out AbsoluteCompositionPattern pattern)
    {
        budget.Charge(8);
        if (!TryExtractPattern(expression.Value, request.Variable, budget, out pattern) || !TrigonometricAndLatticeAnalyzer.DomainMatches(expression, request.Variable, request.AngleUnit, AllRealSet.Instance, budget))
        {
            value = null!;
            return false;
        }

        budget.CheckCoefficient(pattern.Slope);
        budget.CheckCoefficient(pattern.Intercept);
        value = pattern.Form switch
        {
            AbsoluteCompositionForm.AbsoluteOfAffineFunction => ComputeAbsoluteOuter(pattern, request.AngleUnit, feature),
            AbsoluteCompositionForm.FunctionOfAbsoluteAffine => ComputeAbsoluteInner(pattern, request.AngleUnit, feature),
            _ => null!
        };
        return value is not null;
    }

    private static bool TryExtractPattern(ValueTerm term, string variable, ResourceBudget budget, out AbsoluteCompositionPattern pattern)
    {
        budget.Charge();
        if (TryExtractAbsoluteOuter(term, variable, budget, out pattern) || TryExtractAbsoluteInner(term, variable, budget, out pattern))
        {
            if (pattern.Slope.Sign < 0)
            {
                pattern = pattern with
                {
                    Slope = -pattern.Slope,
                    Intercept = -pattern.Intercept
                };
            }

            return true;
        }

        pattern = null!;
        return false;
    }

    private static bool TryExtractAbsoluteOuter(ValueTerm term, string variable, ResourceBudget budget, out AbsoluteCompositionPattern pattern)
    {
        if (term is not { Kind: ValueKind.Function, Name: "abs", Operands.Length: 1 })
        {
            pattern = null!;
            return false;
        }

        ValueTerm operand = term.Operands[0];
        while (operand is { Kind: ValueKind.Function, Name: "abs", Operands.Length: 1 })
        {
            budget.Charge();
            operand = operand.Operands[0];
        }

        if (!TryStripScale(operand, budget, out ValueTerm core, out ExactScalar scale) || core is not { Kind: ValueKind.Function, Operands.Length: 1 } || core.Name is not ("sin" or "cos" or "sinh" or "cosh" or "tanh") || !TryAffineArgument(core.Operands[0], variable, budget, out BigRational slope, out BigRational intercept))
        {
            pattern = null!;
            return false;
        }

        pattern = new AbsoluteCompositionPattern(AbsoluteCompositionForm.AbsoluteOfAffineFunction, core.Name, scale.Abs(), slope, intercept);
        return true;
    }

    private static bool TryExtractAbsoluteInner(ValueTerm term, string variable, ResourceBudget budget, out AbsoluteCompositionPattern pattern)
    {
        if (term is not { Kind: ValueKind.Function, Operands.Length: 1 } || term.Name is not ("sin" or "cos" or "sinh" or "cosh" or "tanh"))
        {
            pattern = null!;
            return false;
        }

        ValueTerm absolute = term.Operands[0];
        while (absolute is { Kind: ValueKind.Function, Name: "abs", Operands.Length: 1 } nested && nested.Operands[0] is { Kind: ValueKind.Function, Name: "abs", Operands.Length: 1 })
        {
            budget.Charge();
            absolute = nested.Operands[0];
        }

        if (absolute is not { Kind: ValueKind.Function, Name: "abs", Operands.Length: 1 } || !TryAffineArgument(absolute.Operands[0], variable, budget, out BigRational slope, out BigRational intercept))
        {
            pattern = null!;
            return false;
        }

        pattern = new AbsoluteCompositionPattern(AbsoluteCompositionForm.FunctionOfAbsoluteAffine, term.Name, ExactScalar.One, slope, intercept);
        return true;
    }

    private static bool TryStripScale(ValueTerm term, ResourceBudget budget, out ValueTerm core, out ExactScalar scale)
    {
        core = term;
        scale = ExactScalar.One;
        bool changed;
        do
        {
            changed = false;
            while (core.Kind == ValueKind.Negate)
            {
                budget.Charge();
                scale = scale.Negate();
                core = core.Operands[0];
                changed = true;
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
            else if (core.Kind == ValueKind.Divide && ExactScalar.TryCreate(core.Operands[1], budget, out ExactScalar denominator) && !denominator.IsZero)
            {
                scale = scale.Multiply(denominator.Reciprocal(budget), budget);
                core = core.Operands[0];
                changed = true;
            }
        }
        while (changed);
        return !scale.IsZero;
    }

    private static bool TryAffineArgument(ValueTerm term, string variable, ResourceBudget budget, out BigRational slope, out BigRational intercept)
    {
        if (!RationalFunctionExtractor.TryExtract(term, variable, budget, out RationalExtraction extraction) || extraction.Function.Denominator.Degree != 0 || extraction.Function.Numerator.Degree != 1)
        {
            slope = default;
            intercept = default;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        slope = extraction.Function.Numerator[1] / denominator;
        intercept = extraction.Function.Numerator[0] / denominator;
        return !slope.IsZero;
    }

    private static object ComputeAbsoluteOuter(AbsoluteCompositionPattern pattern, AngleUnit angleUnit, AnalysisFeatures feature) => feature switch
    {
        AnalysisFeatures.Domain => AllRealSet.Instance,
        AnalysisFeatures.Range => AbsoluteOuterRange(pattern),
        AnalysisFeatures.Parity => AbsoluteOuterParity(pattern, angleUnit),
        AnalysisFeatures.Zeros => AbsoluteOuterZeros(pattern, angleUnit),
        AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(AbsoluteOuterAtOrigin(pattern, angleUnit)),
        AnalysisFeatures.Minima => AbsoluteOuterExtrema(pattern, angleUnit, minimum: true),
        AnalysisFeatures.Maxima => AbsoluteOuterExtrema(pattern, angleUnit, minimum: false),
        AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
        AnalysisFeatures.VerticalAsymptotes => ImmutableArray<Asymptote>.Empty,
        AnalysisFeatures.HorizontalAsymptotes => AbsoluteHorizontalAsymptotes(pattern),
        AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
        AnalysisFeatures.Monotonicity => AbsoluteOuterMonotonicity(pattern, angleUnit),
        AnalysisFeatures.Period => AbsoluteOuterPeriod(pattern, angleUnit),
        _ => null!
    };
    private static object ComputeAbsoluteInner(AbsoluteCompositionPattern pattern, AngleUnit angleUnit, AnalysisFeatures feature)
    {
        if (pattern.Function == "sin")
        {
            return ComputeSineAbsoluteInner(pattern, angleUnit, feature);
        }

        return feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range => AbsoluteInnerRange(pattern),
            AnalysisFeatures.Parity => AbsoluteInnerParity(pattern, angleUnit),
            AnalysisFeatures.Zeros => AbsoluteInnerZeros(pattern, angleUnit),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(AbsoluteInnerAtOrigin(pattern, angleUnit)),
            AnalysisFeatures.Minima => AbsoluteInnerExtrema(pattern, angleUnit, minimum: true),
            AnalysisFeatures.Maxima => AbsoluteInnerExtrema(pattern, angleUnit, minimum: false),
            AnalysisFeatures.InflectionPoints => AbsoluteInnerInflections(pattern, angleUnit),
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => AbsoluteHorizontalAsymptotes(pattern),
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => AbsoluteInnerMonotonicity(pattern, angleUnit),
            AnalysisFeatures.Period => AbsoluteInnerPeriod(pattern, angleUnit),
            _ => null!
        };
    }

    private static object ComputeSineAbsoluteInner(AbsoluteCompositionPattern pattern, AngleUnit angleUnit, AnalysisFeatures feature) => feature switch
    {
        AnalysisFeatures.Domain => AllRealSet.Instance,
        AnalysisFeatures.Range => ClosedInterval(BigRational.MinusOne, BigRational.One),
        AnalysisFeatures.Parity => AbsoluteInnerParity(pattern, angleUnit),
        AnalysisFeatures.Zeros => TrigPointSet(pattern, angleUnit, 0, 1),
        AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(AbsoluteInnerAtOrigin(pattern, angleUnit)),
        AnalysisFeatures.Minima => SineAbsoluteMinima(pattern, angleUnit),
        AnalysisFeatures.Maxima => SineAbsoluteMaxima(pattern, angleUnit),
        AnalysisFeatures.InflectionPoints => SineAbsoluteInflections(pattern, angleUnit),
        AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
        AnalysisFeatures.Monotonicity => SineAbsoluteMonotonicity(pattern, angleUnit),
        AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
        _ => null!
    };
    private static Graphing.Symbolics.IntervalSet AbsoluteOuterRange(AbsoluteCompositionPattern pattern)
    {
        ExactReal zero = RationalReal(BigRational.Zero);
        ExactReal magnitude = pattern.Scale.Value;
        return pattern.Function switch
        {
            "sin" or "cos" => new IntervalSet(RealBound.Finite(zero), true, RealBound.Finite(magnitude), true),
            "tanh" => new IntervalSet(RealBound.Finite(zero), true, RealBound.Finite(magnitude), false),
            "cosh" => new IntervalSet(RealBound.Finite(magnitude), true, RealBound.PositiveInfinity, false),
            _ => new IntervalSet(RealBound.Finite(zero), true, RealBound.PositiveInfinity, false)
        };
    }

    private static Graphing.Symbolics.IntervalSet AbsoluteInnerRange(AbsoluteCompositionPattern pattern) => pattern.Function switch
    {
        "cos" => ClosedInterval(BigRational.MinusOne, BigRational.One),
        "tanh" => new IntervalSet(RealBound.Finite(RationalReal(BigRational.Zero)), true, RealBound.Finite(RationalReal(BigRational.One)), false),
        "cosh" => new IntervalSet(RealBound.Finite(RationalReal(BigRational.One)), true, RealBound.PositiveInfinity, false),
        _ => new IntervalSet(RealBound.Finite(RationalReal(BigRational.Zero)), true, RealBound.PositiveInfinity, false)
    };
    private static Graphing.Symbolics.IntervalSet ClosedInterval(BigRational lower, BigRational upper) => new IntervalSet(RealBound.Finite(RationalReal(lower)), true, RealBound.Finite(RationalReal(upper)), true);
    private static FunctionParity AbsoluteOuterParity(AbsoluteCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Function is "sin" or "cos" && TryQuarterTurnMultiple(pattern.Intercept, angleUnit, out _))
        {
            // |sin(u)| and |cos(u)| are even about every quarter-turn phase.
            return FunctionParity.Even;
        }

        return CenteredParity(pattern);
    }

    private static FunctionParity AbsoluteInnerParity(AbsoluteCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Function == "cos" && TryQuarterTurnMultiple(pattern.Intercept, angleUnit, out bool oddMultiple))
        {
            // cos(|u|) = cos(u). Integer half-turns produce an even function;
            // odd quarter-turns produce an odd one.
            return oddMultiple ? FunctionParity.Odd : FunctionParity.Even;
        }

        return CenteredParity(pattern);
    }

    private static FunctionParity CenteredParity(AbsoluteCompositionPattern pattern) => pattern.Intercept.IsZero ? FunctionParity.Even : FunctionParity.Neither;
    private static bool TryQuarterTurnMultiple(BigRational phase, AngleUnit angleUnit, out bool oddMultiple)
    {
        if (angleUnit == AngleUnit.Radians)
        {
            // The extracted phase is rational. By irrationality of pi, the only
            // rational integer multiple of pi/2 is zero.
            oddMultiple = false;
            return phase.IsZero;
        }

        BigRational quarterTurn = angleUnit switch
        {
            AngleUnit.Degrees => new BigRational(90),
            AngleUnit.Grads => new BigRational(100),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnit))
        };
        BigRational multiple = phase / quarterTurn;
        if (!multiple.IsInteger)
        {
            oddMultiple = false;
            return false;
        }

        oddMultiple = !multiple.Numerator.IsEven;
        return true;
    }

    private static RealSet AbsoluteOuterZeros(AbsoluteCompositionPattern pattern, AngleUnit angleUnit) => pattern.Function switch
    {
        "sin" => TrigPointSet(pattern, angleUnit, 0, 1),
        "cos" => TrigPointSet(pattern, angleUnit, new BigRational(1, 2), 1),
        "sinh" or "tanh" => RealSets.Points([Center(pattern)]),
        _ => EmptySet.Instance
    };
    private static RealSet AbsoluteInnerZeros(AbsoluteCompositionPattern pattern, AngleUnit angleUnit) => pattern.Function switch
    {
        "cos" => TrigPointSet(pattern, angleUnit, new BigRational(1, 2), 1),
        "sinh" or "tanh" => RealSets.Points([Center(pattern)]),
        _ => EmptySet.Instance
    };
    private static ExactReal AbsoluteOuterAtOrigin(AbsoluteCompositionPattern pattern, AngleUnit angleUnit)
    {
        ExactReal primitive = PrimitiveAtRational(pattern.Function, pattern.Intercept, angleUnit);
        ExactReal absolute = pattern.Function == "cosh" ? primitive : Absolute(primitive);
        return ExactRealArithmetic.Multiply(pattern.Scale.Value, absolute);
    }

    private static ExactReal AbsoluteInnerAtOrigin(AbsoluteCompositionPattern pattern, AngleUnit angleUnit) => PrimitiveAtRational(pattern.Function, pattern.Intercept.Abs(), angleUnit);
    private static ExactReal PrimitiveAtRational(string function, BigRational argument, AngleUnit angleUnit)
    {
        if (argument.IsZero)
        {
            return function is "cos" or "cosh" ? RationalReal(BigRational.One) : RationalReal(BigRational.Zero);
        }

        if (function == "cos" && angleUnit == AngleUnit.Radians && argument.Sign < 0)
        {
            argument = argument.Abs();
        }

        ExactReal exactArgument = RationalReal(argument);
        if (function is "sin" or "cos")
        {
            exactArgument = ExactAngleArithmetic.ToRadians(exactArgument, angleUnit);
        }

        return new FunctionReal(function, [exactArgument]);
    }

    private static ExactReal Absolute(ExactReal value) => value switch
    {
        RationalReal rational => RationalReal(rational.Value.Abs()),
        FunctionReal { Function: "abs", Arguments.Length: 1 } function => function,
        _ => new FunctionReal("abs", [value])
    };
    private static ImmutableArray<FeaturePoint> AbsoluteOuterExtrema(AbsoluteCompositionPattern pattern, AngleUnit angleUnit, bool minimum)
    {
        if (pattern.Function is "sin" or "cos")
        {
            BigRational fraction = (pattern.Function, minimum) switch
            {
                ("sin", true) => BigRational.Zero,
                ("sin", false) => new BigRational(1, 2),
                ("cos", true) => new BigRational(1, 2),
                _ => BigRational.Zero
            };
            ExactReal y = minimum ? RationalReal(BigRational.Zero) : pattern.Scale.Value;
            return [PeriodicFeature(pattern, angleUnit, fraction, 1, IntegerConstraint.All(Parameter), y)];
        }

        if (minimum)
        {
            ExactReal y = pattern.Function == "cosh" ? pattern.Scale.Value : RationalReal(BigRational.Zero);
            return [SingletonFeature(Center(pattern), y)];
        }

        return [];
    }

    private static ImmutableArray<FeaturePoint> AbsoluteInnerExtrema(AbsoluteCompositionPattern pattern, AngleUnit angleUnit, bool minimum)
    {
        if (pattern.Function == "cos")
        {
            BigRational fraction = minimum ? BigRational.One : BigRational.Zero;
            ExactReal y = RationalReal(minimum ? BigRational.MinusOne : BigRational.One);
            return [PeriodicFeature(pattern, angleUnit, fraction, 2, IntegerConstraint.All(Parameter), y)];
        }

        if (minimum)
        {
            ExactReal y = pattern.Function == "cosh" ? RationalReal(BigRational.One) : RationalReal(BigRational.Zero);
            return [SingletonFeature(Center(pattern), y)];
        }

        return [];
    }

    private static ImmutableArray<FeaturePoint> AbsoluteInnerInflections(AbsoluteCompositionPattern pattern, AngleUnit angleUnit) => pattern.Function == "cos" ? [PeriodicFeature(pattern, angleUnit, new BigRational(1, 2), 1, IntegerConstraint.All(Parameter), RationalReal(BigRational.Zero))] : [];
    private static ImmutableArray<Asymptote> AbsoluteHorizontalAsymptotes(AbsoluteCompositionPattern pattern)
    {
        if (pattern.Function != "tanh")
        {
            return [];
        }

        ExactReal value = pattern.Form == AbsoluteCompositionForm.AbsoluteOfAffineFunction ? pattern.Scale.Value : RationalReal(BigRational.One);
        return [new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(value), null, value)];
    }

    private static ImmutableArray<MonotoneRegion> AbsoluteOuterMonotonicity(AbsoluteCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Function is not ("sin" or "cos"))
        {
            return CenteredMonotonicity(pattern);
        }

        ExactReal period = TrigStep(pattern, angleUnit, 1);
        RealSet first = PeriodicInterval(pattern, angleUnit, 0, new BigRational(1, 2), period, IntegerConstraint.All(Parameter));
        RealSet second = PeriodicInterval(pattern, angleUnit, new BigRational(1, 2), 1, period, IntegerConstraint.All(Parameter));
        bool sine = pattern.Function == "sin";
        return [new MonotoneRegion(first, sine ? Monotonicity.Increasing : Monotonicity.Decreasing), new MonotoneRegion(second, sine ? Monotonicity.Decreasing : Monotonicity.Increasing)];
    }

    private static ImmutableArray<MonotoneRegion> AbsoluteInnerMonotonicity(AbsoluteCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Function != "cos")
        {
            return CenteredMonotonicity(pattern);
        }

        ExactReal period = TrigStep(pattern, angleUnit, 2);
        return [new MonotoneRegion(PeriodicInterval(pattern, angleUnit, 0, 1, period, IntegerConstraint.All(Parameter)), Monotonicity.Decreasing), new MonotoneRegion(PeriodicInterval(pattern, angleUnit, 1, 2, period, IntegerConstraint.All(Parameter)), Monotonicity.Increasing)];
    }

    private static ImmutableArray<MonotoneRegion> CenteredMonotonicity(AbsoluteCompositionPattern pattern)
    {
        ExactReal center = Center(pattern);
        return [new MonotoneRegion(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(center), false), Monotonicity.Decreasing), new MonotoneRegion(new IntervalSet(RealBound.Finite(center), false, RealBound.PositiveInfinity, false), Monotonicity.Increasing)];
    }

    private static Periodicity AbsoluteOuterPeriod(AbsoluteCompositionPattern pattern, AngleUnit angleUnit) => pattern.Function is "sin" or "cos" ? new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, TrigStep(pattern, angleUnit, 1)) : new Periodicity(PeriodicityKind.NotPeriodic, null);
    private static Periodicity AbsoluteInnerPeriod(AbsoluteCompositionPattern pattern, AngleUnit angleUnit) => pattern.Function == "cos" ? new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, TrigStep(pattern, angleUnit, 2)) : new Periodicity(PeriodicityKind.NotPeriodic, null);
    private static ImmutableArray<FeaturePoint> SineAbsoluteMinima(AbsoluteCompositionPattern pattern, AngleUnit angleUnit)
    {
        ExactReal zero = RationalReal(BigRational.Zero);
        ExactReal minusOne = RationalReal(BigRational.MinusOne);
        return [SingletonFeature(Center(pattern), zero), PeriodicFeature(pattern, angleUnit, new BigRational(3, 2), 2, Constraint(Comparison.GreaterOrEqual), minusOne), PeriodicFeature(pattern, angleUnit, new BigRational(-3, 2), 2, Constraint(Comparison.LessOrEqual), minusOne)];
    }

    private static ImmutableArray<FeaturePoint> SineAbsoluteMaxima(AbsoluteCompositionPattern pattern, AngleUnit angleUnit)
    {
        ExactReal one = RationalReal(BigRational.One);
        return [PeriodicFeature(pattern, angleUnit, new BigRational(1, 2), 2, Constraint(Comparison.GreaterOrEqual), one), PeriodicFeature(pattern, angleUnit, new BigRational(-1, 2), 2, Constraint(Comparison.LessOrEqual), one)];
    }

    private static ImmutableArray<FeaturePoint> SineAbsoluteInflections(AbsoluteCompositionPattern pattern, AngleUnit angleUnit)
    {
        ExactReal zero = RationalReal(BigRational.Zero);
        return [PeriodicFeature(pattern, angleUnit, 1, 1, Constraint(Comparison.GreaterOrEqual), zero), PeriodicFeature(pattern, angleUnit, -1, 1, Constraint(Comparison.LessOrEqual), zero)];
    }

    private static ImmutableArray<MonotoneRegion> SineAbsoluteMonotonicity(AbsoluteCompositionPattern pattern, AngleUnit angleUnit)
    {
        ExactReal center = Center(pattern);
        ExactReal negativeHalf = SolveAngle(pattern, Angle(angleUnit, new BigRational(-1, 2)));
        ExactReal positiveHalf = SolveAngle(pattern, Angle(angleUnit, new BigRational(1, 2)));
        ExactReal period = TrigStep(pattern, angleUnit, 2);
        return [new MonotoneRegion(new IntervalSet(RealBound.Finite(negativeHalf), false, RealBound.Finite(center), false), Monotonicity.Decreasing), new MonotoneRegion(new IntervalSet(RealBound.Finite(center), false, RealBound.Finite(positiveHalf), false), Monotonicity.Increasing), new MonotoneRegion(PeriodicInterval(pattern, angleUnit, new BigRational(1, 2), new BigRational(3, 2), period, Constraint(Comparison.GreaterOrEqual)), Monotonicity.Decreasing), new MonotoneRegion(PeriodicInterval(pattern, angleUnit, new BigRational(3, 2), new BigRational(5, 2), period, Constraint(Comparison.GreaterOrEqual)), Monotonicity.Increasing), new MonotoneRegion(PeriodicInterval(pattern, angleUnit, new BigRational(-3, 2), new BigRational(-1, 2), period, Constraint(Comparison.LessOrEqual)), Monotonicity.Increasing), new MonotoneRegion(PeriodicInterval(pattern, angleUnit, new BigRational(-5, 2), new BigRational(-3, 2), period, Constraint(Comparison.LessOrEqual)), Monotonicity.Decreasing)];
    }

    private static IntegerConstraint Constraint(Comparison comparison) => new(Parameter, comparison, 0);
    private static ConstantYFeaturePoint SingletonFeature(ExactReal x, ExactReal y) => new(new SingletonReal(x), y);
    private static ConstantYFeaturePoint PeriodicFeature(AbsoluteCompositionPattern pattern, AngleUnit angleUnit, BigRational fraction, BigRational periodFraction, IntegerConstraint constraint, ExactReal y) => new(new PeriodicReal(SolveAngle(pattern, Angle(angleUnit, fraction)), TrigStep(pattern, angleUnit, periodFraction), Parameter, constraint), y);
    private static Graphing.Symbolics.PeriodicPointSet TrigPointSet(AbsoluteCompositionPattern pattern, AngleUnit angleUnit, BigRational fraction, BigRational periodFraction) => new PeriodicPointSet(SolveAngle(pattern, Angle(angleUnit, fraction)), TrigStep(pattern, angleUnit, periodFraction), Parameter, IntegerConstraint.All(Parameter));
    private static Graphing.Symbolics.PeriodicIntervalSet PeriodicInterval(AbsoluteCompositionPattern pattern, AngleUnit angleUnit, BigRational lowerFraction, BigRational upperFraction, ExactReal period, IntegerConstraint constraint) => new PeriodicIntervalSet(period, Parameter, constraint, [new PeriodicInterval(SolveAngle(pattern, Angle(angleUnit, lowerFraction)), false, SolveAngle(pattern, Angle(angleUnit, upperFraction)), false)]);
    private static Graphing.Symbolics.RationalReal Center(AbsoluteCompositionPattern pattern) => RationalReal(-pattern.Intercept / pattern.Slope);
    private static ExactReal SolveAngle(AbsoluteCompositionPattern pattern, ExactReal angle) => ExactRealArithmetic.Scale(ExactRealArithmetic.Subtract(angle, RationalReal(pattern.Intercept)), pattern.Slope.Reciprocal());
    private static ExactReal TrigStep(AbsoluteCompositionPattern pattern, AngleUnit angleUnit, BigRational fraction) => ExactRealArithmetic.Scale(Angle(angleUnit, fraction), pattern.Slope.Reciprocal());
    private static ExactReal Angle(AngleUnit unit, BigRational piFraction) => unit switch
    {
        AngleUnit.Radians => new AffinePiReal(piFraction, BigRational.Zero),
        AngleUnit.Degrees => RationalReal(new BigRational(180) * piFraction),
        AngleUnit.Grads => RationalReal(new BigRational(200) * piFraction),
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };
    private static RationalReal RationalReal(BigRational value) => new(value);
}
