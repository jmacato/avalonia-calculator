using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Independent production replay for exact-coefficient affine trigonometric
/// claims. The replay re-derives every published feature from the checked
/// pattern and never invokes the solver theorem dispatcher.
/// </summary>
internal static class ExactTrigCertificateReplay
{
    public static bool TryCompute(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        return feature switch
        {
            AnalysisFeatures.Domain => Assign(BuildDomain(pattern, angleUnit), out value),
            AnalysisFeatures.Range => TryBuildRange(pattern, budget, out value),
            AnalysisFeatures.Parity => TryClassifyParity(pattern, angleUnit, out value),
            AnalysisFeatures.Zeros => TryBuildZeros(pattern, angleUnit, budget, out value),
            AnalysisFeatures.YIntercept =>
                TryBuildYIntercept(pattern, angleUnit, budget, out value),
            AnalysisFeatures.Minima =>
                TryBuildExtrema(pattern, angleUnit, minimum: true, budget, out value),
            AnalysisFeatures.Maxima =>
                TryBuildExtrema(pattern, angleUnit, minimum: false, budget, out value),
            AnalysisFeatures.InflectionPoints =>
                Assign(BuildInflections(pattern, angleUnit), out value),
            AnalysisFeatures.VerticalAsymptotes =>
                Assign(BuildVerticalAsymptotes(pattern, angleUnit), out value),
            AnalysisFeatures.HorizontalAsymptotes or
            AnalysisFeatures.ObliqueAsymptotes =>
                Assign(ImmutableArray<Asymptote>.Empty, out value),
            AnalysisFeatures.Monotonicity =>
                Assign(BuildMonotonicity(pattern, angleUnit), out value),
            AnalysisFeatures.Period => Assign(BuildPeriod(pattern, angleUnit), out value),
            _ => Fail(out value)
        };
    }

    private static RealSet BuildDomain(ExactTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent)
        {
            return AllRealSet.Instance;
        }

        ExactReal lower = SolveCoordinate(
            pattern,
            UnitPiFraction(angleUnit, new BigRational(-1, 2)));
        ExactReal upper = SolveCoordinate(
            pattern,
            UnitPiFraction(angleUnit, new BigRational(1, 2)));
        ExactReal period = DivideCoordinate(
            UnitPiFraction(angleUnit, BigRational.One),
            pattern.Frequency);
        return new PeriodicIntervalSet(
            period,
            "m",
            IntegerConstraint.All("m"),
            [new PeriodicInterval(lower, false, upper, false)]);
    }

    private static bool TryBuildRange(
        ExactTrigPattern pattern,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
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
            return Fail(out value);
        }

        return Assign(
            new IntervalSet(
                RealBound.Finite(lower.Value),
                true,
                RealBound.Finite(upper.Value),
                true),
            out value);
    }

    private static bool TryClassifyParity(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        out object value)
    {
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent &&
            angleUnit == AngleUnit.Radians &&
            pattern.Phase.Value is RationalReal { Value.IsZero: false })
        {
            return Assign(FunctionParity.Neither, out value);
        }

        if (!TryPhasePiFraction(pattern.Phase, angleUnit, out BigRational phase))
        {
            return Fail(out value);
        }

        bool integerPhase = phase.IsInteger;
        bool halfIntegerPhase = (phase - new BigRational(1, 2)).IsInteger;
        FunctionParity parity = pattern.Kind switch
        {
            ExactCoefficientPatternKind.AffineSine when halfIntegerPhase =>
                FunctionParity.Even,
            ExactCoefficientPatternKind.AffineSine when
                integerPhase && pattern.Shift.IsZero => FunctionParity.Odd,
            ExactCoefficientPatternKind.AffineCosine when integerPhase =>
                FunctionParity.Even,
            ExactCoefficientPatternKind.AffineCosine when
                halfIntegerPhase && pattern.Shift.IsZero => FunctionParity.Odd,
            ExactCoefficientPatternKind.AffineTangent when
                (integerPhase || halfIntegerPhase) && pattern.Shift.IsZero =>
                FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
        return Assign(parity, out value);
    }

    private static bool TryBuildZeros(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        if (pattern.Shift.IsZero)
        {
            BigRational offsetFraction =
                pattern.Kind == ExactCoefficientPatternKind.AffineCosine
                    ? new BigRational(1, 2)
                    : BigRational.Zero;
            RealSet lattice = new PeriodicPointSet(
                SolveCoordinate(pattern, UnitPiFraction(angleUnit, offsetFraction)),
                DivideCoordinate(
                    UnitPiFraction(angleUnit, BigRational.One),
                    pattern.Frequency),
                "m",
                IntegerConstraint.All("m"));
            return Assign(lattice, out value);
        }

        ExactScalar target = pattern.Shift
            .Negate()
            .Multiply(pattern.Amplitude.Reciprocal(budget), budget);
        int boundaryComparison = 0;
        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent &&
            !target.TryCompareAbsoluteTo(
                BigRational.One,
                budget,
                out boundaryComparison))
        {
            return Fail(out value);
        }

        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent &&
            boundaryComparison > 0)
        {
            return Assign(EmptySet.Instance, out value);
        }

        string inverse = pattern.Kind switch
        {
            ExactCoefficientPatternKind.AffineSine => "asin",
            ExactCoefficientPatternKind.AffineCosine => "acos",
            ExactCoefficientPatternKind.AffineTangent => "atan",
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
        ExactReal principal = PrincipalAngle(inverse, target, angleUnit);
        BigRational periodFraction =
            pattern.Kind == ExactCoefficientPatternKind.AffineTangent
                ? BigRational.One
                : new BigRational(2);
        ExactReal period = DivideCoordinate(
            UnitPiFraction(angleUnit, periodFraction),
            pattern.Frequency);
        RealSet first = new PeriodicPointSet(
            SolveCoordinate(pattern, principal),
            period,
            "m",
            IntegerConstraint.All("m"));
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent ||
            boundaryComparison == 0)
        {
            return Assign(first, out value);
        }

        ExactReal reflected = pattern.Kind == ExactCoefficientPatternKind.AffineSine
            ? ExactRealArithmetic.Subtract(
                UnitPiFraction(angleUnit, BigRational.One),
                principal)
            : ExactRealArithmetic.Negate(principal);
        RealSet second = new PeriodicPointSet(
            SolveCoordinate(pattern, reflected),
            period,
            "m",
            IntegerConstraint.All("m"));
        return Assign(RealSets.Union(first, second), out value);
    }

    private static bool TryBuildYIntercept(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        if (!TryPrimitiveAtPhase(
                pattern,
                angleUnit,
                out bool defined,
                out ExactReal primitive))
        {
            return Fail(out value);
        }

        if (!defined)
        {
            return Assign(OptionalValue<ExactReal>.None, out value);
        }

        ExactScalar amplitude = pattern.Amplitude;
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent &&
            pattern.Phase.Value is RationalReal { Value.Sign: < 0 } phase &&
            primitive is FunctionReal { Function: "tan" })
        {
            amplitude = amplitude.Negate();
            primitive = new FunctionReal(
                "tan",
                [UnitAngleToRadians(
                    new RationalReal(phase.Value.Abs()),
                    angleUnit)]);
        }

        ExactReal scaled = ExactRealArithmetic.Multiply(amplitude.Value, primitive);
        ExactReal intercept = pattern.Shift.IsZero
            ? scaled
            : ExactRealArithmetic.Add(scaled, pattern.Shift.Value);
        return Assign(OptionalValue<ExactReal>.Some(intercept), out value);
    }

    private static bool TryBuildExtrema(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        bool minimum,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent)
        {
            return Assign(ImmutableArray<FeaturePoint>.Empty, out value);
        }

        bool amplitudePositive = pattern.Amplitude.Sign > 0;
        BigRational extremumFraction;
        if (pattern.Kind == ExactCoefficientPatternKind.AffineSine)
        {
            bool positivePeak = minimum != amplitudePositive;
            extremumFraction = positivePeak
                ? new BigRational(1, 2)
                : new BigRational(3, 2);
        }
        else
        {
            bool positivePeak = minimum != amplitudePositive;
            extremumFraction = positivePeak
                ? BigRational.Zero
                : BigRational.One;
        }

        ExactScalar signedMagnitude = minimum
            ? pattern.Amplitude.Abs().Negate()
            : pattern.Amplitude.Abs();
        if (!ExactCoefficientMath.TryAdd(
                pattern.Shift,
                signedMagnitude,
                budget,
                out ExactScalar ordinate))
        {
            return Fail(out value);
        }

        ExactReal period = DivideCoordinate(
            UnitPiFraction(angleUnit, new BigRational(2)),
            pattern.Frequency);
        ImmutableArray<FeaturePoint> points =
        [
            new ConstantYFeaturePoint(
                new PeriodicReal(
                    SolveCoordinate(
                        pattern,
                        UnitPiFraction(angleUnit, extremumFraction)),
                    period,
                    "m",
                    IntegerConstraint.All("m")),
                ordinate.Value)
        ];
        return Assign(points, out value);
    }

    private static ImmutableArray<FeaturePoint> BuildInflections(
        ExactTrigPattern pattern,
        AngleUnit angleUnit)
    {
        BigRational fraction = pattern.Kind == ExactCoefficientPatternKind.AffineCosine
            ? new BigRational(1, 2)
            : BigRational.Zero;
        ExactReal period = DivideCoordinate(
            UnitPiFraction(angleUnit, BigRational.One),
            pattern.Frequency);
        return
        [
            new ConstantYFeaturePoint(
                new PeriodicReal(
                    SolveCoordinate(pattern, UnitPiFraction(angleUnit, fraction)),
                    period,
                    "m",
                    IntegerConstraint.All("m")),
                pattern.Shift.Value)
        ];
    }

    private static ImmutableArray<Asymptote> BuildVerticalAsymptotes(
        ExactTrigPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent)
        {
            return [];
        }

        ExactReal period = DivideCoordinate(
            UnitPiFraction(angleUnit, BigRational.One),
            pattern.Frequency);
        return
        [
            new Asymptote(
                AsymptoteOrientation.Vertical,
                new PeriodicReal(
                    SolveCoordinate(
                        pattern,
                        UnitPiFraction(angleUnit, new BigRational(1, 2))),
                    period,
                    "m",
                    IntegerConstraint.All("m")),
                null,
                null)
        ];
    }

    private static ImmutableArray<MonotoneRegion> BuildMonotonicity(
        ExactTrigPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent)
        {
            ExactReal tangentPeriod = DivideCoordinate(
                UnitPiFraction(angleUnit, BigRational.One),
                pattern.Frequency);
            return
            [
                new MonotoneRegion(
                    PeriodicRegion(
                        pattern,
                        angleUnit,
                        tangentPeriod,
                        new BigRational(1, 2),
                        new BigRational(3, 2)),
                    pattern.Amplitude.Sign > 0
                        ? Monotonicity.Increasing
                        : Monotonicity.Decreasing)
            ];
        }

        ExactReal period = DivideCoordinate(
            UnitPiFraction(angleUnit, new BigRational(2)),
            pattern.Frequency);
        (BigRational IncreasingLower, BigRational IncreasingUpper,
            BigRational DecreasingLower, BigRational DecreasingUpper) fractions =
            pattern.Kind == ExactCoefficientPatternKind.AffineSine
                ? (new BigRational(3, 2), new BigRational(5, 2),
                    new BigRational(1, 2), new BigRational(3, 2))
                : (BigRational.One, new BigRational(2),
                    BigRational.Zero, BigRational.One);
        bool positive = pattern.Amplitude.Sign > 0;
        return
        [
            new MonotoneRegion(
                PeriodicRegion(
                    pattern,
                    angleUnit,
                    period,
                    fractions.DecreasingLower,
                    fractions.DecreasingUpper),
                positive ? Monotonicity.Decreasing : Monotonicity.Increasing),
            new MonotoneRegion(
                PeriodicRegion(
                    pattern,
                    angleUnit,
                    period,
                    fractions.IncreasingLower,
                    fractions.IncreasingUpper),
                positive ? Monotonicity.Increasing : Monotonicity.Decreasing)
        ];
    }

    private static Periodicity BuildPeriod(
        ExactTrigPattern pattern,
        AngleUnit angleUnit)
    {
        BigRational fraction = pattern.Kind == ExactCoefficientPatternKind.AffineTangent
            ? BigRational.One
            : new BigRational(2);
        return new Periodicity(
            PeriodicityKind.PeriodicWithFundamentalPeriod,
            DivideCoordinate(UnitPiFraction(angleUnit, fraction), pattern.Frequency));
    }

    private static PeriodicIntervalSet PeriodicRegion(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        ExactReal period,
        BigRational lower,
        BigRational upper)
    {
        return new PeriodicIntervalSet(
            period,
            "m",
            IntegerConstraint.All("m"),
            [
                new PeriodicInterval(
                    SolveCoordinate(pattern, UnitPiFraction(angleUnit, lower)),
                    false,
                    SolveCoordinate(pattern, UnitPiFraction(angleUnit, upper)),
                    false)
            ]);
    }

    private static bool TryPrimitiveAtPhase(
        ExactTrigPattern pattern,
        AngleUnit angleUnit,
        out bool defined,
        out ExactReal value)
    {
        if (pattern.Kind == ExactCoefficientPatternKind.AffineTangent &&
            TryPhasePiFraction(pattern.Phase, angleUnit, out BigRational eighthFraction) &&
            (eighthFraction * new BigRational(4)).IsInteger)
        {
            int residue = ModuloFour(
                (eighthFraction * new BigRational(4)).Numerator);
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

        if (TryPhasePiFraction(pattern.Phase, angleUnit, out BigRational phaseFraction) &&
            (phaseFraction * new BigRational(2)).IsInteger)
        {
            int residue = ModuloFour(
                (phaseFraction * new BigRational(2)).Numerator);
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
            !TryPhasePiFraction(pattern.Phase, angleUnit, out _) &&
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
            [UnitAngleToRadians(pattern.Phase.Value, angleUnit)]);
        return true;
    }

    private static int ModuloFour(ExactInteger value)
    {
        int residue = (int)(value % 4);
        return residue < 0 ? residue + 4 : residue;
    }

    private static ExactReal PrincipalAngle(
        string inverse,
        ExactScalar target,
        AngleUnit angleUnit)
    {
        if (target.RationalValue is { } rational &&
            TrySpecialInverseFraction(inverse, rational, out BigRational fraction))
        {
            return fraction.IsZero
                ? new RationalReal(BigRational.Zero)
                : UnitPiFraction(angleUnit, fraction);
        }

        ExactReal radians = target.Sign < 0 && inverse is "asin" or "atan"
            ? ExactRealArithmetic.Negate(
                new FunctionReal(inverse, [target.Negate().Value]))
            : new FunctionReal(inverse, [target.Value]);
        return RadiansToUnitAngle(radians, angleUnit);
    }

    private static bool TrySpecialInverseFraction(
        string inverse,
        BigRational target,
        out BigRational fraction)
    {
        BigRational? candidate = inverse switch
        {
            "asin" => target switch
            {
                var value when value == BigRational.MinusOne => new BigRational(-1, 2),
                var value when value == new BigRational(-1, 2) => new BigRational(-1, 6),
                var value when value.IsZero => BigRational.Zero,
                var value when value == new BigRational(1, 2) => new BigRational(1, 6),
                var value when value.IsOne => new BigRational(1, 2),
                _ => null
            },
            "acos" => target switch
            {
                var value when value == BigRational.MinusOne => BigRational.One,
                var value when value == new BigRational(-1, 2) => new BigRational(2, 3),
                var value when value.IsZero => new BigRational(1, 2),
                var value when value == new BigRational(1, 2) => new BigRational(1, 3),
                var value when value.IsOne => BigRational.Zero,
                _ => null
            },
            "atan" => target switch
            {
                var value when value == BigRational.MinusOne => new BigRational(-1, 4),
                var value when value.IsZero => BigRational.Zero,
                var value when value.IsOne => new BigRational(1, 4),
                _ => null
            },
            _ => throw new ArgumentOutOfRangeException(nameof(inverse))
        };
        if (candidate is not { } exact)
        {
            fraction = default;
            return false;
        }

        fraction = exact;
        return true;
    }

    private static bool TryPhasePiFraction(
        ExactScalar phase,
        AngleUnit angleUnit,
        out BigRational fraction)
    {
        switch (angleUnit, phase.Value)
        {
            case (AngleUnit.Radians, AffinePiReal { Constant.IsZero: true } radians):
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

    private static ExactReal SolveCoordinate(
        ExactTrigPattern pattern,
        ExactReal angle)
    {
        return DivideCoordinate(
            ExactRealArithmetic.Subtract(angle, pattern.Phase.Value),
            pattern.Frequency);
    }

    private static ExactReal DivideCoordinate(
        ExactReal coordinate,
        ExactScalar frequency)
    {
        if (string.Equals(
                ExactRealCanonical.Format(coordinate),
                ExactRealCanonical.Format(frequency.Value),
                StringComparison.Ordinal))
        {
            return new RationalReal(BigRational.One);
        }

        if (coordinate is AffinePiReal { Constant.IsZero: true } numerator &&
            frequency.Value is AffinePiReal { Constant.IsZero: true } denominator)
        {
            return new RationalReal(
                numerator.PiCoefficient / denominator.PiCoefficient);
        }

        return frequency.RationalValue is { } rational
            ? ExactRealArithmetic.Scale(coordinate, rational.Reciprocal())
            : ExactRealArithmetic.Divide(coordinate, frequency.Value);
    }

    private static ExactReal UnitPiFraction(
        AngleUnit angleUnit,
        BigRational fraction)
    {
        return angleUnit switch
        {
            AngleUnit.Radians => new AffinePiReal(fraction, BigRational.Zero),
            AngleUnit.Degrees => new RationalReal(new BigRational(180) * fraction),
            AngleUnit.Grads => new RationalReal(new BigRational(200) * fraction),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnit))
        };
    }

    private static ExactReal UnitAngleToRadians(
        ExactReal angle,
        AngleUnit angleUnit)
    {
        if (angleUnit == AngleUnit.Radians)
        {
            return angle;
        }

        BigRational halfTurn = angleUnit == AngleUnit.Degrees
            ? new BigRational(180)
            : angleUnit == AngleUnit.Grads
                ? new BigRational(200)
                : throw new ArgumentOutOfRangeException(nameof(angleUnit));
        return ExactRealArithmetic.Multiply(
            angle,
            new AffinePiReal(halfTurn.Reciprocal(), BigRational.Zero));
    }

    private static ExactReal RadiansToUnitAngle(
        ExactReal radians,
        AngleUnit angleUnit)
    {
        if (angleUnit == AngleUnit.Radians)
        {
            return radians;
        }

        BigRational halfTurn = angleUnit == AngleUnit.Degrees
            ? new BigRational(180)
            : angleUnit == AngleUnit.Grads
                ? new BigRational(200)
                : throw new ArgumentOutOfRangeException(nameof(angleUnit));
        return ExactRealArithmetic.Divide(
            ExactRealArithmetic.Scale(radians, halfTurn),
            new AffinePiReal(BigRational.One, BigRational.Zero));
    }

    private static bool Assign<T>(T assigned, out object value)
    {
        value = assigned!;
        return true;
    }

    private static bool Fail(out object value)
    {
        value = null!;
        return false;
    }
}
