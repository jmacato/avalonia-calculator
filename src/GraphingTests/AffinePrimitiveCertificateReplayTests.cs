using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffinePrimitiveCertificateReplayTests
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

    private static readonly AngleUnit[] AngleUnits =
    [
        AngleUnit.Radians,
        AngleUnit.Degrees,
        AngleUnit.Grads
    ];

    [Fact]
    public void EveryElementaryAndOddRootFeatureReplaysInEveryAngleUnit()
    {
        foreach ((InputExpression input, TheoremRule theorem) in Cases())
        {
            foreach (AngleUnit angleUnit in AngleUnits)
            {
                foreach (AnalysisFeatures feature in SingleFeatures)
                {
                    (AnalysisRequest request, SemanticExpression expression,
                        ProofOutcome<object> outcome) = Produce(
                            input,
                            feature,
                            angleUnit);
                    var certificate = Assert.IsType<TheoremProofCertificate>(
                        outcome.Certificate);

                    Assert.Equal(theorem, certificate.Theorem);
                    Assert.Equal(feature, certificate.Feature);
                    Assert.True(AffinePrimitiveCertificateReplay.Check(
                        request,
                        expression,
                        certificate,
                        ClaimCanonical.ForObject(outcome.Value!),
                        new ResourceBudget()),
                        $"specialized replay: {theorem}; {angleUnit}; {feature}");
                    Assert.True(CertificateChecker.Check(
                        request,
                        expression,
                        outcome),
                        $"production replay: {theorem}; {angleUnit}; {feature}");
                }
            }
        }
    }

    [Fact]
    public void AlgebraicNormalizationsAndCaseAliasesPreserveEveryClaim()
    {
        InputExpression exp = Function("exp", Affine(2, -1));
        InputExpression[] equivalentExponential =
        [
            Add(Multiply(Number(2), exp), Number(3)),
            Add(Number(3), Multiply(exp, Number(2))),
            Multiply(Number(2), Add(exp, Number(new BigRational(3, 2)))),
            Add(Add(exp, exp), Number(3)),
            Divide(Add(Multiply(Number(4), exp), Number(6)), Number(2)),
            Add(Multiply(Number(2), Function("EXP", Affine(2, -1))), Number(3))
        ];
        AssertEveryFeatureMatches(equivalentExponential);

        InputExpression root = Function("root", Affine(-2, 1), Number(5));
        InputExpression[] equivalentRoot =
        [
            Add(Multiply(Number(-3), root), Number(6)),
            Add(Number(6), Multiply(root, Number(-3))),
            Multiply(Number(-3), Add(root, Number(-2))),
            Divide(
                Add(Multiply(Number(-6), root), Number(12)),
                Number(2)),
            Add(
                Multiply(
                    Number(-3),
                    Function("ROOT", Affine(-2, 1), Number(5))),
                Number(6))
        ];
        AssertEveryFeatureMatches(equivalentRoot);

        foreach ((string lower, string caseAlias) in new[]
                 {
                     ("sinh", "SINH"),
                     ("cosh", "COSH"),
                     ("tanh", "TANH"),
                     ("log", "LOG"),
                     ("ln", "LN")
                 })
        {
            AssertEveryFeatureMatches(
            [
                Function(lower, Affine(3, 1)),
                Function(caseAlias, Affine(3, 1))
            ]);
        }
    }

    [Fact]
    public void OpaqueAliasesAndNaturalCommonLogCertificatesAreRejected()
    {
        foreach (string alias in new[]
                 {
                     "exponential",
                     "hyperbolicsine",
                     "hyperboliccosine",
                     "hyperbolictangent",
                     "log10",
                     "loge",
                     "oddroot"
                 })
        {
            InputExpression aliasInput = Function(alias, Affine(2, 1));
            AnalysisRequest request = Request(
                aliasInput,
                AnalysisFeatures.Range,
                AngleUnit.Radians);
            SemanticExpression expression = Build(aliasInput);
            string claim = AllRealSet.Instance.Canonical;
            var forged = new TheoremProofCertificate(
                AnalysisFeatures.Range,
                expression.Value.Canonical,
                claim,
                alias == "oddroot"
                    ? TheoremRule.OddRootPrimitive
                    : TheoremRule.ElementaryPrimitive,
                [
                    $"primitive:{alias}:2:1:1:0:0:{expression.Value.Canonical}",
                    AngleUnit.Radians.ToString(),
                    expression.DefinedWhen.Canonical
                ]);

            Assert.False(AffinePrimitiveCertificateReplay.Check(
                request,
                expression,
                forged,
                claim,
                new ResourceBudget()), alias);
        }

        InputExpression naturalInput = Function("ln", Affine(2, 10));
        InputExpression commonInput = Function("log", Affine(2, 10));
        (_, _, ProofOutcome<object> natural) = Produce(
            naturalInput,
            AnalysisFeatures.YIntercept,
            AngleUnit.Radians);
        (AnalysisRequest commonRequest, SemanticExpression commonExpression,
            ProofOutcome<object> common) = Produce(
                commonInput,
                AnalysisFeatures.YIntercept,
                AngleUnit.Radians);
        var naturalCertificate = Assert.IsType<TheoremProofCertificate>(
            natural.Certificate);

        Assert.NotEqual(
            ClaimCanonical.ForObject(natural.Value!),
            ClaimCanonical.ForObject(common.Value!));
        Assert.False(AffinePrimitiveCertificateReplay.Check(
            commonRequest,
            commonExpression,
            naturalCertificate,
            ClaimCanonical.ForObject(common.Value!),
            new ResourceBudget()));
    }

    [Fact]
    public void OuterAndInnerHiddenHolesAreRejectedAfterRegularityForgery()
    {
        InputExpression x = Variable();
        InputExpression cleanInput = Subtract(
            Multiply(Number(2), Function("exp", Affine(2, 1))),
            Number(3));
        (AnalysisRequest _, SemanticExpression cleanExpression,
            ProofOutcome<object> cleanOutcome) = Produce(
                cleanInput,
                AnalysisFeatures.Range,
                AngleUnit.Radians);
        var certificate = Assert.IsType<TheoremProofCertificate>(
            cleanOutcome.Certificate);
        string claim = ClaimCanonical.ForObject(cleanOutcome.Value!);

        InputExpression outerHole = Add(cleanInput, HiddenZero(x));
        AssertHiddenSourceRejected(
            outerHole,
            cleanExpression,
            certificate,
            claim);

        InputExpression hiddenAffine = Add(Affine(2, 1), HiddenZero(x));
        InputExpression innerHole = Subtract(
            Multiply(Number(2), Function("exp", hiddenAffine)),
            Number(3));
        AssertHiddenSourceRejected(
            innerHole,
            cleanExpression,
            certificate,
            claim);
    }

    [Fact]
    public void OddRootDegreeHolesAreRejectedEvenWhenEveryRootGuardIsForged()
    {
        InputExpression x = Variable();
        InputExpression cleanInput = Function("root", Affine(2, 1), Number(5));
        (AnalysisRequest _, SemanticExpression cleanExpression,
            ProofOutcome<object> outcome) = Produce(
                cleanInput,
                AnalysisFeatures.Monotonicity,
                AngleUnit.Radians);
        var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);
        InputExpression hiddenInput = Function(
            "root",
            Affine(2, 1),
            Add(Number(5), HiddenZero(x)));
        AnalysisRequest hiddenRequest = Request(
            hiddenInput,
            AnalysisFeatures.Monotonicity,
            AngleUnit.Radians);
        SemanticExpression hiddenExpression = Build(hiddenInput);

        Assert.Equal(cleanExpression.Value.Canonical, hiddenExpression.Value.Canonical);
        SemanticExpression forged = hiddenExpression with
        {
            DefinedWhen = cleanExpression.DefinedWhen,
            ContinuousWhen = cleanExpression.ContinuousWhen,
            DifferentiableWhen = cleanExpression.DifferentiableWhen
        };
        Assert.False(AffinePrimitiveCertificateReplay.Check(
            hiddenRequest,
            forged,
            certificate,
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget()));
    }

    [Fact]
    public void MetadataRegularityAndTheoremMutationsAreRejected()
    {
        InputExpression input = Add(
            Multiply(Number(-2), Function("tanh", Affine(3, -1))),
            Number(4));
        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<object> outcome) = Produce(
                input,
                AnalysisFeatures.Range,
                AngleUnit.Grads);
        var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        AssertRejected(certificate with
        {
            Theorem = TheoremRule.OddRootPrimitive
        });
        AssertRejected(certificate with
        {
            Parameters = certificate.Parameters.SetItem(
                0,
                certificate.Parameters[0] + ":changed")
        });
        AssertRejected(certificate with
        {
            Parameters = certificate.Parameters.SetItem(1, AngleUnit.Radians.ToString())
        });
        AssertRejected(certificate with
        {
            Parameters = certificate.Parameters.SetItem(2, Formula.False.Canonical)
        });
        AssertRejected(certificate with { Parameters = [] });
        Assert.False(CheckExpression(expression with
        {
            ContinuousWhen = Formula.False
        }));
        Assert.False(CheckExpression(expression with
        {
            DifferentiableWhen = Formula.False
        }));
        Assert.False(CheckExpression(expression with
        {
            SourceOperands = ImmutableArray<SemanticExpression>.Empty
        }));

        (_, _, ProofOutcome<object> rootOutcome) = Produce(
            Function("root", Affine(2, 1), Number(5)),
            AnalysisFeatures.Range,
            AngleUnit.Grads);
        var rootCertificate = Assert.IsType<TheoremProofCertificate>(
            rootOutcome.Certificate);
        Assert.False(AffinePrimitiveCertificateReplay.Check(
            Request(input, AnalysisFeatures.Range, AngleUnit.Grads),
            expression,
            rootCertificate with
            {
                Subject = expression.Value.Canonical,
                Claim = claim,
                Parameters = certificate.Parameters,
                Theorem = TheoremRule.ElementaryPrimitive
            },
            claim,
            new ResourceBudget()));

        void AssertRejected(TheoremProofCertificate changed) =>
            Assert.False(AffinePrimitiveCertificateReplay.Check(
                request,
                expression,
                changed,
                claim,
                new ResourceBudget()));

        bool CheckExpression(SemanticExpression changed) =>
            AffinePrimitiveCertificateReplay.Check(
                request,
                changed,
                certificate,
                claim,
                new ResourceBudget());
    }

    [Fact]
    public void DeadAndUnknownTheoremRulesFailClosedWithoutProducerFallback()
    {
        InputExpression input = Function("exp", Affine(2, 1));
        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<object> outcome) = Produce(
                input,
                AnalysisFeatures.Range,
                AngleUnit.Radians);
        var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);

        foreach (TheoremRule rule in new[]
                 {
                     TheoremRule.IntegralPowerDomain,
                     TheoremRule.TangentIntegerLattice,
                     (TheoremRule)int.MaxValue
                 })
        {
            TheoremProofCertificate changed = certificate with { Theorem = rule };
            Assert.False(CertificateChecker.Check(
                request,
                expression,
                ProofOutcome<object>.Proved(outcome.Value!, changed)),
                rule.ToString());
        }
    }

    [Fact]
    public void ReplayChargesBudgetAndObservesCancellation()
    {
        InputExpression input = Function("ln", Affine(-3, 2));
        (AnalysisRequest request, SemanticExpression expression,
            ProofOutcome<object> outcome) = Produce(
                input,
                AnalysisFeatures.Zeros,
                AngleUnit.Radians);
        var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);
        string claim = ClaimCanonical.ForObject(outcome.Value!);

        var cancelled = new ResourceBudget(static () => false);
        Assert.Throws<AnalysisCancelledException>(() =>
            AffinePrimitiveCertificateReplay.Check(
                request,
                expression,
                certificate,
                claim,
                cancelled));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            AffinePrimitiveCertificateReplay.Check(
                request,
                expression,
                certificate,
                claim,
                exhausted));
    }

    private static IEnumerable<(InputExpression Input, TheoremRule Theorem)> Cases()
    {
        yield return (
            Subtract(
                Multiply(Number(2), Function("exp", Affine(-3, 1))),
                Number(4)),
            TheoremRule.ElementaryPrimitive);
        yield return (
            Add(
                Multiply(Number(-2), Function("sinh", Affine(-3, 1))),
                Number(4)),
            TheoremRule.ElementaryPrimitive);
        yield return (
            Add(
                Multiply(Number(-2), Function("cosh", Affine(3, -1))),
                Number(4)),
            TheoremRule.ElementaryPrimitive);
        yield return (
            Add(
                Multiply(Number(-3), Function("tanh", Affine(2, -1))),
                Number(1)),
            TheoremRule.ElementaryPrimitive);
        yield return (
            Subtract(
                Multiply(Number(2), Function("log", Affine(-3, 2))),
                Number(1)),
            TheoremRule.ElementaryPrimitive);
        yield return (
            Add(
                Multiply(Number(-2), Function("ln", Affine(3, 2))),
                Number(1)),
            TheoremRule.ElementaryPrimitive);
        yield return (
            Add(
                Multiply(
                    Number(-3),
                    Function("root", Affine(-2, 1), Number(5))),
                Number(6)),
            TheoremRule.OddRootPrimitive);
    }

    private static void AssertEveryFeatureMatches(
        InputExpression[] equivalent)
    {
        foreach (AnalysisFeatures feature in SingleFeatures)
        {
            (_, _, ProofOutcome<object> expected) = Produce(
                equivalent[0],
                feature,
                AngleUnit.Radians);
            string expectedClaim = ClaimCanonical.ForObject(expected.Value!);
            foreach (InputExpression input in equivalent.Skip(1))
            {
                (AnalysisRequest request, SemanticExpression expression,
                    ProofOutcome<object> actual) = Produce(
                        input,
                        feature,
                        AngleUnit.Radians);
                Assert.Equal(
                    expectedClaim,
                    ClaimCanonical.ForObject(actual.Value!));
                Assert.True(CertificateChecker.Check(
                    request,
                    expression,
                    actual));
            }
        }
    }

    private static void AssertHiddenSourceRejected(
        InputExpression hiddenInput,
        SemanticExpression cleanExpression,
        TheoremProofCertificate certificate,
        string claim)
    {
        AnalysisRequest hiddenRequest = Request(
            hiddenInput,
            certificate.Feature,
            AngleUnit.Radians);
        SemanticExpression hiddenExpression = Build(hiddenInput);
        Assert.Equal(cleanExpression.Value.Canonical, hiddenExpression.Value.Canonical);

        TheoremProofCertificate guardMatched = certificate with
        {
            Parameters = certificate.Parameters.SetItem(
                2,
                hiddenExpression.DefinedWhen.Canonical)
        };
        Assert.False(AffinePrimitiveCertificateReplay.Check(
            hiddenRequest,
            hiddenExpression,
            guardMatched,
            claim,
            new ResourceBudget()));

        SemanticExpression forged = hiddenExpression with
        {
            DefinedWhen = cleanExpression.DefinedWhen,
            ContinuousWhen = cleanExpression.ContinuousWhen,
            DifferentiableWhen = cleanExpression.DifferentiableWhen
        };
        Assert.False(AffinePrimitiveCertificateReplay.Check(
            hiddenRequest,
            forged,
            certificate,
            claim,
            new ResourceBudget()));
    }

    private static (
        AnalysisRequest Request,
        SemanticExpression Expression,
        ProofOutcome<object> Outcome) Produce(
        InputExpression input,
        AnalysisFeatures feature,
        AngleUnit angleUnit)
    {
        AnalysisRequest request = Request(input, feature, angleUnit);
        var budget = new ResourceBudget();
        SemanticExpression expression =
            new SemanticGraphBuilder(budget).Build(input);
        Assert.True(TrigonometricAndLatticeAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out ProofOutcome<object> outcome),
            $"{expression.Value.Canonical}; {angleUnit}; {feature}");
        Assert.Equal(ProofState.Proved, outcome.State);
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
        AngleUnit angleUnit) =>
        new(expression, feature, angleUnit, "x", static () => true);

    private static InputExpression Affine(int slope, int intercept) =>
        Add(Multiply(Number(slope), Variable()), Number(intercept));

    private static InputExpression Variable() =>
        InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) =>
        Number(new BigRational(value));

    private static InputExpression Number(BigRational value) =>
        InputExpression.Number(value, Source);

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
