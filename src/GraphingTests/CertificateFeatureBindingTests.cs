using Graphing.Symbolics;

namespace GraphingTests;

public sealed class CertificateFeatureBindingTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void RealSetCertificatesCannotCrossDomainRangeOrZeroSlots()
    {
        AnalysisRequest request = Request(Variable());
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertCrossSlotCertificatesRejected(
            request,
            Assert.IsType<SemanticExpression>(report.Expression),
            new[]
            {
                (AnalysisFeatures.Domain, report.Domain),
                (AnalysisFeatures.Range, report.Range),
                (AnalysisFeatures.Zeros, report.Zeros)
            });
    }

    [Fact]
    public void FeaturePointCertificatesCannotCrossExtremaOrInflectionSlots()
    {
        InputExpression x = Variable();
        InputExpression expression = Subtract(
            Power(x, 3),
            Multiply(Number(3), x));
        AnalysisRequest request = Request(expression);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertCrossSlotCertificatesRejected(
            request,
            Assert.IsType<SemanticExpression>(report.Expression),
            new[]
            {
                (AnalysisFeatures.Minima, report.Minima),
                (AnalysisFeatures.Maxima, report.Maxima),
                (AnalysisFeatures.InflectionPoints, report.InflectionPoints)
            });
    }

    [Fact]
    public void AsymptoteCertificatesCannotCrossOrientationSlots()
    {
        AnalysisRequest request = Request(Divide(Number(1), Variable()));
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertCrossSlotCertificatesRejected(
            request,
            Assert.IsType<SemanticExpression>(report.Expression),
            new[]
            {
                (AnalysisFeatures.VerticalAsymptotes, report.VerticalAsymptotes),
                (AnalysisFeatures.HorizontalAsymptotes, report.HorizontalAsymptotes),
                (AnalysisFeatures.ObliqueAsymptotes, report.ObliqueAsymptotes)
            });
    }

    private static void AssertCrossSlotCertificatesRejected<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        (AnalysisFeatures Feature, ProofOutcome<T> Outcome)[] outcomes)
    {
        foreach ((AnalysisFeatures feature, ProofOutcome<T> outcome) in outcomes)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.Equal(
                feature,
                Assert.IsAssignableFrom<ProofCertificate>(outcome.Certificate).Feature);
            Assert.True(CertificateChecker.Check(request, expression, outcome));
        }

        foreach ((AnalysisFeatures _, ProofOutcome<T> outcome) in outcomes)
        {
            foreach ((AnalysisFeatures expectedFeature, ProofOutcome<T> _) in outcomes)
            {
                if (outcome.Certificate!.Feature == expectedFeature)
                {
                    continue;
                }

                Assert.False(CertificateChecker.Check(
                    request,
                    expression,
                    expectedFeature,
                    outcome,
                    new ResourceBudget(request.RevisionIsCurrent)));
            }
        }
    }

    private static AnalysisRequest Request(InputExpression expression)
    {
        return new AnalysisRequest(
            expression,
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => true);
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return InputExpression.Number(new BigRational(value), Source);
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
}
