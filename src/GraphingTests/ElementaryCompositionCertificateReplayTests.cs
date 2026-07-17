using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ElementaryCompositionCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    private static readonly AnalysisFeatures[] SingleFeatures =
    [
        AnalysisFeatures.Domain,
        AnalysisFeatures.Range,
        AnalysisFeatures.Parity,
        AnalysisFeatures.Zeros,
        AnalysisFeatures.YIntercept,
        AnalysisFeatures.Minima,
        AnalysisFeatures.Maxima,
        AnalysisFeatures.InflectionPoints,
        AnalysisFeatures.VerticalAsymptotes,
        AnalysisFeatures.HorizontalAsymptotes,
        AnalysisFeatures.ObliqueAsymptotes,
        AnalysisFeatures.Monotonicity,
        AnalysisFeatures.Period
    ];

    private static readonly AngleUnit[] AllAngleUnits =
    [
        AngleUnit.Radians,
        AngleUnit.Degrees,
        AngleUnit.Grads
    ];

    [Fact]
    public void EveryCurrentlyPublishedFormAndFeatureReplaysIndependently()
    {
        foreach ((InputExpression input, ElementaryCompositionKind kind,
                     bool supportsInflection) in Cases())
        {
            IEnumerable<AngleUnit> angleUnits =
                kind == ElementaryCompositionKind.ExponentialPlusIdentity
                    ? AllAngleUnits
                    : [AngleUnit.Radians];
            foreach (AngleUnit angleUnit in angleUnits)
            {
                foreach (AnalysisFeatures feature in SingleFeatures)
                {
                    AnalysisRequest request = Request(input, feature, angleUnit);
                    var producerBudget = new ResourceBudget();
                    SemanticExpression expression =
                        new SemanticGraphBuilder(producerBudget).Build(input);
                    bool produced = ElementaryCompositionAnalyzer.TryAnalyze(
                        request,
                        expression,
                        feature,
                        producerBudget,
                        out ProofOutcome<object> outcome);

                    if (feature == AnalysisFeatures.InflectionPoints &&
                        !supportsInflection)
                    {
                        Assert.False(produced);
                        continue;
                    }

                    Assert.True(produced, $"{kind}; {angleUnit}; {feature}");
                    Assert.Equal(ProofState.Proved, outcome.State);
                    var certificate = Assert.IsType<ElementaryCompositionProofCertificate>(
                        outcome.Certificate);
                    Assert.Equal(kind, certificate.Kind);
                    Assert.Equal(feature, certificate.Feature);
                    Assert.True(ElementaryCompositionCertificateChecker.Check(
                        request,
                        expression,
                        certificate,
                        ClaimCanonical.ForObject(outcome.Value!),
                        new ResourceBudget()),
                        $"specialized replay: {kind}; {angleUnit}; {feature}");
                    Assert.True(CertificateChecker.Check(
                        request,
                        expression,
                        outcome),
                        $"production replay: {kind}; {angleUnit}; {feature}");
                }
            }
        }
    }

    [Fact]
    public void NestedCompositionReplayRejectsHiddenSourceHolesEvenWithForgedRegularity()
    {
        InputExpression cleanInput = Function(
            "sin",
            Function("exp", Variable()));
        InputExpression x = Variable();
        InputExpression guardedArgument = Add(x, HiddenZero(x));
        InputExpression hiddenInput = Function(
            "sin",
            Function("exp", guardedArgument));

        (AnalysisRequest _, SemanticExpression cleanExpression,
            ProofOutcome<object> cleanOutcome) = Produce(
                cleanInput,
                AnalysisFeatures.Range);
        var certificate = Assert.IsType<ElementaryCompositionProofCertificate>(
            cleanOutcome.Certificate);
        AnalysisRequest hiddenRequest = Request(hiddenInput, AnalysisFeatures.Range);
        SemanticExpression hiddenExpression = Build(hiddenInput);

        Assert.Equal(cleanExpression.Value.Canonical, hiddenExpression.Value.Canonical);
        Assert.NotEqual(
            Formula.True.Canonical,
            hiddenExpression.DefinedWhen.Canonical);

        ElementaryCompositionProofCertificate guardMatched = certificate with
        {
            DefinednessCanonical = hiddenExpression.DefinedWhen.Canonical
        };
        Assert.False(ElementaryCompositionCertificateChecker.Check(
            hiddenRequest,
            hiddenExpression,
            guardMatched,
            ClaimCanonical.ForObject(cleanOutcome.Value!),
            new ResourceBudget()));

        SemanticExpression forgedRegularity = hiddenExpression with
        {
            DefinedWhen = Formula.True,
            ContinuousWhen = Formula.True,
            DifferentiableWhen = Formula.True
        };
        Assert.False(ElementaryCompositionCertificateChecker.Check(
            hiddenRequest,
            forgedRegularity,
            certificate,
            ClaimCanonical.ForObject(cleanOutcome.Value!),
            new ResourceBudget()));
    }

    [Fact]
    public void ExponentialIdentityReplayRejectsHiddenOperandHoles()
    {
        InputExpression x = Variable();
        InputExpression cleanInput = Add(Function("exp", x), x);
        InputExpression hiddenInput = Add(
            Function("exp", x),
            Add(x, HiddenZero(x)));
        (_, _, ProofOutcome<object> cleanOutcome) = Produce(
            cleanInput,
            AnalysisFeatures.Monotonicity);
        var certificate = Assert.IsType<ElementaryCompositionProofCertificate>(
            cleanOutcome.Certificate);
        AnalysisRequest hiddenRequest = Request(
            hiddenInput,
            AnalysisFeatures.Monotonicity);
        SemanticExpression hiddenExpression = Build(hiddenInput);
        SemanticExpression forgedRegularity = hiddenExpression with
        {
            DefinedWhen = Formula.True,
            ContinuousWhen = Formula.True,
            DifferentiableWhen = Formula.True
        };

        Assert.Equal(certificate.Subject, hiddenExpression.Value.Canonical);
        Assert.False(ElementaryCompositionCertificateChecker.Check(
            hiddenRequest,
            forgedRegularity,
            certificate,
            ClaimCanonical.ForObject(cleanOutcome.Value!),
            new ResourceBudget()));
    }

    [Fact]
    public void ReplayRejectsForgedContinuityDifferentiabilityAndSourceChain()
    {
        InputExpression input = Function("sin", Function("ln", Variable()));
        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<object> outcome) = Produce(
                input,
                AnalysisFeatures.Range);
        var certificate = Assert.IsType<ElementaryCompositionProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        Assert.False(Check(expression with { ContinuousWhen = Formula.False }));
        Assert.False(Check(expression with { DifferentiableWhen = Formula.False }));
        Assert.False(Check(expression with
        {
            SourceOperands = ImmutableArray<SemanticExpression>.Empty
        }));

        bool Check(SemanticExpression changed) =>
            ElementaryCompositionCertificateChecker.Check(
                request,
                changed,
                certificate,
                claim,
                new ResourceBudget());
    }

    [Fact]
    public void ReplayChargesBudgetAndObservesCancellation()
    {
        InputExpression input = Function("sin", Function("tan", Variable()));
        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<object> outcome) = Produce(
                input,
                AnalysisFeatures.Zeros);
        var certificate = Assert.IsType<ElementaryCompositionProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            ElementaryCompositionCertificateChecker.Check(
                request,
                expression,
                certificate,
                claim,
                cancelled));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            ElementaryCompositionCertificateChecker.Check(
                request,
                expression,
                certificate,
                claim,
                exhausted));
    }

    private static IEnumerable<(
        InputExpression Input,
        ElementaryCompositionKind Kind,
        bool SupportsInflection)> Cases()
    {
        yield return (
            Function("sin", Function("sqrt", Variable())),
            ElementaryCompositionKind.SineOfSquareRoot,
            false);
        yield return (
            Function("sin", Function("ln", Variable())),
            ElementaryCompositionKind.SineOfLogarithm,
            true);
        yield return (
            Function("sin", Function("log", Variable())),
            ElementaryCompositionKind.SineOfCommonLogarithm,
            true);
        yield return (
            Function("sin", Function("exp", Variable())),
            ElementaryCompositionKind.SineOfExponential,
            false);
        yield return (
            Function("sin", Function("tan", Variable())),
            ElementaryCompositionKind.SineOfTangent,
            false);
        yield return (
            Add(Function("exp", Variable()), Variable()),
            ElementaryCompositionKind.ExponentialPlusIdentity,
            true);
        yield return (
            Add(Variable(), Function("exp", Variable())),
            ElementaryCompositionKind.ExponentialPlusIdentity,
            true);
    }

    private static (
        AnalysisRequest Request,
        SemanticExpression Expression,
        ProofOutcome<object> Outcome) Produce(
        InputExpression input,
        AnalysisFeatures feature)
    {
        AnalysisRequest request = Request(input, feature);
        var budget = new ResourceBudget();
        SemanticExpression expression =
            new SemanticGraphBuilder(budget).Build(input);
        Assert.True(ElementaryCompositionAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out ProofOutcome<object> outcome));
        return (request, expression, outcome);
    }

    private static SemanticExpression Build(InputExpression input) =>
        new SemanticGraphBuilder(new ResourceBudget()).Build(input);

    private static InputExpression HiddenZero(InputExpression variable) =>
        Multiply(
            Number(0),
            Divide(Number(1), Subtract(variable, Number(3))));

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures feature,
        AngleUnit angleUnit = AngleUnit.Radians) =>
        new(expression, feature, angleUnit, "x", static () => true);

    private static InputExpression Variable() =>
        InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(
        InputExpression left,
        InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(
        InputExpression left,
        InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(
        InputExpression left,
        InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(
        InputExpression left,
        InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Function(
        string name,
        params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
