using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class SingleHoleSineTheorems
{
    private const string Parameter = "m";
    public static bool TryCompute(SingleHoleSineContext context, AngleUnit angleUnit, AnalysisFeatures feature, out object value)
    {
        ExactReal zero = new RationalReal(BigRational.Zero);
        ExactReal halfTurn = SolveAngle(context.Pattern, PiAngle(angleUnit, BigRational.One));
        ExactReal fullTurn = SolveStep(context.Pattern, PiAngle(angleUnit, new BigRational(2)));
        RealSet zeros = new PeriodicPointSet(zero, halfTurn, Parameter, new IntegerConstraint(Parameter, Comparison.NotEqual, 0));
        value = feature switch
        {
            AnalysisFeatures.Domain => context.Domain,
            AnalysisFeatures.Range => Range(context.Pattern),
            AnalysisFeatures.Parity => FunctionParity.Odd,
            AnalysisFeatures.Zeros => zeros,
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima => Extrema(context.Pattern, angleUnit, minimum: true, fullTurn),
            AnalysisFeatures.Maxima => Extrema(context.Pattern, angleUnit, minimum: false, fullTurn),
            AnalysisFeatures.InflectionPoints => ImmutableArray.Create<FeaturePoint>(new ConstantYFeaturePoint(new PeriodicReal(zero, halfTurn, Parameter, new IntegerConstraint(Parameter, Comparison.NotEqual, 0)), zero)),
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => MonotonicityRegions(context.Pattern, angleUnit, fullTurn),
            // A single retained hole destroys every nonzero translation
            // symmetry, even though the value expression alone is periodic.
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static Graphing.Symbolics.IntervalSet Range(AffineTrigPattern pattern)
    {
        ExactReal magnitude = pattern.Amplitude.Abs().Value;
        return new IntervalSet(RealBound.Finite(ExactRealArithmetic.Negate(magnitude)), true, RealBound.Finite(magnitude), true);
    }

    private static ImmutableArray<FeaturePoint> Extrema(AffineTrigPattern pattern, AngleUnit angleUnit, bool minimum, ExactReal period)
    {
        bool positive = pattern.Amplitude.Sign > 0;
        BigRational fraction = minimum == positive ? new BigRational(3, 2) : new BigRational(1, 2);
        ExactReal magnitude = pattern.Amplitude.Abs().Value;
        ExactReal y = minimum ? ExactRealArithmetic.Negate(magnitude) : magnitude;
        return [new ConstantYFeaturePoint(new PeriodicReal(SolveAngle(pattern, PiAngle(angleUnit, fraction)), period, Parameter, IntegerConstraint.All(Parameter)), y)];
    }

    private static ImmutableArray<MonotoneRegion> MonotonicityRegions(AffineTrigPattern pattern, AngleUnit angleUnit, ExactReal fullTurn)
    {
        ExactReal negativeQuarter = SolveAngle(pattern, PiAngle(angleUnit, new BigRational(-1, 2)));
        ExactReal positiveQuarter = SolveAngle(pattern, PiAngle(angleUnit, new BigRational(1, 2)));
        ExactReal threeQuarters = SolveAngle(pattern, PiAngle(angleUnit, new BigRational(3, 2)));
        ExactReal zero = new RationalReal(BigRational.Zero);
        Monotonicity aroundZero = pattern.Amplitude.Sign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing;
        Monotonicity opposite = aroundZero == Monotonicity.Increasing ? Monotonicity.Decreasing : Monotonicity.Increasing;
        return [new MonotoneRegion(new PeriodicIntervalSet(fullTurn, Parameter, new IntegerConstraint(Parameter, Comparison.NotEqual, 0), [new PeriodicInterval(negativeQuarter, false, positiveQuarter, false)]), aroundZero), new MonotoneRegion(new IntervalSet(RealBound.Finite(negativeQuarter), false, RealBound.Finite(zero), false), aroundZero), new MonotoneRegion(new IntervalSet(RealBound.Finite(zero), false, RealBound.Finite(positiveQuarter), false), aroundZero), new MonotoneRegion(new PeriodicIntervalSet(fullTurn, Parameter, IntegerConstraint.All(Parameter), [new PeriodicInterval(positiveQuarter, false, threeQuarters, false)]), opposite)];
    }

    private static ExactReal SolveAngle(AffineTrigPattern pattern, ExactReal angle) => ExactRealArithmetic.Scale(angle, pattern.Frequency.Reciprocal());
    private static ExactReal SolveStep(AffineTrigPattern pattern, ExactReal angle) => ExactRealArithmetic.Scale(angle, pattern.Frequency.Reciprocal());
    private static ExactReal PiAngle(AngleUnit unit, BigRational fraction) => unit switch
    {
        AngleUnit.Radians => new AffinePiReal(fraction, BigRational.Zero),
        AngleUnit.Degrees => new RationalReal(new BigRational(180) * fraction),
        AngleUnit.Grads => new RationalReal(new BigRational(200) * fraction),
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };
}
