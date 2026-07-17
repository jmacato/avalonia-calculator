namespace Graphing.Symbolics;

internal sealed record AffinePhaseSineCompositionProofCertificate(AnalysisFeatures ProvenFeature, string Subject, string Claim, AffinePhaseSineOuterKind OuterKind, AngleUnit AngleUnit, BigRational Frequency, BigRational PhasePiCoefficient, BigRational PhaseConstant, int InnerSign, string PatternCanonical, string DefinednessCanonical, string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);
