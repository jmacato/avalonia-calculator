using Graphing.Symbolics;

namespace GraphingTests;

public sealed class TheoremDomainPremiseReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void LinearDriftReplayRejectsAHiddenRationalHole()
    {
        InputExpression clean = Add(Variable(), Sin(Variable()));
        AssertHiddenHoleRejected(clean, AnalysisFeatures.Range);
    }

    [Fact]
    public void HalfAngleReplayRejectsAHiddenRationalHole()
    {
        InputExpression clean = Power(Sin(Variable()), 2);
        AssertHiddenHoleRejected(clean, AnalysisFeatures.Zeros);
    }

    private static void AssertHiddenHoleRejected(
        InputExpression clean,
        AnalysisFeatures feature)
    {
        AnalysisRequest cleanRequest = Request(clean, feature);
        AnalysisReport cleanReport = AnalysisEngine.Analyze(cleanRequest);
        SemanticExpression cleanSemantic = Assert.IsType<SemanticExpression>(cleanReport.Expression);
        var cleanOutcome = Feature(cleanReport, feature);
        RealSet cleanValue = Assert.IsAssignableFrom<RealSet>(cleanOutcome.Value);
        var cleanCertificate = Assert.IsType<TheoremProofCertificate>(cleanOutcome.Certificate);

        InputExpression hidden = Add(
            clean,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(3)))));
        AnalysisRequest hiddenRequest = Request(hidden, feature);
        SemanticExpression hiddenSemantic = Build(hidden);
        Assert.Equal(cleanSemantic.Value.Canonical, hiddenSemantic.Value.Canonical);
        Assert.NotEqual(cleanSemantic.DefinedWhen.Canonical, hiddenSemantic.DefinedWhen.Canonical);

        TheoremProofCertificate forged = cleanCertificate;
        if (cleanCertificate.Theorem == TheoremRule.LinearDriftTrigonometric)
        {
            forged = forged with
            {
                Parameters = forged.Parameters.SetItem(2, hiddenSemantic.DefinedWhen.Canonical)
            };
        }

        Assert.False(CertificateChecker.Check(
            hiddenRequest,
            hiddenSemantic,
            ProofOutcome<RealSet>.Proved(cleanValue, forged)));
    }

    private static ProofOutcome<RealSet> Feature(
        AnalysisReport report,
        AnalysisFeatures feature)
    {
        return feature switch
        {
            AnalysisFeatures.Range => report.Range,
            AnalysisFeatures.Zeros => report.Zeros,
            _ => throw new ArgumentOutOfRangeException(nameof(feature))
        };
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures feature)
    {
        return new AnalysisRequest(expression, feature, AngleUnit.Radians, "x", static () => true);
    }

    private static SemanticExpression Build(InputExpression expression)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
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

    private static InputExpression Power(InputExpression basis, int exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);
    }

    private static InputExpression Sin(InputExpression argument)
    {
        return InputExpression.Function("sin", [argument], Source);
    }
}
