using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class VariablePowerCertificateReplayTests
{
    private const AnalysisFeatures SupportedFeatures =
        AnalysisFeatures.Domain |
        AnalysisFeatures.Zeros |
        AnalysisFeatures.YIntercept |
        AnalysisFeatures.HorizontalAsymptotes;

    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void ValidVariablePowerCertificatesReplayEveryPublishedFeature()
    {
        InputExpression expression = VariablePower();
        AnalysisRequest request = Request(expression, SupportedFeatures);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        AssertReplay(
            request,
            semantic,
            AnalysisFeatures.Domain,
            report.Domain,
            [
                semantic.DefinedWhen.Canonical,
                "tan-inverse:atan(n)+k*pi",
                "negative-base:integer-exponent",
                "zero-base:positive-exponent"
            ]);
        AssertReplay(
            request,
            semantic,
            AnalysisFeatures.Zeros,
            report.Zeros,
            ["nonzero-on-every-certified-domain-component"]);
        AssertReplay(
            request,
            semantic,
            AnalysisFeatures.YIntercept,
            report.YIntercept,
            ["origin-fails-power-definedness"]);
        AssertReplay(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes,
            report.HorizontalAsymptotes,
            ["distinct-periodic-subsequences-at-positive-and-negative-infinity"]);

        var domain = Assert.IsType<UnionSet>(report.Domain.Value);
        Assert.Single(domain.Operands.OfType<PeriodicIntervalSet>());
        Assert.Equal(2, domain.Operands.OfType<IntegerLatticeSet>().Count());
    }

    [Fact]
    public void HiddenSourceOperandsAndRetainedTopLevelHolesAreRejected()
    {
        InputExpression cleanInput = VariablePower();
        AnalysisRequest cleanRequest = Request(cleanInput, AnalysisFeatures.Domain);
        AnalysisReport cleanReport = AnalysisEngine.Analyze(cleanRequest);
        SemanticExpression cleanSemantic = Assert.IsType<SemanticExpression>(cleanReport.Expression);
        RealSet cleanDomain = Assert.IsAssignableFrom<RealSet>(cleanReport.Domain.Value);
        var cleanCertificate = Assert.IsType<TheoremProofCertificate>(cleanReport.Domain.Certificate);

        InputExpression x = Variable();
        InputExpression hiddenArgument = Add(
            x,
            Multiply(
                Number(0),
                Tan(Multiply(Number(2), x))));
        InputExpression hiddenInput = Power(Sin(hiddenArgument), Tan(hiddenArgument));
        SemanticExpression hiddenSemantic = Build(hiddenInput);
        AnalysisRequest hiddenRequest = Request(hiddenInput, SupportedFeatures);

        Assert.Equal(cleanSemantic.Value.Canonical, hiddenSemantic.Value.Canonical);
        Assert.NotEqual(cleanSemantic.DefinedWhen.Canonical, hiddenSemantic.DefinedWhen.Canonical);
        TheoremProofCertificate hiddenForgery = cleanCertificate with
        {
            Subject = hiddenSemantic.Value.Canonical,
            Parameters = cleanCertificate.Parameters.SetItem(
                0,
                hiddenSemantic.DefinedWhen.Canonical)
        };
        Assert.False(VariablePowerCertificateReplay.Check(
            hiddenRequest,
            hiddenSemantic,
            hiddenForgery,
            cleanCertificate.Claim,
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            hiddenRequest,
            hiddenSemantic,
            ProofOutcome<RealSet>.Proved(cleanDomain, hiddenForgery)));

        AnalysisReport hiddenReport = AnalysisEngine.Analyze(hiddenRequest);
        AssertCertificateRejected(hiddenReport.Domain);
        AssertCertificateRejected(hiddenReport.Zeros);
        Assert.Equal(ProofState.Proved, hiddenReport.YIntercept.State);
        Assert.False(hiddenReport.YIntercept.Value!.HasValue);
        Assert.IsType<ExactOriginProofCertificate>(hiddenReport.YIntercept.Certificate);
        Assert.True(CertificateChecker.Check(
            hiddenRequest,
            hiddenSemantic,
            hiddenReport.YIntercept));
        AssertCertificateRejected(hiddenReport.HorizontalAsymptotes);

        InputExpression wrappedInput = Add(
            cleanInput,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(x, Number(3)))));
        SemanticExpression wrappedSemantic = Build(wrappedInput);
        AnalysisRequest wrappedRequest = Request(wrappedInput, AnalysisFeatures.Domain);
        Assert.Equal(cleanSemantic.Value.Canonical, wrappedSemantic.Value.Canonical);
        Assert.NotEqual(cleanSemantic.DefinedWhen.Canonical, wrappedSemantic.DefinedWhen.Canonical);

        TheoremProofCertificate wrappedForgery = cleanCertificate with
        {
            Subject = wrappedSemantic.Value.Canonical,
            Parameters = cleanCertificate.Parameters.SetItem(
                0,
                wrappedSemantic.DefinedWhen.Canonical)
        };
        Assert.False(VariablePowerCertificateReplay.Check(
            wrappedRequest,
            wrappedSemantic,
            wrappedForgery,
            cleanCertificate.Claim,
            new ResourceBudget()));
        AssertCertificateRejected(AnalysisEngine.Analyze(wrappedRequest).Domain);
    }

    [Fact]
    public void ReplayRejectsCertificateFormulaUnitAndFeatureMutations()
    {
        InputExpression input = VariablePower();
        AnalysisRequest request = Request(input, AnalysisFeatures.Domain);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
        RealSet domain = Assert.IsAssignableFrom<RealSet>(report.Domain.Value);
        var certificate = Assert.IsType<TheoremProofCertificate>(report.Domain.Certificate);

        AssertRejected(certificate with
        {
            Parameters = certificate.Parameters.SetItem(1, "mutated-inverse-family")
        });
        AssertRejected(certificate with { Theorem = TheoremRule.ConstantFunction });
        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { Subject = "v:y" });
        AssertRejected(certificate with { Parameters = default });

        SemanticExpression changedFormula = semantic with
        {
            DefinedWhen = Formula.True,
            ContinuousWhen = Formula.True,
            DifferentiableWhen = Formula.True
        };
        TheoremProofCertificate changedFormulaCertificate = certificate with
        {
            Parameters = certificate.Parameters.SetItem(0, Formula.True.Canonical)
        };
        Assert.False(VariablePowerCertificateReplay.Check(
            request,
            changedFormula,
            changedFormulaCertificate,
            certificate.Claim,
            new ResourceBudget()));

        AnalysisRequest degrees = request with { AngleUnit = AngleUnit.Degrees };
        Assert.False(VariablePowerCertificateReplay.Check(
            degrees,
            semantic,
            certificate,
            certificate.Claim,
            new ResourceBudget()));

        void AssertRejected(TheoremProofCertificate changed)
        {
            Assert.False(VariablePowerCertificateReplay.Check(
                request,
                semantic,
                changed,
                certificate.Claim,
                new ResourceBudget()));
            Assert.False(CertificateChecker.Check(
                request,
                semantic,
                ProofOutcome<RealSet>.Proved(domain, changed)));
        }
    }

    [Fact]
    public void ReplayHonorsCancellationAndDeterministicWorkBudget()
    {
        InputExpression input = VariablePower();
        AnalysisRequest request = Request(input, AnalysisFeatures.Domain);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
        var certificate = Assert.IsType<TheoremProofCertificate>(report.Domain.Certificate);

        Assert.Throws<AnalysisCancelledException>(() =>
            VariablePowerCertificateReplay.Check(
                request,
                semantic,
                certificate,
                certificate.Claim,
                new ResourceBudget(static () => false)));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            VariablePowerCertificateReplay.Check(
                request,
                semantic,
                certificate,
                certificate.Claim,
                exhausted));
    }

    private static TheoremProofCertificate AssertReplay<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature,
        ProofOutcome<T> outcome,
        ImmutableArray<string> expectedParameters)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);
        Assert.Equal(TheoremRule.VariablePowerDomain, certificate.Theorem);
        Assert.Equal(feature, certificate.Feature);
        Assert.Equal(expectedParameters, certificate.Parameters);
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
        return certificate;
    }

    private static void AssertCertificateRejected<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Unknown, outcome.State);
        Assert.Equal(UnknownReason.CertificateRejected, outcome.UnknownReason);
        Assert.Null(outcome.Certificate);
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

    private static InputExpression VariablePower()
    {
        InputExpression x = Variable();
        return Power(Sin(x), Tan(x));
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

    private static InputExpression Power(InputExpression basis, InputExpression exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);
    }

    private static InputExpression Sin(InputExpression argument)
    {
        return Function("sin", argument);
    }

    private static InputExpression Tan(InputExpression argument)
    {
        return Function("tan", argument);
    }

    private static InputExpression Function(string name, InputExpression argument)
    {
        return InputExpression.Function(name, [argument], Source);
    }
}
