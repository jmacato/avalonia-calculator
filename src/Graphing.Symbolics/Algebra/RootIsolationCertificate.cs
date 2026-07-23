using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record RootIsolationCertificate(UnivariatePolynomial Polynomial, ImmutableArray<ExactReal> Roots, ImmutableArray<UnivariatePolynomial> SturmSequence);
