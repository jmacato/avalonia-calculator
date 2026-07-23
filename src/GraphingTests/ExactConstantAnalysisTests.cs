using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ExactConstantAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    public static TheoryData<string, string> PublicConstantCases => new()
    {
        { "0", "0" },
        { "3", "3" },
        { "-3", "−3" },
        { "1/2", "1/2" },
        { "pi", "π" },
        { "-pi", "−π" },
        { "e", "e" },
        { "-e", "−e" },
        { "sqrt(2)", "sqrt(2)" },
        { "-sqrt(2)", "−sqrt(2)" },
        { "pi+e", "e + π" },
        { "pi-e", "π − e" },
        { "2*pi", "2π" },
        { "pi/2", "π/2" },
        { "pi*sqrt(2)", "πsqrt(2)" }
    };

    public static TheoryData<string> ExactTranscendentalExpressions =>
    [
        "pi",
        "-pi",
        "e",
        "-e",
        "sqrt(2)",
        "-sqrt(2)",
        "pi+e",
        "pi-e",
        "2*pi",
        "pi/2",
        "pi*sqrt(2)"
    ];

    [Theory]
    [MemberData(nameof(ExactTranscendentalExpressions))]
    public void ExactTranscendentalConstantsProveEveryFeatureWithReplayableCertificates(
        string formula)
    {
        InputExpression expression = ExactExpression(formula);
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.NotNull(report.Expression);
        AssertReplay(request, report.Expression, report.Domain);
        AssertReplay(request, report.Expression, report.Range);
        AssertReplay(request, report.Expression, report.Parity);
        AssertReplay(request, report.Expression, report.Zeros);
        AssertReplay(request, report.Expression, report.YIntercept);
        AssertReplay(request, report.Expression, report.Minima);
        AssertReplay(request, report.Expression, report.Maxima);
        AssertReplay(request, report.Expression, report.InflectionPoints);
        AssertReplay(request, report.Expression, report.VerticalAsymptotes);
        AssertReplay(request, report.Expression, report.HorizontalAsymptotes);
        AssertReplay(request, report.Expression, report.ObliqueAsymptotes);
        AssertReplay(request, report.Expression, report.Monotonicity);
        AssertReplay(request, report.Expression, report.Period);

        Assert.IsType<AllRealSet>(AssertProved(report.Domain));
        Assert.IsType<PointSet>(AssertProved(report.Range));
        Assert.Equal(FunctionParity.Even, AssertProved(report.Parity));
        Assert.IsType<EmptySet>(AssertProved(report.Zeros));
        Assert.True(AssertProved(report.YIntercept).HasValue);
        Assert.Empty(AssertProved(report.Minima));
        Assert.Empty(AssertProved(report.Maxima));
        Assert.Empty(AssertProved(report.InflectionPoints));
        Assert.Empty(AssertProved(report.VerticalAsymptotes));
        Assert.Single(AssertProved(report.HorizontalAsymptotes));
        Assert.Empty(AssertProved(report.ObliqueAsymptotes));
        Assert.Equal(Monotonicity.Constant, Assert.Single(AssertProved(report.Monotonicity)).Direction);
        Assert.Equal(
            PeriodicityKind.PeriodicWithoutFundamentalPeriod,
            AssertProved(report.Period).Kind);

        foreach (ProofCertificate certificate in Certificates(report).Skip(1))
        {
            Assert.Equal(
                TheoremRule.ConstantFunction,
                Assert.IsType<TheoremProofCertificate>(certificate).Theorem);
        }
    }

    [Fact]
    public void ZeroConstantIsBothEvenAndOddAndItsZeroSetIsAllReals()
    {
        AnalysisReport report = AnalysisEngine.Analyze(Request(Number(0), AnalysisFeatures.All));

        Assert.Equal(FunctionParity.Both, AssertProved(report.Parity));
        Assert.IsType<AllRealSet>(AssertProved(report.Zeros));
    }

    [Fact]
    public void PiMinusEulerSignIsProvedExactlyAndCertificateMutationIsRejected()
    {
        InputExpression expression = Subtract(Symbol("pi"), Symbol("e"));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        PointSet range = Assert.IsType<PointSet>(AssertProved(report.Range));
        ExactReal point = Assert.Single(range.Points);
        Assert.Equal(
            "fn:add(pi:1:0,fn:negate(named:e))",
            ExactRealCanonical.Format(point));

        var budget = new ResourceBudget();
        SemanticExpression semantic = new SemanticGraphBuilder(budget).Build(expression);
        Assert.True(ExactScalar.TryCreate(semantic.Value, budget, out ExactScalar scalar));
        Assert.Equal(1, scalar.Sign);

        var certificate = Assert.IsType<TheoremProofCertificate>(report.Range.Certificate);
        TheoremProofCertificate changedEvidence = certificate with
        {
            Parameters = certificate.Parameters.SetItem(1, "tampered-sign-evidence")
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(range, changedEvidence)));

        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(EmptySet.Instance, certificate)));
    }

    [Fact]
    public void ExactConstantProofsRequireTheOriginalAllRealDomain()
    {
        InputExpression retainedHole = Add(
            Symbol("pi"),
            Multiply(Number(0), Divide(Number(1), Variable())));
        AnalysisRequest request = Request(retainedHole, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.NotEqual(AllRealSet.Instance.Canonical, AssertProved(report.Domain).Canonical);
        Assert.Equal(ProofState.Unknown, report.Range.State);
        Assert.Equal(ProofState.Unknown, report.Parity.State);
        Assert.False(AssertProved(report.YIntercept).HasValue);
        Assert.IsType<ExactOriginProofCertificate>(report.YIntercept.Certificate);
        Assert.True(CertificateChecker.Check(
            request,
            report.Expression!,
            report.YIntercept));
        Assert.Equal(ProofState.Unknown, report.HorizontalAsymptotes.State);
        Assert.Equal(ProofState.Unknown, report.Monotonicity.State);
        Assert.Equal(ProofState.Unknown, report.Period.State);

        var budget = new ResourceBudget();
        SemanticExpression semantic = new SemanticGraphBuilder(budget).Build(retainedHole);
        Assert.False(ExactConstantAnalyzer.TryCompute(
            semantic,
            "x",
            AngleUnit.Radians,
            AnalysisFeatures.Range,
            budget,
            out _,
            out _));
    }

    [Fact]
    public void ExactConstantReplayRejectsAClaimOverARetainedHole()
    {
        InputExpression cleanExpression = Symbol("pi");
        AnalysisRequest cleanRequest = Request(cleanExpression, AnalysisFeatures.Range);
        AnalysisReport cleanReport = AnalysisEngine.Analyze(cleanRequest);
        RealSet cleanRange = AssertProved(cleanReport.Range);
        var cleanCertificate = Assert.IsType<TheoremProofCertificate>(cleanReport.Range.Certificate);

        InputExpression retainedHole = Add(
            Symbol("pi"),
            Multiply(Number(0), Divide(Number(1), Variable())));
        AnalysisRequest holedRequest = Request(retainedHole, AnalysisFeatures.Range);
        SemanticExpression holedSemantic = new SemanticGraphBuilder(new ResourceBudget())
            .Build(retainedHole);
        Assert.Equal(cleanReport.Expression!.Value.Canonical, holedSemantic.Value.Canonical);
        Assert.NotEqual(Formula.True.Canonical, holedSemantic.DefinedWhen.Canonical);

        TheoremProofCertificate forgedCertificate = cleanCertificate with
        {
            Parameters = cleanCertificate.Parameters.SetItem(
                2,
                holedSemantic.DefinedWhen.Canonical)
        };
        Assert.False(CertificateChecker.Check(
            holedRequest,
            holedSemantic,
            ProofOutcome<RealSet>.Proved(cleanRange, forgedCertificate)));
    }

    [Fact]
    public void ExactConstantsNeverEnterTheRationalPolynomialCoefficientRing()
    {
        foreach (string formula in ExactFormulae())
        {
            InputExpression expression = ExactExpression(formula);
            var budget = new ResourceBudget();
            SemanticExpression semantic = new SemanticGraphBuilder(budget).Build(expression);
            Assert.False(RationalAnalysisContext.TryCreate(semantic, "x", budget, out _));
        }
    }

    [Theory]
    [MemberData(nameof(PublicConstantCases))]
    public void ExactScalarCorpusHasCompleteWindowsCompatibleFormatting(
        string formula,
        string formattedValue)
    {
        GraphFunctionAnalysisData result = Analyze(formula);

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Equal($"y ∈ {{{formattedValue}}}", result.Range);
        Assert.Equal(
            formula == "0" ? "x ∈ ℝ" : string.Empty,
            result.Zeros);
        Assert.Equal($"y = {formattedValue}", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Equal($"y = {formattedValue}", Assert.Single(result.HorizontalAsymptotes));
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal(
            formula == "0"
                ? (int)FunctionParityType.Unknown
                : (int)FunctionParityType.Even,
            result.Parity);
        Assert.Equal(
            (int)FunctionMonotonicityType.Constant,
            Assert.Single(result.MonotoneIntervals).Value);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    private static IEnumerable<ProofCertificate> Certificates(AnalysisReport report)
    {
        yield return report.Domain.Certificate!;
        yield return report.Range.Certificate!;
        yield return report.Parity.Certificate!;
        yield return report.Zeros.Certificate!;
        yield return report.YIntercept.Certificate!;
        yield return report.Minima.Certificate!;
        yield return report.Maxima.Certificate!;
        yield return report.InflectionPoints.Certificate!;
        yield return report.VerticalAsymptotes.Certificate!;
        yield return report.HorizontalAsymptotes.Certificate!;
        yield return report.ObliqueAsymptotes.Certificate!;
        yield return report.Monotonicity.Certificate!;
        yield return report.Period.Certificate!;
    }

    private static GraphFunctionAnalysisData Analyze(string formula)
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

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static IEnumerable<string> ExactFormulae()
    {
        yield return "pi";
        yield return "-pi";
        yield return "e";
        yield return "-e";
        yield return "sqrt(2)";
        yield return "-sqrt(2)";
        yield return "pi+e";
        yield return "pi-e";
        yield return "2*pi";
        yield return "pi/2";
        yield return "pi*sqrt(2)";
    }

    private static InputExpression ExactExpression(string formula)
    {
        return formula switch
        {
            "pi" => Symbol("pi"),
            "-pi" => Negate(Symbol("pi")),
            "e" => Symbol("e"),
            "-e" => Negate(Symbol("e")),
            "sqrt(2)" => Function("sqrt", Number(2)),
            "-sqrt(2)" => Negate(Function("sqrt", Number(2))),
            "pi+e" => Add(Symbol("pi"), Symbol("e")),
            "pi-e" => Subtract(Symbol("pi"), Symbol("e")),
            "2*pi" => Multiply(Number(2), Symbol("pi")),
            "pi/2" => Divide(Symbol("pi"), Number(2)),
            "pi*sqrt(2)" => Multiply(Symbol("pi"), Function("sqrt", Number(2))),
            _ => throw new ArgumentOutOfRangeException(nameof(formula))
        };
    }

    private static InputExpression Number(int value)
    {
        return InputExpression.Number(new BigRational(value), Source);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Symbol(string name)
    {
        return InputExpression.Variable(name, Source);
    }

    private static InputExpression Negate(InputExpression operand)
    {
        return InputExpression.Unary(InputExpressionKind.Negate, operand, Source);
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

    private static InputExpression Function(string name, params InputExpression[] operands)
    {
        return InputExpression.Function(name, operands.ToImmutableArray(), Source);
    }

    private static T AssertProved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        Assert.NotNull(outcome.Certificate);
        return outcome.Value;
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.True(CertificateChecker.Check(request, expression, outcome));
    }
}
