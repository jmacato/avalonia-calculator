using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record TrigonometricPolynomialModel(RationalFunction HalfAngleFunction, FourierPolynomial Fourier, string Canonical);
