using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Direct theorem instantiation for sign of an affine real expression.
/// The sign function is -1, 0, or 1 according to the sign of its argument.
/// </summary>
internal static class AffineSignTheorems
{
    public static bool TryCompute(AffineSignContext context, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge();
        bool constant = context.Slope.IsZero;
        ExactReal constantValue = SignValue(context.Intercept.Sign);
        value = feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range => constant ? RealSets.Points([constantValue]) : RealSets.Points([SignValue(-1), SignValue(0), SignValue(1)]),
            AnalysisFeatures.Parity => Parity(context),
            AnalysisFeatures.Zeros => Zeros(context, budget),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(constantValue),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => HorizontalAsymptotes(context),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(AllRealSet.Instance, constant ? Monotonicity.Constant : context.Slope.Sign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing)),
            AnalysisFeatures.Period => new Periodicity(constant ? PeriodicityKind.PeriodicWithoutFundamentalPeriod : PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static RealSet Zeros(AffineSignContext context, ResourceBudget budget)
    {
        if (context.Slope.IsZero)
        {
            return context.Intercept.IsZero ? AllRealSet.Instance : EmptySet.Instance;
        }

        ExactScalar root = context.Intercept.Negate().Multiply(context.Slope.Reciprocal(budget), budget);
        return RealSets.Points([root.Value]);
    }

    private static FunctionParity Parity(AffineSignContext context)
    {
        if (context.Slope.IsZero)
        {
            return context.Intercept.IsZero ? FunctionParity.Both : FunctionParity.Even;
        }

        return context.Intercept.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
    }

    private static ImmutableArray<Asymptote> HorizontalAsymptotes(AffineSignContext context)
    {
        if (context.Slope.IsZero)
        {
            ExactReal value = SignValue(context.Intercept.Sign);
            return [Horizontal(value)];
        }

        // Asymptote arrays conventionally list the x -> +infinity limit before
        // the x -> -infinity limit.  For sgn(x) this is +1 then -1; reflecting
        // the affine argument reverses that order.
        int rightLimit = context.Slope.Sign;
        return [Horizontal(SignValue(rightLimit)), Horizontal(SignValue(-rightLimit))];
    }

    private static Asymptote Horizontal(ExactReal value)
    {
        return new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(value), null, value);
    }

    private static RationalReal SignValue(int sign)
    {
        return new RationalReal(new BigRational(sign));
    }
}
