using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ExactPiFrequencyTangentAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);
    [Fact]
    public void GeneratedDirectAndGuardedFormsProveRationalPhaseParityAndIntercept()
    {
        foreach (ExactPiFrequencyTangentAnalysisTestsAmplitudeCase amplitude in Amplitudes())
        {
            foreach (int phase in new[]
            {
                -1,
                1
            }

            )
            {
                foreach (bool quotient in new[]
                {
                    false,
                    true
                }

                )
                {
                    InputExpression input = Tangent(amplitude.Expression, phase, quotient);
                    AnalysisRequest request = Request(input, AnalysisFeatures.Parity | AnalysisFeatures.YIntercept);
                    AnalysisReport report = AnalysisEngine.Analyze(request);
                    Assert.Equal(ProofState.Proved, report.Parity.State);
                    Assert.Equal(FunctionParity.Neither, report.Parity.Value);
                    AssertExactTangentCertificate(report.Parity.Certificate);
                    Assert.True(CertificateChecker.Check(request, report.Expression!, report.Parity));
                    Assert.Equal(ProofState.Proved, report.YIntercept.State);
                    OptionalValue<ExactReal> intercept = report.YIntercept.Value!;
                    Assert.True(intercept.HasValue);
                    Assert.Equal(amplitude.ExpectedIntercept(phase), ExactRealCanonical.Format(intercept.Value!));
                    AssertExactTangentCertificate(report.YIntercept.Certificate);
                    Assert.True(CertificateChecker.Check(request, report.Expression!, report.YIntercept));
                }
            }
        }
    }

    [Fact]
    public void RationalRadianPhaseUsesTheIrrationalityOfPiNotSampling()
    {
        foreach (BigRational phase in new[]
        {
            new BigRational(-17, 5),
            new BigRational(-1),
            new BigRational(1, 7),
            new BigRational(9)
        }

        )
        {
            InputExpression input = Tangent(Number(1), phase, quotient: false);
            AnalysisRequest request = Request(input, AnalysisFeatures.Parity);
            AnalysisReport report = AnalysisEngine.Analyze(request);
            Assert.Equal(FunctionParity.Neither, Proved(report.Parity));
            Assert.True(CertificateChecker.Check(request, report.Expression!, report.Parity));
        }
    }

    [Fact]
    public void QuarterPiPhasesUseExactTangentValuesForEveryAmplitudeAndSourceForm()
    {
        var amplitudes = new[]
        {
            new ExactPiFrequencyTangentAnalysisTestsQuarterAmplitudeCase(Number(1), "q:1", "q:-1"),
            new ExactPiFrequencyTangentAnalysisTestsQuarterAmplitudeCase(Number(-1), "q:-1", "q:1"),
            new ExactPiFrequencyTangentAnalysisTestsQuarterAmplitudeCase(Number(2), "q:2", "q:-2"),
            new ExactPiFrequencyTangentAnalysisTestsQuarterAmplitudeCase(Symbol("pi"), "pi:1:0", "pi:-1:0")
        };
        foreach (ExactPiFrequencyTangentAnalysisTestsQuarterAmplitudeCase amplitude in amplitudes)
        {
            foreach (int phaseSign in new[]
            {
                -1,
                1
            }

            )
            {
                foreach (bool quotient in new[]
                {
                    false,
                    true
                }

                )
                {
                    InputExpression phase = Divide(Symbol("pi"), Number(4));
                    if (phaseSign < 0)
                    {
                        phase = Multiply(Number(-1), phase);
                    }

                    InputExpression argument = Add(Multiply(Symbol("pi"), Variable()), phase);
                    InputExpression primitive = quotient ? Divide(Function("sin", argument), Function("cos", argument)) : Function("tan", argument);
                    InputExpression input = Multiply(amplitude.Expression, primitive);
                    AnalysisRequest request = Request(input, AnalysisFeatures.YIntercept);
                    AnalysisReport report = AnalysisEngine.Analyze(request);
                    OptionalValue<ExactReal> intercept = Proved(report.YIntercept);
                    Assert.True(intercept.HasValue);
                    Assert.Equal(phaseSign > 0 ? amplitude.Positive : amplitude.Negative, ExactRealCanonical.Format(intercept.Value!));
                    AssertExactTangentCertificate(report.YIntercept.Certificate);
                    Assert.True(CertificateChecker.Check(request, report.Expression!, report.YIntercept));
                }
            }
        }
    }

    [Fact]
    public void ProductionCheckerRejectsPatternEvidenceFeatureAndClaimMutations()
    {
        InputExpression input = Tangent(Symbol("pi"), -1, quotient: true);
        AnalysisRequest request = Request(input, AnalysisFeatures.Parity | AnalysisFeatures.YIntercept);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        FunctionParity parity = Proved(report.Parity);
        var certificate = Assert.IsType<ExactCoefficientProofCertificate>(report.Parity.Certificate);
        Assert.NotEmpty(certificate.OrderWitnesses);
        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":mutated" });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.YIntercept });
        AssertRejected(certificate with { Claim = "parity:Odd" });
        ExactOrderWitness first = certificate.OrderWitnesses[0];
        AssertRejected(certificate with { OrderWitnesses = certificate.OrderWitnesses.SetItem(0, first with { Upper = first.Upper + BigRational.One }) });
        Assert.False(CertificateChecker.Check(request, report.Expression!, ProofOutcome<FunctionParity>.Proved(FunctionParity.Odd, certificate)));
        return;
        void AssertRejected(ExactCoefficientProofCertificate changed) => Assert.False(CertificateChecker.Check(request, report.Expression!, ProofOutcome<FunctionParity>.Proved(parity, changed)));
    }

    [Fact]
    public void RadianIrrationalityCertificateCannotReplayInAnotherAngleUnit()
    {
        InputExpression input = Tangent(Number(1), 180, quotient: false);
        AnalysisRequest radians = Request(input, AnalysisFeatures.Parity);
        AnalysisReport report = AnalysisEngine.Analyze(radians);
        Assert.Equal(FunctionParity.Neither, Proved(report.Parity));
        AnalysisRequest degrees = new(input, AnalysisFeatures.Parity, AngleUnit.Degrees, "x", static () => true);
        AnalysisReport degreeReport = AnalysisEngine.Analyze(degrees);
        Assert.Equal(FunctionParity.Odd, Proved(degreeReport.Parity));
        Assert.Equal("q:0", ExactRealCanonical.Format(Proved(AnalysisEngine.Analyze(degrees with { Features = AnalysisFeatures.YIntercept }).YIntercept).Value!));
        Assert.False(CertificateChecker.Check(degrees, report.Expression!, report.Parity));
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, -180, true, "q:0")]
    [InlineData((int)AngleUnit.Degrees, 90, false, null)]
    [InlineData((int)AngleUnit.Grads, -200, true, "q:0")]
    [InlineData((int)AngleUnit.Grads, 100, false, null)]
    public void ExactDegreeAndGradPhasesKeepTheirOwnSpecialValues(int unit, int phase, bool hasIntercept, string? expectedIntercept)
    {
        InputExpression input = Tangent(Number(1), phase, quotient: false);
        AnalysisRequest request = new(input, AnalysisFeatures.Parity | AnalysisFeatures.YIntercept, (AngleUnit)unit, "x", static () => true);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.Equal(FunctionParity.Odd, Proved(report.Parity));
        OptionalValue<ExactReal> intercept = Proved(report.YIntercept);
        Assert.Equal(hasIntercept, intercept.HasValue);
        if (hasIntercept)
        {
            Assert.Equal(expectedIntercept, ExactRealCanonical.Format(intercept.Value!));
        }

        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Parity));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.YIntercept));
    }

    [Fact]
    public void SymbolicPhasesMismatchedRatiosAndRetainedHolesDoNotEnterTheTheorem()
    {
        InputExpression x = Variable();
        InputExpression symbolic = Function("tan", Add(Multiply(Symbol("pi"), x), Symbol("e")));
        AnalysisReport symbolicReport = AnalysisEngine.Analyze(Request(symbolic, AnalysisFeatures.Parity | AnalysisFeatures.YIntercept));
        AssertUnsupported(symbolicReport.Parity);
        AssertUnsupported(symbolicReport.YIntercept);
        InputExpression positiveArgument = Add(Multiply(Symbol("pi"), x), Number(1));
        InputExpression negativeArgument = Subtract(Multiply(Symbol("pi"), x), Number(1));
        InputExpression mismatched = Divide(Function("sin", positiveArgument), Function("cos", negativeArgument));
        AnalysisReport mismatchedReport = AnalysisEngine.Analyze(Request(mismatched, AnalysisFeatures.Parity | AnalysisFeatures.YIntercept));
        Assert.DoesNotContain(new[] { mismatchedReport.Parity.Certificate, mismatchedReport.YIntercept.Certificate }, static certificate => certificate is ExactCoefficientProofCertificate);
        InputExpression tangent = Function("tan", positiveArgument);
        InputExpression retainedHole = Multiply(tangent, Divide(x, x));
        AnalysisReport holeReport = AnalysisEngine.Analyze(Request(retainedHole, AnalysisFeatures.Parity | AnalysisFeatures.YIntercept));
        Assert.DoesNotContain(new[] { holeReport.Parity.Certificate, holeReport.YIntercept.Certificate }, static certificate => certificate is ExactCoefficientProofCertificate);
    }

    [Fact]
    public void CoefficientWorkAndCancellationLimitsFailClosed()
    {
        ExactInteger huge = ExactInteger.One << AnalysisLimits.CoefficientBits;
        InputExpression oversized = Tangent(Number(new BigRational(huge)), 1, quotient: false);
        AnalysisReport first = AnalysisEngine.Analyze(Request(oversized, AnalysisFeatures.Parity | AnalysisFeatures.YIntercept));
        AnalysisReport second = AnalysisEngine.Analyze(Request(oversized, AnalysisFeatures.Parity | AnalysisFeatures.YIntercept));
        AssertBudgetExceeded(first.Parity);
        AssertBudgetExceeded(first.YIntercept);
        Assert.Equal(first.ChargedWorkUnits, second.ChargedWorkUnits);
        InputExpression valid = Tangent(Number(1), 1, quotient: false);
        AnalysisRequest request = Request(valid, AnalysisFeatures.Parity);
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(valid);
        var exhaustedAnalyzer = new ResourceBudget();
        exhaustedAnalyzer.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() => ExactCoefficientAnalyzer.TryAnalyze(request, semantic, AnalysisFeatures.Parity, exhaustedAnalyzer, out ProofOutcome<FunctionParity> _));
        AnalysisReport validReport = AnalysisEngine.Analyze(request);
        var exhaustedChecker = new ResourceBudget();
        exhaustedChecker.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() => CertificateChecker.Check(request, validReport.Expression!, validReport.Parity, exhaustedChecker));
        var cancelledRequest = request with
        {
            RevisionIsCurrent = static () => false
        };
        Assert.Throws<AnalysisCancelledException>(() => AnalysisEngine.Analyze(cancelledRequest));
    }

    private static IEnumerable<ExactPiFrequencyTangentAnalysisTestsAmplitudeCase> Amplitudes()
    {
        yield return new ExactPiFrequencyTangentAnalysisTestsAmplitudeCase(Number(1), static phase => phase > 0 ? "fn:tan(q:1)" : "fn:negate(fn:tan(q:1))");
        yield return new ExactPiFrequencyTangentAnalysisTestsAmplitudeCase(Number(-1), static phase => phase > 0 ? "fn:negate(fn:tan(q:1))" : "fn:tan(q:1)");
        yield return new ExactPiFrequencyTangentAnalysisTestsAmplitudeCase(Number(2), static phase => phase > 0 ? "fn:scale(fn:tan(q:1),q:2)" : "fn:scale(fn:tan(q:1),q:-2)");
        yield return new ExactPiFrequencyTangentAnalysisTestsAmplitudeCase(Symbol("pi"), static phase => phase > 0 ? "fn:multiply(pi:1:0,fn:tan(q:1))" : "fn:multiply(pi:-1:0,fn:tan(q:1))");
    }

    private static InputExpression Tangent(InputExpression amplitude, int phase, bool quotient)
    {
        return Tangent(amplitude, new BigRational(phase), quotient);
    }

    private static InputExpression Tangent(InputExpression amplitude, BigRational phase, bool quotient)
    {
        InputExpression argument = Add(Multiply(Symbol("pi"), Variable()), Number(phase));
        InputExpression core = quotient ? Divide(Function("sin", argument), Function("cos", argument)) : Function("tan", argument);
        return Multiply(amplitude, core);
    }

    private static void AssertExactTangentCertificate(ProofCertificate? candidate)
    {
        var certificate = Assert.IsType<ExactCoefficientProofCertificate>(candidate);
        Assert.Equal(ExactCoefficientPatternKind.AffineTangent, certificate.PatternKind);
        Assert.Equal(ExactCoefficientEvidence.Rule, certificate.Rule);
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome.Value!;
    }

    private static void AssertUnsupported<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Unknown, outcome.State);
        Assert.Equal(UnknownReason.UnsupportedFragment, outcome.UnknownReason);
        Assert.Null(outcome.Certificate);
    }

    private static void AssertBudgetExceeded<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Unknown, outcome.State);
        Assert.Equal(UnknownReason.BudgetExceeded, outcome.UnknownReason);
        Assert.Null(outcome.Certificate);
    }

    private static AnalysisRequest Request(InputExpression expression, AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Symbol(string name)
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

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
