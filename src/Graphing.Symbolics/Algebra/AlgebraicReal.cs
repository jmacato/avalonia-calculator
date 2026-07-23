using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AlgebraicReal(UnivariatePolynomial Polynomial, RationalInterval IsolatingInterval, int RootIndex, ImmutableArray<int> ThomEncoding) : ExactReal;
