using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class RationalRangeProjectionTests
{
    private static readonly SourceRange Source = new(0, 1);

    public static TheoryData<string, string> WindowsRangeCases => new()
    {
        { "1/x", "y ∈ ℝ ∖ {0}" },
        { "-1/x", "y ∈ ℝ ∖ {0}" },
        { "1/(x-1)", "y ∈ ℝ ∖ {0}" },
        { "2/(x-3)+4", "y ∈ ℝ ∖ {4}" },
        { "(2*x+1)/(x-3)", "y ∈ ℝ ∖ {2}" },
        { "(x^2-1)/(x-1)", "y ∈ ℝ ∖ {2}" },
        { "(x-1)^2/(x-1)", "y ∈ ℝ ∖ {0}" },
        { "(x-1)/(x-1)", "y ∈ {1}" },
        { "(x^2-1)/(x^2-1)", "y ∈ {1}" },
        { "0*(1/x)", "y ∈ {0}" },
        { "0/(x-1)", "y ∈ {0}" },
        { "1/x-1/x", "y ∈ {0}" },
        { "x/x", "y ∈ {1}" },
        { "0/0", "y ∈ ∅" },
        { "x/(x^2+1)", "y ∈ [−1/2, 1/2]" },
        { "(x^2-1)/(x^2+1)", "y ∈ [−1, 1)" },
        { "1/(x^2+1)", "y ∈ (0, 1]" },
        { "-1/(x^2+1)", "y ∈ [−1, 0)" },
        { "(x^2+1)/x", "y ∈ (−∞, −2] ∪ [2, ∞)" },
        { "x+1/x", "y ∈ (−∞, −2] ∪ [2, ∞)" },
        { "1/(x^2-1)", "y ∈ (−∞, −1] ∪ (0, ∞)" },
        { "1/(x-1)^2", "y ∈ (0, ∞)" },
        { "(x^2-1)/((x-1)*(x-2))", "y ∈ ℝ ∖ {−2, 1}" },
        { "1/(1/x)", "y ∈ ℝ ∖ {0}" },
        { "-1/(-x)", "y ∈ ℝ ∖ {0}" }
    };

    public static TheoryData<string> CertifiedProjectionCases => new()
    {
        "1/x",
        "2/(x-3)+4",
        "(x^2-1)/(x-1)",
        "(x-1)/(x-1)",
        "x/(x^2+1)",
        "(x^2-1)/(x^2+1)",
        "1/(x^2+1)",
        "-1/(x^2+1)",
        "(x^2+1)/x",
        "x+1/x",
        "1/(x^2-1)",
        "1/(x-1)^2",
        "(x^2-1)/((x-1)*(x-2))",
        "1/(1/x)",
        "-1/(-x)"
    };

    [Theory]
    [MemberData(nameof(WindowsRangeCases))]
    public void TargetRationalImagesMatchWindowsExactly(string formula, string expected)
    {
        GraphFunctionAnalysisData result = Analyze(formula, PerformAnalysisType.Range);

        Assert.Equal(expected, result.Range);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void DomainAlternativesRemainLogicalDisjunctions()
    {
        GraphFunctionAnalysisData result = Analyze(
            "sqrt(x^2-1)",
            PerformAnalysisType.Domain);

        Assert.Contains(" ∨ ", result.Domain, StringComparison.Ordinal);
        Assert.DoesNotContain(" ∪ ", result.Domain, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(CertifiedProjectionCases))]
    public void EveryGenericProjectionHasAReplayableDedicatedCertificate(string formula)
    {
        InputExpression expression = RationalExpression(formula);
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.IsType<RationalRangeProofCertificate>(report.Range.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
    }

    [Fact]
    public void ProjectionCertificateRejectsBoundaryFiberAndPartitionMutations()
    {
        InputExpression expression = RationalExpression("x/(x^2+1)");
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = AssertProved(report.Range);
        var certificate = Assert.IsType<RationalRangeProofCertificate>(report.Range.Certificate);
        Assert.Equal(
            [new BigRational(-1, 2), BigRational.Zero, new BigRational(1, 2)],
            certificate.Boundaries);
        Assert.Equal(7, certificate.Fibers.Length);

        RationalRangeProofCertificate changedBoundary = certificate with
        {
            Boundaries = certificate.Boundaries.SetItem(0, new BigRational(-1, 3))
        };
        AssertRejected(changedBoundary);

        RationalRangeFiberWitness first = certificate.Fibers[0];
        RationalRangeProofCertificate changedMembership = certificate with
        {
            Fibers = certificate.Fibers.SetItem(
                0,
                first with { HasPreimage = !first.HasPreimage })
        };
        AssertRejected(changedMembership);

        CellWitness firstCell = first.Fiber.Cells[0];
        CellDecompositionCertificate changedFiber = first.Fiber with
        {
            Cells = first.Fiber.Cells.SetItem(
                0,
                firstCell with { Included = !firstCell.Included })
        };
        RationalRangeProofCertificate changedFiberCells = certificate with
        {
            Fibers = certificate.Fibers.SetItem(0, first with { Fiber = changedFiber })
        };
        AssertRejected(changedFiberCells);

        RationalRangeProofCertificate changedRoots = certificate with
        {
            PartitionRoots = certificate.PartitionRoots with { Roots = [] }
        };
        AssertRejected(changedRoots);

        void AssertRejected(RationalRangeProofCertificate changed) =>
            Assert.False(CertificateChecker.Check(
                request,
                report.Expression!,
                ProofOutcome<RealSet>.Proved(range, changed)));
    }

    [Fact]
    public void EmptyDomainAndConstantFunctionsWithHolesHaveExactImages()
    {
        GraphFunctionAnalysisData empty = Analyze("0/0", PerformAnalysisType.Range);
        GraphFunctionAnalysisData oneHole = Analyze(
            "(x-1)/(x-1)",
            PerformAnalysisType.Range);
        GraphFunctionAnalysisData twoHoles = Analyze(
            "(x^2-1)/(x^2-1)",
            PerformAnalysisType.Range);
        GraphFunctionAnalysisData zeroHole = Analyze(
            "0*(1/x)",
            PerformAnalysisType.Range);

        Assert.Equal("y ∈ ∅", empty.Range);
        Assert.Equal("y ∈ {1}", oneHole.Range);
        Assert.Equal("y ∈ {1}", twoHoles.Range);
        Assert.Equal("y ∈ {0}", zeroHole.Range);
        Assert.Equal(0, empty.TooComplexFeatures);
        Assert.Equal(0, oneHole.TooComplexFeatures);
        Assert.Equal(0, twoHoles.TooComplexFeatures);
        Assert.Equal(0, zeroHole.TooComplexFeatures);

        InputExpression nowhereDefinedConstant = Power(Number(0), 0);
        AnalysisRequest request = Request(
            nowhereDefinedConstant,
            AnalysisFeatures.Domain | AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.IsType<EmptySet>(AssertProved(report.Domain));
        Assert.IsType<EmptySet>(AssertProved(report.Range));
        Assert.IsType<RationalRangeProofCertificate>(report.Range.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
    }

    [Fact]
    public void NonRationalFiniteBoundaryRemainsExplicitlyUnknown()
    {
        InputExpression expression = Divide(
            Variable(),
            Add(Power(Variable(), 4), Number(1)));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);

        AnalysisReport first = AnalysisEngine.Analyze(request);
        AnalysisReport second = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Unknown, first.Range.State);
        Assert.Equal(UnknownReason.ProjectionUnsupported, first.Range.UnknownReason);
        Assert.Equal(first.Range.State, second.Range.State);
        Assert.Equal(first.Range.UnknownReason, second.Range.UnknownReason);
    }

    [Fact]
    public void ExistingSingleAlgebraicPolynomialBoundaryProofRemainsAvailable()
    {
        InputExpression expression = Add(Power(Variable(), 4), Variable());
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.IsType<RationalFunctionProofCertificate>(report.Range.Certificate);
        var interval = Assert.IsType<IntervalSet>(AssertProved(report.Range));
        Assert.Equal(BoundKind.Finite, interval.Lower.Kind);
        Assert.IsType<AlgebraicImageReal>(interval.Lower.Value);
        Assert.Equal(BoundKind.PositiveInfinity, interval.Upper.Kind);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
    }

    [Theory]
    [InlineData("(x^2-1)*(x^2-4)", "y ∈ [−9/4, ∞)")]
    [InlineData("x^4-2*x^2+1", "y ∈ [0, ∞)")]
    [InlineData("x^4+2*x^2+1", "y ∈ [1, ∞)")]
    [InlineData("-x^4+2*x^2-1", "y ∈ (−∞, 0]")]
    [InlineData("-x^4-2*x^2-1", "y ∈ (−∞, −1]")]
    public void EvenQuarticRangeUsesItsExactRationalCriticalImage(
        string formula,
        string expected)
    {
        GraphFunctionAnalysisData result = Analyze(formula, PerformAnalysisType.Range);
        Assert.Equal(expected, result.Range);
        Assert.Equal(0, result.TooComplexFeatures);

        InputExpression expression = RationalExpression(formula);
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.True(
            report.Range.Certificate is RationalFunctionProofCertificate or
                RationalRangeProofCertificate,
            $"Unexpected certificate: {report.Range.Certificate?.GetType().Name}");
        if (formula == "(x^2-1)*(x^2-4)")
        {
            Assert.IsType<RationalFunctionProofCertificate>(report.Range.Certificate);
        }

        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
    }

    [Fact]
    public void LargeRationalHoleIsRecoveredFromItsExactLinearBoundary()
    {
        InputExpression x = Variable();
        InputExpression expression = Divide(
            Subtract(Power(x, 2), Number(10_000)),
            Subtract(x, Number(100)));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        var difference = Assert.IsType<DifferenceSet>(AssertProved(report.Range));
        var removed = Assert.IsType<PointSet>(difference.Removed);
        Assert.Equal(
            new BigRational(200),
            Assert.IsType<RationalReal>(Assert.Single(removed.Points)).Value);
        Assert.IsType<RationalRangeProofCertificate>(report.Range.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
    }

    [Fact]
    public void ProjectionDegreeLimitReturnsDeterministicBudgetExceeded()
    {
        InputExpression expression = Power(Variable(), 257);
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);

        AnalysisReport first = AnalysisEngine.Analyze(request);
        AnalysisReport second = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Unknown, first.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, first.Range.UnknownReason);
        Assert.Equal(first.Range.State, second.Range.State);
        Assert.Equal(first.Range.UnknownReason, second.Range.UnknownReason);
    }

    [Fact]
    public void RationalDerivativeSignsIncludeTheDenominatorOnEveryDomainComponent()
    {
        GraphFunctionAnalysisData result = Analyze(
            "1/(x-1)^2",
            PerformAnalysisType.Monotonicity);

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(−∞, 1)"] = (int)FunctionMonotonicityType.Ascending,
                ["(1, ∞)"] = (int)FunctionMonotonicityType.Descending
            },
            result.MonotoneIntervals);
        Assert.Equal(0, result.TooComplexFeatures);

        InputExpression expression = RationalExpression("1/(x-1)^2");
        AnalysisRequest request = Request(expression, AnalysisFeatures.Monotonicity);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Monotonicity));
        var certificate = Assert.IsType<RationalFunctionProofCertificate>(
            report.Monotonicity.Certificate);
        RationalFunctionProofCertificate changed = certificate with
        {
            Rule = "numerator-sign-only"
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<MonotoneRegion>>.Proved(
                AssertProved(report.Monotonicity),
                changed)));
    }

    private static GraphFunctionAnalysisData Analyze(
        string formula,
        PerformAnalysisType requested)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)requested));
        return solver.Analyze(analyzer);
    }

    private static InputExpression RationalExpression(string formula)
    {
        InputExpression x = Variable();
        InputExpression one = Number(1);
        return formula switch
        {
            "1/x" => Divide(one, x),
            "2/(x-3)+4" => Add(Divide(Number(2), Subtract(x, Number(3))), Number(4)),
            "(x^2-1)/(x-1)" => Divide(
                Subtract(Power(x, 2), one),
                Subtract(x, one)),
            "(x-1)/(x-1)" => Divide(Subtract(x, one), Subtract(x, one)),
            "x/(x^2+1)" => Divide(x, Add(Power(x, 2), one)),
            "(x^2-1)/(x^2+1)" => Divide(
                Subtract(Power(x, 2), one),
                Add(Power(x, 2), one)),
            "1/(x^2+1)" => Divide(one, Add(Power(x, 2), one)),
            "-1/(x^2+1)" => Divide(Number(-1), Add(Power(x, 2), one)),
            "(x^2+1)/x" => Divide(Add(Power(x, 2), one), x),
            "x+1/x" => Add(x, Divide(one, x)),
            "1/(x^2-1)" => Divide(one, Subtract(Power(x, 2), one)),
            "1/(x-1)^2" => Divide(one, Power(Subtract(x, one), 2)),
            "(x^2-1)/((x-1)*(x-2))" => Divide(
                Subtract(Power(x, 2), one),
                Multiply(Subtract(x, one), Subtract(x, Number(2)))),
            "1/(1/x)" => Divide(one, Divide(one, x)),
            "-1/(-x)" => Divide(Number(-1), Negate(x)),
            "(x^2-1)*(x^2-4)" => Multiply(
                Subtract(Power(x, 2), one),
                Subtract(Power(x, 2), Number(4))),
            "x^4-2*x^2+1" => Add(
                Subtract(Power(x, 4), Multiply(Number(2), Power(x, 2))),
                one),
            "x^4+2*x^2+1" => Add(
                Add(Power(x, 4), Multiply(Number(2), Power(x, 2))),
                one),
            "-x^4+2*x^2-1" => Subtract(
                Add(Negate(Power(x, 4)), Multiply(Number(2), Power(x, 2))),
                one),
            "-x^4-2*x^2-1" => Subtract(
                Subtract(Negate(Power(x, 4)), Multiply(Number(2), Power(x, 2))),
                one),
            _ => throw new ArgumentOutOfRangeException(nameof(formula))
        };
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features) =>
        new(expression, features, AngleUnit.Radians, "x", static () => true);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Negate(InputExpression value) =>
        InputExpression.Unary(InputExpressionKind.Negate, value, Source);

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

    private static T AssertProved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        Assert.NotNull(outcome.Certificate);
        return outcome.Value;
    }
}
