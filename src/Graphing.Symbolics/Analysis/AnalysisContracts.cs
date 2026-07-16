using System.Collections.Immutable;

namespace Graphing.Symbolics;

[Flags]
internal enum AnalysisFeatures : uint
{
    None = 0,
    Domain = 1u << 0,
    Range = 1u << 1,
    Parity = 1u << 2,
    Zeros = 1u << 3,
    YIntercept = 1u << 4,
    Minima = 1u << 5,
    Maxima = 1u << 6,
    InflectionPoints = 1u << 7,
    VerticalAsymptotes = 1u << 8,
    HorizontalAsymptotes = 1u << 9,
    ObliqueAsymptotes = 1u << 10,
    Monotonicity = 1u << 11,
    Period = 1u << 12,
    All = (1u << 13) - 1
}

internal enum AngleUnit
{
    Radians,
    Degrees,
    Grads
}

internal sealed record AnalysisRequest(
    InputExpression Expression,
    AnalysisFeatures Features,
    AngleUnit AngleUnit,
    string Variable,
    Func<bool> RevisionIsCurrent);

internal enum ProofState
{
    Proved,
    Disproved,
    Unknown
}

internal enum UnknownReason
{
    NotRequested,
    UnsupportedFragment,
    BudgetExceeded,
    CertificateRejected,
    ProjectionUnsupported,
    AnalyticBackendUnavailable,
    UndecidableInCurrentTheory
}

internal sealed record ProofOutcome<T>
{
    private ProofOutcome(
        ProofState state,
        T? value,
        UnknownReason? unknownReason,
        ProofCertificate? certificate)
    {
        State = state;
        Value = value;
        UnknownReason = unknownReason;
        Certificate = certificate;
    }

    public ProofState State { get; }

    public T? Value { get; }

    public UnknownReason? UnknownReason { get; }

    public ProofCertificate? Certificate { get; }

    public bool IsProved => State == ProofState.Proved;

    public static ProofOutcome<T> Proved(T value, ProofCertificate certificate) =>
        new(ProofState.Proved, value, null, certificate);

    public static ProofOutcome<T> Disproved(T counterexample, ProofCertificate certificate) =>
        new(ProofState.Disproved, counterexample, null, certificate);

    public static ProofOutcome<T> Unknown(UnknownReason reason) =>
        new(ProofState.Unknown, default, reason, null);
}

internal readonly record struct OptionalValue<T>(bool HasValue, T? Value)
{
    public static OptionalValue<T> None => new(false, default);

    public static OptionalValue<T> Some(T value) => new(true, value);
}

internal enum FunctionParity
{
    Odd,
    Even,
    Both,
    Neither
}

internal enum PeriodicityKind
{
    PeriodicWithFundamentalPeriod,
    PeriodicWithoutFundamentalPeriod,
    NotPeriodic
}

internal sealed record Periodicity(
    PeriodicityKind Kind,
    ExactReal? FundamentalPeriod);

internal abstract record RealFamily;

internal sealed record SingletonReal(ExactReal Value) : RealFamily;

internal sealed record PeriodicReal(
    ExactReal Offset,
    ExactReal Period,
    string Parameter,
    IntegerConstraint Constraint) : RealFamily;

internal sealed record LatticeReal(
    string Expression,
    ImmutableArray<string> Parameters,
    ImmutableArray<string> Predicates) : RealFamily;

internal sealed record FeaturePoint(RealFamily X, ExactReal Y);

internal enum Monotonicity
{
    Increasing,
    Decreasing,
    Constant
}

internal sealed record MonotoneRegion(RealSet Region, Monotonicity Direction);

internal enum AsymptoteOrientation
{
    Vertical,
    Horizontal,
    Oblique
}

internal sealed record Asymptote(
    AsymptoteOrientation Orientation,
    RealFamily Coordinate,
    ExactReal? Slope,
    ExactReal? Intercept);

internal sealed record AnalysisReport(
    SemanticExpression? Expression,
    ProofOutcome<RealSet> Domain,
    ProofOutcome<RealSet> Range,
    ProofOutcome<FunctionParity> Parity,
    ProofOutcome<RealSet> Zeros,
    ProofOutcome<OptionalValue<ExactReal>> YIntercept,
    ProofOutcome<ImmutableArray<FeaturePoint>> Minima,
    ProofOutcome<ImmutableArray<FeaturePoint>> Maxima,
    ProofOutcome<ImmutableArray<FeaturePoint>> InflectionPoints,
    ProofOutcome<ImmutableArray<Asymptote>> VerticalAsymptotes,
    ProofOutcome<ImmutableArray<Asymptote>> HorizontalAsymptotes,
    ProofOutcome<ImmutableArray<Asymptote>> ObliqueAsymptotes,
    ProofOutcome<ImmutableArray<MonotoneRegion>> Monotonicity,
    ProofOutcome<Periodicity> Period,
    long ChargedWorkUnits);

internal abstract record ProofCertificate(
    AnalysisFeatures Feature,
    string SubjectCanonical,
    string ClaimCanonical);

internal sealed record DomainProofCertificate(
    string Subject,
    string Claim,
    string DefinednessFormula,
    CellDecompositionCertificate? Cells,
    string Rule) : ProofCertificate(AnalysisFeatures.Domain, Subject, Claim);

internal sealed record RationalFunctionProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    UnivariatePolynomial Numerator,
    UnivariatePolynomial Denominator,
    ImmutableArray<UnivariatePolynomial> OriginalDomainExclusions,
    ImmutableArray<RootIsolationCertificate> RootIsolations,
    string Rule) : ProofCertificate(ProvenFeature, Subject, Claim);

internal enum TheoremRule
{
    ConstantFunction,
    AffineSine,
    AffineCosine,
    AffineTangent,
    TrigonometricPolynomial,
    IntegralPowerDomain,
    VariablePowerDomain,
    TangentIntegerLattice,
    InversePrimitive,
    ElementaryPrimitive,
    OddRootPrimitive,
    SemialgebraicCellDecomposition
}

internal sealed record TheoremProofCertificate(
    AnalysisFeatures ProvenFeature,
    string Subject,
    string Claim,
    TheoremRule Theorem,
    ImmutableArray<string> Parameters) : ProofCertificate(ProvenFeature, Subject, Claim);

internal static class AnalysisReportFactory
{
    public static AnalysisReport Unknown(
        SemanticExpression? expression,
        AnalysisFeatures requested,
        UnknownReason reason,
        long work) =>
        new(
            expression,
            UnknownFor<RealSet>(requested, AnalysisFeatures.Domain, reason),
            UnknownFor<RealSet>(requested, AnalysisFeatures.Range, reason),
            UnknownFor<FunctionParity>(requested, AnalysisFeatures.Parity, reason),
            UnknownFor<RealSet>(requested, AnalysisFeatures.Zeros, reason),
            UnknownFor<OptionalValue<ExactReal>>(requested, AnalysisFeatures.YIntercept, reason),
            UnknownFor<ImmutableArray<FeaturePoint>>(requested, AnalysisFeatures.Minima, reason),
            UnknownFor<ImmutableArray<FeaturePoint>>(requested, AnalysisFeatures.Maxima, reason),
            UnknownFor<ImmutableArray<FeaturePoint>>(requested, AnalysisFeatures.InflectionPoints, reason),
            UnknownFor<ImmutableArray<Asymptote>>(requested, AnalysisFeatures.VerticalAsymptotes, reason),
            UnknownFor<ImmutableArray<Asymptote>>(requested, AnalysisFeatures.HorizontalAsymptotes, reason),
            UnknownFor<ImmutableArray<Asymptote>>(requested, AnalysisFeatures.ObliqueAsymptotes, reason),
            UnknownFor<ImmutableArray<MonotoneRegion>>(requested, AnalysisFeatures.Monotonicity, reason),
            UnknownFor<Periodicity>(requested, AnalysisFeatures.Period, reason),
            work);

    private static ProofOutcome<T> UnknownFor<T>(
        AnalysisFeatures requested,
        AnalysisFeatures feature,
        UnknownReason reason) =>
        ProofOutcome<T>.Unknown(requested.HasFlag(feature) ? reason : UnknownReason.NotRequested);
}
