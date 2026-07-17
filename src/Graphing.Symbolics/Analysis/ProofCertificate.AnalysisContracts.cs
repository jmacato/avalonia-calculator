using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal abstract record ProofCertificate(AnalysisFeatures Feature, string SubjectCanonical, string ClaimCanonical);
