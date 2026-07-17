using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record UnarySignChartCertificate(ImmutableArray<UnivariatePolynomial> Polynomials, UnivariatePolynomial CombinedPolynomial, RootIsolationCertificate RootIsolation, ImmutableArray<BigRational> GapSamples, ImmutableArray<ImmutableArray<int>> GapSigns, ImmutableArray<ImmutableArray<int>> PointSigns, ImmutableArray<bool> GapDomain, ImmutableArray<bool> PointDomain);
