using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class BoundedRadicalTangentProductAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void OneTenthProductProvesOnlyTheSoundBoundedClaims()
    {
        InputExpression input = Product(new BigRational(1, 10));
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);

        Assert.Equal(
            "interval[q:0,1,q:1/10,1]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Domain).Canonical);
        Assert.Equal(
            "points[q:0,q:1/10]",
            Proved<RealSet>(request, semantic, AnalysisFeatures.Zeros).Canonical);
        OptionalValue<ExactReal> yIntercept = Proved<OptionalValue<ExactReal>>(
            request,
            semantic,
            AnalysisFeatures.YIntercept);
        Assert.True(yIntercept.HasValue);
        Assert.Equal("q:0", ExactRealCanonical.Format(yIntercept.Value!));
        Assert.Equal(
            FunctionParity.Neither,
            Proved<FunctionParity>(request, semantic, AnalysisFeatures.Parity));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.VerticalAsymptotes));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes));
        Assert.Empty(Proved<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.ObliqueAsymptotes));
        Periodicity period = Proved<Periodicity>(
            request,
            semantic,
            AnalysisFeatures.Period);
        Assert.Equal(PeriodicityKind.NotPeriodic, period.Kind);
        Assert.Null(period.FundamentalPeriod);

        AssertUnsupported<RealSet>(request, semantic, AnalysisFeatures.Range);
        AssertUnsupported<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Minima);
        AssertUnsupported<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Maxima);
        AssertUnsupported<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.InflectionPoints);
        AssertUnsupported<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity);
    }

    [Fact]
    public void EverySupportedFeatureReplaysWithoutSolverControlFlow()
    {
        InputExpression input = QuotientEndpointProduct();
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);

        Replay<RealSet>(AnalysisFeatures.Domain);
        Replay<FunctionParity>(AnalysisFeatures.Parity);
        Replay<RealSet>(AnalysisFeatures.Zeros);
        Replay<OptionalValue<ExactReal>>(AnalysisFeatures.YIntercept);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.VerticalAsymptotes);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.HorizontalAsymptotes);
        Replay<ImmutableArray<Asymptote>>(AnalysisFeatures.ObliqueAsymptotes);
        Replay<Periodicity>(AnalysisFeatures.Period);
        return;

        void Replay<T>(AnalysisFeatures feature)
        {
            ProofOutcome<T> outcome = Analyze<T>(request, semantic, feature);
            var certificate = Assert.IsType<BoundedRadicalTangentProductProofCertificate>(
                outcome.Certificate);
            Assert.Equal(new BigRational(1, 2), certificate.UpperEndpoint);
            Assert.Contains("upper-endpoint[divide[", certificate.SourceShapeCanonical, StringComparison.Ordinal);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
            Assert.True(BoundedRadicalTangentProductCertificateChecker.Check(
                request,
                semantic,
                certificate,
                ClaimCanonical.ForObject(outcome.Value!),
                new ResourceBudget()));
        }
    }

    [Fact]
    public void NestedExactRationalSourceArithmeticIsAcceptedWithItsSourceProof()
    {
        InputExpression endpoint = Divide(
            Add(Number(BigRational.One), Number(BigRational.One)),
            Multiply(Number(new BigRational(2)), Number(new BigRational(2))));
        InputExpression input = Product(endpoint);
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);

        ProofOutcome<RealSet> outcome = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Domain);
        var certificate = Assert.IsType<BoundedRadicalTangentProductProofCertificate>(
            outcome.Certificate);

        Assert.Equal("interval[q:0,1,q:1/2,1]", outcome.Value!.Canonical);
        Assert.Equal(new BigRational(1, 2), certificate.UpperEndpoint);
        Assert.Contains(
            "upper-endpoint[divide[add[",
            certificate.SourceShapeCanonical,
            StringComparison.Ordinal);
        Assert.Contains(
            ",multiply[",
            certificate.SourceShapeCanonical,
            StringComparison.Ordinal);
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
    }

    [Fact]
    public void ProductionEnginePublishesOnlyCentrallyReplayedBoundedClaims()
    {
        InputExpression input = Product(new BigRational(1, 10));
        AnalysisRequest request = Request(input);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.True(report.Domain.Certificate is
            DomainProofCertificate or
            BoundedRadicalTangentProductProofCertificate);
        Assert.True(CertificateChecker.Check(request, semantic, report.Domain));

        AssertProductionProof(report.Parity);
        AssertProductionProof(report.Zeros);
        AssertProductionProof(report.YIntercept);
        AssertProductionProof(report.VerticalAsymptotes);
        AssertProductionProof(report.HorizontalAsymptotes);
        AssertProductionProof(report.ObliqueAsymptotes);
        AssertProductionProof(report.Period);

        Assert.Equal(ProofState.Unknown, report.Range.State);
        Assert.Equal(ProofState.Unknown, report.Minima.State);
        Assert.Equal(ProofState.Unknown, report.Maxima.State);
        Assert.Equal(ProofState.Unknown, report.InflectionPoints.State);
        Assert.Equal(ProofState.Unknown, report.Monotonicity.State);
        Assert.Equal(UnknownReason.UnsupportedFragment, report.Range.UnknownReason);
        Assert.Equal(UnknownReason.UnsupportedFragment, report.Minima.UnknownReason);
        Assert.Equal(UnknownReason.UnsupportedFragment, report.Maxima.UnknownReason);
        Assert.Equal(UnknownReason.UnsupportedFragment, report.InflectionPoints.UnknownReason);
        Assert.Equal(UnknownReason.UnsupportedFragment, report.Monotonicity.UnknownReason);
        return;

        void AssertProductionProof<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<BoundedRadicalTangentProductProofCertificate>(
                outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }
    }

    [Fact]
    public void RationalEndpointsAndEveryOperandAssociationRemainExact()
    {
        BigRational[] endpoints =
        [
            new(1, 10),
            new(1, 7),
            new(1, 2),
            BigRational.One
        ];
        foreach (BigRational endpoint in endpoints)
        {
            var shapes = new HashSet<string>(StringComparer.Ordinal);
            foreach (InputExpression input in AssociatedProducts(endpoint))
            {
                AnalysisRequest request = Request(input);
                SemanticExpression semantic = Build(input);
                ProofOutcome<RealSet> domain = Analyze<RealSet>(
                    request,
                    semantic,
                    AnalysisFeatures.Domain);
                var certificate = Assert.IsType<BoundedRadicalTangentProductProofCertificate>(
                    domain.Certificate);

                Assert.Equal(endpoint, certificate.UpperEndpoint);
                Assert.Equal(
                    $"interval[q:0,1,q:{endpoint},1]",
                    domain.Value!.Canonical);
                Assert.True(shapes.Add(certificate.SourceShapeCanonical));
                Assert.True(BoundedRadicalTangentProductCertificateChecker.Check(
                    request,
                    semantic,
                    certificate,
                    ClaimCanonical.ForObject(domain.Value),
                    new ResourceBudget()));
            }

            Assert.Equal(12, shapes.Count);
        }
    }

    [Fact]
    public void HiddenHolesAndValueEquivalentShapesFailClosed()
    {
        InputExpression x = Variable();
        InputExpression c = Number(new BigRational(1, 10));
        InputExpression hiddenX = Add(
            x,
            Multiply(
                Number(BigRational.Zero),
                Divide(
                    Number(BigRational.One),
                    Subtract(x, Number(new BigRational(1, 20))))));
        InputExpression quotientTangent = Divide(Sin(x), Cos(x));
        InputExpression endpointWithHiddenReciprocal = Divide(
            Number(BigRational.One),
            Add(
                Number(new BigRational(2)),
                Multiply(
                    Number(BigRational.Zero),
                    Divide(Number(BigRational.One), x))));
        InputExpression endpointWithSelfDivisionHole = Divide(
            Number(BigRational.One),
            Divide(x, x));
        InputExpression[] unsupported =
        [
            Multiply(Product(new BigRational(1, 10)), Divide(x, x)),
            Multiply(
                Multiply(Sqrt(hiddenX), Sqrt(Subtract(c, x))),
                Tan(x)),
            Multiply(
                Multiply(Sqrt(x), Sqrt(Subtract(c, hiddenX))),
                Tan(x)),
            Multiply(
                Multiply(Sqrt(x), Sqrt(Subtract(c, x))),
                quotientTangent),
            Multiply(
                Multiply(Power(x, new BigRational(1, 2)), Sqrt(Subtract(c, x))),
                Tan(x)),
            Multiply(
                Multiply(Sqrt(x), Sqrt(Add(c, x))),
                Tan(x)),
            Multiply(
                Multiply(Sqrt(x), Sqrt(Subtract(c, x))),
                Tan(Add(x, Number(BigRational.One)))),
            Multiply(
                Multiply(Sqrt(x), Sqrt(Subtract(c, x))),
                Sin(x)),
            Product(endpointWithHiddenReciprocal),
            Product(endpointWithSelfDivisionHole),
            Multiply(
                Multiply(Sqrt(x), Sqrt(Subtract(c, x))),
                Tan(x)),
            Multiply(
                Multiply(Sqrt(x), Sqrt(Subtract(c, x))),
                Tan(x))
        ];

        for (int index = 0; index < unsupported.Length; index++)
        {
            AngleUnit angleUnit = index == unsupported.Length - 1
                ? AngleUnit.Degrees
                : AngleUnit.Radians;
            string variable = index == unsupported.Length - 2 ? "t" : "x";
            AnalysisRequest request = Request(unsupported[index], angleUnit, variable);
            Assert.False(BoundedRadicalTangentProductAnalyzer.TryAnalyze(
                request,
                Build(unsupported[index]),
                AnalysisFeatures.Domain,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));
        }
    }

    [Theory]
    [MemberData(nameof(RejectedEndpoints))]
    public void UnsafeOrDegenerateEndpointBoundsFailClosed(object endpointValue)
    {
        var endpoint = Assert.IsType<BigRational>(endpointValue);
        InputExpression input = Product(endpoint);
        AnalysisRequest request = Request(input);

        Assert.False(BoundedRadicalTangentProductAnalyzer.TryAnalyze(
            request,
            Build(input),
            AnalysisFeatures.Domain,
            new ResourceBudget(),
            out ProofOutcome<RealSet> _));
    }

    [Fact]
    public void CertificateReplayRejectsEveryMaterialMutation()
    {
        InputExpression input = QuotientEndpointProduct();
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);
        ProofOutcome<RealSet> outcome = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Domain);
        var certificate = Assert.IsType<BoundedRadicalTangentProductProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);
        InputExpression literalInput = Product(new BigRational(1, 2));
        AnalysisRequest literalRequest = Request(literalInput);
        SemanticExpression literalSemantic = Build(literalInput);
        var literalCertificate = Assert.IsType<BoundedRadicalTangentProductProofCertificate>(
            Analyze<RealSet>(
                literalRequest,
                literalSemantic,
                AnalysisFeatures.Domain).Certificate);

        AssertAccepted(certificate);
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
        AssertRejected(certificate with { UpperEndpoint = new BigRational(1, 9) });
        AssertRejected(certificate with { VariableCanonical = "v:t" });
        AssertRejected(certificate with
        {
            FactorCanonicals = certificate.FactorCanonicals.SetItem(
                0,
                certificate.FactorCanonicals[0] + ":changed")
        });
        AssertRejected(certificate with
        {
            SourceShapeCanonical = certificate.SourceShapeCanonical + ":changed"
        });
        Assert.NotEqual(
            certificate.SourceShapeCanonical,
            literalCertificate.SourceShapeCanonical);
        AssertRejected(certificate with
        {
            SourceShapeCanonical = literalCertificate.SourceShapeCanonical
        });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with { ContinuityCanonical = "true" });
        AssertRejected(certificate with { DifferentiabilityCanonical = "true" });
        AssertRejected(certificate with { DomainCanonical = "reals" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "reals" });
        Assert.False(BoundedRadicalTangentProductCertificateChecker.Check(
            Request(input, AngleUnit.Degrees),
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        Assert.False(BoundedRadicalTangentProductCertificateChecker.Check(
            request,
            semantic with { SourceOperands = [] },
            certificate,
            claim,
            new ResourceBudget()));
        SemanticExpression forgedRegularity = semantic with
        {
            DefinedWhen = Formula.True,
            ContinuousWhen = Formula.True,
            DifferentiableWhen = Formula.True
        };
        Assert.False(BoundedRadicalTangentProductCertificateChecker.Check(
            request,
            forgedRegularity,
            certificate with
            {
                DefinednessCanonical = Formula.True.Canonical,
                ContinuityCanonical = Formula.True.Canonical,
                DifferentiabilityCanonical = Formula.True.Canonical
            },
            claim,
            new ResourceBudget()));
        Assert.False(BoundedRadicalTangentProductCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.ForObject(AllRealSet.Instance),
            new ResourceBudget()));
        return;

        void AssertAccepted(BoundedRadicalTangentProductProofCertificate candidate) =>
            Assert.True(BoundedRadicalTangentProductCertificateChecker.Check(
                request,
                semantic,
                candidate,
                claim,
                new ResourceBudget()));

        void AssertRejected(BoundedRadicalTangentProductProofCertificate candidate)
        {
            Assert.False(BoundedRadicalTangentProductCertificateChecker.Check(
                request,
                semantic,
                candidate,
                claim,
                new ResourceBudget()));
            Assert.False(CertificateChecker.Check(
                request,
                semantic,
                ProofOutcome<RealSet>.Proved(outcome.Value!, candidate)));
        }
    }

    [Fact]
    public void RevisionCancellationAndDeterministicBudgetsFailClosed()
    {
        InputExpression input = QuotientEndpointProduct();
        AnalysisRequest request = Request(input);
        SemanticExpression semantic = Build(input);

        Assert.Throws<AnalysisCancelledException>(() =>
            BoundedRadicalTangentProductAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Domain,
                new ResourceBudget(static () => false),
                out ProofOutcome<RealSet> _));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            BoundedRadicalTangentProductAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Domain,
                exhausted,
                out ProofOutcome<RealSet> _));

        ExactInteger denominator = ExactInteger.One << AnalysisLimits.CoefficientBits;
        InputExpression oversized = Product(new BigRational(ExactInteger.One, denominator));
        AnalysisRequest oversizedRequest = Request(oversized);
        Assert.Throws<BudgetExceededException>(() =>
            BoundedRadicalTangentProductAnalyzer.TryAnalyze(
                oversizedRequest,
                Build(oversized),
                AnalysisFeatures.Domain,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));

        ExactInteger halfLimit = ExactInteger.One << (AnalysisLimits.CoefficientBits / 2);
        InputExpression oversizedFold = Product(
            Divide(
                Number(new BigRational(ExactInteger.One, halfLimit)),
                Number(new BigRational(halfLimit))));
        AnalysisRequest oversizedFoldRequest = Request(oversizedFold);
        Assert.Throws<BudgetExceededException>(() =>
            BoundedRadicalTangentProductAnalyzer.TryAnalyze(
                oversizedFoldRequest,
                Build(oversizedFold),
                AnalysisFeatures.Domain,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));

        ProofOutcome<RealSet> replayOutcome = Analyze<RealSet>(
            request,
            semantic,
            AnalysisFeatures.Domain);
        var replayCertificate = Assert.IsType<BoundedRadicalTangentProductProofCertificate>(
            replayOutcome.Certificate);
        string replayClaim = ClaimCanonical.ForObject(replayOutcome.Value!);
        Assert.Throws<AnalysisCancelledException>(() =>
            BoundedRadicalTangentProductCertificateChecker.Check(
                request,
                semantic,
                replayCertificate,
                replayClaim,
                new ResourceBudget(static () => false)));
        var exhaustedReplay = new ResourceBudget();
        exhaustedReplay.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            BoundedRadicalTangentProductCertificateChecker.Check(
                request,
                semantic,
                replayCertificate,
                replayClaim,
                exhaustedReplay));
    }

    [Fact]
    public void PublicContractAnalyzesTheExactOneHalfSourceQuotient()
    {
        GraphFunctionAnalysisData result = AnalyzePublic(
            "sqrt(x)*sqrt(1/2-x)*tan(x)");

        Assert.Equal("0 ≤ x ≤ 1/2", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = 0 ∨ x = 1/2", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal(
            (int)FunctionPeriodicityType.Unknown,
            result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Period,
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.InflectionPoints,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    public static IEnumerable<object[]> RejectedEndpoints()
    {
        yield return [BigRational.Zero];
        yield return [new BigRational(-1, 10)];
        yield return [new BigRational(11, 10)];
        yield return [new BigRational(3, 2)];
    }

    private static T Proved<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        return Analyze<T>(request, semantic, feature).Value!;
    }

    private static ProofOutcome<T> Analyze<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.True(BoundedRadicalTangentProductAnalyzer.TryAnalyze(
            request,
            semantic,
            feature,
            new ResourceBudget(),
            out ProofOutcome<T> outcome));
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome;
    }

    private static void AssertUnsupported<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.False(BoundedRadicalTangentProductAnalyzer.TryAnalyze(
            request,
            semantic,
            feature,
            new ResourceBudget(),
            out ProofOutcome<T> _));
    }

    private static IEnumerable<InputExpression> AssociatedProducts(BigRational endpoint)
    {
        InputExpression x = Variable();
        InputExpression[] factors =
        [
            Sqrt(x),
            Sqrt(Subtract(Number(endpoint), x)),
            Tan(x)
        ];
        foreach (int[] permutation in Permutations())
        {
            InputExpression first = factors[permutation[0]];
            InputExpression second = factors[permutation[1]];
            InputExpression third = factors[permutation[2]];
            yield return Multiply(Multiply(first, second), third);
            yield return Multiply(first, Multiply(second, third));
        }
    }

    private static IEnumerable<int[]> Permutations()
    {
        yield return [0, 1, 2];
        yield return [0, 2, 1];
        yield return [1, 0, 2];
        yield return [1, 2, 0];
        yield return [2, 0, 1];
        yield return [2, 1, 0];
    }

    private static InputExpression Product(BigRational endpoint)
    {
        return Product(Number(endpoint));
    }

    private static InputExpression QuotientEndpointProduct()
    {
        return Product(
            Divide(
                Number(BigRational.One),
                Number(new BigRational(2))));
    }

    private static InputExpression Product(InputExpression endpoint)
    {
        InputExpression x = Variable();
        return Multiply(
            Multiply(
                Sqrt(x),
                Sqrt(Subtract(endpoint, x))),
            Tan(x));
    }

    private static GraphFunctionAnalysisData AnalyzePublic(string formula)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.All));
        return solver.Analyze(analyzer);
    }

    private static int Bits(params AnalysisType[] features)
    {
        return features
            .Aggregate(0, static (bits, feature) => bits | FeatureBit(feature));
    }

    private static int FeatureBit(AnalysisType type)
    {
        return type switch
        {
            AnalysisType.Domain => 1,
            AnalysisType.Range => 2,
            AnalysisType.Parity => 4,
            AnalysisType.Period => 8,
            AnalysisType.Zeros => 16,
            AnalysisType.YIntercept => 32,
            AnalysisType.Minima => 64,
            AnalysisType.Maxima => 128,
            AnalysisType.InflectionPoints => 256,
            AnalysisType.VerticalAsymptotes => 512,
            AnalysisType.HorizontalAsymptotes => 1024,
            AnalysisType.ObliqueAsymptotes => 2048,
            AnalysisType.Monotonicity => 4096,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AngleUnit angleUnit = AngleUnit.Radians,
        string variable = "x")
    {
        return new AnalysisRequest(expression, AnalysisFeatures.All, angleUnit, variable, static () => true);
    }

    private static SemanticExpression Build(InputExpression input)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(input);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(BigRational value)
    {
        return InputExpression.Number(value, Source);
    }

    private static InputExpression Add(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Add, left, right, Source);
    }

    private static InputExpression Subtract(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);
    }

    private static InputExpression Multiply(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);
    }

    private static InputExpression Divide(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);
    }

    private static InputExpression Power(InputExpression basis, BigRational exponent)
    {
        return InputExpression.Binary(
            InputExpressionKind.Power,
            basis,
            Number(exponent),
            Source);
    }

    private static InputExpression Sqrt(InputExpression argument)
    {
        return InputExpression.Function("sqrt", [argument], Source);
    }

    private static InputExpression Sin(InputExpression argument)
    {
        return InputExpression.Function("sin", [argument], Source);
    }

    private static InputExpression Cos(InputExpression argument)
    {
        return InputExpression.Function("cos", [argument], Source);
    }

    private static InputExpression Tan(InputExpression argument)
    {
        return InputExpression.Function("tan", [argument], Source);
    }
}
