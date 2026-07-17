using System.Collections.Immutable;
using System.Numerics;

namespace Graphing.Symbolics;

internal sealed record AlgebraicReal(UnivariatePolynomial Polynomial, RationalInterval IsolatingInterval, int RootIndex, ImmutableArray<int> ThomEncoding) : ExactReal;
