using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record DomainProofCertificate(string Subject, string Claim, string DefinednessFormula, CellDecompositionCertificate? Cells, string Rule) : ProofCertificate(AnalysisFeatures.Domain, Subject, Claim);
