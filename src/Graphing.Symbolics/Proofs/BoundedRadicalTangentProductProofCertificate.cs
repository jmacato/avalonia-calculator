using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Replayable evidence for the exact source family
/// <c>sqrt(x) * sqrt(c - x) * tan(x)</c>, where <c>0 &lt; c &lt;= 1</c>
/// is rational and angles are measured in radians.
/// </summary>
internal sealed record BoundedRadicalTangentProductProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    BigRational UpperEndpoint,
    string VariableCanonical,
    ImmutableArray<string> FactorCanonicals,
    string SourceShapeCanonical,
    string DefinednessCanonical,
    string ContinuityCanonical,
    string DifferentiabilityCanonical,
    string DomainCanonical,
    string Rule)
    : ProofCertificate(ProvenFeature, Subject, Claim);
