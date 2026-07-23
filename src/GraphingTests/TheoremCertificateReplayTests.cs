using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class TheoremCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    public static TheoryData<string, int> AffineCases => new()
    {
        { "sin", (int)AngleUnit.Radians },
        { "sin", (int)AngleUnit.Degrees },
        { "sin", (int)AngleUnit.Grads },
        { "cos", (int)AngleUnit.Radians },
        { "cos", (int)AngleUnit.Degrees },
        { "cos", (int)AngleUnit.Grads },
        { "tan", (int)AngleUnit.Radians },
        { "tan", (int)AngleUnit.Degrees },
        { "tan", (int)AngleUnit.Grads }
    };

    public static TheoryData<string, int> InverseCases => new()
    {
        { "asin", (int)AngleUnit.Radians },
        { "asin", (int)AngleUnit.Degrees },
        { "asin", (int)AngleUnit.Grads },
        { "acos", (int)AngleUnit.Radians },
        { "acos", (int)AngleUnit.Degrees },
        { "acos", (int)AngleUnit.Grads },
        { "atan", (int)AngleUnit.Radians },
        { "atan", (int)AngleUnit.Degrees },
        { "atan", (int)AngleUnit.Grads }
    };

    public static TheoryData<string, int> ReciprocalCases => new()
    {
        { "cot", (int)AngleUnit.Radians },
        { "cot", (int)AngleUnit.Degrees },
        { "cot", (int)AngleUnit.Grads },
        { "sec", (int)AngleUnit.Radians },
        { "sec", (int)AngleUnit.Degrees },
        { "sec", (int)AngleUnit.Grads },
        { "csc", (int)AngleUnit.Radians },
        { "csc", (int)AngleUnit.Degrees },
        { "csc", (int)AngleUnit.Grads }
    };

    public static TheoryData<string, int> AffineHoleCases => new()
    {
        { "sin", (int)AngleUnit.Radians },
        { "sin", (int)AngleUnit.Degrees },
        { "sin", (int)AngleUnit.Grads },
        { "cos", (int)AngleUnit.Radians },
        { "cos", (int)AngleUnit.Degrees },
        { "cos", (int)AngleUnit.Grads },
        { "tan", (int)AngleUnit.Radians },
        { "tan", (int)AngleUnit.Degrees },
        { "tan", (int)AngleUnit.Grads }
    };

    [Theory]
    [MemberData(nameof(AffineCases))]
    public void AffineTheoremsReplayEveryFeatureIndependently(
        string function,
        int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression expression = Add(
            Multiply(
                Number(function == "cos" ? -3 : 2),
                Function(
                    function,
                    Add(Multiply(Number(3), Variable()), Number(1)))),
            Number(1));
        AnalysisRequest request = Request(expression, angleUnit, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllProvedAndReplay(request, report);
        Assert.Equal(
            function switch
            {
                "sin" => TheoremRule.AffineSine,
                "cos" => TheoremRule.AffineCosine,
                _ => TheoremRule.AffineTangent
            },
            Assert.IsType<TheoremProofCertificate>(report.Range.Certificate).Theorem);
    }

    [Theory]
    [MemberData(nameof(InverseCases))]
    public void InversePrimitiveTheoremsReplayEveryFeatureIndependently(
        string function,
        int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression expression = Function(
            function,
            Multiply(Number(2), Variable()));
        AnalysisRequest request = Request(expression, angleUnit, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllProvedAndReplay(request, report);
        Assert.Equal(
            TheoremRule.InversePrimitive,
            Assert.IsType<TheoremProofCertificate>(report.Range.Certificate).Theorem);
    }

    [Theory]
    [MemberData(nameof(ReciprocalCases))]
    public void ReciprocalTheoremsReplayEveryFeatureIndependently(
        string function,
        int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression expression = Function(
            function,
            Add(Multiply(Number(2), Variable()), Number(1)));
        AnalysisRequest request = Request(expression, angleUnit, AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllProvedAndReplay(request, report);
        Assert.Equal(
            TheoremRule.AffineReciprocalTrigonometric,
            Assert.IsType<TheoremProofCertificate>(report.Range.Certificate).Theorem);
    }

    [Fact]
    public void LinearDriftTheoremReplaysEveryPublishedFeatureIndependently()
    {
        InputExpression expression = Add(Variable(), Function("sin", Variable()));
        AnalysisRequest request = Request(
            expression,
            AngleUnit.Radians,
            AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAllProvedAndReplay(request, report);
        Assert.Equal(
            TheoremRule.LinearDriftTrigonometric,
            Assert.IsType<TheoremProofCertificate>(report.Range.Certificate).Theorem);
    }

    [Theory]
    [InlineData((int)AngleUnit.Radians)]
    [InlineData((int)AngleUnit.Degrees)]
    [InlineData((int)AngleUnit.Grads)]
    public void HalfAngleTheoremReplaysEveryPublishedFeatureIndependently(
        int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression expression = Multiply(
            Function("sin", Variable()),
            Function("cos", Variable()));
        AnalysisRequest request = Request(
            expression,
            angleUnit,
            AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertProvedAndReplay(request, report.Expression!, report.Domain);
        AssertProvedAndReplay(request, report.Expression!, report.Range);
        AssertProvedAndReplay(request, report.Expression!, report.Parity);
        AssertProvedAndReplay(request, report.Expression!, report.Zeros);
        AssertProvedAndReplay(request, report.Expression!, report.YIntercept);
        AssertProvedAndReplay(request, report.Expression!, report.Minima);
        AssertProvedAndReplay(request, report.Expression!, report.Maxima);
        AssertProvedAndReplay(request, report.Expression!, report.InflectionPoints);
        AssertProvedAndReplay(request, report.Expression!, report.VerticalAsymptotes);
        AssertProvedAndReplay(request, report.Expression!, report.HorizontalAsymptotes);
        AssertProvedAndReplay(request, report.Expression!, report.ObliqueAsymptotes);
        AssertProvedAndReplay(request, report.Expression!, report.Monotonicity);
        AssertProvedAndReplay(request, report.Expression!, report.Period);
        Assert.Equal(
            TheoremRule.TrigonometricPolynomial,
            Assert.IsType<TheoremProofCertificate>(report.Zeros.Certificate).Theorem);
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees)]
    [InlineData((int)AngleUnit.Grads)]
    public void NonRadianAffineReplayRejectsRawRadianZeroAndInterceptClaims(
        int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression expression = Add(
            Multiply(
                Number(2),
                Function(
                    "sin",
                    Add(Multiply(Number(3), Variable()), Number(1)))),
            Number(1));
        AnalysisRequest unitRequest = Request(
            expression,
            angleUnit,
            AnalysisFeatures.Zeros | AnalysisFeatures.YIntercept);
        AnalysisRequest radianRequest = Request(
            expression,
            AngleUnit.Radians,
            AnalysisFeatures.Zeros | AnalysisFeatures.YIntercept);
        AnalysisReport unitReport = AnalysisEngine.Analyze(unitRequest);
        AnalysisReport radianReport = AnalysisEngine.Analyze(radianRequest);

        var unitZeroCertificate = Assert.IsType<TheoremProofCertificate>(
            unitReport.Zeros.Certificate);
        RealSet rawRadianZeros = AssertProved(radianReport.Zeros);
        TheoremProofCertificate forgedZero = unitZeroCertificate with
        {
            Claim = ClaimCanonical.For(rawRadianZeros)
        };
        Assert.False(CertificateChecker.Check(
            unitRequest,
            unitReport.Expression!,
            ProofOutcome<RealSet>.Proved(rawRadianZeros, forgedZero)));

        var unitInterceptCertificate = Assert.IsType<TheoremProofCertificate>(
            unitReport.YIntercept.Certificate);
        OptionalValue<ExactReal> rawRadianIntercept = AssertProved(
            radianReport.YIntercept);
        TheoremProofCertificate forgedIntercept = unitInterceptCertificate with
        {
            Claim = ClaimCanonical.For(rawRadianIntercept)
        };
        Assert.False(CertificateChecker.Check(
            unitRequest,
            unitReport.Expression!,
            ProofOutcome<OptionalValue<ExactReal>>.Proved(
                rawRadianIntercept,
                forgedIntercept)));
    }

    [Theory]
    [MemberData(nameof(AffineHoleCases))]
    public void AffineReplayRejectsRetainedHiddenHoles(
        string function,
        int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression core = Function(
            function,
            Add(Multiply(Number(2), Variable()), Number(1)));
        InputExpression hiddenHole = Add(
            core,
            Multiply(
                Number(0),
                Divide(
                    Number(1),
                    Subtract(Variable(), Number(2)))));
        SemanticExpression semantic = Build(hiddenHole);
        var budget = new ResourceBudget();
        Assert.True(TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(
            semantic.Value,
            "x",
            budget,
            out AffineTrigPattern pattern));
        Assert.NotEqual(Formula.True.Canonical, semantic.DefinedWhen.Canonical);

        AnalysisRequest cleanRequest = Request(
            core,
            angleUnit,
            AnalysisFeatures.Range);
        AnalysisReport clean = AnalysisEngine.Analyze(cleanRequest);
        RealSet range = AssertProved(clean.Range);
        var forged = new TheoremProofCertificate(
            AnalysisFeatures.Range,
            semantic.Value.Canonical,
            ClaimCanonical.For(range),
            function switch
            {
                "sin" => TheoremRule.AffineSine,
                "cos" => TheoremRule.AffineCosine,
                _ => TheoremRule.AffineTangent
            },
            [
                pattern.Canonical,
                angleUnit.ToString(),
                semantic.DefinedWhen.Canonical
            ]);
        AnalysisRequest holedRequest = Request(
            hiddenHole,
            angleUnit,
            AnalysisFeatures.Range);

        Assert.False(CertificateChecker.Check(
            holedRequest,
            semantic,
            ProofOutcome<RealSet>.Proved(range, forged)));
    }

    [Theory]
    [InlineData((int)AngleUnit.Radians)]
    [InlineData((int)AngleUnit.Degrees)]
    [InlineData((int)AngleUnit.Grads)]
    public void AffineTangentReplayRequiresItsExactPoleGuard(
        int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression expression = Function(
            "tan",
            Add(Multiply(Number(3), Variable()), Number(1)));
        AnalysisRequest request = Request(
            expression,
            angleUnit,
            AnalysisFeatures.Domain);
        SemanticExpression semantic = Build(expression);
        Assert.True(TrigonometricAndLatticeAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Domain,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        Assert.IsType<PeriodicIntervalSet>(AssertProved(outcome));
        Assert.True(CertificateChecker.Check(request, semantic, outcome));

        var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);
        SemanticExpression missingPoleGuard = semantic with
        {
            DefinedWhen = Formula.True,
            ContinuousWhen = Formula.True,
            DifferentiableWhen = Formula.True
        };
        var forged = certificate with
        {
            Claim = ClaimCanonical.For(AllRealSet.Instance),
            Parameters = certificate.Parameters.SetItem(2, Formula.True.Canonical)
        };

        Assert.False(CertificateChecker.Check(
            request,
            missingPoleGuard,
            ProofOutcome<RealSet>.Proved(AllRealSet.Instance, forged)));
    }

    [Fact]
    public void GenericTheoremReplayFailsClosedAndPreservesBudgetAndCancellation()
    {
        InputExpression expression = Function("sin", Variable());
        AnalysisRequest request = Request(
            expression,
            AngleUnit.Radians,
            AnalysisFeatures.All);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet zeros = AssertProved(report.Zeros);
        var certificate = Assert.IsType<TheoremProofCertificate>(
            report.Zeros.Certificate);

        TheoremProofCertificate defaultParameters = certificate with
        {
            Parameters = default
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(zeros, defaultParameters)));

        TheoremProofCertificate nullParameter = certificate with
        {
            Parameters = certificate.Parameters.SetItem(0, null!)
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(zeros, nullParameter)));

        TheoremProofCertificate combinedFeature = certificate with
        {
            ProvenFeature = AnalysisFeatures.Domain | AnalysisFeatures.Range
        };
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(zeros, combinedFeature)));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() => CertificateChecker.Check(
            request,
            report.Expression!,
            report.Zeros,
            exhausted));

        AnalysisRequest cancelled = request with { RevisionIsCurrent = static () => false };
        Assert.Throws<AnalysisCancelledException>(() => CertificateChecker.Check(
            cancelled,
            report.Expression!,
            report.Zeros));
    }

    private static void AssertAllProvedAndReplay(
        AnalysisRequest request,
        AnalysisReport report)
    {
        AssertProvedAndReplay(request, report.Expression!, report.Domain);
        AssertProvedAndReplay(request, report.Expression!, report.Range);
        AssertProvedAndReplay(request, report.Expression!, report.Parity);
        AssertProvedAndReplay(request, report.Expression!, report.Zeros);
        AssertProvedAndReplay(request, report.Expression!, report.YIntercept);
        AssertProvedAndReplay(request, report.Expression!, report.Minima);
        AssertProvedAndReplay(request, report.Expression!, report.Maxima);
        AssertProvedAndReplay(request, report.Expression!, report.InflectionPoints);
        AssertProvedAndReplay(request, report.Expression!, report.VerticalAsymptotes);
        AssertProvedAndReplay(request, report.Expression!, report.HorizontalAsymptotes);
        AssertProvedAndReplay(request, report.Expression!, report.ObliqueAsymptotes);
        AssertProvedAndReplay(request, report.Expression!, report.Monotonicity);
        AssertProvedAndReplay(request, report.Expression!, report.Period);
    }

    private static void AssertProvedAndReplay<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.True(CertificateChecker.Check(request, expression, outcome));
    }

    private static T AssertProved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome.Value!;
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AngleUnit angleUnit,
        AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, angleUnit, "x", static () => true);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return InputExpression.Number(new BigRational(value), Source);
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

    private static InputExpression Function(string name, InputExpression argument)
    {
        return InputExpression.Function(name, ImmutableArray.Create(argument), Source);
    }

    private static SemanticExpression Build(InputExpression expression)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
    }
}
