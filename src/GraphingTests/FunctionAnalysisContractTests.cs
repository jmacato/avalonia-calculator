using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class FunctionAnalysisContractTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void DeterministicRcfGrammarPublishesOnlyReplayableProofs()
    {
        InputExpression x = Variable();
        InputExpression one = Number(1);
        InputExpression positive = Add(Power(x, 2), one);
        InputExpression[] grammar =
        [
            x,
            Add(Power(x, 2), one),
            Subtract(Power(x, 3), one),
            Divide(one, positive),
            Divide(Subtract(Power(x, 2), one), positive),
            Multiply(Number(0), Divide(one, x)),
            Divide(Power(Subtract(x, one), 2), Subtract(x, one))
        ];

        foreach (InputExpression expression in grammar)
        {
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
        }
    }

    [Fact]
    public void CertificateMutationIsRejectedByIndependentReplay()
    {
        InputExpression expression = Divide(Number(1), Subtract(Variable(), Number(1)));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Domain);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet value = AssertProved(report.Domain);
        var certificate = Assert.IsType<DomainProofCertificate>(report.Domain.Certificate);

        DomainProofCertificate changedClaim = certificate with { Claim = "reals" };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(value, changedClaim)));

        CellDecompositionCertificate cells = Assert.IsType<CellDecompositionCertificate>(certificate.Cells);
        CellWitness first = cells.Cells[0];
        CellDecompositionCertificate changedCells = cells with
        {
            Cells = cells.Cells.SetItem(0, first with { Included = !first.Included })
        };
        DomainProofCertificate changedWitness = certificate with { Cells = changedCells };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(value, changedWitness)));
    }

    [Fact]
    public void GuardedRewritesPreservePartialFunctionDomains()
    {
        SemanticExpression zeroProduct = Build(Multiply(Number(0), Divide(Number(1), Variable())));
        Assert.Equal(ValueKind.Constant, zeroProduct.Value.Kind);
        Assert.True(zeroProduct.Value.Constant.IsZero);
        Assert.NotEqual(Formula.True.Canonical, zeroProduct.DefinedWhen.Canonical);

        SemanticExpression squareRootSquare = Build(
            Function("sqrt", Power(Variable(), 2)));
        Assert.Equal(ValueKind.Function, squareRootSquare.Value.Kind);
        Assert.Equal("abs", squareRootSquare.Value.Name);
        Assert.Contains(
            squareRootSquare.RewriteHistory,
            rewrite => rewrite.Rule == "sqrt-square-to-absolute-value");

        SemanticExpression normalized = Build(Add(Variable(), Number(0)));
        Assert.Equal("v:x", normalized.Value.Canonical);
    }

    [Fact]
    public void ExactSelfCancellationSimplifiesOnlyTheValueAndRetainsEveryGuard()
    {
        SemanticExpression symbolicZero = Build(Subtract(Named("pi"), Named("pi")));
        Assert.Equal("q:0", symbolicZero.Value.Canonical);
        Assert.Contains(
            symbolicZero.RewriteHistory,
            static rewrite => rewrite.Rule == "self-subtraction-value");

        SemanticExpression additiveInverse = Build(Add(Named("pi"), Negate(Named("pi"))));
        Assert.Equal("q:0", additiveInverse.Value.Canonical);
        Assert.Contains(
            additiveInverse.RewriteHistory,
            static rewrite => rewrite.Rule == "exact-scalar-fold");

        SemanticExpression rationalHole = Build(Divide(Variable(), Variable()));
        Assert.Equal("q:1", rationalHole.Value.Canonical);
        Assert.NotEqual(Formula.True.Canonical, rationalHole.DefinedWhen.Canonical);
        RewriteStep division = Assert.Single(rationalHole.RewriteHistory);
        Assert.Equal("self-division-value", division.Rule);
        Assert.NotEqual(Formula.True.Canonical, division.Guard.Canonical);

        InputExpression tangent = Function("tan", Variable());
        SemanticExpression cancelledTangent = Build(Subtract(tangent, tangent));
        Assert.Equal("q:0", cancelledTangent.Value.Canonical);
        Assert.NotEqual(Formula.True.Canonical, cancelledTangent.DefinedWhen.Canonical);
        Assert.Contains("cos", cancelledTangent.DefinedWhen.Canonical, StringComparison.Ordinal);

        SemanticExpression zeroOverZero = Build(Divide(Number(0), Number(0)));
        Assert.Equal("q:1", zeroOverZero.Value.Canonical);
        Assert.Equal(Formula.False.Canonical, zeroOverZero.DefinedWhen.Canonical);
    }

    [Fact]
    public void ExhaustiveShallowRationalDefinednessMatchesStructuralSemantics()
    {
        InputExpression[] leaves = [Variable(), Number(-1), Number(0), Number(1)];
        var expressions = new List<InputExpression>(leaves);
        foreach (InputExpression left in leaves)
        {
            foreach (InputExpression right in leaves)
            {
                expressions.Add(Add(left, right));
                expressions.Add(Multiply(left, right));
                expressions.Add(Divide(left, right));
            }
        }

        BigRational[] probes = [-2, -1, 0, 1, 2];
        foreach (InputExpression expression in expressions)
        {
            var budget = new ResourceBudget();
            SemanticExpression semantic = new SemanticGraphBuilder(budget).Build(expression);
            Assert.True(PolynomialFormulaConverter.TryConvert(
                semantic.DefinedWhen,
                "x",
                budget,
                out PolynomialFormula formula));
            foreach (BigRational probe in probes)
            {
                bool structural = TryEvaluate(expression, probe, out _);
                bool formulaValue = Evaluate(formula, probe, budget);
                Assert.Equal(structural, formulaValue);
            }
        }
    }

    [Fact]
    public void TrigonometricPolynomialGrammarUsesHalfAngleProofs()
    {
        InputExpression x = Variable();
        InputExpression sin = Function("sin", x);
        InputExpression cos = Function("cos", x);
        InputExpression[] grammar =
        [
            Multiply(sin, cos),
            Subtract(Add(Power(sin, 2), Power(cos, 2)), Number(1)),
            Subtract(Power(cos, 2), Power(sin, 2))
        ];

        foreach (InputExpression expression in grammar)
        {
            AnalysisRequest request = Request(
                expression,
                AnalysisFeatures.Domain |
                AnalysisFeatures.Parity |
                AnalysisFeatures.Zeros |
                AnalysisFeatures.Minima |
                AnalysisFeatures.Maxima |
                AnalysisFeatures.InflectionPoints |
                AnalysisFeatures.Monotonicity |
                AnalysisFeatures.Period);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            Assert.Equal(ProofState.Proved, report.Domain.State);
            Assert.Equal(ProofState.Proved, report.Parity.State);
            Assert.Equal(ProofState.Proved, report.Zeros.State);
            Assert.Equal(ProofState.Proved, report.Minima.State);
            Assert.Equal(ProofState.Proved, report.Maxima.State);
            Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
            Assert.Equal(ProofState.Proved, report.Monotonicity.State);
            Assert.Equal(ProofState.Proved, report.Period.State);
            AssertReplay(request, report.Expression!, report.Zeros);
            AssertReplay(request, report.Expression!, report.Period);
        }
    }

    [Fact]
    public void CampaignPrimitiveProofsReplayAndRejectMutation()
    {
        InputExpression x = Variable();
        InputExpression[] grammar =
        [
            Function("exp", x),
            Function("sinh", x),
            Function("cosh", x),
            Function("log", Subtract(Multiply(Number(2), x), Number(4))),
            Function("root", x, Number(3)),
            Negate(Function("sin", x)),
            Function(
                "tan",
                Subtract(Multiply(Number(2), x), Number(1))),
            Add(Power(Function("sin", x), 2), Power(Function("cos", x), 2))
        ];

        foreach (InputExpression expression in grammar)
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
            Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
            Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
            Assert.Equal(ProofState.Proved, report.ObliqueAsymptotes.State);
            Assert.Equal(ProofState.Proved, report.Monotonicity.State);
            Assert.Equal(ProofState.Proved, report.Period.State);

            AssertReplay(request, report.Expression!, report.Domain);
            AssertReplay(request, report.Expression!, report.Range);
            AssertReplay(request, report.Expression!, report.Parity);
            AssertReplay(request, report.Expression!, report.Zeros);
            AssertReplay(request, report.Expression!, report.YIntercept);
            AssertReplay(request, report.Expression!, report.Minima);
            AssertReplay(request, report.Expression!, report.Maxima);
            AssertReplay(request, report.Expression!, report.InflectionPoints);
            AssertReplay(request, report.Expression!, report.VerticalAsymptotes);
            AssertReplay(request, report.Expression!, report.HorizontalAsymptotes);
            AssertReplay(request, report.Expression!, report.ObliqueAsymptotes);
            AssertReplay(request, report.Expression!, report.Monotonicity);
            AssertReplay(request, report.Expression!, report.Period);
        }

        AnalysisRequest mutationRequest = Request(grammar[0], AnalysisFeatures.Range);
        AnalysisReport mutationReport = AnalysisEngine.Analyze(mutationRequest);
        RealSet range = AssertProved(mutationReport.Range);
        var certificate = Assert.IsType<TheoremProofCertificate>(mutationReport.Range.Certificate);
        TheoremProofCertificate changedParameters = certificate with
        {
            Parameters = certificate.Parameters.Add("mutated")
        };
        Assert.False(CertificateChecker.Check(
            mutationRequest,
            mutationReport.Expression!,
            ProofOutcome<RealSet>.Proved(range, changedParameters)));
    }

    [Fact]
    public void ExactScalarAffineTrigProofsReplayAndRejectMutation()
    {
        InputExpression sine = Function("sin", Variable());
        InputExpression piScaled = Multiply(Named("pi"), sine);
        AnalysisRequest request = Request(piScaled, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(ProofState.Proved, report.Parity.State);
        Assert.Equal(ProofState.Proved, report.Zeros.State);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.Equal(ProofState.Proved, report.Minima.State);
        Assert.Equal(ProofState.Proved, report.Maxima.State);
        Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
        Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.ObliqueAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.Monotonicity.State);
        Assert.Equal(ProofState.Proved, report.Period.State);

        AssertReplay(request, report.Expression!, report.Domain);
        AssertReplay(request, report.Expression!, report.Range);
        AssertReplay(request, report.Expression!, report.Parity);
        AssertReplay(request, report.Expression!, report.Zeros);
        AssertReplay(request, report.Expression!, report.YIntercept);
        AssertReplay(request, report.Expression!, report.Minima);
        AssertReplay(request, report.Expression!, report.Maxima);
        AssertReplay(request, report.Expression!, report.InflectionPoints);
        AssertReplay(request, report.Expression!, report.VerticalAsymptotes);
        AssertReplay(request, report.Expression!, report.HorizontalAsymptotes);
        AssertReplay(request, report.Expression!, report.ObliqueAsymptotes);
        AssertReplay(request, report.Expression!, report.Monotonicity);
        AssertReplay(request, report.Expression!, report.Period);

        AnalysisRequest eRequest = Request(
            Multiply(Named("e"), sine),
            AnalysisFeatures.Range | AnalysisFeatures.Monotonicity);
        AnalysisReport eReport = AnalysisEngine.Analyze(eRequest);
        AssertReplay(eRequest, eReport.Expression!, eReport.Range);
        AssertReplay(eRequest, eReport.Expression!, eReport.Monotonicity);

        AnalysisRequest algebraicRequest = Request(
            Multiply(Function("sqrt", Number(2)), sine),
            AnalysisFeatures.Range | AnalysisFeatures.Minima | AnalysisFeatures.Maxima);
        AnalysisReport algebraicReport = AnalysisEngine.Analyze(algebraicRequest);
        AssertReplay(algebraicRequest, algebraicReport.Expression!, algebraicReport.Range);
        AssertReplay(algebraicRequest, algebraicReport.Expression!, algebraicReport.Minima);
        AssertReplay(algebraicRequest, algebraicReport.Expression!, algebraicReport.Maxima);

        RealSet range = AssertProved(report.Range);
        var certificate = Assert.IsType<ExactCoefficientProofCertificate>(
            report.Range.Certificate);
        ExactCoefficientProofCertificate changedPattern = certificate with
        {
            PatternCanonical = certificate.PatternCanonical + ":changed"
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(range, changedPattern)));

        AnalysisReport cancelledTangent = AnalysisEngine.Analyze(Request(
            Multiply(
                Subtract(Named("pi"), Named("pi")),
                Function("tan", Variable())),
            AnalysisFeatures.Domain | AnalysisFeatures.Period));
        Assert.Equal(ProofState.Proved, cancelledTangent.Domain.State);
        Periodicity cancelledPeriod = AssertProved(cancelledTangent.Period);
        Assert.Equal(
            PeriodicityKind.PeriodicWithFundamentalPeriod,
            cancelledPeriod.Kind);
        Assert.Equal(
            "pi:1:0",
            ExactRealCanonical.Format(cancelledPeriod.FundamentalPeriod!));

        InputExpression retainedHole = Add(
            piScaled,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(1)))));
        AnalysisReport holeReport = AnalysisEngine.Analyze(Request(
            retainedHole,
            AnalysisFeatures.Domain | AnalysisFeatures.Parity | AnalysisFeatures.Period));
        Assert.Equal(ProofState.Proved, holeReport.Domain.State);
        Assert.Equal(ProofState.Unknown, holeReport.Parity.State);
        Assert.Equal(UnknownReason.UnsupportedFragment, holeReport.Parity.UnknownReason);
        Assert.Equal(ProofState.Unknown, holeReport.Period.State);
        Assert.Equal(UnknownReason.UnsupportedFragment, holeReport.Period.UnknownReason);

        ExactInteger largeFactor = ExactInteger.One << (AnalysisLimits.CoefficientBits / 2 + 1);
        InputExpression oversizedScalar = Multiply(
            Multiply(Number(new BigRational(largeFactor)), Named("pi")),
            Number(new BigRational(largeFactor)));
        AnalysisReport oversizedReport = AnalysisEngine.Analyze(Request(
            Multiply(oversizedScalar, sine),
            AnalysisFeatures.Range));
        Assert.Equal(ProofState.Unknown, oversizedReport.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, oversizedReport.Range.UnknownReason);
    }

    [Fact]
    public void NonRadianShiftedTrigValuesUseUnitAwareExactValues()
    {
        InputExpression argument = Subtract(
            Multiply(Number(2), Variable()),
            Number(1));
        var request = new AnalysisRequest(
            Function("tan", argument),
            AnalysisFeatures.Parity | AnalysisFeatures.YIntercept,
            AngleUnit.Degrees,
            "x",
            static () => true);

        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(FunctionParity.Neither, AssertProved(report.Parity));
        Assert.Equal(
            "fn:negate(fn:tan(pi:1/180:0))",
            ExactRealCanonical.Format(AssertProved(report.YIntercept).Value!));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Parity));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.YIntercept));

        AnalysisRequest grads = request with { AngleUnit = AngleUnit.Grads };
        Assert.False(CertificateChecker.Check(grads, report.Expression!, report.Parity));
        Assert.False(CertificateChecker.Check(grads, report.Expression!, report.YIntercept));
    }

    [Fact]
    public void LinearDriftTrigBoundaryProofsAreExactReplayableAndDomainAware()
    {
        InputExpression sine = Function("sin", Variable());
        InputExpression expression = Add(sine, Variable());
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
        Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.ObliqueAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.Monotonicity.State);
        Assert.Equal(ProofState.Proved, report.Period.State);

        var zero = Assert.IsType<PointSet>(AssertProved(report.Zeros));
        Assert.Equal(BigRational.Zero, Assert.IsType<RationalReal>(Assert.Single(zero.Points)).Value);
        Assert.Empty(AssertProved(report.Minima));
        Assert.Empty(AssertProved(report.Maxima));
        Assert.Equal(FunctionParity.Odd, AssertProved(report.Parity));
        Assert.Equal(
            Monotonicity.Increasing,
            Assert.Single(AssertProved(report.Monotonicity)).Direction);
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            AssertProved(report.Period).Kind);

        var inflection = Assert.IsType<IntegerAffineFeaturePoint>(
            Assert.Single(AssertProved(report.InflectionPoints)));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(inflection.XStep));
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(inflection.YStep));

        AssertReplay(request, report.Expression!, report.Domain);
        AssertReplay(request, report.Expression!, report.Range);
        AssertReplay(request, report.Expression!, report.Parity);
        AssertReplay(request, report.Expression!, report.Zeros);
        AssertReplay(request, report.Expression!, report.YIntercept);
        AssertReplay(request, report.Expression!, report.Minima);
        AssertReplay(request, report.Expression!, report.Maxima);
        AssertReplay(request, report.Expression!, report.InflectionPoints);
        AssertReplay(request, report.Expression!, report.VerticalAsymptotes);
        AssertReplay(request, report.Expression!, report.HorizontalAsymptotes);
        AssertReplay(request, report.Expression!, report.ObliqueAsymptotes);
        AssertReplay(request, report.Expression!, report.Monotonicity);
        AssertReplay(request, report.Expression!, report.Period);

        ImmutableArray<FeaturePoint> changedPoints =
        [
            inflection with { YStep = new RationalReal(BigRational.Zero) }
        ];
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<FeaturePoint>>.Proved(
                changedPoints,
                report.InflectionPoints.Certificate!)));
        var theoremCertificate = Assert.IsType<TheoremProofCertificate>(
            report.InflectionPoints.Certificate);
        TheoremProofCertificate forgedClaim = theoremCertificate with
        {
            Claim = ClaimCanonical.For(changedPoints)
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<ImmutableArray<FeaturePoint>>.Proved(
                changedPoints,
                forgedClaim)));

        foreach (InputExpression equivalent in new[]
                 {
                     Add(Variable(), sine),
                     Subtract(Variable(), sine),
                     Add(Negate(Variable()), sine),
                     Add(Multiply(Number(2), Variable()), sine)
                 })
        {
            AnalysisReport equivalentReport = AnalysisEngine.Analyze(Request(
                equivalent,
                AnalysisFeatures.Range |
                AnalysisFeatures.Parity |
                AnalysisFeatures.Minima |
                AnalysisFeatures.Maxima |
                AnalysisFeatures.InflectionPoints |
                AnalysisFeatures.Monotonicity |
                AnalysisFeatures.Period));
            Assert.Equal(ProofState.Proved, equivalentReport.Range.State);
            Assert.Equal(ProofState.Proved, equivalentReport.Parity.State);
            Assert.Equal(ProofState.Proved, equivalentReport.Minima.State);
            Assert.Equal(ProofState.Proved, equivalentReport.Maxima.State);
            Assert.Equal(ProofState.Proved, equivalentReport.InflectionPoints.State);
            Assert.Equal(ProofState.Proved, equivalentReport.Monotonicity.State);
            Assert.Equal(ProofState.Proved, equivalentReport.Period.State);
        }

        InputExpression retainedHole = Add(
            expression,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(1)))));
        AnalysisReport holeReport = AnalysisEngine.Analyze(Request(
            retainedHole,
            AnalysisFeatures.Domain | AnalysisFeatures.Range | AnalysisFeatures.Monotonicity));
        Assert.Equal(ProofState.Proved, holeReport.Domain.State);
        Assert.Equal(ProofState.Unknown, holeReport.Range.State);
        Assert.Equal(ProofState.Unknown, holeReport.Monotonicity.State);
    }

    [Fact]
    public void OscillatoryLinearDriftPublishesOnlyFeaturesAlreadyProved()
    {
        InputExpression expression = Add(
            Divide(Variable(), Number(2)),
            Function("sin", Variable()));
        AnalysisRequest request = Request(expression, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal(ProofState.Proved, report.Range.State);
        Assert.Equal(ProofState.Proved, report.Parity.State);
        Assert.Equal(ProofState.Unknown, report.Zeros.State);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        Assert.Equal(ProofState.Proved, report.Minima.State);
        Assert.Equal(ProofState.Proved, report.Maxima.State);
        var minimum = Assert.IsType<IntegerAffineFeaturePoint>(
            Assert.Single(AssertProved(report.Minima)));
        var maximum = Assert.IsType<IntegerAffineFeaturePoint>(
            Assert.Single(AssertProved(report.Maxima)));
        Assert.Equal("pi:4/3:0", ExactRealCanonical.Format(minimum.XOffset));
        Assert.Equal("pi:2:0", ExactRealCanonical.Format(minimum.XStep));
        Assert.Equal("pi:2/3:0", ExactRealCanonical.Format(maximum.XOffset));
        Assert.Equal("pi:2:0", ExactRealCanonical.Format(maximum.XStep));
        Assert.Equal(ProofState.Proved, report.InflectionPoints.State);
        Assert.Equal(ProofState.Proved, report.VerticalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.HorizontalAsymptotes.State);
        Assert.Equal(ProofState.Proved, report.ObliqueAsymptotes.State);
        Assert.Equal(ProofState.Unknown, report.Monotonicity.State);
        Assert.Equal(ProofState.Proved, report.Period.State);
        AssertReplay(request, report.Expression!, report.Minima);
        AssertReplay(request, report.Expression!, report.Maxima);
    }

    [Fact]
    public void LinearDriftDerivedCoefficientsRespectTheGlobalLimit()
    {
        ExactInteger large = ExactInteger.One << (AnalysisLimits.CoefficientBits / 2 + 100);
        InputExpression expression = Add(
            Multiply(Number(new BigRational(large)), Variable()),
            Function(
                "sin",
                Multiply(Number(new BigRational(ExactInteger.One, large)), Variable())));

        AnalysisReport report = AnalysisEngine.Analyze(Request(
            expression,
            AnalysisFeatures.All));

        Assert.Equal(ProofState.Unknown, report.InflectionPoints.State);
        Assert.Equal(UnknownReason.BudgetExceeded, report.InflectionPoints.UnknownReason);
    }

    [Fact]
    public void VariablePowerDomainUsesGenericIntegerPreimageMetadata()
    {
        InputExpression x = Variable();
        InputExpression expression = Power(Function("sin", x), Function("tan", x));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Domain);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet domain = AssertProved(report.Domain);

        var union = Assert.IsType<UnionSet>(domain);
        Assert.Single(union.Operands.OfType<PeriodicIntervalSet>());
        Assert.Equal(2, union.Operands.OfType<IntegerLatticeSet>().Count());
        Assert.All(
            union.Operands.OfType<IntegerLatticeSet>(),
            family => Assert.Contains("arctan", family.Expression, StringComparison.Ordinal));
        AssertReplay(request, report.Expression!, report.Domain);
    }

    [Fact]
    public void VariablePowerProvesWindowsKnownEmptyFeatures()
    {
        InputExpression x = Variable();
        InputExpression expression = Power(Function("sin", x), Function("tan", x));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Zeros |
            AnalysisFeatures.YIntercept |
            AnalysisFeatures.HorizontalAsymptotes);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.True(AssertProved(report.Zeros).IsEmpty);
        Assert.False(AssertProved(report.YIntercept).HasValue);
        Assert.Empty(AssertProved(report.HorizontalAsymptotes));
        AssertReplay(request, report.Expression!, report.Zeros);
        AssertReplay(request, report.Expression!, report.YIntercept);
        AssertReplay(request, report.Expression!, report.HorizontalAsymptotes);
    }

    [Fact]
    public void EmptyAndIsolatedDomainsAreNotConfusedWithUnknown()
    {
        AnalysisReport empty = AnalysisEngine.Analyze(Request(
            Divide(Number(0), Number(0)),
            AnalysisFeatures.All));
        Assert.True(AssertProved(empty.Domain).IsEmpty);
        Assert.True(AssertProved(empty.Range).IsEmpty);
        Assert.True(AssertProved(empty.Zeros).IsEmpty);
        Assert.Empty(AssertProved(empty.Minima));
        Assert.Empty(AssertProved(empty.Monotonicity));

        InputExpression isolatedExpression = Function(
            "sqrt",
            Negate(Power(Variable(), 2)));
        AnalysisReport isolated = AnalysisEngine.Analyze(Request(
            isolatedExpression,
            AnalysisFeatures.All));
        Assert.IsType<PointSet>(AssertProved(isolated.Domain));
        Assert.IsType<PointSet>(AssertProved(isolated.Range));
        Assert.Empty(AssertProved(isolated.Minima));
        Assert.Empty(AssertProved(isolated.Maxima));
        Assert.Empty(AssertProved(isolated.Monotonicity));
    }

    [Fact]
    public void EndpointsPlateausPolesAndRepeatedRootsFollowLogicalDefinitions()
    {
        AnalysisReport squareRoot = AnalysisEngine.Analyze(Request(
            Function("sqrt", Variable()),
            AnalysisFeatures.Minima | AnalysisFeatures.Maxima | AnalysisFeatures.Monotonicity));
        Assert.Single(AssertProved(squareRoot.Minima));
        Assert.Empty(AssertProved(squareRoot.Maxima));

        AnalysisReport plateau = AnalysisEngine.Analyze(Request(
            Number(3),
            AnalysisFeatures.Minima | AnalysisFeatures.Maxima | AnalysisFeatures.Monotonicity));
        Assert.Empty(AssertProved(plateau.Minima));
        Assert.Empty(AssertProved(plateau.Maxima));
        Assert.Equal(Monotonicity.Constant, Assert.Single(AssertProved(plateau.Monotonicity)).Direction);

        InputExpression hole = Divide(
            Power(Subtract(Variable(), Number(1)), 2),
            Subtract(Variable(), Number(1)));
        AnalysisReport repeated = AnalysisEngine.Analyze(Request(
            hole,
            AnalysisFeatures.Domain | AnalysisFeatures.Zeros | AnalysisFeatures.VerticalAsymptotes));
        Assert.False(AssertProved(repeated.Domain).IsEmpty);
        Assert.True(AssertProved(repeated.Zeros).IsEmpty);
        Assert.Empty(AssertProved(repeated.VerticalAsymptotes));
    }

    [Fact]
    public void AlgebraicRootsRemainExactWithoutRadicalFormatting()
    {
        InputExpression polynomial = Subtract(
            Subtract(Power(Variable(), 3), Variable()),
            Number(1));
        AnalysisReport report = AnalysisEngine.Analyze(Request(
            polynomial,
            AnalysisFeatures.Zeros));
        var roots = Assert.IsType<PointSet>(AssertProved(report.Zeros));
        Assert.IsType<AlgebraicReal>(Assert.Single(roots.Points));
    }

    [Fact]
    public void SparseMultivariateArithmeticRemainsExactAndBudgeted()
    {
        var budget = new ResourceBudget();
        SparseMultivariatePolynomial polynomial = SparseMultivariatePolynomial.Create(
            2,
            [
                new(new Monomial([2, 0]), 1),
                new(new Monomial([1, 1]), 3),
                new(new Monomial([0, 1]), -1)
            ],
            budget);

        Assert.Equal(2, polynomial.TotalDegree);
        Assert.Equal(
            new BigRational(29),
            polynomial.Evaluate([new BigRational(2), new BigRational(5)], budget));

        SparseMultivariatePolynomial derivative = polynomial.Differentiate(0, budget);
        Assert.Equal(
            new BigRational(19),
            derivative.Evaluate([new BigRational(2), new BigRational(5)], budget));

        SparseMultivariatePolynomial squared = polynomial.Multiply(polynomial, budget);
        Assert.Equal(4, squared.TotalDegree);
        Assert.Equal(
            new BigRational(841),
            squared.Evaluate([new BigRational(2), new BigRational(5)], budget));
    }

    [Fact]
    public void CooperEliminationProvesEqualityAndUnitBoundProjections()
    {
        var budget = new ResourceBudget();
        var equality = new PresburgerComparison(
            LinearIntegerExpression.From(0, ("n", 2), ("x", -1)),
            IntegerRelation.Equal);

        Assert.True(CooperEliminator.TryEliminateExists(
            "n",
            equality,
            budget,
            out PresburgerFormula equalityProjection));
        var divisibility = Assert.IsType<PresburgerDivisibility>(equalityProjection);
        Assert.Equal(new ExactInteger(2), divisibility.Divisor);
        Assert.Equal(new ExactInteger(-1), divisibility.Expression.Coefficient("x"));

        PresburgerFormula bounds = PresburgerNormalizer.And(
        [
            new PresburgerComparison(
                LinearIntegerExpression.From(0, ("n", -1), ("x", 1)),
                IntegerRelation.LessOrEqual),
            new PresburgerComparison(
                LinearIntegerExpression.From(0, ("n", 1), ("y", -1)),
                IntegerRelation.LessOrEqual)
        ]);
        Assert.True(CooperEliminator.TryEliminateExists(
            "n",
            bounds,
            budget,
            out PresburgerFormula boundProjection));
        var projectedBound = Assert.IsType<PresburgerComparison>(boundProjection);
        Assert.Equal(new ExactInteger(1), projectedBound.Expression.Coefficient("x"));
        Assert.Equal(new ExactInteger(-1), projectedBound.Expression.Coefficient("y"));
        Assert.Equal(ExactInteger.Zero, projectedBound.Expression.Constant);
    }

    [Fact]
    public void DeterministicLimitsReturnUnknownAndCancellationNeverPublishes()
    {
        InputExpression excessiveDegree = Power(Variable(), 257);
        AnalysisReport degreeReport = AnalysisEngine.Analyze(Request(
            excessiveDegree,
            AnalysisFeatures.All));
        Assert.Equal(ProofState.Unknown, degreeReport.Domain.State);
        Assert.Equal(UnknownReason.BudgetExceeded, degreeReport.Domain.UnknownReason);

        ExactInteger huge = ExactInteger.One << (AnalysisLimits.CoefficientBits + 1);
        AnalysisReport coefficientReport = AnalysisEngine.Analyze(Request(
            Multiply(Number(new BigRational(huge)), Variable()),
            AnalysisFeatures.All));
        Assert.Equal(UnknownReason.BudgetExceeded, coefficientReport.Domain.UnknownReason);

        var cellBudget = new ResourceBudget();
        Assert.Throws<BudgetExceededException>(() => cellBudget.AddCells(AnalysisLimits.Cells + 1));

        AnalysisRequest checkedRequest = Request(Variable(), AnalysisFeatures.Domain);
        AnalysisReport checkedReport = AnalysisEngine.Analyze(checkedRequest);
        Assert.Equal(ProofState.Proved, checkedReport.Domain.State);
        var exhaustedCheckerBudget = new ResourceBudget();
        exhaustedCheckerBudget.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() => CertificateChecker.Check(
            checkedRequest,
            checkedReport.Expression!,
            checkedReport.Domain,
            exhaustedCheckerBudget));

        AnalysisRequest cancelled = new(
            Variable(),
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => false);
        Assert.ThrowsAny<OperationCanceledException>(() => AnalysisEngine.Analyze(cancelled));
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        ProofOutcome<T> outcome)
    {
        if (outcome.State != ProofState.Proved)
        {
            Assert.Equal(ProofState.Unknown, outcome.State);
            Assert.Null(outcome.Certificate);
            return;
        }

        Assert.NotNull(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, expression, outcome));
    }

    private static T AssertProved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return outcome.Value;
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static SemanticExpression Build(InputExpression expression)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
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

    private static InputExpression Power(InputExpression basis, int exponent)
    {
        return Power(basis, Number(exponent));
    }

    private static InputExpression Power(InputExpression basis, InputExpression exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);
    }

    private static InputExpression Negate(InputExpression value)
    {
        return InputExpression.Unary(InputExpressionKind.Negate, value, Source);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }

    private static bool Evaluate(
        PolynomialFormula formula,
        BigRational value,
        ResourceBudget budget)
    {
        ImmutableArray<UnivariatePolynomial> atoms = PolynomialFormulaConverter.Atoms(formula);
        var signs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnivariatePolynomial polynomial in atoms)
        {
            signs.Add(polynomial.Canonical, polynomial.Evaluate(value, budget).Sign);
        }

        return PolynomialFormulaConverter.Evaluate(formula, signs);
    }

    private static bool TryEvaluate(
        InputExpression expression,
        BigRational variable,
        out BigRational value)
    {
        switch (expression.Kind)
        {
            case InputExpressionKind.Constant:
                value = expression.Constant;
                return true;
            case InputExpressionKind.Variable:
                value = variable;
                return true;
            case InputExpressionKind.Negate:
                if (TryEvaluate(expression.Arguments[0], variable, out BigRational negated))
                {
                    value = -negated;
                    return true;
                }

                break;
            case InputExpressionKind.Add:
            case InputExpressionKind.Multiply:
            case InputExpressionKind.Divide:
                if (TryEvaluate(expression.Arguments[0], variable, out BigRational left) &&
                    TryEvaluate(expression.Arguments[1], variable, out BigRational right) &&
                    (expression.Kind != InputExpressionKind.Divide || !right.IsZero))
                {
                    value = expression.Kind switch
                    {
                        InputExpressionKind.Add => left + right,
                        InputExpressionKind.Multiply => left * right,
                        InputExpressionKind.Divide => left / right,
                        _ => throw new InvalidOperationException()
                    };
                    return true;
                }

                break;
        }

        value = default;
        return false;
    }
}
