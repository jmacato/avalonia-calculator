using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ClaimCanonical
{
    public static string ForObject(object value)
    {
        return value switch
        {
            RealSet set => set.Canonical,
            FunctionParity parity => For(parity),
            Periodicity periodicity => For(periodicity),
            OptionalValue<ExactReal> optional => For(optional),
            ImmutableArray<FeaturePoint> points => For(points),
            ImmutableArray<Asymptote> asymptotes => For(asymptotes),
            ImmutableArray<MonotoneRegion> regions => For(regions),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    public static string For<T>(T value)
    {
        return value switch
        {
            RealSet set => set.Canonical,
            FunctionParity parity => "parity:" + (int)parity,
            Periodicity periodicity => Periodicity(periodicity),
            OptionalValue<ExactReal> optional => Optional(optional),
            ImmutableArray<FeaturePoint> points =>
                $"feature-points[{string.Join(',', points.Select(FeaturePoint))}]",
            ImmutableArray<Asymptote> asymptotes =>
                $"asymptotes[{string.Join(',', asymptotes.Select(Asymptote))}]",
            ImmutableArray<MonotoneRegion> regions =>
                $"monotonicity[{string.Join(',', regions.Select(MonotoneRegion))}]",
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static string Optional(OptionalValue<ExactReal> value)
    {
        return value.HasValue
            ? "some:" + ExactRealCanonical.Format(value.Value!)
            : "none";
    }

    private static string Periodicity(Periodicity value)
    {
        return
            $"periodicity:{(int)value.Kind}:{(value.FundamentalPeriod is null ? "none" : ExactRealCanonical.Format(value.FundamentalPeriod))}";
    }

    private static string FeaturePoint(FeaturePoint point)
    {
        return point switch
        {
            ConstantYFeaturePoint constant =>
                $"point[{RealFamily(constant.X)},{ExactRealCanonical.Format(constant.Y)}]",
            IntegerAffineFeaturePoint affine =>
                $"integer-affine-point[{ExactRealCanonical.Format(affine.XOffset)},{ExactRealCanonical.Format(affine.XStep)},{ExactRealCanonical.Format(affine.YOffset)},{ExactRealCanonical.Format(affine.YStep)},{affine.Parameter},{affine.Constraint.Canonical}]",
            _ => throw new ArgumentOutOfRangeException(nameof(point))
        };
    }

    private static string Asymptote(Asymptote asymptote)
    {
        return
            $"asymptote[{(int)asymptote.Orientation},{RealFamily(asymptote.Coordinate)},{Maybe(asymptote.Slope)},{Maybe(asymptote.Intercept)}]";
    }

    private static string MonotoneRegion(MonotoneRegion region)
    {
        return $"region[{region.Region.Canonical},{(int)region.Direction}]";
    }

    private static string RealFamily(RealFamily family)
    {
        return family switch
        {
            SingletonReal singleton => "single:" + ExactRealCanonical.Format(singleton.Value),
            PeriodicReal periodic =>
                $"periodic:{ExactRealCanonical.Format(periodic.Offset)}:{ExactRealCanonical.Format(periodic.Period)}:{periodic.Parameter}:{periodic.Constraint.Canonical}",
            LatticeReal lattice =>
                $"lattice:{lattice.Expression}:{string.Join(',', lattice.Parameters)}:{string.Join(',', lattice.Predicates)}",
            PolynomialPhasePreimageReal phase => phase.Preimage.Canonical,
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
    }

    private static string Maybe(ExactReal? value)
    {
        return value is null
            ? "none"
            : ExactRealCanonical.Format(value);
    }
}
