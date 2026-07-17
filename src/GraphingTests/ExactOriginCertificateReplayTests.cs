using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ExactOriginCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);
    [Fact]
    public void ReplayRejectsEveryCertificateEnvelopeMutation()
    {
        ExactOriginCertificateReplayTestsProducedOrigin produced = Produce(GenericDefinedExpression());
        ExactOriginProofCertificate certificate = produced.Certificate;
        string claim = ClaimCanonical.For(produced.Outcome.Value!);
        ExactOriginProofCertificate[] mutations = [certificate with
        {
            Feature = AnalysisFeatures.Zeros
        }, certificate with
        {
            Subject = certificate.Subject + ":changed"
        }, certificate with
        {
            SubjectCanonical = certificate.SubjectCanonical + ":changed"
        }, certificate with
        {
            Claim = certificate.Claim + ":changed"
        }, certificate with
        {
            ClaimCanonical = certificate.ClaimCanonical + ":changed"
        }, certificate with
        {
            DefinednessCanonical = certificate.DefinednessCanonical + ":changed"
        }, certificate with
        {
            AngleUnit = AngleUnit.Degrees
        }, certificate with
        {
            Rule = "untrusted"
        }

        ];
        foreach (ExactOriginProofCertificate changed in mutations)
        {
            Assert.False(ExactOriginCertificateReplay.Check(produced.Request, produced.Semantic, changed, claim, new ResourceBudget()));
            Assert.False(CertificateChecker.Check(produced.Request, produced.Semantic, ProofOutcome<OptionalValue<ExactReal>>.Proved(produced.Outcome.Value!, changed)));
        }

        Assert.False(ExactOriginCertificateReplay.Check(produced.Request, produced.Semantic, certificate, claim + ":changed", new ResourceBudget()));
    }

    [Fact]
    public void ReplayRejectsRequestAndSemanticExpressionMismatches()
    {
        InputExpression expression = GenericDefinedExpression();
        ExactOriginCertificateReplayTestsProducedOrigin produced = Produce(expression);
        string claim = ClaimCanonical.For(produced.Outcome.Value!);
        Assert.False(ExactOriginCertificateReplay.Check(produced.Request with { Features = AnalysisFeatures.None }, produced.Semantic, produced.Certificate, claim, new ResourceBudget()));
        Assert.False(ExactOriginCertificateReplay.Check(produced.Request with { Variable = "z" }, produced.Semantic, produced.Certificate, claim, new ResourceBudget()));
        SemanticExpression changedValue = Build(Add(expression, Number(1)));
        Assert.False(ExactOriginCertificateReplay.Check(produced.Request, changedValue, produced.Certificate, claim, new ResourceBudget()));
        InputExpression retainedHole = Add(expression, Multiply(Number(0), Divide(Number(1), Subtract(Variable(), Number(2)))));
        SemanticExpression changedDefinedness = Build(retainedHole);
        Assert.Equal(produced.Semantic.Value.Canonical, changedDefinedness.Value.Canonical);
        Assert.NotEqual(produced.Semantic.DefinedWhen.Canonical, changedDefinedness.DefinedWhen.Canonical);
        Assert.False(ExactOriginCertificateReplay.Check(produced.Request, changedDefinedness, produced.Certificate, claim, new ResourceBudget()));
        Assert.False(CertificateChecker.Check(produced.Request, changedDefinedness, produced.Outcome));
    }

    [Fact]
    public void ReplayRejectsCoherentSomeAndNoneClaimSwaps()
    {
        ExactOriginCertificateReplayTestsProducedOrigin defined = Produce(GenericDefinedExpression());
        OptionalValue<ExactReal> none = OptionalValue<ExactReal>.None;
        string noneClaim = ClaimCanonical.For(none);
        ExactOriginProofCertificate forgedNoneCertificate = defined.Certificate with
        {
            Claim = noneClaim,
            ClaimCanonical = noneClaim
        };
        var forgedNone = ProofOutcome<OptionalValue<ExactReal>>.Proved(none, forgedNoneCertificate);
        Assert.False(ExactOriginCertificateReplay.Check(defined.Request, defined.Semantic, forgedNoneCertificate, noneClaim, new ResourceBudget()));
        Assert.False(CertificateChecker.Check(defined.Request, defined.Semantic, forgedNone));
        ExactOriginCertificateReplayTestsProducedOrigin undefined = Produce(Divide(Number(1), Variable()));
        var zero = OptionalValue<ExactReal>.Some(new RationalReal(BigRational.Zero));
        string zeroClaim = ClaimCanonical.For(zero);
        ExactOriginProofCertificate forgedSomeCertificate = undefined.Certificate with
        {
            Claim = zeroClaim,
            ClaimCanonical = zeroClaim
        };
        var forgedSome = ProofOutcome<OptionalValue<ExactReal>>.Proved(zero, forgedSomeCertificate);
        Assert.False(ExactOriginCertificateReplay.Check(undefined.Request, undefined.Semantic, forgedSomeCertificate, zeroClaim, new ResourceBudget()));
        Assert.False(CertificateChecker.Check(undefined.Request, undefined.Semantic, forgedSome));
    }

    [Fact]
    public void ProducerAndReplayHonorBudgetAndCancellation()
    {
        ExactOriginCertificateReplayTestsProducedOrigin produced = Produce(GenericDefinedExpression());
        string claim = ClaimCanonical.For(produced.Outcome.Value!);
        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() => ExactOriginCertificateReplay.Check(produced.Request, produced.Semantic, produced.Certificate, claim, exhausted));
        var cancelledReplay = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() => ExactOriginCertificateReplay.Check(produced.Request, produced.Semantic, produced.Certificate, claim, cancelledReplay));
        var cancelledProducer = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() => ExactOriginAnalyzer.TryAnalyze(produced.Request, produced.Semantic, AnalysisFeatures.YIntercept, cancelledProducer, out ProofOutcome<OptionalValue<ExactReal>> _));
    }

    private static ExactOriginCertificateReplayTestsProducedOrigin Produce(InputExpression expression)
    {
        var request = new AnalysisRequest(expression, AnalysisFeatures.YIntercept, AngleUnit.Radians, "x", static () => true);
        SemanticExpression semantic = Build(expression);
        Assert.True(ExactOriginAnalyzer.TryAnalyze(request, semantic, AnalysisFeatures.YIntercept, new ResourceBudget(), out ProofOutcome<OptionalValue<ExactReal>> outcome));
        Assert.Equal(ProofState.Proved, outcome.State);
        var certificate = Assert.IsType<ExactOriginProofCertificate>(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
        return new ExactOriginCertificateReplayTestsProducedOrigin(request, semantic, outcome, certificate);
    }

    private static SemanticExpression Build(InputExpression expression) => new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
    private static InputExpression GenericDefinedExpression()
    {
        InputExpression x = Variable();
        return Add(Function("sin", Add(x, Function("sqrt", Add(x, Number(1))))), Subtract(Function("cos", x), Number(1)));
    }

    private static InputExpression Variable() => InputExpression.Variable("x", Source);
    private static InputExpression Number(int value) => InputExpression.Number(new BigRational(value), Source);
    private static InputExpression Add(InputExpression left, InputExpression right) => InputExpression.Binary(InputExpressionKind.Add, left, right, Source);
    private static InputExpression Subtract(InputExpression left, InputExpression right) => InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);
    private static InputExpression Multiply(InputExpression left, InputExpression right) => InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);
    private static InputExpression Divide(InputExpression left, InputExpression right) => InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);
    private static InputExpression Function(string name, params InputExpression[] arguments) => InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
