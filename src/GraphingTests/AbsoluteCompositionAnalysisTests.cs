using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AbsoluteCompositionAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void PriorityAbsoluteCompositionsHaveCompleteExactAnalysis()
    {
        InputExpression x = Variable();

        AnalysisReport absoluteSine = Analyze(Abs(Sin(x)));
        Assert.Equal("interval[q:0,1,q:1,1]", Proved(absoluteSine.Range).Canonical);
        Assert.IsType<PeriodicPointSet>(Proved(absoluteSine.Zeros));
        Assert.Single(Proved(absoluteSine.Minima));
        Assert.Single(Proved(absoluteSine.Maxima));
        Assert.Empty(Proved(absoluteSine.InflectionPoints));
        Assert.Equal(FunctionParity.Even, Proved(absoluteSine.Parity));
        Assert.Equal(2, Proved(absoluteSine.Monotonicity).Length);
        AssertPeriod(absoluteSine, "pi:1:0");

        AnalysisReport absoluteSinh = Analyze(Abs(Sinh(x)));
        Assert.Equal("interval[q:0,1,+inf,0]", Proved(absoluteSinh.Range).Canonical);
        Assert.Equal("points[q:0]", Proved(absoluteSinh.Zeros).Canonical);
        AssertSingletonPoint(Assert.Single(Proved(absoluteSinh.Minima)), "q:0", "q:0");
        Assert.Empty(Proved(absoluteSinh.Maxima));
        Assert.Empty(Proved(absoluteSinh.InflectionPoints));
        Assert.Equal(2, Proved(absoluteSinh.Monotonicity).Length);
        AssertNotPeriodic(absoluteSinh);

        AnalysisReport sineAbsolute = Analyze(Sin(Abs(x)));
        Assert.Equal("interval[q:-1,1,q:1,1]", Proved(sineAbsolute.Range).Canonical);
        Assert.IsType<PeriodicPointSet>(Proved(sineAbsolute.Zeros));
        Assert.Equal(3, Proved(sineAbsolute.Minima).Length);
        Assert.Equal(2, Proved(sineAbsolute.Maxima).Length);
        Assert.Equal(2, Proved(sineAbsolute.InflectionPoints).Length);
        Assert.Equal(6, Proved(sineAbsolute.Monotonicity).Length);
        Assert.Equal(FunctionParity.Even, Proved(sineAbsolute.Parity));
        AssertNotPeriodic(sineAbsolute);

        AnalysisReport coshAbsolute = Analyze(Cosh(Abs(x)));
        Assert.Equal("interval[q:1,1,+inf,0]", Proved(coshAbsolute.Range).Canonical);
        Assert.True(Proved(coshAbsolute.Zeros).IsEmpty);
        AssertSingletonPoint(Assert.Single(Proved(coshAbsolute.Minima)), "q:0", "q:1");
        Assert.Empty(Proved(coshAbsolute.Maxima));
        Assert.Empty(Proved(coshAbsolute.InflectionPoints));
        Assert.Equal(FunctionParity.Even, Proved(coshAbsolute.Parity));
        Assert.Equal(2, Proved(coshAbsolute.Monotonicity).Length);
        AssertNotPeriodic(coshAbsolute);
    }

    [Fact]
    public void SupportedPrimitiveAndAffineCorpusUsesDedicatedReplayableProofs()
    {
        InputExpression x = Variable();
        InputExpression affine = Subtract(Multiply(Number(2), x), Number(1));
        (string Name, InputExpression Expression)[] cases =
        [
            ("abs(sin(2x-1))", Abs(Sin(affine))),
            ("abs(cos(2x-1))", Abs(Cos(affine))),
            ("abs(sinh(2x-1))", Abs(Sinh(affine))),
            ("abs(cosh(2x-1))", Abs(Cosh(affine))),
            ("abs(tanh(2x-1))", Abs(Tanh(affine))),
            ("sin(abs(2x-1))", Sin(Abs(affine))),
            ("cos(abs(2x-1))", Cos(Abs(affine))),
            ("sinh(abs(2x-1))", Sinh(Abs(affine))),
            ("cosh(abs(2x-1))", Cosh(Abs(affine))),
            ("tanh(abs(2x-1))", Tanh(Abs(affine)))
        ];

        foreach ((string name, InputExpression expression) in cases)
        {
            AnalysisRequest request = Request(expression, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);
            AssertAllProved(report, name);
            AssertDedicatedCertificates(report, name);
            AssertAllCertificatesReplay(request, report, name);
        }
    }

    [Fact]
    public void ScalingShiftingAndReflectionPreserveExactClaims()
    {
        InputExpression x = Variable();
        InputExpression reflectedArgument = Add(Multiply(Number(-3), x), Number(1));
        InputExpression normalizedArgument = Subtract(Multiply(Number(3), x), Number(1));

        AnalysisReport reflected = Analyze(Abs(Multiply(Number(-2), Sin(reflectedArgument))));
        AnalysisReport normalized = Analyze(Abs(Multiply(Number(2), Sin(normalizedArgument))));
        AssertSameClaims(normalized, reflected);
        Assert.Equal("interval[q:0,1,q:2,1]", Proved(reflected.Range).Canonical);
        Assert.Equal(FunctionParity.Neither, Proved(reflected.Parity));
        AssertPeriod(reflected, "pi:1/3:0");

        AnalysisReport innerReflected = Analyze(Cosh(Abs(Add(Multiply(Number(-2), x), Number(1)))));
        AnalysisReport innerNormalized = Analyze(Cosh(Abs(Subtract(Multiply(Number(2), x), Number(1)))));
        AssertSameClaims(innerNormalized, innerReflected);
        AssertSingletonPoint(Assert.Single(Proved(innerReflected.Minima)), "q:1/2", "q:1");
        Assert.Equal(FunctionParity.Neither, Proved(innerReflected.Parity));
    }

    [Fact]
    public void GuardedSquareAndNestedAbsoluteRewritesAreMetamorphic()
    {
        InputExpression x = Variable();
        InputExpression affine = Subtract(Multiply(Number(2), x), Number(1));
        InputExpression sine = Sin(affine);

        AnalysisReport direct = Analyze(Abs(sine));
        AnalysisReport nested = Analyze(Abs(Abs(sine)));
        AnalysisReport squareRootSquare = Analyze(Sqrt(Power(sine, 2)));
        AssertSameClaims(direct, nested);
        AssertSameClaims(direct, squareRootSquare);

        AnalysisReport directInner = Analyze(Sin(Abs(affine)));
        AnalysisReport nestedInner = Analyze(Sin(Abs(Abs(affine))));
        AssertSameClaims(directInner, nestedInner);
    }

    [Fact]
    public void DegreeAndGradQuarterTurnsReceiveProvedParityClassification()
    {
        InputExpression x = Variable();

        AnalysisReport absoluteDegreeQuarterTurn = Analyze(
            Abs(Sin(Add(x, Number(90)))),
            AngleUnit.Degrees);
        Assert.Equal(FunctionParity.Even, Proved(absoluteDegreeQuarterTurn.Parity));

        AnalysisReport cosineDegreeQuarterTurn = Analyze(
            Cos(Abs(Add(x, Number(90)))),
            AngleUnit.Degrees);
        Assert.Equal(FunctionParity.Odd, Proved(cosineDegreeQuarterTurn.Parity));

        AnalysisReport cosineDegreeHalfTurn = Analyze(
            Cos(Abs(Add(x, Number(180)))),
            AngleUnit.Degrees);
        Assert.Equal(FunctionParity.Even, Proved(cosineDegreeHalfTurn.Parity));

        AnalysisReport cosineGradQuarterTurn = Analyze(
            Cos(Abs(Add(x, Number(100)))),
            AngleUnit.Grads);
        Assert.Equal(FunctionParity.Odd, Proved(cosineGradQuarterTurn.Parity));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 90)]
    [InlineData(2, 100)]
    public void IndependentReplayCoversEveryFeatureAcrossAngleUnits(
        int angleUnitValue,
        int phase)
    {
        var angleUnit = (AngleUnit)angleUnitValue;
        InputExpression x = Variable();
        InputExpression affine = Add(Multiply(Number(2), x), Number(phase));
        InputExpression[] cases =
        [
            Abs(Multiply(Number(-3), Sin(affine))),
            Cos(Abs(affine)),
            Sin(Abs(affine))
        ];

        foreach (InputExpression input in cases)
        {
            AnalysisRequest request = Request(input, AnalysisFeatures.All, angleUnit);
            AnalysisReport report = AnalysisEngine.Analyze(request);
            string label = $"{angleUnit}:{input.Kind}";
            AssertAllProved(report, label);
            AssertAllCertificatesReplay(request, report, label);
        }
    }

    [Fact]
    public void OriginalDomainHolePreventsWholeLineTheoremsButNotOriginSubstitution()
    {
        InputExpression x = Variable();
        InputExpression retainedHole = Add(
            Abs(Sin(x)),
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(x, Number(3)))));
        AnalysisRequest request = Request(retainedHole, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(
            "union[interval[-inf,0,q:3,0],interval[q:3,0,+inf,0]]",
            Proved(report.Domain).Canonical);
        OptionalValue<ExactReal> intercept = Proved(report.YIntercept);
        Assert.True(intercept.HasValue);
        Assert.Equal("q:0", ExactRealCanonical.Format(intercept.Value!));
        Assert.IsType<ExactOriginProofCertificate>(report.YIntercept.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.YIntercept));

        foreach (ProofOutcome<object> outcome in BoxWholeLineOutcomes(report))
        {
            Assert.Equal(ProofState.Unknown, outcome.State);
            Assert.Null(outcome.Certificate);
        }
    }

    [Fact]
    public void DedicatedCertificateReplayRejectsPatternFormRuleFeatureAndClaimMutations()
    {
        InputExpression expression = Abs(Sin(Subtract(Multiply(Number(2), Variable()), Number(1))));
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet value = Proved(report.Range);
        var certificate = Assert.IsType<AbsoluteCompositionProofCertificate>(report.Range.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));

        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":changed" });
        AssertRejected(certificate with
        {
            Form = AbsoluteCompositionForm.FunctionOfAbsoluteAffine
        });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Claim = "empty" });

        void AssertRejected(AbsoluteCompositionProofCertificate changed) =>
            Assert.False(CertificateChecker.Check(
                request,
                report.Expression!,
                ProofOutcome<RealSet>.Proved(value, changed)));
    }

    [Fact]
    public void IndependentReplayRejectsWrongUnitsAndRetainedDomainHoles()
    {
        InputExpression x = Variable();
        InputExpression input = Abs(Sin(Add(x, Number(90))));
        AnalysisRequest degreeRequest = Request(
            input,
            AnalysisFeatures.Period,
            AngleUnit.Degrees);
        AnalysisReport degreeReport = AnalysisEngine.Analyze(degreeRequest);
        Periodicity degreePeriod = Proved(degreeReport.Period);
        var degreeCertificate = Assert.IsType<AbsoluteCompositionProofCertificate>(
            degreeReport.Period.Certificate);
        string degreeClaim = ClaimCanonical.For(degreePeriod);

        Assert.False(AbsoluteCompositionCertificateChecker.Check(
            Request(input, AnalysisFeatures.Period, AngleUnit.Radians),
            degreeReport.Expression!,
            degreeCertificate,
            degreeClaim,
            new ResourceBudget()));
        Assert.False(AbsoluteCompositionCertificateChecker.Check(
            Request(input, AnalysisFeatures.Period, AngleUnit.Grads),
            degreeReport.Expression!,
            degreeCertificate,
            degreeClaim,
            new ResourceBudget()));

        InputExpression baseline = Abs(Sin(x));
        AnalysisRequest baselineRequest = Request(baseline, AnalysisFeatures.Range);
        AnalysisReport baselineReport = AnalysisEngine.Analyze(baselineRequest);
        RealSet range = Proved(baselineReport.Range);
        var rangeCertificate = Assert.IsType<AbsoluteCompositionProofCertificate>(
            baselineReport.Range.Certificate);
        InputExpression retainedHole = Add(
            baseline,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(x, Number(3)))));
        AnalysisRequest hiddenRequest = Request(retainedHole, AnalysisFeatures.Range);
        SemanticExpression hiddenSemantic = new SemanticGraphBuilder(new ResourceBudget()).Build(
            retainedHole);
        Assert.Equal(baselineReport.Expression!.Value.Canonical, hiddenSemantic.Value.Canonical);
        Assert.NotEqual(
            baselineReport.Expression.DefinedWhen.Canonical,
            hiddenSemantic.DefinedWhen.Canonical);
        Assert.False(AbsoluteCompositionCertificateChecker.Check(
            hiddenRequest,
            hiddenSemantic,
            rangeCertificate,
            ClaimCanonical.For(range),
            new ResourceBudget()));
    }

    [Fact]
    public void IndependentReplayHonorsBudgetAndCancellation()
    {
        InputExpression input = Abs(Sin(Subtract(Multiply(Number(2), Variable()), Number(1))));
        AnalysisRequest request = Request(input, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = Proved(report.Range);
        var certificate = Assert.IsType<AbsoluteCompositionProofCertificate>(
            report.Range.Certificate);
        string claim = ClaimCanonical.For(range);

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AbsoluteCompositionCertificateChecker.Check(
                request,
                report.Expression!,
                certificate,
                claim,
                exhausted));

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            AbsoluteCompositionCertificateChecker.Check(
                request,
                report.Expression!,
                certificate,
                claim,
                cancelled));
    }

    [Fact]
    public void WorkAccountingIsDeterministicAndRevisionCancellationPropagates()
    {
        InputExpression expression = Sin(Abs(Subtract(Multiply(Number(7), Variable()), Number(3))));
        AnalysisReport first = Analyze(expression);
        AnalysisReport second = Analyze(expression);
        Assert.Equal(first.ChargedWorkUnits, second.ChargedWorkUnits);

        AnalysisRequest cancelled = new(
            expression,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => false);
        Assert.Throws<AnalysisCancelledException>(() => AnalysisEngine.Analyze(cancelled));
    }

    [Fact]
    public void PublicAdapterProjectsCertifiedResultsThroughWindowsCompatibility()
    {
        (string Formula, int TooComplex)[] cases =
        [
            ("abs(sin(x))", 2 | 64 | 128 | 4096),
            ("abs(sinh(x))", 2 | 4 | 1024 | 2048 | 4096),
            ("sin(abs(x))", 2 | 8 | 64 | 128 | 256 | 4096),
            ("cosh(abs(x))", 2 | 1024 | 2048 | 4096)
        ];

        foreach ((string formula, int tooComplex) in cases)
        {
            GraphFunctionAnalysisData analysis = AnalyzePublic(formula);
            Assert.Equal("x ∈ ℝ", analysis.Domain);
            Assert.Equal(tooComplex, analysis.TooComplexFeatures);
        }
    }

    private static IEnumerable<ProofOutcome<object>> BoxWholeLineOutcomes(AnalysisReport report)
    {
        yield return Box(report.Range);
        yield return Box(report.Parity);
        yield return Box(report.Zeros);
        yield return Box(report.Minima);
        yield return Box(report.Maxima);
        yield return Box(report.InflectionPoints);
        yield return Box(report.VerticalAsymptotes);
        yield return Box(report.HorizontalAsymptotes);
        yield return Box(report.ObliqueAsymptotes);
        yield return Box(report.Monotonicity);
        yield return Box(report.Period);

        static ProofOutcome<object> Box<T>(ProofOutcome<T> outcome) => outcome.State switch
        {
            ProofState.Proved => ProofOutcome<object>.Proved(outcome.Value!, outcome.Certificate!),
            ProofState.Disproved => ProofOutcome<object>.Disproved(outcome.Value!, outcome.Certificate!),
            _ => ProofOutcome<object>.Unknown(outcome.UnknownReason!.Value)
        };
    }

    private static void AssertAllProved(AnalysisReport report, string label)
    {
        AssertProved(report.Domain, label);
        AssertProved(report.Range, label);
        AssertProved(report.Parity, label);
        AssertProved(report.Zeros, label);
        AssertProved(report.YIntercept, label);
        AssertProved(report.Minima, label);
        AssertProved(report.Maxima, label);
        AssertProved(report.InflectionPoints, label);
        AssertProved(report.VerticalAsymptotes, label);
        AssertProved(report.HorizontalAsymptotes, label);
        AssertProved(report.ObliqueAsymptotes, label);
        AssertProved(report.Monotonicity, label);
        AssertProved(report.Period, label);
    }

    private static void AssertProved<T>(ProofOutcome<T> outcome, string label) =>
        Assert.True(outcome.State == ProofState.Proved, $"{label}: {outcome.UnknownReason}");

    private static void AssertDedicatedCertificates(AnalysisReport report, string label)
    {
        foreach (ProofCertificate? certificate in new[]
                 {
                     report.Range.Certificate,
                     report.Parity.Certificate,
                     report.Zeros.Certificate,
                     report.YIntercept.Certificate,
                     report.Minima.Certificate,
                     report.Maxima.Certificate,
                     report.InflectionPoints.Certificate,
                     report.VerticalAsymptotes.Certificate,
                     report.HorizontalAsymptotes.Certificate,
                     report.ObliqueAsymptotes.Certificate,
                     report.Monotonicity.Certificate,
                     report.Period.Certificate
                 })
        {
            Assert.IsType<AbsoluteCompositionProofCertificate>(certificate);
            Assert.NotNull(certificate);
        }
    }

    private static void AssertAllCertificatesReplay(
        AnalysisRequest request,
        AnalysisReport report,
        string label)
    {
        AssertReplay(request, report, report.Domain, label);
        AssertReplay(request, report, report.Range, label);
        AssertReplay(request, report, report.Parity, label);
        AssertReplay(request, report, report.Zeros, label);
        AssertReplay(request, report, report.YIntercept, label);
        AssertReplay(request, report, report.Minima, label);
        AssertReplay(request, report, report.Maxima, label);
        AssertReplay(request, report, report.InflectionPoints, label);
        AssertReplay(request, report, report.VerticalAsymptotes, label);
        AssertReplay(request, report, report.HorizontalAsymptotes, label);
        AssertReplay(request, report, report.ObliqueAsymptotes, label);
        AssertReplay(request, report, report.Monotonicity, label);
        AssertReplay(request, report, report.Period, label);
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome,
        string label) =>
        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome), label);

    private static void AssertSameClaims(AnalysisReport expected, AnalysisReport actual)
    {
        AssertSame(expected.Domain, actual.Domain);
        AssertSame(expected.Range, actual.Range);
        AssertSame(expected.Parity, actual.Parity);
        AssertSame(expected.Zeros, actual.Zeros);
        AssertSame(expected.YIntercept, actual.YIntercept);
        AssertSame(expected.Minima, actual.Minima);
        AssertSame(expected.Maxima, actual.Maxima);
        AssertSame(expected.InflectionPoints, actual.InflectionPoints);
        AssertSame(expected.VerticalAsymptotes, actual.VerticalAsymptotes);
        AssertSame(expected.HorizontalAsymptotes, actual.HorizontalAsymptotes);
        AssertSame(expected.ObliqueAsymptotes, actual.ObliqueAsymptotes);
        AssertSame(expected.Monotonicity, actual.Monotonicity);
        AssertSame(expected.Period, actual.Period);
    }

    private static void AssertSame<T>(ProofOutcome<T> expected, ProofOutcome<T> actual)
    {
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.UnknownReason, actual.UnknownReason);
        if (expected.State == ProofState.Proved)
        {
            Assert.Equal(ClaimCanonical.For(expected.Value!), ClaimCanonical.For(actual.Value!));
        }
    }

    private static void AssertPeriod(AnalysisReport report, string expectedCanonical)
    {
        Periodicity period = Proved(report.Period);
        Assert.Equal(PeriodicityKind.PeriodicWithFundamentalPeriod, period.Kind);
        Assert.Equal(expectedCanonical, ExactRealCanonical.Format(period.FundamentalPeriod!));
    }

    private static void AssertNotPeriodic(AnalysisReport report) =>
        Assert.Equal(PeriodicityKind.NotPeriodic, Proved(report.Period).Kind);

    private static void AssertSingletonPoint(FeaturePoint value, string x, string y)
    {
        var point = Assert.IsType<ConstantYFeaturePoint>(value);
        var singleton = Assert.IsType<SingletonReal>(point.X);
        Assert.Equal(x, ExactRealCanonical.Format(singleton.Value));
        Assert.Equal(y, ExactRealCanonical.Format(point.Y));
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Certificate);
        Assert.NotNull(outcome.Value);
        return outcome.Value;
    }

    private static AnalysisReport Analyze(
        InputExpression expression,
        AngleUnit angleUnit = AngleUnit.Radians) =>
        AnalysisEngine.Analyze(Request(expression, AnalysisFeatures.All, angleUnit));

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features,
        AngleUnit angleUnit = AngleUnit.Radians) =>
        new(expression, features, angleUnit, "x", static () => true);

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

    private static InputExpression Power(InputExpression basis, int exponent) =>
        InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);

    private static InputExpression Abs(InputExpression value) => Function("abs", value);

    private static InputExpression Sqrt(InputExpression value) => Function("sqrt", value);

    private static InputExpression Sin(InputExpression value) => Function("sin", value);

    private static InputExpression Cos(InputExpression value) => Function("cos", value);

    private static InputExpression Sinh(InputExpression value) => Function("sinh", value);

    private static InputExpression Cosh(InputExpression value) => Function("cosh", value);

    private static InputExpression Tanh(InputExpression value) => Function("tanh", value);

    private static InputExpression Function(string name, params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
