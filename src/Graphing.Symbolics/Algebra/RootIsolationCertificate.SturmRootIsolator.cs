using System.Collections.Immutable;
using System.Numerics;

namespace Graphing.Symbolics;

internal sealed record RootIsolationCertificate(UnivariatePolynomial Polynomial, ImmutableArray<ExactReal> Roots, ImmutableArray<UnivariatePolynomial> SturmSequence);
