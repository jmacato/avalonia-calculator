using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record RationalFunctionProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, UnivariatePolynomial Numerator, UnivariatePolynomial Denominator, ImmutableArray<UnivariatePolynomial> OriginalDomainExclusions, ImmutableArray<RootIsolationCertificate> RootIsolations, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
