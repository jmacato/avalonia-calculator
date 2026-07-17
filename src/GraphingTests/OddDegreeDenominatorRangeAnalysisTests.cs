using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class OddDegreeDenominatorRangeAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void CubicReciprocalUsesOddDegreeSurjectivityWithProductionReplay()
    {
        InputExpression expression = Reciprocal(CubicDenominator());
        AnalysisRequest request = Request(expression);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        var difference = Assert.IsType<DifferenceSet>(Proved(report.Range));
        Assert.IsType<AllRealSet>(difference.Source);
        var removed = Assert.IsType<PointSet>(difference.Removed);
        Assert.Equal(
            BigRational.Zero,
            Assert.IsType<RationalReal>(Assert.Single(removed.Points)).Value);
        var certificate = Assert.IsType<OddDegreeDenominatorRangeProofCertificate>(
            report.Range.Certificate);
        Assert.Equal(3, certificate.Denominator.Degree);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
    }

    [Fact]
    public void OddDegreeRangeCertificateRejectsEveryTheoremPremiseMutation()
    {
        InputExpression expression = Reciprocal(CubicDenominator());
        AnalysisRequest request = Request(expression);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = Proved(report.Range);
        var certificate = Assert.IsType<OddDegreeDenominatorRangeProofCertificate>(
            report.Range.Certificate);

        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Rule = "sampled-surjectivity" });
        AssertRejected(certificate with { Numerator = UnivariatePolynomial.Zero });
        AssertRejected(certificate with { Denominator = UnivariatePolynomial.One });
        AssertRejected(certificate with { DefinednessCanonical = "true" });
        AssertRejected(certificate with
        {
            DomainFormula = new PolynomialBoolean(true)
        });

        CellWitness sourceFirst = certificate.DomainCells.Cells[0];
        AssertRejected(certificate with
        {
            DomainCells = certificate.DomainCells with
            {
                Formula = new PolynomialBoolean(true)
            }
        });
        AssertRejected(certificate with
        {
            DomainCells = certificate.DomainCells with
            {
                Cells = certificate.DomainCells.Cells.SetItem(
                    0,
                    sourceFirst with { Included = !sourceFirst.Included })
            }
        });
        AssertRejected(certificate with
        {
            DomainCells = certificate.DomainCells with
            {
                Result = AllRealSet.Instance
            }
        });

        CellWitness first = certificate.DenominatorDomain.Cells[0];
        AssertRejected(certificate with
        {
            DenominatorDomain = certificate.DenominatorDomain with
            {
                Cells = certificate.DenominatorDomain.Cells.SetItem(
                    0,
                    first with { Included = !first.Included })
            }
        });
        AssertRejected(certificate with
        {
            DenominatorDomain = certificate.DenominatorDomain with
            {
                Formula = new PolynomialBoolean(true)
            }
        });

        var trueFormula = new PolynomialBoolean(true);
        CellDecompositionCertificate trueCells = CellDecomposer.Decompose(
            trueFormula,
            new ResourceBudget());
        SemanticExpression forgedAllRealDomain = report.Expression! with
        {
            DefinedWhen = Formula.True
        };
        Assert.False(OddDegreeDenominatorRangeCertificateChecker.Check(
            request,
            forgedAllRealDomain,
            certificate with
            {
                DefinednessCanonical = Formula.True.Canonical,
                DomainFormula = trueFormula,
                DomainCells = trueCells
            },
            ClaimCanonical.For(range),
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(AllRealSet.Instance, certificate)));
        return;

        void AssertRejected(OddDegreeDenominatorRangeProofCertificate changed) =>
            Assert.False(CertificateChecker.Check(
                request,
                report.Expression!,
                ProofOutcome<RealSet>.Proved(range, changed)));
    }

    [Fact]
    public void ExtraHolesEvenDegreesAndNonconstantNumeratorsDoNotUseTheTheorem()
    {
        InputExpression cubic = CubicDenominator();
        InputExpression retainedHole = Add(
            Reciprocal(cubic),
            Multiply(
                Number(0),
                Reciprocal(Subtract(Variable(), Number(3)))));
        AssertRejected(retainedHole);
        AssertRejected(Reciprocal(Add(Power(Variable(), 2), Number(1))));
        AssertRejected(Divide(Variable(), cubic));

        static void AssertRejected(InputExpression expression)
        {
            SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
            Assert.True(RationalAnalysisContext.TryCreate(
                semantic,
                "x",
                new ResourceBudget(),
                out RationalAnalysisContext context));
            Assert.False(OddDegreeDenominatorRangeAnalyzer.TryAnalyze(
                context,
                new ResourceBudget(),
                out ProofOutcome<RealSet> _));
        }
    }

    [Fact]
    public void OddDegreeTheoremHonorsCancellationAndDegreeBudgetsDeterministically()
    {
        InputExpression expression = Reciprocal(CubicDenominator());
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
        Assert.True(RationalAnalysisContext.TryCreate(
            semantic,
            "x",
            new ResourceBudget(),
            out RationalAnalysisContext context));
        Assert.Throws<AnalysisCancelledException>(() =>
            OddDegreeDenominatorRangeAnalyzer.TryAnalyze(
                context,
                new ResourceBudget(static () => false),
                out ProofOutcome<RealSet> _));

        InputExpression oversized = Reciprocal(Power(
            Variable(),
            AnalysisLimits.UnivariateDegree + 1));
        AnalysisReport first = AnalysisEngine.Analyze(Request(oversized));
        AnalysisReport second = AnalysisEngine.Analyze(Request(oversized));
        Assert.Equal(ProofState.Unknown, first.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, first.Range.UnknownReason);
        Assert.Equal(first.Range.State, second.Range.State);
        Assert.Equal(first.Range.UnknownReason, second.Range.UnknownReason);
    }

    [Theory]
    [InlineData("1/((x-2)*(x-1)*(x+1))")]
    [InlineData("2/(x^3+x+1)")]
    [InlineData("-3/(x^5+x^2+1)")]
    public void OddDegreeReciprocalPublicCorpusFormatsThePuncturedRealRange(string formula)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal("y ∈ ℝ ∖ {0}", result.Range);
        Assert.Equal(0, result.TooComplexFeatures);
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
        Assert.Equal(
            GraphStatus.Ok,
            analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.Range));
        return solver.Analyze(analyzer);
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Certificate);
        return outcome.Value!;
    }

    private static AnalysisRequest Request(InputExpression expression) =>
        new(expression, AnalysisFeatures.Range, AngleUnit.Radians, "x", static () => true);

    private static InputExpression CubicDenominator()
    {
        InputExpression x = Variable();
        return Multiply(
            Multiply(Subtract(x, Number(2)), Subtract(x, Number(1))),
            Add(x, Number(1)));
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

    private static InputExpression Reciprocal(InputExpression denominator) =>
        Divide(Number(1), denominator);

    private static InputExpression Power(InputExpression basis, int exponent) =>
        InputExpression.Binary(
            InputExpressionKind.Power,
            basis,
            Number(exponent),
            Source);
}
