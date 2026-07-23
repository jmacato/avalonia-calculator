using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

internal sealed record MonotoneTrigonometricPhaseAnalysisTestsPhaseCase(string Name, InputExpression Input, ImmutableArray<PuiseuxTerm> Terms, int SubstitutionDegree, string ParameterPolynomialCanonical, bool ParameterIsNonnegative, string DomainCanonical, FunctionParity Parity);
