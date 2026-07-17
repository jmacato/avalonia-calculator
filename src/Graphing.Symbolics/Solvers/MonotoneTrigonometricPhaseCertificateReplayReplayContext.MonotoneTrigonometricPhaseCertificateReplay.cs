using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal readonly record struct MonotoneTrigonometricPhaseCertificateReplayReplayContext(string Variable, AngleUnit AngleUnit, string OuterFunction, bool IsDirectComposition, ValueTerm? QuotientDenominator, ValueTerm PhaseValue, PuiseuxPolynomial Phase, BigRational CoordinateOffset, RealSet Domain, bool ParameterIsNonnegative, bool BoundaryIncluded, int SubstitutionDegree, UnivariatePolynomial ParameterPolynomial, PhaseOrientation ParameterOrientation, PhaseOrientation Orientation);
