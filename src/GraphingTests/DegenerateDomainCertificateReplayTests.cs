using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class DegenerateDomainCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void EmptyDomainWithNonSemialgebraicValueReplaysEveryFeature()
    {
        InputExpression x = Variable();
        InputExpression input = Divide(Sin(x), Number(0));
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Assert.Contains("sin", semantic.Value.Canonical, StringComparison.Ordinal);
        Assert.IsType<EmptySet>(AssertProved(report.Domain));
        Assert.IsType<EmptySet>(AssertProved(report.Range));
        Assert.Equal(FunctionParity.Both, AssertProved(report.Parity));
        Assert.IsType<EmptySet>(AssertProved(report.Zeros));
        Assert.False(AssertProved(report.YIntercept).HasValue);
        Assert.Empty(AssertProved(report.Minima));
        Assert.Empty(AssertProved(report.Maxima));
        Assert.Empty(AssertProved(report.InflectionPoints));
        Assert.Empty(AssertProved(report.VerticalAsymptotes));
        Assert.Empty(AssertProved(report.HorizontalAsymptotes));
        Assert.Empty(AssertProved(report.ObliqueAsymptotes));
        Assert.Empty(AssertProved(report.Monotonicity));
        Assert.Equal(
            PeriodicityKind.PeriodicWithoutFundamentalPeriod,
            AssertProved(report.Period).Kind);

        AssertAllEngineFeatureReplays(request, semantic, report);
        AssertDirectDomainReplay(request, semantic, EmptySet.Instance);
    }

    [Fact]
    public void RationalIsolatedPointWithNonSemialgebraicValueReplaysEveryFeature()
    {
        InputExpression x = Variable();
        InputExpression isolatedGuard = Multiply(
            Number(0),
            Sqrt(Negate(Power(x, Number(2)))));
        InputExpression input = Add(Sin(x), isolatedGuard);
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Assert.Equal(Build(Sin(Variable())).Value.Canonical, semantic.Value.Canonical);
        AssertPoint(AssertProved(report.Domain), BigRational.Zero);
        AssertPoint(AssertProved(report.Range), BigRational.Zero);
        Assert.Equal(FunctionParity.Both, AssertProved(report.Parity));
        AssertPoint(AssertProved(report.Zeros), BigRational.Zero);
        OptionalValue<ExactReal> intercept = AssertProved(report.YIntercept);
        Assert.True(intercept.HasValue);
        Assert.Equal("q:0", ExactRealCanonical.Format(intercept.Value!));
        Assert.Empty(AssertProved(report.Minima));
        Assert.Empty(AssertProved(report.Maxima));
        Assert.Empty(AssertProved(report.InflectionPoints));
        Assert.Empty(AssertProved(report.VerticalAsymptotes));
        Assert.Empty(AssertProved(report.HorizontalAsymptotes));
        Assert.Empty(AssertProved(report.ObliqueAsymptotes));
        Assert.Empty(AssertProved(report.Monotonicity));
        Assert.Equal(PeriodicityKind.NotPeriodic, AssertProved(report.Period).Kind);

        AssertAllEngineFeatureReplays(request, semantic, report);
        AssertDirectDomainReplay(request, semantic, report.Domain.Value!);
    }

    [Fact]
    public void ReplayRejectsMetadataClaimsDefinednessAndDeletedLegacyDiscriminators()
    {
        InputExpression input = RationalIsolatedSine();
        AnalysisRequest request = Request(input, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
        RealSet range = AssertProved(report.Range);
        var certificate = Assert.IsType<TheoremProofCertificate>(report.Range.Certificate);

        AssertRejected(certificate with
        {
            Parameters = certificate.Parameters.SetItem(1, EmptySet.Instance.Canonical)
        });
        AssertRejected(certificate with
        {
            Parameters = certificate.Parameters.SetItem(2, "v:y")
        });
        AssertRejected(certificate with
        {
            Parameters = certificate.Parameters.SetItem(0, "principal-square-root")
        });
        AssertRejected(certificate with { Theorem = TheoremRule.ConstantFunction });
        AssertRejected(certificate with { Subject = "v:y" });
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Parameters = default });

        SemanticExpression everywhereDefined = semantic with
        {
            DefinedWhen = Formula.True
        };
        var everywhereDefinedCertificate = new TheoremProofCertificate(
            AnalysisFeatures.Range,
            everywhereDefined.Value.Canonical,
            certificate.Claim,
            TheoremRule.SemialgebraicCellDecomposition,
            [
                "zero-dimensional-domain",
                AllRealSet.Instance.Canonical,
                everywhereDefined.Value.Canonical
            ]);
        Assert.False(DegenerateDomainCertificateReplay.Check(
            request,
            everywhereDefined,
            everywhereDefinedCertificate,
            certificate.Claim,
            new ResourceBudget()));

        RealSet forgedRange = RealSets.Points(
            [new RationalReal(BigRational.One)]);
        var forgedClaim = new TheoremProofCertificate(
            AnalysisFeatures.Range,
            semantic.Value.Canonical,
            ClaimCanonical.For(forgedRange),
            TheoremRule.SemialgebraicCellDecomposition,
            certificate.Parameters);
        Assert.False(CertificateChecker.Check(
            request,
            semantic,
            ProofOutcome<RealSet>.Proved(forgedRange, forgedClaim)));

        RealSet domain = AssertProved(report.Domain);
        foreach (ImmutableArray<string> legacyParameters in new[]
                 {
                     ImmutableArray.Create("absolute-affine", "1", "0"),
                     ImmutableArray.Create("principal-square-root")
                 })
        {
            var legacy = new TheoremProofCertificate(
                AnalysisFeatures.Domain,
                semantic.Value.Canonical,
                ClaimCanonical.For(domain),
                TheoremRule.SemialgebraicCellDecomposition,
                legacyParameters);
            Assert.False(CertificateChecker.Check(
                request,
                semantic,
                ProofOutcome<RealSet>.Proved(domain, legacy)));
        }

        void AssertRejected(TheoremProofCertificate changed)
        {
            Assert.False(DegenerateDomainCertificateReplay.Check(
                request,
                semantic,
                changed,
                certificate.Claim,
                new ResourceBudget()));
            Assert.False(CertificateChecker.Check(
                request,
                semantic,
                ProofOutcome<RealSet>.Proved(range, changed)));
        }
    }

    [Fact]
    public void ReplayRejectsAnAlgebraicIsolatedPointItCannotEvaluateRationally()
    {
        InputExpression x = Variable();
        InputExpression difference = Subtract(Power(x, Number(2)), Number(2));
        InputExpression algebraicEquality = Multiply(
            Number(0),
            Sqrt(Negate(Power(difference, Number(2)))));
        InputExpression positiveRestriction = Multiply(Number(0), Sqrt(x));
        InputExpression input = Add(
            Sin(x),
            Add(algebraicEquality, positiveRestriction));
        AnalysisRequest request = Request(input, AnalysisFeatures.Domain);
        SemanticExpression semantic = Build(input);

        Assert.True(PolynomialFormulaConverter.TryConvert(
            semantic.DefinedWhen,
            request.Variable,
            new ResourceBudget(),
            out PolynomialFormula formula));
        CellDecompositionCertificate cells = CellDecomposer.Decompose(
            formula,
            new ResourceBudget());
        var pointSet = Assert.IsType<PointSet>(cells.Result);
        Assert.IsType<AlgebraicReal>(Assert.Single(pointSet.Points));

        var certificate = new TheoremProofCertificate(
            AnalysisFeatures.Domain,
            semantic.Value.Canonical,
            ClaimCanonical.For(cells.Result),
            TheoremRule.SemialgebraicCellDecomposition,
            [
                "zero-dimensional-domain",
                cells.Result.Canonical,
                semantic.Value.Canonical
            ]);
        Assert.False(DegenerateDomainCertificateReplay.Check(
            request,
            semantic,
            certificate,
            certificate.Claim,
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            request,
            semantic,
            ProofOutcome<RealSet>.Proved(cells.Result, certificate)));
    }

    [Fact]
    public void ReplayHonorsCancellationAndDeterministicWorkBudget()
    {
        InputExpression input = RationalIsolatedSine();
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
        var certificate = Assert.IsType<TheoremProofCertificate>(report.Range.Certificate);

        Assert.Throws<AnalysisCancelledException>(() =>
            DegenerateDomainCertificateReplay.Check(
                request,
                semantic,
                certificate,
                certificate.Claim,
                new ResourceBudget(static () => false)));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            DegenerateDomainCertificateReplay.Check(
                request,
                semantic,
                certificate,
                certificate.Claim,
                exhausted));
    }

    private static void AssertAllEngineFeatureReplays(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisReport report)
    {
        AssertDegenerateReplay(request, semantic, report.Range);
        AssertDegenerateReplay(request, semantic, report.Parity);
        AssertDegenerateReplay(request, semantic, report.Zeros);
        AssertDegenerateReplay(request, semantic, report.YIntercept);
        AssertDegenerateReplay(request, semantic, report.Minima);
        AssertDegenerateReplay(request, semantic, report.Maxima);
        AssertDegenerateReplay(request, semantic, report.InflectionPoints);
        AssertDegenerateReplay(request, semantic, report.VerticalAsymptotes);
        AssertDegenerateReplay(request, semantic, report.HorizontalAsymptotes);
        AssertDegenerateReplay(request, semantic, report.ObliqueAsymptotes);
        AssertDegenerateReplay(request, semantic, report.Monotonicity);
        AssertDegenerateReplay(request, semantic, report.Period);
    }

    private static void AssertDirectDomainReplay(
        AnalysisRequest request,
        SemanticExpression semantic,
        RealSet expected)
    {
        Assert.True(TrigonometricAndLatticeAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Domain,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        Assert.Equal(expected.Canonical, AssertProved(outcome).Canonical);
        AssertDegenerateReplay(request, semantic, outcome);
    }

    private static void AssertDegenerateReplay<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);
        Assert.Equal(
            TheoremRule.SemialgebraicCellDecomposition,
            certificate.Theorem);
        Assert.Equal("zero-dimensional-domain", certificate.Parameters[0]);
        Assert.Equal(3, certificate.Parameters.Length);
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
    }

    private static void AssertPoint(RealSet set, BigRational expected)
    {
        var points = Assert.IsType<PointSet>(set);
        var point = Assert.IsType<RationalReal>(Assert.Single(points.Points));
        Assert.Equal(expected, point.Value);
    }

    private static T AssertProved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return Assert.IsAssignableFrom<T>(outcome.Value);
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features) =>
        new(expression, features, AngleUnit.Radians, "x", static () => true);

    private static SemanticExpression Build(InputExpression expression) =>
        new SemanticGraphBuilder(new ResourceBudget()).Build(expression);

    private static InputExpression RationalIsolatedSine()
    {
        InputExpression x = Variable();
        return Add(
            Sin(x),
            Multiply(
                Number(0),
                Sqrt(Negate(Power(x, Number(2))))));
    }

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Power(InputExpression basis, InputExpression exponent) =>
        InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);

    private static InputExpression Negate(InputExpression value) =>
        InputExpression.Unary(InputExpressionKind.Negate, value, Source);

    private static InputExpression Sin(InputExpression argument) => Function("sin", argument);

    private static InputExpression Sqrt(InputExpression argument) => Function("sqrt", argument);

    private static InputExpression Function(string name, InputExpression argument) =>
        InputExpression.Function(name, [argument], Source);
}
