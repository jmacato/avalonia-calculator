namespace Graphing.Symbolics;

internal sealed record AffinePhaseSineCompositionPattern(AffinePhaseSineOuterKind OuterKind, BigRational Frequency, BigRational PhasePiCoefficient, BigRational PhaseConstant, int InnerSign)
{
    public string OuterFunction => OuterKind switch
    {
        AffinePhaseSineOuterKind.SquareRoot => "sqrt",
        AffinePhaseSineOuterKind.NaturalLogarithm => "ln",
        AffinePhaseSineOuterKind.CommonLogarithm => "log",
        _ => throw new ArgumentOutOfRangeException()
    };
    public ExactReal Phase => PhasePiCoefficient.IsZero ? new RationalReal(PhaseConstant) : new AffinePiReal(PhasePiCoefficient, PhaseConstant);
    public bool IsSquareRoot => OuterKind == AffinePhaseSineOuterKind.SquareRoot;
    public string Canonical => $"affine-phase-sine-composition:{(int)OuterKind}:{Frequency}:" + $"{PhasePiCoefficient}:{PhaseConstant}:{InnerSign}";
}
