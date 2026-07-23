using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class LinearDriftTrigCompositionTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void RationalDistributionProducesTheSameCertifiedAnalysis()
    {
        InputExpression x = Variable();
        InputExpression sine = Function("sin", x);
        InputExpression shared = Add(x, sine);
        InputExpression[] equivalent =
        [
            Multiply(Number(2), shared),
            Multiply(shared, Number(2)),
            Multiply(Function("sqrt", Number(4)), shared),
            Multiply(Negate(Negate(Number(2))), shared),
            Divide(shared, Number(new BigRational(1, 2))),
            Add(Add(x, x), Add(sine, sine)),
            Add(Multiply(Number(2), x), Add(sine, sine))
        ];

        string? expectedRange = null;
        string? expectedYIntercept = null;
        string? expectedInflections = null;
        string? expectedMonotonicity = null;
        foreach (InputExpression expression in equivalent)
        {
            AnalysisRequest request = Request(expression, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            Assert.Equal(ProofState.Proved, report.Domain.State);
            Assert.Equal(ProofState.Proved, report.Range.State);
            Assert.Equal(ProofState.Proved, report.Parity.State);
            Assert.Equal(ProofState.Proved, report.Zeros.State);
            Assert.Equal(ProofState.Proved, report.YIntercept.State);
            Assert.Equal(ProofState.Proved, report.Minima.State);
            Assert.Equal(ProofState.Proved, report.Maxima.State);
            Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
            Assert.Equal(ProofState.Proved, report.Monotonicity.State);
            Assert.Equal(ProofState.Proved, report.Period.State);
            Assert.Equal(FunctionParity.Odd, Proved(report.Parity));
            Assert.Empty(Proved(report.Minima));
            Assert.Empty(Proved(report.Maxima));
            Assert.Equal(Monotonicity.Increasing, Assert.Single(Proved(report.Monotonicity)).Direction);

            expectedRange = SameClaim(expectedRange, Proved(report.Range));
            expectedYIntercept = SameClaim(expectedYIntercept, Proved(report.YIntercept));
            expectedInflections = SameClaim(expectedInflections, Proved(report.InflectionPoints));
            expectedMonotonicity = SameClaim(expectedMonotonicity, Proved(report.Monotonicity));
            AssertReplay(request, report.Expression!, report.Range);
            AssertReplay(request, report.Expression!, report.InflectionPoints);
            AssertReplay(request, report.Expression!, report.Monotonicity);
        }
    }

    [Fact]
    public void DistributionCombinesLineTrigAndInterceptPieces()
    {
        InputExpression expression = Add(
            Variable(),
            Multiply(
                Number(2),
                Add(Function("sin", Variable()), Number(1))));
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(FunctionParity.Neither, Proved(report.Parity));
        Assert.Equal(
            new RationalReal(new BigRational(2)),
            Proved(report.YIntercept).Value);
        Assert.Single(Proved(report.Minima));
        Assert.Single(Proved(report.Maxima));
        var inflection = Assert.IsType<IntegerAffineFeaturePoint>(
            Assert.Single(Proved(report.InflectionPoints)));
        Assert.Equal("pi:0:0", ExactRealCanonical.Format(inflection.XOffset));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(inflection.XStep));
        Assert.Equal("pi:0:2", ExactRealCanonical.Format(inflection.YOffset));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(inflection.YStep));
        Assert.Equal(ProofState.Unknown, report.Monotonicity.State);

        AssertReplay(request, report.Expression!, report.Range);
        AssertReplay(request, report.Expression!, report.Minima);
        AssertReplay(request, report.Expression!, report.Maxima);
        AssertReplay(request, report.Expression!, report.InflectionPoints);

        AnalysisReport halved = AnalysisEngine.Analyze(Request(
            Divide(
                Add(Variable(), Function("sin", Variable())),
                Number(2)),
            AnalysisFeatures.Range |
            AnalysisFeatures.Minima |
            AnalysisFeatures.Maxima |
            AnalysisFeatures.InflectionPoints |
            AnalysisFeatures.Monotonicity));
        Assert.Equal(ProofState.Proved, halved.Range.State);
        Assert.Empty(Proved(halved.Minima));
        Assert.Empty(Proved(halved.Maxima));
        Assert.Equal(
            "pi:1/2:0",
            ExactRealCanonical.Format(
                Assert.IsType<IntegerAffineFeaturePoint>(
                    Assert.Single(Proved(halved.InflectionPoints))).YStep));
        Assert.Equal(
            Monotonicity.Increasing,
            Assert.Single(Proved(halved.Monotonicity)).Direction);
    }

    [Fact]
    public void DuplicateTrigTermsCombineOnlyWhenTheirBasisIsEquivalent()
    {
        InputExpression duplicated = Add(
            Variable(),
            Add(Function("sin", Variable()), Function("sin", Variable())));
        AnalysisReport duplicatedReport = AnalysisEngine.Analyze(Request(
            duplicated,
            AnalysisFeatures.Range |
            AnalysisFeatures.Minima |
            AnalysisFeatures.Maxima |
            AnalysisFeatures.InflectionPoints));

        Assert.Equal(ProofState.Proved, duplicatedReport.Range.State);
        Assert.Single(Proved(duplicatedReport.Minima));
        Assert.Single(Proved(duplicatedReport.Maxima));
        Assert.Equal(ProofState.Proved, duplicatedReport.InflectionPoints.State);

        InputExpression nonEquivalent = Add(
            Variable(),
            Add(Function("sin", Variable()), Function("cos", Variable())));
        AnalysisReport nonEquivalentReport = AnalysisEngine.Analyze(Request(
            nonEquivalent,
            AnalysisFeatures.Range | AnalysisFeatures.Monotonicity));

        Assert.Equal(ProofState.Unknown, nonEquivalentReport.Range.State);
        Assert.Equal(ProofState.Unknown, nonEquivalentReport.Monotonicity.State);
    }

    [Fact]
    public void ExactConstantInterceptAndShiftedPhaseHaveCertifiedParity()
    {
        InputExpression exactIntercept = Add(
            Add(Variable(), Named("pi")),
            Function("sin", Variable()));
        AnalysisRequest request = Request(exactIntercept, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(FunctionParity.Neither, Proved(report.Parity));
        Assert.Equal(
            "pi:1:0",
            ExactRealCanonical.Format(Proved(report.YIntercept).Value!));
        Assert.Empty(Proved(report.Minima));
        Assert.Empty(Proved(report.Maxima));
        var inflection = Assert.IsType<IntegerAffineFeaturePoint>(
            Assert.Single(Proved(report.InflectionPoints)));
        Assert.Equal("pi:0:0", ExactRealCanonical.Format(inflection.XOffset));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(inflection.XStep));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(inflection.YOffset));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(inflection.YStep));
        Assert.Equal(Monotonicity.Increasing, Assert.Single(Proved(report.Monotonicity)).Direction);
        AssertReplay(request, report.Expression!, report.YIntercept);
        AssertReplay(request, report.Expression!, report.InflectionPoints);

        InputExpression shifted = Add(
            Variable(),
            Function("sin", Add(Variable(), Number(1))));
        AnalysisRequest shiftedRequest = Request(shifted, AnalysisFeatures.Parity);
        AnalysisReport shiftedReport = AnalysisEngine.Analyze(shiftedRequest);
        Assert.Equal(FunctionParity.Neither, Proved(shiftedReport.Parity));
        AssertReplay(shiftedRequest, shiftedReport.Expression!, shiftedReport.Parity);
    }

    [Fact]
    public void DistributedZeroTermsNeverEraseRetainedDomainHoles()
    {
        InputExpression hole = Multiply(
            Number(0),
            Divide(Number(1), Subtract(Variable(), Number(1))));
        InputExpression core = Add(Variable(), Function("sin", Variable()));
        InputExpression[] expressions =
        [
            Multiply(Number(2), Add(core, hole)),
            Divide(Add(core, hole), Number(2)),
            Add(Multiply(core, Number(2)), hole)
        ];

        foreach (InputExpression expression in expressions)
        {
            AnalysisReport report = AnalysisEngine.Analyze(Request(
                expression,
                AnalysisFeatures.Domain |
                AnalysisFeatures.Range |
                AnalysisFeatures.Parity |
                AnalysisFeatures.Monotonicity));

            Assert.Equal(ProofState.Proved, report.Domain.State);
            Assert.NotEqual(AllRealSet.Instance.Canonical, Proved(report.Domain).Canonical);
            Assert.Equal(ProofState.Unknown, report.Range.State);
            Assert.Equal(ProofState.Unknown, report.Parity.State);
            Assert.Equal(ProofState.Unknown, report.Monotonicity.State);
        }
    }

    [Fact]
    public void SharedDagCollectionIsLinearAndCertificateMutationIsRejected()
    {
        ValueTerm shared = BaseValueTerms();
        for (int i = 0; i < 512; i++)
        {
            shared = new ValueTerm(
                shared.Id + 1,
                ValueKind.Add,
                default,
                string.Empty,
                [shared, shared],
                $"double:{i}");
        }

        Assert.True(LinearDriftTrigAnalyzer.TryCompute(
            Semantic(shared),
            "x",
            AngleUnit.Radians,
            AnalysisFeatures.InflectionPoints,
            new ResourceBudget(),
            out object sharedValue,
            out _));
        Assert.Single(Assert.IsAssignableFrom<ImmutableArray<FeaturePoint>>(sharedValue));

        InputExpression expression = Multiply(
            Number(2),
            Add(Variable(), Function("sin", Variable())));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Range | AnalysisFeatures.InflectionPoints);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
        AssertReplay(request, report.Expression!, report.InflectionPoints);

        var certificate = Assert.IsType<TheoremProofCertificate>(
            report.InflectionPoints.Certificate);
        TheoremProofCertificate mutation = certificate with
        {
            Parameters = certificate.Parameters.SetItem(
                0,
                certificate.Parameters[0] + ":mutated")
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<FeaturePoint>>.Proved(
                Proved(report.InflectionPoints),
                mutation)));
    }

    [Fact]
    public void IterativeCollectorHandlesDeepTreesAndEnforcesItsNodeLimit()
    {
        ValueTerm baseTerm = BaseValueTerms();
        ValueTerm rationalFactor = new(
            4,
            ValueKind.Constant,
            new BigRational(2),
            string.Empty,
            [],
            "q:2");
        rationalFactor = NegateChain(20_000, rationalFactor, out int factorId);
        ValueTerm deeplyScaled = new(
            factorId + 1,
            ValueKind.Multiply,
            default,
            string.Empty,
            [baseTerm, rationalFactor],
            "deep-right-scaled-linear-trig");
        SemanticExpression withinLimit = Semantic(deeplyScaled);
        var budget = new ResourceBudget();
        Assert.True(LinearDriftTrigAnalyzer.TryCompute(
            withinLimit,
            "x",
            AngleUnit.Radians,
            AnalysisFeatures.Range,
            budget,
            out object value,
            out _));
        Assert.Same(AllRealSet.Instance, value);

        ValueTerm overLimit = NegateChain(
            AnalysisLimits.SemanticNodes + 1,
            baseTerm,
            out _);
        Assert.Throws<BudgetExceededException>(() =>
            LinearDriftTrigAnalyzer.TryCompute(
                Semantic(overLimit),
                "x",
                AngleUnit.Radians,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out _,
                out _));

        var two = new ValueTerm(
            4,
            ValueKind.Constant,
            new BigRational(2),
            string.Empty,
            [],
            "q:2");
        var hugeExponent = new ValueTerm(
            5,
            ValueKind.Constant,
            new BigRational(int.MaxValue),
            string.Empty,
            [],
            "q:int-max");
        var hugePower = new ValueTerm(
            6,
            ValueKind.Power,
            default,
            string.Empty,
            [two, hugeExponent],
            "huge-rational-power");
        var hugeScale = new ValueTerm(
            7,
            ValueKind.Multiply,
            default,
            string.Empty,
            [BaseValueTerms(), hugePower],
            "huge-rational-scale");
        Assert.Throws<BudgetExceededException>(() =>
            LinearDriftTrigAnalyzer.TryCompute(
                Semantic(hugeScale),
                "x",
                AngleUnit.Radians,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out _,
                out _));
    }

    private static string SameClaim<T>(string? expected, T value)
    {
        string actual = ClaimCanonical.ForObject(value!);
        if (expected is not null)
        {
            Assert.Equal(expected, actual);
        }

        return actual;
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.True(CertificateChecker.Check(request, expression, outcome));
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return Assert.IsAssignableFrom<T>(outcome.Value);
    }

    private static SemanticExpression Semantic(ValueTerm value)
    {
        return new SemanticExpression(value, Formula.True, Formula.True, Formula.True, [], [], []);
    }

    private static ValueTerm BaseValueTerms()
    {
        var variable = new ValueTerm(
            1,
            ValueKind.Variable,
            default,
            "x",
            [],
            "v:x");
        var sine = new ValueTerm(
            2,
            ValueKind.Function,
            default,
            "sin",
            [variable],
            "sin:x");
        return new ValueTerm(
            3,
            ValueKind.Add,
            default,
            string.Empty,
            [variable, sine],
            "linear-trig-base");
    }

    private static ValueTerm NegateChain(int depth, ValueTerm value, out int lastId)
    {
        int id = value.Id;
        for (int i = 0; i < depth; i++)
        {
            value = new ValueTerm(
                ++id,
                ValueKind.Negate,
                default,
                string.Empty,
                [value],
                $"negate:{id}");
        }

        lastId = id;
        return value;
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Named(string name)
    {
        return InputExpression.Variable(name, Source);
    }

    private static InputExpression Number(int value)
    {
        return Number(new BigRational(value));
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

    private static InputExpression Negate(InputExpression value)
    {
        return InputExpression.Unary(InputExpressionKind.Negate, value, Source);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
