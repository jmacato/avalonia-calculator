using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Exact feature kernel for a lower or upper envelope of two affine lines.
/// A nonparallel envelope has one rational kink and is exactly affine on both
/// open tail cells.
/// </summary>
internal static class AffineMinMaxTheorems
{
    public static bool TryCompute(AffineMinMaxContext context, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge();
        AffineMinMaxModel model = context.CreateModel(budget);
        value = feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range => Range(model),
            AnalysisFeatures.Parity => Parity(context, model),
            AnalysisFeatures.Zeros => Zeros(model, budget),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(Rational(context.IsMinimum ? Min(context.FirstLine.Intercept, context.SecondLine.Intercept) : Max(context.FirstLine.Intercept, context.SecondLine.Intercept))),
            AnalysisFeatures.Minima => Extrema(model, minimum: true),
            AnalysisFeatures.Maxima => Extrema(model, minimum: false),
            AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => Asymptotes(model, AsymptoteOrientation.Horizontal),
            AnalysisFeatures.ObliqueAsymptotes => Asymptotes(model, AsymptoteOrientation.Oblique),
            AnalysisFeatures.Monotonicity => MonotonicityRegions(model),
            AnalysisFeatures.Period => new Periodicity(!model.HasKink && model.EffectiveLine.Slope.IsZero ? PeriodicityKind.PeriodicWithoutFundamentalPeriod : PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static RealSet Range(AffineMinMaxModel model)
    {
        if (!model.HasKink)
        {
            return model.EffectiveLine.Slope.IsZero ? RealSets.Points([Rational(model.EffectiveLine.Intercept)]) : AllRealSet.Instance;
        }

        bool unboundedBelow = model.LeftTail.Slope.Sign > 0 || model.RightTail.Slope.Sign < 0;
        bool unboundedAbove = model.LeftTail.Slope.Sign < 0 || model.RightTail.Slope.Sign > 0;
        if (unboundedBelow && unboundedAbove)
        {
            return AllRealSet.Instance;
        }

        ExactReal kinkY = Rational(model.KinkY);
        if (unboundedBelow)
        {
            return new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(kinkY), true);
        }

        if (unboundedAbove)
        {
            return new IntervalSet(RealBound.Finite(kinkY), true, RealBound.PositiveInfinity, false);
        }

        return RealSets.Points([kinkY]);
    }

    private static RealSet Zeros(AffineMinMaxModel model, ResourceBudget budget)
    {
        if (!model.HasKink)
        {
            return AffineZeros(model.EffectiveLine, budget);
        }

        IntervalSet? zeroInterval = null;
        var roots = new List<BigRational>(2);
        AddSide(model.LeftTail, left: true);
        AddSide(model.RightTail, left: false);
        if (zeroInterval is not null)
        {
            // The other affine side can only meet a zero plateau at the kink,
            // which is already included by the closed half-line.
            return zeroInterval;
        }

        ImmutableArray<ExactReal> orderedRoots = roots.Distinct().Order().Select(static root => (ExactReal)Rational(root)).ToImmutableArray();
        return orderedRoots.IsEmpty ? EmptySet.Instance : new PointSet(orderedRoots);
        void AddSide(RationalAffineLine line, bool left)
        {
            budget.Charge();
            if (line.Slope.IsZero)
            {
                if (model.KinkY.IsZero)
                {
                    ExactReal kink = Rational(model.KinkX);
                    zeroInterval = left ? new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(kink), true) : new IntervalSet(RealBound.Finite(kink), true, RealBound.PositiveInfinity, false);
                }

                return;
            }

            BigRational root = -line.Intercept / line.Slope;
            budget.CheckCoefficient(root);
            if (left ? root <= model.KinkX : root >= model.KinkX)
            {
                roots.Add(root);
            }
        }
    }

    private static RealSet AffineZeros(RationalAffineLine line, ResourceBudget budget)
    {
        if (line.Slope.IsZero)
        {
            return line.Intercept.IsZero ? AllRealSet.Instance : EmptySet.Instance;
        }

        BigRational root = -line.Intercept / line.Slope;
        budget.CheckCoefficient(root);
        return RealSets.Points([Rational(root)]);
    }

    private static FunctionParity Parity(AffineMinMaxContext context, AffineMinMaxModel model)
    {
        if (!model.HasKink)
        {
            RationalAffineLine line = model.EffectiveLine;
            if (line.Slope.IsZero)
            {
                return line.Intercept.IsZero ? FunctionParity.Both : FunctionParity.Even;
            }

            return line.Intercept.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
        }

        return context.FirstLine.Slope == -context.SecondLine.Slope && context.FirstLine.Intercept == context.SecondLine.Intercept ? FunctionParity.Even : FunctionParity.Neither;
    }

    private static ImmutableArray<FeaturePoint> Extrema(AffineMinMaxModel model, bool minimum)
    {
        if (!model.HasKink)
        {
            return [];
        }

        bool strictMinimum = model.LeftTail.Slope.Sign < 0 && model.RightTail.Slope.Sign > 0;
        bool strictMaximum = model.LeftTail.Slope.Sign > 0 && model.RightTail.Slope.Sign < 0;
        if (minimum ? !strictMinimum : !strictMaximum)
        {
            return [];
        }

        return [new ConstantYFeaturePoint(new SingletonReal(Rational(model.KinkX)), Rational(model.KinkY))];
    }

    private static ImmutableArray<Asymptote> Asymptotes(AffineMinMaxModel model, AsymptoteOrientation orientation)
    {
        ImmutableArray<RationalAffineLine> tails = model.HasKink ? [model.RightTail, model.LeftTail] : [model.EffectiveLine];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = ImmutableArray.CreateBuilder<Asymptote>();
        foreach (RationalAffineLine tail in tails)
        {
            bool horizontal = tail.Slope.IsZero;
            if (orientation == AsymptoteOrientation.Horizontal != horizontal || !seen.Add(tail.Canonical))
            {
                continue;
            }

            ExactReal intercept = Rational(tail.Intercept);
            result.Add(horizontal ? new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(intercept), null, intercept) : new Asymptote(AsymptoteOrientation.Oblique, new SingletonReal(intercept), Rational(tail.Slope), intercept));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<MonotoneRegion> MonotonicityRegions(AffineMinMaxModel model)
    {
        if (!model.HasKink)
        {
            return [new MonotoneRegion(AllRealSet.Instance, Direction(model.EffectiveLine.Slope))];
        }

        int leftSign = model.LeftTail.Slope.Sign;
        int rightSign = model.RightTail.Slope.Sign;
        if (leftSign == rightSign)
        {
            return [new MonotoneRegion(AllRealSet.Instance, Direction(model.LeftTail.Slope))];
        }

        ExactReal kink = Rational(model.KinkX);
        return [new MonotoneRegion(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(kink), false), Direction(model.LeftTail.Slope)), new MonotoneRegion(new IntervalSet(RealBound.Finite(kink), false, RealBound.PositiveInfinity, false), Direction(model.RightTail.Slope))];
    }

    private static Monotonicity Direction(BigRational slope)
    {
        return slope.Sign switch
        {
            > 0 => Monotonicity.Increasing,
            < 0 => Monotonicity.Decreasing,
            _ => Monotonicity.Constant
        };
    }

    private static BigRational Min(BigRational first, BigRational second)
    {
        return first <= second ? first : second;
    }

    private static BigRational Max(BigRational first, BigRational second)
    {
        return first >= second ? first : second;
    }

    private static RationalReal Rational(BigRational value)
    {
        return new RationalReal(value);
    }
}
