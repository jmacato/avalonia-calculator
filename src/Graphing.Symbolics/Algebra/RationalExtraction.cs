using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record RationalExtraction(RationalFunction Function, ImmutableArray<UnivariatePolynomial> DomainExclusions);
