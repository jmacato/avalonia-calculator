namespace Graphing.Symbolics;

internal sealed record AffinePiReal(BigRational PiCoefficient, BigRational Constant) : ExactReal;
