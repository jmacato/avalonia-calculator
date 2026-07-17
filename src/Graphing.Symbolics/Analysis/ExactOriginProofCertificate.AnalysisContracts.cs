using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record ExactOriginProofCertificate(string Subject, string Claim, string DefinednessCanonical, AngleUnit AngleUnit, string Rule) : ProofCertificate(AnalysisFeatures.YIntercept, Subject, Claim);
