using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class AffinePhaseSineCompositionProofKernel
{
    private const string Parameter = "m";
    public static bool TryCompute(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge(4);
        RealSet domain = Domain(pattern, angleUnit);
        if (feature == AnalysisFeatures.YIntercept)
        {
            OptionalValue<ExactReal> intercept = YIntercept(pattern, angleUnit, out bool known);
            value = intercept;
            return known;
        }

        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => Range(pattern),
            AnalysisFeatures.Parity => Parity(pattern, angleUnit),
            AnalysisFeatures.Zeros => Zeros(pattern, angleUnit),
            AnalysisFeatures.Minima => Minima(pattern, angleUnit),
            AnalysisFeatures.Maxima => Maxima(pattern, angleUnit),
            AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => VerticalAsymptotes(pattern, angleUnit),
            AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => MonotonicityRegions(pattern, angleUnit),
            AnalysisFeatures.Period => Period(pattern, angleUnit),
            _ => null!
        };
        return value is not null;
    }

    private static Graphing.Symbolics.PeriodicIntervalSet Domain(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit)
    {
        BigRational start = pattern.InnerSign > 0 ? BigRational.Zero : BigRational.One;
        return PeriodicIntervals(pattern, angleUnit, start, pattern.IsSquareRoot, start + BigRational.One, pattern.IsSquareRoot, new BigRational(2));
    }

    private static Graphing.Symbolics.IntervalSet Range(AffinePhaseSineCompositionPattern pattern)
    {
        ExactReal zero = Rational(BigRational.Zero);
        return pattern.IsSquareRoot ? new IntervalSet(RealBound.Finite(zero), true, RealBound.Finite(Rational(BigRational.One)), true) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(zero), true);
    }

    private static FunctionParity Parity(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit) => TryPhaseFraction(pattern, angleUnit, out BigRational fraction) && (fraction - new BigRational(1, 2)).IsInteger ? FunctionParity.Even : FunctionParity.Neither;
    private static Graphing.Symbolics.PeriodicPointSet Zeros(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit)
    {
        BigRational angle = pattern.IsSquareRoot ? BigRational.Zero : pattern.InnerSign > 0 ? new BigRational(1, 2) : new BigRational(3, 2);
        BigRational step = pattern.IsSquareRoot ? BigRational.One : new BigRational(2);
        return new PeriodicPointSet(SolveAngle(pattern, Angle(angleUnit, angle)), Step(pattern, angleUnit, step), Parameter, IntegerConstraint.All(Parameter));
    }

    private static OptionalValue<ExactReal> YIntercept(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit, out bool known)
    {
        if (!TryPhaseFraction(pattern, angleUnit, out BigRational fraction))
        {
            known = false;
            return default;
        }

        BigRational reduced = PositiveModulo(fraction, new BigRational(2));
        int rawSign = reduced.IsZero || reduced.IsOne ? 0 : reduced < BigRational.One ? 1 : -1;
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
            return OptionalValue<ExactReal>.Some(Rational(pattern.IsSquareRoot ? BigRational.One : BigRational.Zero));
        }

        known = true;
        return OptionalValue<ExactReal>.Some(new FunctionReal(pattern.OuterFunction, [inner]));
    }

    private static ExactReal ExactSineAtPhase(AffinePhaseSineCompositionPattern pattern, BigRational phaseFraction)
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

        ExactReal sine = new FunctionReal("sin", [new AffinePiReal(phaseFraction, BigRational.Zero)]);
        return pattern.InnerSign > 0 ? sine : ExactRealArithmetic.Negate(sine);
    }

    private static ImmutableArray<FeaturePoint> Minima(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (!pattern.IsSquareRoot)
        {
            return [];
        }

        BigRational start = pattern.InnerSign > 0 ? BigRational.Zero : BigRational.One;
        ExactReal period = Step(pattern, angleUnit, new BigRational(2));
        return [PeriodicFeature(pattern, angleUnit, start, period, Rational(BigRational.Zero)), PeriodicFeature(pattern, angleUnit, start + BigRational.One, period, Rational(BigRational.Zero))];
    }

    private static ImmutableArray<FeaturePoint> Maxima(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit)
    {
        BigRational angle = pattern.InnerSign > 0 ? new BigRational(1, 2) : new BigRational(3, 2);
        ExactReal y = Rational(pattern.IsSquareRoot ? BigRational.One : BigRational.Zero);
        return [PeriodicFeature(pattern, angleUnit, angle, Step(pattern, angleUnit, new BigRational(2)), y)];
    }

    private static ImmutableArray<Asymptote> VerticalAsymptotes(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.IsSquareRoot)
        {
            return [];
        }

        return [new Asymptote(AsymptoteOrientation.Vertical, new PeriodicReal(SolveAngle(pattern, Angle(angleUnit, BigRational.Zero)), Step(pattern, angleUnit, BigRational.One), Parameter, IntegerConstraint.All(Parameter)), null, null)];
    }

    private static ImmutableArray<MonotoneRegion> MonotonicityRegions(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit)
    {
        BigRational start = pattern.InnerSign > 0 ? BigRational.Zero : BigRational.One;
        return [new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, start, false, start + new BigRational(1, 2), false, new BigRational(2)), Graphing.Symbolics.Monotonicity.Increasing), new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, start + new BigRational(1, 2), false, start + BigRational.One, false, new BigRational(2)), Graphing.Symbolics.Monotonicity.Decreasing)];
    }

    private static Periodicity Period(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit) => new(PeriodicityKind.PeriodicWithFundamentalPeriod, Step(pattern, angleUnit, new BigRational(2)));
    private static ConstantYFeaturePoint PeriodicFeature(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit, BigRational angle, ExactReal period, ExactReal y) => new(new PeriodicReal(SolveAngle(pattern, Angle(angleUnit, angle)), period, Parameter, IntegerConstraint.All(Parameter)), y);
    private static PeriodicIntervalSet PeriodicIntervals(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit, BigRational lower, bool includesLower, BigRational upper, bool includesUpper, BigRational period) => new(Step(pattern, angleUnit, period), Parameter, IntegerConstraint.All(Parameter), [new PeriodicInterval(SolveAngle(pattern, Angle(angleUnit, lower)), includesLower, SolveAngle(pattern, Angle(angleUnit, upper)), includesUpper)]);
    private static ExactReal SolveAngle(AffinePhaseSineCompositionPattern pattern, ExactReal angle) => ExactRealArithmetic.Scale(ExactRealArithmetic.Subtract(angle, pattern.Phase), pattern.Frequency.Reciprocal());
    private static ExactReal Step(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit, BigRational fraction) => ExactRealArithmetic.Scale(Angle(angleUnit, fraction), pattern.Frequency.Reciprocal());
    private static bool TryPhaseFraction(AffinePhaseSineCompositionPattern pattern, AngleUnit angleUnit, out BigRational fraction)
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

    private static ExactReal Angle(AngleUnit unit, BigRational piFraction) => ExactAngleArithmetic.PiFraction(unit, piFraction);
    private static RationalReal Rational(BigRational value) => new(value);
}
