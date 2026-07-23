using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffineReciprocalTheoremCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [MemberData(nameof(ValidReplayMatrix))]
    public void SecantCosecantAndCotangentReplayAcrossUnitsAndExactAmplitudes(
        string function,
        int angleUnitValue,
        string amplitudeName)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression expression = Multiply(
            Amplitude(amplitudeName),
            Function(function, Affine(-2, 1)));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Range,
            angleUnit);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = AssertProved(report.Range);
        var certificate = Assert.IsType<TheoremProofCertificate>(
            report.Range.Certificate);

        Assert.Equal(
            TheoremRule.AffineReciprocalTrigonometric,
            certificate.Theorem);
        Assert.True(AffineReciprocalTheoremCertificateReplay.Check(
            request,
            report.Expression!,
            certificate,
            ClaimCanonical.For(range),
            new ResourceBudget()));
        Assert.True(CertificateChecker.Check(
            request,
            report.Expression!,
            report.Range));
    }

    [Fact]
    public void ReplayAcceptsOnlyOneMatchingGuardPlusProvedConstantPremises()
    {
        InputExpression expression = Function("sec", Affine(-2, 1));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Range,
            AngleUnit.Radians);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = AssertProved(report.Range);
        var certificate = Assert.IsType<TheoremProofCertificate>(
            report.Range.Certificate);
        SemanticExpression semantic = report.Expression!;
        string claim = ClaimCanonical.For(range);

        Formula constantTrue = Formula.Compare(
            Build(Number(1)).Value,
            Comparison.NotEqual,
            Build(Number(0)).Value);
        Formula guardWithTruePremise = Formula.And(
            semantic.DefinedWhen,
            constantTrue);
        SemanticExpression allowed = semantic with
        {
            DefinedWhen = guardWithTruePremise
        };
        TheoremProofCertificate allowedCertificate = WithDefinedness(
            certificate,
            guardWithTruePremise);
        Assert.True(AffineReciprocalTheoremCertificateReplay.Check(
            request,
            allowed,
            allowedCertificate,
            claim,
            new ResourceBudget()));

        Formula normalizedEquivalentGuard = Build(
            Function("sec", Affine(2, -1))).DefinedWhen;
        Assert.True(AffineReciprocalTheoremCertificateReplay.Check(
            request,
            semantic with { DefinedWhen = normalizedEquivalentGuard },
            WithDefinedness(certificate, normalizedEquivalentGuard),
            claim,
            new ResourceBudget()));

        Formula wrongGuard = Build(Function("csc", Affine(-2, 1))).DefinedWhen;
        AssertRejected(
            semantic with { DefinedWhen = wrongGuard },
            WithDefinedness(certificate, wrongGuard));

        Formula wrongArgumentGuard = Build(
            Function("sec", Affine(-2, 2))).DefinedWhen;
        AssertRejected(
            semantic with { DefinedWhen = wrongArgumentGuard },
            WithDefinedness(certificate, wrongArgumentGuard));

        Formula duplicatedGuard = new JunctionFormula(
            true,
            [semantic.DefinedWhen, semantic.DefinedWhen]);
        AssertRejected(
            semantic with { DefinedWhen = duplicatedGuard },
            WithDefinedness(certificate, duplicatedGuard));

        void AssertRejected(
            SemanticExpression forgedExpression,
            TheoremProofCertificate forgedCertificate)
        {
            Assert.False(AffineReciprocalTheoremCertificateReplay.Check(
                request,
                forgedExpression,
                forgedCertificate,
                claim,
                new ResourceBudget()));
            Assert.False(CertificateChecker.Check(
                request,
                forgedExpression,
                ProofOutcome<RealSet>.Proved(range, forgedCertificate)));
        }
    }

    [Fact]
    public void ReplayRejectsRetainedVariableHolesEvenWhenCertificateTextIsForged()
    {
        InputExpression clean = Function("sec", Variable());
        AnalysisRequest cleanRequest = Request(
            clean,
            AnalysisFeatures.Range,
            AngleUnit.Radians);
        AnalysisReport cleanReport = AnalysisEngine.Analyze(cleanRequest);
        RealSet range = AssertProved(cleanReport.Range);
        var certificate = Assert.IsType<TheoremProofCertificate>(
            cleanReport.Range.Certificate);

        InputExpression hiddenHole = Add(
            clean,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(1)))));
        AnalysisRequest holedRequest = Request(
            hiddenHole,
            AnalysisFeatures.Range,
            AngleUnit.Radians);
        SemanticExpression holedSemantic = Build(hiddenHole);
        Assert.Equal(
            cleanReport.Expression!.Value.Canonical,
            holedSemantic.Value.Canonical);
        Assert.NotEqual(
            cleanReport.Expression.DefinedWhen.Canonical,
            holedSemantic.DefinedWhen.Canonical);

        TheoremProofCertificate forged = WithDefinedness(
            certificate,
            holedSemantic.DefinedWhen);
        Assert.False(AffineReciprocalTheoremCertificateReplay.Check(
            holedRequest,
            holedSemantic,
            forged,
            ClaimCanonical.For(range),
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            holedRequest,
            holedSemantic,
            ProofOutcome<RealSet>.Proved(range, forged)));
    }

    [Fact]
    public void ReplayHonorsBudgetAndRevisionCancellation()
    {
        InputExpression expression = Function("cot", Variable());
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Range,
            AngleUnit.Grads);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = AssertProved(report.Range);
        var certificate = Assert.IsType<TheoremProofCertificate>(
            report.Range.Certificate);
        string claim = ClaimCanonical.For(range);

        Assert.Throws<AnalysisCancelledException>(() =>
            AffineReciprocalTheoremCertificateReplay.Check(
                request,
                report.Expression!,
                certificate,
                claim,
                new ResourceBudget(static () => false)));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffineReciprocalTheoremCertificateReplay.Check(
                request,
                report.Expression!,
                certificate,
                claim,
                exhausted));
    }

    public static IEnumerable<object[]> ValidReplayMatrix()
    {
        string[] functions = ["sec", "csc", "cot"];
        int[] angleUnits =
        [
            (int)AngleUnit.Radians,
            (int)AngleUnit.Degrees,
            (int)AngleUnit.Grads
        ];
        string[] amplitudes = ["-3/2", "pi"];
        foreach (string function in functions)
        {
            foreach (int angleUnit in angleUnits)
            {
                foreach (string amplitude in amplitudes)
                {
                    yield return [function, angleUnit, amplitude];
                }
            }
        }
    }

    private static TheoremProofCertificate WithDefinedness(
        TheoremProofCertificate certificate,
        Formula definedWhen)
    {
        return certificate with
        {
            Parameters = certificate.Parameters.SetItem(
                2,
                definedWhen.Canonical)
        };
    }

    private static RealSet AssertProved(ProofOutcome<RealSet> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Value);
        return outcome.Value;
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features,
        AngleUnit angleUnit)
    {
        return new AnalysisRequest(expression, features, angleUnit, "x", static () => true);
    }

    private static SemanticExpression Build(InputExpression expression)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
    }

    private static InputExpression Amplitude(string name)
    {
        return name == "pi"
            ? InputExpression.Variable("pi", Source)
            : Number(new BigRational(-3, 2));
    }

    private static InputExpression Affine(int slope, int intercept)
    {
        return Add(Multiply(Number(slope), Variable()), Number(intercept));
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
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

    private static InputExpression Function(string name, InputExpression argument)
    {
        return InputExpression.Function(name, [argument], Source);
    }
}
