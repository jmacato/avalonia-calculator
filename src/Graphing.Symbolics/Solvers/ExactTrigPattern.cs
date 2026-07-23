using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record ExactTrigPattern(ExactCoefficientPatternKind TrigKind, ExactScalar Amplitude, ExactScalar Frequency, ExactScalar Phase, ExactScalar Shift) : ExactCoefficientPattern(TrigKind)
{
    public string Function => Kind switch
    {
        ExactCoefficientPatternKind.AffineSine => "sin",
        ExactCoefficientPatternKind.AffineCosine => "cos",
        ExactCoefficientPatternKind.AffineTangent => "tan",
        _ => throw new InvalidOperationException("The pattern is not trigonometric.")
    };
    public override string Canonical => $"{(int)Kind}:{Amplitude.Canonical}:{Frequency.Canonical}:{Phase.Canonical}:{Shift.Canonical}";
    public override ImmutableArray<ExactScalar> BaseScalars => new[]
    {
        Amplitude,
        Frequency,
        Phase,
        Shift
    }.DistinctBy(static scalar => scalar.Canonical).OrderBy(static scalar => scalar.Canonical, StringComparer.Ordinal).ToImmutableArray();
}
