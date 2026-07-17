using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class GuardedConstantAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [MemberData(nameof(ZeroOnTangentExpressions))]
    public void EquivalentZeroOnTangentFormsRetainThePeriodicPunctures(
        object expressionValue)
    {
        var expression = Assert.IsType<InputExpression>(expressionValue);
        (AnalysisRequest request, SemanticExpression semantic) = Build(expression);
        GuardedConstantContext context = Context(request, semantic);

        Assert.True(context.Scalar.IsZero);
        Assert.True(context.Symmetric);
        Assert.True(context.ContainsZero);
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(context.Period));
        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1/2:0,0,pi:1/2:0,0]]",
            context.Domain.Canonical);

        AssertEveryFeatureReplays(request, semantic);
        Assert.Equal(context.Domain.Canonical, Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros).Canonical);
        Assert.Equal("points[q:0]", Proved<RealSet>(request, semantic, AnalysisFeatures.Range).Canonical);
        Assert.Equal(FunctionParity.Both, Proved<FunctionParity>(request, semantic, AnalysisFeatures.Parity));
        Assert.True(Proved<OptionalValue<ExactReal>>(request, semantic, AnalysisFeatures.YIntercept).HasValue);
        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(request, semantic, AnalysisFeatures.Minima));
        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(request, semantic, AnalysisFeatures.Maxima));
        Assert.Empty(Proved<ImmutableArray<FeaturePoint>>(request, semantic, AnalysisFeatures.InflectionPoints));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(request, semantic, AnalysisFeatures.VerticalAsymptotes));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(request, semantic, AnalysisFeatures.ObliqueAsymptotes));
        Assert.Equal(
            "q:0",
            Coordinate(Assert.Single(Proved<ImmutableArray<Asymptote>>(
                request,
                semantic,
                AnalysisFeatures.HorizontalAsymptotes))));
        MonotoneRegion monotone = Assert.Single(Proved<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity));
        Assert.Equal(Monotonicity.Constant, monotone.Direction);
        Assert.Equal(context.Domain.Canonical, monotone.Region.Canonical);
        Assert.Equal(
            "pi:1:0",
            ExactRealCanonical.Format(Proved<Periodicity>(
                request,
                semantic,
                AnalysisFeatures.Period).FundamentalPeriod!));
    }

    [Fact]
    public void SelfDivisionBySineIsOneExactlyOnTheSineNonzeroDomain()
    {
        InputExpression sine = Function("sin", Variable());
        (AnalysisRequest request, SemanticExpression semantic) = Build(Divide(sine, sine));
        GuardedConstantContext context = Context(request, semantic);

        Assert.True(context.Scalar.IsOne);
        Assert.True(context.Symmetric);
        Assert.False(context.ContainsZero);
        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1:0,0,pi:0:0,0]]",
            context.Domain.Canonical);
        Assert.Equal("points[q:1]", Proved<RealSet>(request, semantic, AnalysisFeatures.Range).Canonical);
        Assert.IsType<EmptySet>(Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros));
        Assert.False(Proved<OptionalValue<ExactReal>>(
            request,
            semantic,
            AnalysisFeatures.YIntercept).HasValue);
        Assert.Equal(FunctionParity.Even, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Fact]
    public void SelfDivisionByCosineIsOneExactlyOnTheCosineNonzeroDomain()
    {
        InputExpression cosine = Function("cos", Variable());
        (AnalysisRequest request, SemanticExpression semantic) = Build(Divide(cosine, cosine));
        GuardedConstantContext context = Context(request, semantic);

        Assert.True(context.Scalar.IsOne);
        Assert.True(context.Symmetric);
        Assert.True(context.ContainsZero);
        Assert.Equal(
            "periodic-intervals[pi:1:0,m,m:Z,[pi:-1/2:0,0,pi:1/2:0,0]]",
            context.Domain.Canonical);
        Assert.Equal("points[q:1]", Proved<RealSet>(request, semantic, AnalysisFeatures.Range).Canonical);
        Assert.IsType<EmptySet>(Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros));
        Assert.True(Proved<OptionalValue<ExactReal>>(
            request,
            semantic,
            AnalysisFeatures.YIntercept).HasValue);
        Assert.Equal(FunctionParity.Even, Proved<FunctionParity>(
            request,
            semantic,
            AnalysisFeatures.Parity));
        AssertEveryFeatureReplays(request, semantic);
    }

    [Fact]
    public void IntegerFrequencyPuncturesProduceTheProvedLeastPeriod()
    {
        InputExpression tangent = Function(
            "tan",
            Multiply(Number(2), Variable()));
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            Multiply(Number(0), tangent));
        GuardedConstantContext context = Context(request, semantic);

        Assert.Equal("pi:1/2:0", ExactRealCanonical.Format(context.Period));
        Assert.Equal(
            "pi:1/2:0",
            ExactRealCanonical.Format(Proved<Periodicity>(
                request,
                semantic,
                AnalysisFeatures.Period).FundamentalPeriod!));
        Assert.True(context.Symmetric);
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, "q:180")]
    [InlineData((int)AngleUnit.Grads, "q:200")]
    public void CertificateReplayUsesTheRequestedAngleUnitExactly(
        int angleUnitValue,
        string expectedPeriod)
    {
        InputExpression tangent = Function("tan", Variable());
        InputExpression input = Multiply(Number(0), tangent);
        var request = new AnalysisRequest(
            input,
            AnalysisFeatures.All,
            (AngleUnit)angleUnitValue,
            "x",
            static () => true);
        SemanticExpression semantic = new SemanticGraphBuilder(
            new ResourceBudget()).Build(input);

        AssertEveryFeatureReplays(request, semantic);
        Assert.Equal(
            expectedPeriod,
            ExactRealCanonical.Format(Proved<Periodicity>(
                request,
                semantic,
                AnalysisFeatures.Period).FundamentalPeriod!));
    }

    [Theory]
    [MemberData(nameof(ProductionExpressions))]
    public void ProductionEngineChecksEveryGuardedConstantClaim(object expressionValue)
    {
        var expression = Assert.IsType<InputExpression>(expressionValue);
        var request = new AnalysisRequest(
            expression,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => true);

        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.NotNull(report.Expression);
        AssertProductionProof(report.Domain, allowGeneralDomainCertificate: true);
        AssertProductionProof(report.Range);
        AssertProductionProof(report.Parity);
        AssertProductionProof(report.Zeros);
        AssertProductionProof(report.YIntercept);
        AssertProductionProof(report.Minima);
        AssertProductionProof(report.Maxima);
        AssertProductionProof(report.InflectionPoints);
        AssertProductionProof(report.VerticalAsymptotes);
        AssertProductionProof(report.HorizontalAsymptotes);
        AssertProductionProof(report.ObliqueAsymptotes);
        AssertProductionProof(report.Monotonicity);
        AssertProductionProof(report.Period);
        return;

        void AssertProductionProof<T>(
            ProofOutcome<T> outcome,
            bool allowGeneralDomainCertificate = false)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            if (allowGeneralDomainCertificate)
            {
                Assert.True(outcome.Certificate is DomainProofCertificate or GuardedConstantProofCertificate);
            }
            else
            {
                Assert.IsType<GuardedConstantProofCertificate>(outcome.Certificate);
            }

            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    [Fact]
    public void UnsupportedShiftedPunctureGeometryRemainsUnknown()
    {
        InputExpression shifted = Multiply(
            Number(0),
            Function("tan", Add(Variable(), Number(1))));
        (AnalysisRequest request, SemanticExpression semantic) = Build(shifted);
        var budget = new ResourceBudget();

        Assert.False(GuardedConstantAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Parity,
            budget,
            out ProofOutcome<FunctionParity> _));
    }

    [Fact]
    public void DedicatedCertificateRejectsScalarDomainDefinednessRuleAndClaimMutation()
    {
        InputExpression tangent = Function("tan", Variable());
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            Subtract(tangent, tangent));
        ProofOutcome<RealSet> range = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<GuardedConstantProofCertificate>(range.Certificate);

        Assert.True(Check(request, semantic, range));
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { ScalarCanonical = certificate.ScalarCanonical + ":mutated" });
        AssertRejected(certificate with { DomainCanonical = "reals" });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = "mutated" });
        AssertRejected(certificate with { SubjectCanonical = "mutated" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });
        AssertRejected(certificate with { ClaimCanonical = "empty" });
        Assert.False(GuardedConstantCertificateChecker.Check(
            request with { Features = AnalysisFeatures.Domain },
            semantic,
            certificate,
            ClaimCanonical.ForObject(range.Value!),
            new ResourceBudget()));
        Assert.False(GuardedConstantCertificateChecker.Check(
            request with { Variable = "t" },
            semantic,
            certificate,
            ClaimCanonical.ForObject(range.Value!),
            new ResourceBudget()));
        Assert.False(GuardedConstantCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.ForObject(EmptySet.Instance),
            new ResourceBudget()));

        void AssertRejected(GuardedConstantProofCertificate changed) =>
            Assert.False(GuardedConstantCertificateChecker.Check(
                request,
                semantic,
                changed,
                ClaimCanonical.ForObject(range.Value!),
                new ResourceBudget()));
    }

    [Fact]
    public void CertificateReplayRejectsForgedRewriteRegularityAndExtraPunctures()
    {
        InputExpression tangent = Function("tan", Variable());
        (AnalysisRequest request, SemanticExpression semantic) = Build(
            Subtract(tangent, tangent));
        ProofOutcome<RealSet> range = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Range);
        var certificate = Assert.IsType<GuardedConstantProofCertificate>(range.Certificate);
        string claim = ClaimCanonical.ForObject(range.Value!);

        AssertRejected(semantic with { RewriteHistory = [] }, certificate);
        AssertRejected(semantic with { ContinuousWhen = Formula.False }, certificate);
        AssertRejected(
            semantic with
            {
                RewriteHistory =
                [semantic.RewriteHistory[0] with { Before = "forged" }]
            },
            certificate);

        ValueTerm variable = Build(Variable()).Semantic.Value;
        ValueTerm one = Build(Number(1)).Semantic.Value;
        Formula retainedHole = Formula.Compare(
            variable,
            Comparison.NotEqual,
            one);
        Formula holed = Formula.And(semantic.DefinedWhen, retainedHole);
        SemanticExpression hiddenHole = semantic with
        {
            DefinedWhen = holed,
            ContinuousWhen = holed,
            DifferentiableWhen = holed
        };
        AssertRejected(
            hiddenHole,
            certificate with { DefinednessCanonical = holed.Canonical });

        Formula duplicate = new JunctionFormula(
            true,
            [semantic.DefinedWhen, semantic.DefinedWhen]);
        SemanticExpression duplicated = semantic with
        {
            DefinedWhen = duplicate,
            ContinuousWhen = duplicate,
            DifferentiableWhen = duplicate
        };
        AssertRejected(
            duplicated,
            certificate with { DefinednessCanonical = duplicate.Canonical });
        return;

        void AssertRejected(
            SemanticExpression changed,
            GuardedConstantProofCertificate changedCertificate) =>
            Assert.False(GuardedConstantCertificateChecker.Check(
                request,
                changed,
                changedCertificate,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void CancellationIsCheckedBeforeAnyGuardedConstantClaim()
    {
        InputExpression tangent = Function("tan", Variable());
        var request = new AnalysisRequest(
            Subtract(tangent, tangent),
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => false);
        var budget = new ResourceBudget(request.RevisionIsCurrent);
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(request.Expression);

        Assert.Throws<AnalysisCancelledException>(() =>
            GuardedConstantAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                budget,
                out ProofOutcome<RealSet> _));

        AnalysisRequest validRequest = request with
        {
            RevisionIsCurrent = static () => true
        };
        Assert.True(GuardedConstantAnalyzer.TryAnalyze(
            validRequest,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<GuardedConstantProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);
        Assert.Throws<AnalysisCancelledException>(() =>
            GuardedConstantCertificateChecker.Check(
                validRequest,
                semantic,
                certificate,
                claim,
                new ResourceBudget(static () => false)));
        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            GuardedConstantCertificateChecker.Check(
                validRequest,
                semantic,
                certificate,
                claim,
                exhausted));
    }

    public static IEnumerable<object[]> ZeroOnTangentExpressions()
    {
        InputExpression x = Variable();
        InputExpression tangent = Function("tan", x);
        yield return [Multiply(Number(0), tangent)];
        yield return [Subtract(tangent, tangent)];
        yield return
        [
            Multiply(
                Subtract(Named("pi"), Named("pi")),
                tangent)
        ];
    }

    public static IEnumerable<object[]> ProductionExpressions()
    {
        foreach (object[] values in ZeroOnTangentExpressions())
        {
            yield return values;
        }

        InputExpression sine = Function("sin", Variable());
        yield return [Divide(sine, sine)];
        InputExpression cosine = Function("cos", Variable());
        yield return [Divide(cosine, cosine)];
    }

    private static void AssertEveryFeatureReplays(
        AnalysisRequest request,
        SemanticExpression semantic)
    {
        Replay<RealSet>(AnalysisFeatures.Domain);
        Replay<RealSet>(AnalysisFeatures.Range);
        Replay<FunctionParity>(AnalysisFeatures.Parity);
        Replay<RealSet>(AnalysisFeatures.Zeros);
        Replay<OptionalValue<ExactReal>>(AnalysisFeatures.YIntercept);
        Replay<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Minima);
        Replay<ImmutableArray<FeaturePoint>>(AnalysisFeatures.Maxima);
        Replay<ImmutableArray<FeaturePoint>>(AnalysisFeatures.InflectionPoints);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.VerticalAsymptotes);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.HorizontalAsymptotes);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.ObliqueAsymptotes);
        Replay<ImmutableArray<MonotoneRegion>>(AnalysisFeatures.Monotonicity);
        Replay<Periodicity>(AnalysisFeatures.Period);
        return;

        void Replay<T>(AnalysisFeatures feature)
        {
            ProofOutcome<T> outcome = Analyze<T>(request, semantic, feature);
            Assert.IsType<GuardedConstantProofCertificate>(outcome.Certificate);
            Assert.True(Check(request, semantic, outcome));
        }
    }

    private static bool Check<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        ProofOutcome<T> outcome)
    {
        var certificate = Assert.IsType<GuardedConstantProofCertificate>(outcome.Certificate);
        return GuardedConstantCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget());
    }

    private static T Proved<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature) => Analyze<T>(request, semantic, feature).Value!;

    private static ProofOutcome<T> Analyze<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.True(GuardedConstantAnalyzer.TryAnalyze(
            request,
            semantic,
            feature,
            new ResourceBudget(),
            out ProofOutcome<T> outcome));
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome;
    }

    private static GuardedConstantContext Context(
        AnalysisRequest request,
        SemanticExpression semantic)
    {
        Assert.True(GuardedConstantContext.TryCreate(
            semantic,
            request.Variable,
            request.AngleUnit,
            new ResourceBudget(),
            out GuardedConstantContext context));
        return context;
    }

    private static (AnalysisRequest Request, SemanticExpression Semantic) Build(
        InputExpression expression)
    {
        var request = new AnalysisRequest(
            expression,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => true);
        return (request, new SemanticGraphBuilder(new ResourceBudget()).Build(expression));
    }

    private static string Coordinate(Asymptote asymptote) =>
        ExactRealCanonical.Format(Assert.IsType<SingletonReal>(asymptote.Coordinate).Value);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Named(string name) => InputExpression.Variable(name, Source);

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

    private static InputExpression Function(string name, InputExpression argument) =>
        InputExpression.Function(name, ImmutableArray.Create(argument), Source);
}
