using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record CellDecompositionCertificate(PolynomialFormula Formula, ImmutableArray<UnivariatePolynomial> Atoms, UnivariatePolynomial CombinedPolynomial, RootIsolationCertificate RootIsolation, ImmutableArray<CellWitness> Cells, RealSet Result);
