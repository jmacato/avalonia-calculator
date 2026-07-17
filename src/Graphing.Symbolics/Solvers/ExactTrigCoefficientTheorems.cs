using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ExactTrigCoefficientTheorems
{
    public static bool TryCompute(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        value = null!;
        return feature switch
        {
            AnalysisFeatures.Domain => Assign(Domain(pattern, angleUnit), out value),
            AnalysisFeatures.Range => TryRange(pattern, budget, out value),
            AnalysisFeatures.Parity => TryParity(pattern, angleUnit, out value),
            AnalysisFeatures.Zeros => TryZeros(pattern, angleUnit, budget, out value),
            AnalysisFeatures.YIntercept => TryYIntercept(pattern, angleUnit, budget, out value),
            AnalysisFeatures.Minima => TryExtrema(pattern, angleUnit, true, budget, out value),
            AnalysisFeatures.Maxima => TryExtrema(pattern, angleUnit, false, budget, out value),
            AnalysisFeatures.InflectionPoints =>
                TryInflections(pattern, angleUnit, budget, out value),
            AnalysisFeatures.VerticalAsymptotes =>
                TryVerticalAsymptotes(pattern, angleUnit, budget, out value),
            AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes =>
                Assign(ImmutableArray<Asymptote>.Empty, out value),
            AnalysisFeatures.Monotonicity =>
                TryMonotonicity(pattern, angleUnit, budget, out value),
            AnalysisFeatures.Period => TryPeriod(pattern, angleUnit, budget, out value),
            _ => false
        };
    }

    private static RealSet Domain(ExactTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent)
        {
            return AllRealSet.Instance;
        }

        ExactReal lower = SolveAngle(pattern, Angle(angleUnit, new BigRational(-1, 2)));
        ExactReal upper = SolveAngle(pattern, Angle(angleUnit, new BigRational(1, 2)));
        ExactReal period = DivideAngle(Angle(angleUnit, BigRational.One), pattern.Frequency);
        return new PeriodicIntervalSet(
            period,
            "m",
            IntegerConstraint.All("m"),
            [new PeriodicInterval(lower, false, upper, false)]);
    }

    private static bool TryRange(
        ExactTrigPattern pattern,
        ResourceBudget budget,
        out object value)
    {
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent)
        {
            return Assign(AllRealSet.Instance, out value);
        }

        ExactScalar magnitude = pattern.Amplitude.Abs();
        if (!ExactCoefficientMath.TrySubtract(
                pattern.Shift,
                magnitude,
                budget,
                out ExactScalar lower) ||
            !ExactCoefficientMath.TryAdd(
                pattern.Shift,
                magnitude,
                budget,
                out ExactScalar upper))
        {
            value = null!;
            return false;
        }

        return Assign(new IntervalSet(
            RealBound.Finite(lower.Value),
            true,
            RealBound.Finite(upper.Value),
            true), out value);
    }

    private static bool TryParity(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        out object value)
    {
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent &&
            angleUnit == AngleUnit.Radians &&
            pattern.Phase.Value is RationalReal { Value.IsZero: false })
        {
            // The pole lattice of tan(a*x+b) is
            // (pi/2-b)/a + (pi/a)Z.  It is invariant under x -> -x only if
            // 2b/pi is an integer.  For nonzero rational b that quotient is
            // irrational because pi is irrational, so the domain is not
            // symmetric and the function is neither even nor odd.
            return Assign(FunctionParity.Neither, out value);
        }

        if (!TryPhaseFraction(pattern.Phase, angleUnit, out BigRational phase))
        {
            value = null!;
            return false;
        }

        bool integer = phase.IsInteger;
        bool halfInteger = (phase - new BigRational(1, 2)).IsInteger;
        FunctionParity parity = pattern.Kind switch
        {
            ExactCoefficientPatternKind.AffineSine when halfInteger => FunctionParity.Even,
            ExactCoefficientPatternKind.AffineSine when integer && pattern.Shift.IsZero =>
                FunctionParity.Odd,
            ExactCoefficientPatternKind.AffineCosine when integer => FunctionParity.Even,
            ExactCoefficientPatternKind.AffineCosine when halfInteger && pattern.Shift.IsZero =>
                FunctionParity.Odd,
            ExactCoefficientPatternKind.AffineTangent when
                (integer || halfInteger) && pattern.Shift.IsZero => FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
        return Assign(parity, out value);
    }

    private static bool TryZeros(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        if (pattern.Shift.IsZero)
        {
            BigRational offsetFraction = pattern.Kind == ExactCoefficientPatternKind.AffineCosine
                ? new BigRational(1, 2)
                : BigRational.Zero;
            ExactReal offset = SolveAngle(pattern, Angle(angleUnit, offsetFraction));
            ExactReal step = DivideAngle(Angle(angleUnit, BigRational.One), pattern.Frequency);
            return Assign(new PeriodicPointSet(
                offset,
                step,
                "m",
                IntegerConstraint.All("m")), out value);
        }

        ExactScalar target = pattern.Shift
            .Negate()
            .Multiply(pattern.Amplitude.Reciprocal(budget), budget);
        int comparison = 0;
        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent &&
            (!target.TryCompareAbsoluteTo(BigRational.One, budget, out comparison)))
        {
            value = null!;
            return false;
        }

        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent && comparison > 0)
        {
            return Assign(EmptySet.Instance, out value);
        }

        return TryInverseZeros(pattern, target, angleUnit, budget, out value);
    }

    private static bool TryInverseZeros(
        ExactTrigPattern pattern,
        ExactScalar target,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        string inverse = pattern.Kind switch
        {
            ExactCoefficientPatternKind.AffineSine => "asin",
            ExactCoefficientPatternKind.AffineCosine => "acos",
            ExactCoefficientPatternKind.AffineTangent => "atan",
            _ => throw new InvalidOperationException()
        };
        ExactReal principal = PrincipalInverseAngle(
            inverse,
            target,
            angleUnit,
            budget);
        ExactReal offset = SolveAngle(pattern, principal);
        BigRational stepFraction = pattern.Kind == ExactCoefficientPatternKind.AffineTangent
            ? BigRational.One
            : new BigRational(2);
        ExactReal step = DivideAngle(Angle(angleUnit, stepFraction), pattern.Frequency);
        RealSet first = new PeriodicPointSet(
            offset,
            step,
            "m",
            IntegerConstraint.All("m"));
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent ||
            target.TryCompareAbsoluteTo(BigRational.One, budget, out int comparison) && comparison == 0)
        {
            return Assign(first, out value);
        }

        ExactReal reflectedAngle = pattern.Kind == ExactCoefficientPatternKind.AffineSine
            ? ExactRealArithmetic.Subtract(Angle(angleUnit, BigRational.One), principal)
            : ExactRealArithmetic.Negate(principal);
        RealSet second = new PeriodicPointSet(
            SolveAngle(pattern, reflectedAngle),
            step,
            "m",
            IntegerConstraint.All("m"));
        return Assign(RealSets.Union(first, second), out value);
    }

    private static bool TryYIntercept(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        if (!TryPrimitiveAtPhase(
                pattern,
                angleUnit,
                out bool defined,
                out ExactReal primitive))
        {
            value = null!;
            return false;
        }

        if (!defined)
        {
            return Assign(OptionalValue<ExactReal>.None, out value);
        }

        ExactScalar amplitude = pattern.Amplitude;
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent &&
            pattern.Phase.Value is RationalReal { Value.Sign: < 0 } negativePhase &&
            primitive is FunctionReal { Function: "tan" })
        {
            // Normalize tan(-b)=-tan(b) before applying the amplitude.  This
            // keeps the exact value canonical for every amplitude sign rather
            // than producing nested negate/scale FunctionReal nodes.
            amplitude = amplitude.Negate();
            primitive = new FunctionReal(
                pattern.Function,
                [ExactAngleArithmetic.ToRadians(
                    new RationalReal(negativePhase.Value.Abs()),
                    angleUnit)]);
        }

        ExactReal scaled = ExactRealArithmetic.Multiply(amplitude.Value, primitive);
        ExactReal result = pattern.Shift.IsZero
            ? scaled
            : ExactRealArithmetic.Add(scaled, pattern.Shift.Value);
        return Assign(OptionalValue<ExactReal>.Some(result), out value);
    }

    private static bool TryExtrema(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        bool minimum,
        ResourceBudget budget,
        out object value)
    {
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent)
        {
            return Assign(ImmutableArray<FeaturePoint>.Empty, out value);
        }

        bool positiveAmplitude = pattern.Amplitude.Sign > 0;
        BigRational angleFraction;
        if (pattern.Kind == ExactCoefficientPatternKind.AffineSine)
        {
            bool positivePeak = minimum != positiveAmplitude;
            angleFraction = positivePeak ? new BigRational(1, 2) : new BigRational(3, 2);
        }
        else
        {
            bool positivePeak = minimum != positiveAmplitude;
            angleFraction = positivePeak ? BigRational.Zero : BigRational.One;
        }

        ExactScalar signedMagnitude = minimum
            ? pattern.Amplitude.Abs().Negate()
            : pattern.Amplitude.Abs();
        if (!ExactCoefficientMath.TryAdd(
                pattern.Shift,
                signedMagnitude,
                budget,
                out ExactScalar y))
        {
            value = null!;
            return false;
        }

        ExactReal period = DivideAngle(Angle(angleUnit, new BigRational(2)), pattern.Frequency);
        ImmutableArray<FeaturePoint> points =
        [
            new ConstantYFeaturePoint(
                new PeriodicReal(
                    SolveAngle(pattern, Angle(angleUnit, angleFraction)),
                    period,
                    "m",
                    IntegerConstraint.All("m")),
                y.Value)
        ];
        return Assign(points, out value);
    }

    private static bool TryInflections(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        BigRational fraction = pattern.Kind == ExactCoefficientPatternKind.AffineCosine
            ? new BigRational(1, 2)
            : BigRational.Zero;
        ExactReal period = DivideAngle(Angle(angleUnit, BigRational.One), pattern.Frequency);
        ImmutableArray<FeaturePoint> points =
        [
            new ConstantYFeaturePoint(
                new PeriodicReal(
                    SolveAngle(pattern, Angle(angleUnit, fraction)),
                    period,
                    "m",
                    IntegerConstraint.All("m")),
                pattern.Shift.Value)
        ];
        return Assign(points, out value);
    }

    private static bool TryVerticalAsymptotes(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent)
        {
            return Assign(ImmutableArray<Asymptote>.Empty, out value);
        }

        ExactReal period = DivideAngle(Angle(angleUnit, BigRational.One), pattern.Frequency);
        ImmutableArray<Asymptote> asymptotes =
        [
            new Asymptote(
                AsymptoteOrientation.Vertical,
                new PeriodicReal(
                    SolveAngle(pattern, Angle(angleUnit, new BigRational(1, 2))),
                    period,
                    "m",
                    IntegerConstraint.All("m")),
                null,
                null)
        ];
        return Assign(asymptotes, out value);
    }

    private static bool TryMonotonicity(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent)
        {
            ExactReal period = DivideAngle(Angle(angleUnit, BigRational.One), pattern.Frequency);
            RealSet intervals = PeriodicInterval(
                pattern,
                angleUnit,
                period,
                new BigRational(1, 2),
                new BigRational(3, 2));
            return Assign(ImmutableArray.Create(new MonotoneRegion(
                intervals,
                pattern.Amplitude.Sign > 0
                    ? Monotonicity.Increasing
                    : Monotonicity.Decreasing)), out value);
        }

        ExactReal fullPeriod = DivideAngle(
            Angle(angleUnit, new BigRational(2)),
            pattern.Frequency);
        (BigRational IncreasingStart, BigRational IncreasingEnd,
            BigRational DecreasingStart, BigRational DecreasingEnd) fractions =
            pattern.Kind == ExactCoefficientPatternKind.AffineSine
                ? (new BigRational(3, 2), new BigRational(5, 2),
                    new BigRational(1, 2), new BigRational(3, 2))
                : (BigRational.One, new BigRational(2),
                    BigRational.Zero, BigRational.One);
        bool positive = pattern.Amplitude.Sign > 0;
        ImmutableArray<MonotoneRegion> regions =
        [
            new MonotoneRegion(
                PeriodicInterval(
                    pattern,
                    angleUnit,
                    fullPeriod,
                    fractions.DecreasingStart,
                    fractions.DecreasingEnd),
                positive ? Monotonicity.Decreasing : Monotonicity.Increasing),
            new MonotoneRegion(
                PeriodicInterval(
                    pattern,
                    angleUnit,
                    fullPeriod,
                    fractions.IncreasingStart,
                    fractions.IncreasingEnd),
                positive ? Monotonicity.Increasing : Monotonicity.Decreasing)
        ];
        return Assign(regions, out value);
    }

    private static bool TryPeriod(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        BigRational fraction = pattern.Kind == ExactCoefficientPatternKind.AffineTangent
            ? BigRational.One
            : new BigRational(2);
        ExactReal period = DivideAngle(Angle(angleUnit, fraction), pattern.Frequency);
        return Assign(new Periodicity(
            PeriodicityKind.PeriodicWithFundamentalPeriod,
            period), out value);
    }

    private static Graphing.Symbolics.PeriodicIntervalSet PeriodicInterval(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ExactReal period,
        BigRational lower,
        BigRational upper) => new PeriodicIntervalSet(
        period,
        "m",
        IntegerConstraint.All("m"),
        [new PeriodicInterval(
            SolveAngle(pattern, Angle(angleUnit, lower)),
            false,
            SolveAngle(pattern, Angle(angleUnit, upper)),
            false)]);

    private static ExactReal SolveAngle(ExactTrigPattern pattern, ExactReal angle) =>
        DivideAngle(
            ExactRealArithmetic.Subtract(angle, pattern.Phase.Value),
            pattern.Frequency);

    private static ExactReal DivideAngle(ExactReal angle, ExactScalar frequency)
    {
        if (string.Equals(
                ExactRealCanonical.Format(angle),
                ExactRealCanonical.Format(frequency.Value),
                StringComparison.Ordinal))
        {
            return new RationalReal(BigRational.One);
        }

        if (angle is AffinePiReal
            {
                Constant.IsZero: true
            } numerator &&
            frequency.Value is AffinePiReal
            {
                Constant.IsZero: true
            } denominator)
        {
            return new RationalReal(numerator.PiCoefficient / denominator.PiCoefficient);
        }

        return frequency.RationalValue is { } rational
            ? ExactRealArithmetic.Scale(angle, rational.Reciprocal())
            : ExactRealArithmetic.Divide(angle, frequency.Value);
    }

    private static ExactReal Angle(AngleUnit unit, BigRational piFraction) =>
        ExactAngleArithmetic.PiFraction(unit, piFraction);

    private static bool TryPhaseFraction(
        ExactScalar phase,
        AngleUnit angleUnit,
        out BigRational fraction)
    {
        switch (angleUnit, phase.Value)
        {
            case (AngleUnit.Radians, AffinePiReal
            {
                Constant.IsZero: true
            } radians):
                fraction = radians.PiCoefficient;
                return true;
            case (AngleUnit.Degrees, RationalReal degrees):
                fraction = degrees.Value / new BigRational(180);
                return true;
            case (AngleUnit.Grads, RationalReal grads):
                fraction = grads.Value / new BigRational(200);
                return true;
            default:
                fraction = default;
                return phase.IsZero;
        }
    }

    private static bool TryPrimitiveAtPhase(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        out bool defined,
        out ExactReal value)
    {
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent &&
            TryPhaseFraction(pattern.Phase, angleUnit, out BigRational tangentFraction) &&
            (tangentFraction * new BigRational(4)).IsInteger)
        {
            ExactInteger quarterTurns = (tangentFraction * new BigRational(4)).Numerator;
            int residue = (int)(quarterTurns % 4);
            if (residue < 0)
            {
                residue += 4;
            }

            if (residue == 2)
            {
                defined = false;
                value = null!;
                return true;
            }

            defined = true;
            value = new RationalReal(residue switch
            {
                1 => BigRational.One,
                3 => BigRational.MinusOne,
                _ => BigRational.Zero
            });
            return true;
        }

        if (TryPhaseFraction(pattern.Phase, angleUnit, out BigRational fraction) &&
            (fraction * new BigRational(2)).IsInteger)
        {
            ExactInteger quarterTurns = (fraction * new BigRational(2)).Numerator;
            int residue = (int)(quarterTurns % 4);
            if (residue < 0)
            {
                residue += 4;
            }

            if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent &&
                residue is 1 or 3)
            {
                defined = false;
                value = null!;
                return true;
            }

            BigRational primitive = pattern.Kind switch
            {
                ExactCoefficientPatternKind.AffineSine => residue switch
                {
                    1 => BigRational.One,
                    3 => BigRational.MinusOne,
                    _ => BigRational.Zero
                },
                ExactCoefficientPatternKind.AffineCosine => residue switch
                {
                    0 => BigRational.One,
                    2 => BigRational.MinusOne,
                    _ => BigRational.Zero
                },
                _ => BigRational.Zero
            };
            defined = true;
            value = new RationalReal(primitive);
            return true;
        }

        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent &&
            !TryPhaseFraction(pattern.Phase, angleUnit, out _) &&
            !(angleUnit == AngleUnit.Radians &&
              pattern.Phase.Value is RationalReal))
        {
            defined = false;
            value = null!;
            return false;
        }

        defined = true;
        value = new FunctionReal(
            pattern.Function,
            [ExactAngleArithmetic.ToRadians(pattern.Phase.Value, angleUnit)]);
        return true;
    }

    private static ExactReal PrincipalInverseAngle(
        string inverse,
        ExactScalar target,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        budget.Charge();
        return ExactInverseTrigonometry.PrincipalAngle(inverse, target, angleUnit);
    }

    private static bool Assign<T>(T assigned, out object value)
    {
        value = assigned!;
        return true;
    }
}
