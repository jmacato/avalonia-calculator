using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class SemanticSourceProjectionTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void NeutralSourceEnvelopesPreserveEveryPublishedClaim()
    {
        InputExpression x = Variable();
        InputExpression absolute = Function("abs", x);
        InputExpression sine = Function("sin", x);
        InputExpression sineQuotient = Divide(sine, sine);
        InputExpression sineCube = Function("sin", Power(x, 3));
        InputExpression sinePuiseuxSum = Function("sin", Add(
            Power(x, 3),
            Function("sqrt", x)));
        InputExpression absoluteSine = Function("abs", sine);
        InputExpression exponentialSine = Function("exp", sine);
        InputExpression shiftedRadicalSine = Function(
            "sqrt",
            Function("sin", Add(
                x,
                Divide(InputExpression.Variable("pi", Source), Number(4)))));
        InputExpression affineMinimum = Function(
            "min",
            x,
            Multiply(Number(2), x));

        (InputExpression Direct, InputExpression Wrapped)[] cases =
        [
            (absolute, Add(absolute, Number(0))),
            (absolute, Add(Multiply(Add(absolute, Number(0)), Number(1)), Number(0))),
            (Function("sqrt", x), Add(Function("sqrt", x), Number(0))),
            (Function("root", x, Number(2)), Add(Function("root", x, Number(2)), Number(0))),
            (Function("sin", Function("exp", x)), Add(Function("sin", Function("exp", x)), Number(0))),
            (sineCube, Add(sineCube, Number(0))),
            (sinePuiseuxSum, Add(sinePuiseuxSum, Number(0))),
            (sinePuiseuxSum, Add(Number(0), sinePuiseuxSum)),
            (sinePuiseuxSum, Divide(sinePuiseuxSum, Number(1))),
            (absoluteSine, Add(absoluteSine, Number(0))),
            (exponentialSine, Add(exponentialSine, Number(0))),
            (shiftedRadicalSine, Add(shiftedRadicalSine, Number(0))),
            (sineQuotient, Add(sineQuotient, Number(0))),
            (sineQuotient, Multiply(sineQuotient, Number(1))),
            (sineQuotient, Multiply(Number(1), sineQuotient)),
            (Function("sign", x), Function("sign", Add(x, Multiply(Number(0), absolute)))),
            (Function("floor", x), Add(Function("floor", x), Number(0))),
            (Function("floor", x), Function("floor", Add(x, Multiply(Number(0), absolute)))),
            (affineMinimum, Add(affineMinimum, Number(0))),
            (affineMinimum, Function("min", Add(x, Multiply(Number(0), absolute)), Multiply(Number(2), x)))
        ];

        foreach ((InputExpression direct, InputExpression wrapped) in cases)
        {
            AnalysisRequest directRequest = Request(direct);
            AnalysisRequest wrappedRequest = Request(wrapped);
            AnalysisReport expected = AnalysisEngine.Analyze(directRequest);
            AnalysisReport actual = AnalysisEngine.Analyze(wrappedRequest);

            AssertSame(expected.Domain, actual.Domain, wrappedRequest, actual.Expression!);
            AssertSame(expected.Range, actual.Range, wrappedRequest, actual.Expression!);
            AssertSame(expected.Parity, actual.Parity, wrappedRequest, actual.Expression!);
            AssertSame(expected.Zeros, actual.Zeros, wrappedRequest, actual.Expression!);
            AssertSame(expected.YIntercept, actual.YIntercept, wrappedRequest, actual.Expression!);
            AssertSame(expected.Minima, actual.Minima, wrappedRequest, actual.Expression!);
            AssertSame(expected.Maxima, actual.Maxima, wrappedRequest, actual.Expression!);
            AssertSame(expected.InflectionPoints, actual.InflectionPoints, wrappedRequest, actual.Expression!);
            AssertSame(expected.VerticalAsymptotes, actual.VerticalAsymptotes, wrappedRequest, actual.Expression!);
            AssertSame(expected.HorizontalAsymptotes, actual.HorizontalAsymptotes, wrappedRequest, actual.Expression!);
            AssertSame(expected.ObliqueAsymptotes, actual.ObliqueAsymptotes, wrappedRequest, actual.Expression!);
            AssertSame(expected.Monotonicity, actual.Monotonicity, wrappedRequest, actual.Expression!);
            AssertSame(expected.Period, actual.Period, wrappedRequest, actual.Expression!);
        }
    }

    [Fact]
    public void NeutralProjectionReachesEachIntendedCompositionReplay()
    {
        InputExpression x = Variable();
        InputExpression sine = Function("sin", x);
        InputExpression pi = InputExpression.Variable("pi", Source);
        (InputExpression Wrapped, Type CertificateType)[] cases =
        [
            (Add(Function("abs", sine), Number(0)),
                typeof(AbsoluteCompositionProofCertificate)),
            (Add(Function("exp", sine), Number(0)),
                typeof(UnaryCompositionProofCertificate)),
            (Add(Function(
                    "sqrt",
                    Function("sin", Add(x, Divide(pi, Number(4))))),
                Number(0)),
                typeof(AffinePhaseSineCompositionProofCertificate)),
            (Add(Number(0), Function("sin", Add(
                    Power(x, 3),
                    Function("sqrt", x)))),
                typeof(MonotoneTrigonometricPhaseProofCertificate))
        ];

        foreach ((InputExpression wrapped, Type certificateType) in cases)
        {
            AnalysisRequest request = Request(wrapped);
            AnalysisReport report = AnalysisEngine.Analyze(request);
            SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);
            ProofOutcome<object>[] matching = Outcomes(report)
                .Where(outcome => outcome.Certificate?.GetType() == certificateType)
                .ToArray();
            Assert.NotEmpty(matching);
            foreach (ProofOutcome<object> outcome in matching)
            {
                Assert.True(CertificateChecker.Check(request, semantic, outcome));
                Assert.True(CheckDedicated(
                    request,
                    semantic,
                    outcome.Certificate!,
                    ClaimCanonical.ForObject(outcome.Value!)));
            }
        }
    }

    [Fact]
    public void RetainedDomainRestrictionsAreNeverProjectedAway()
    {
        InputExpression x = Variable();
        InputExpression puncture = Multiply(
            Number(0),
            Divide(Number(1), x));
        InputExpression partialAffine = Add(x, puncture);
        InputExpression partialAbsolute = Add(Function("abs", x), puncture);
        SemanticExpression partialSource = Build(partialAbsolute);
        Assert.True(SemanticSourceProjection.TryCreate(
            partialSource,
            new ResourceBudget(),
            out SemanticExpression retainedProjection));
        Assert.Equal("additive-identity", Assert.Single(retainedProjection.RewriteHistory).Rule);
        Assert.Equal(2, retainedProjection.SourceOperands.Length);
        AnalysisReport partialAbsoluteReport = AnalysisEngine.Analyze(Request(partialAbsolute));
        Assert.Equal(
            "union[interval[-inf,0,q:0,0],interval[q:0,0,+inf,0]]",
            Assert.IsAssignableFrom<RealSet>(partialAbsoluteReport.Domain.Value).Canonical);

        InputExpression[] totalOnlyTheoremInputs =
        [
            Function("sign", partialAffine),
            Function("floor", partialAffine),
            Function("min", partialAffine, Number(1))
        ];

        foreach (InputExpression input in totalOnlyTheoremInputs)
        {
            AnalysisReport report = AnalysisEngine.Analyze(Request(input));
            if (report.Domain.State == ProofState.Proved)
            {
                Assert.NotEqual("reals", report.Domain.Value!.Canonical);
            }

            Assert.DoesNotContain(
                Outcomes(report),
                static outcome => outcome.Certificate is
                    AffineSignProofCertificate or
                    AffineFloorProofCertificate or
                    AffineMinMaxProofCertificate);
        }
    }

    [Fact]
    public void ProjectionRejectsForgedNeutralRewriteEvidence()
    {
        SemanticExpression source = Build(Add(Function("abs", Variable()), Number(0)));
        RewriteStep rewrite = Assert.Single(source.RewriteHistory);
        Assert.True(SemanticSourceProjection.TryCreate(
            source,
            new ResourceBudget(),
            out SemanticExpression projected));
        Assert.Single(projected.SourceOperands);
        Assert.Empty(projected.RewriteHistory);

        AssertInvalid(source with
        {
            RewriteHistory = [rewrite with { Rule = "multiplicative-identity" }]
        });
        AssertInvalid(source with
        {
            RewriteHistory = [rewrite with { Before = rewrite.Before + ":forged" }]
        });
        AssertInvalid(source with
        {
            RewriteHistory = [rewrite with { After = rewrite.After + ":forged" }]
        });
        AssertInvalid(source with
        {
            RewriteHistory = [rewrite with { Guard = Formula.False }]
        });
        AssertInvalid(source with { DefinedWhen = Formula.False });
        AssertInvalid(source with { ContinuousWhen = Formula.False });
        AssertInvalid(source with { DifferentiableWhen = Formula.False });
    }

    [Fact]
    public void ConstantValuedNeutralFoldExposesTheUnderlyingGuardedRewrite()
    {
        InputExpression sine = Function("sin", Variable());
        SemanticExpression source = Build(Add(
            Divide(sine, sine),
            Number(0)));

        Assert.Equal("exact-constant-fold", Assert.Single(source.RewriteHistory).Rule);
        Assert.True(SemanticSourceProjection.TryCreate(
            source,
            new ResourceBudget(),
            out SemanticExpression projected));
        Assert.Equal("self-division-value", Assert.Single(projected.RewriteHistory).Rule);
        Assert.Equal(source.DefinedWhen.Canonical, projected.DefinedWhen.Canonical);
    }

    private static void AssertInvalid(SemanticExpression expression)
    {
        Assert.False(SemanticSourceProjection.TryCreate(
            expression,
            new ResourceBudget(),
            out _));
    }

    private static void AssertSame<T>(
        ProofOutcome<T> expected,
        ProofOutcome<T> actual,
        AnalysisRequest request,
        SemanticExpression expression)
    {
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.UnknownReason, actual.UnknownReason);
        Assert.NotEqual(UnknownReason.CertificateRejected, actual.UnknownReason);
        if (expected.State == ProofState.Unknown)
        {
            Assert.Null(actual.Certificate);
            return;
        }

        Assert.Equal(
            ClaimCanonical.ForObject(expected.Value!),
            ClaimCanonical.ForObject(actual.Value!));
        Assert.True(CertificateChecker.Check(request, expression, actual));
        Assert.True(CheckDedicated(
            request,
            expression,
            actual.Certificate!,
            ClaimCanonical.ForObject(actual.Value!)));
    }

    private static bool CheckDedicated(
        AnalysisRequest request,
        SemanticExpression expression,
        ProofCertificate certificate,
        string claim)
    {
        if (!SemanticSourceProjection.TryCreate(
                expression,
                new ResourceBudget(),
                out SemanticExpression projected))
        {
            return false;
        }

        return certificate switch
        {
            SemialgebraicUnaryProofCertificate unary =>
                SemialgebraicUnaryCertificateChecker.Check(
                    request,
                    projected,
                    unary,
                    claim,
                    new ResourceBudget()),
            FixedRationalPowerProofCertificate power =>
                FixedRationalPowerCertificateChecker.Check(
                    request,
                    projected,
                    power,
                    claim,
                    new ResourceBudget()),
            ElementaryCompositionProofCertificate composition =>
                ElementaryCompositionCertificateReplay.Check(
                    request,
                    projected,
                    composition,
                    claim,
                    new ResourceBudget()),
            GuardedConstantProofCertificate constant =>
                GuardedConstantCertificateReplay.Check(
                    request,
                    projected,
                    constant,
                    claim,
                    new ResourceBudget()),
            AffineSignProofCertificate sign =>
                AffineSignCertificateChecker.Check(
                    request,
                    projected,
                    sign,
                    claim,
                    new ResourceBudget()),
            AffineFloorProofCertificate floor =>
                AffineFloorCertificateChecker.Check(
                    request,
                    projected,
                    floor,
                    claim,
                    new ResourceBudget()),
            AffineMinMaxProofCertificate minMax =>
                AffineMinMaxCertificateReplay.Check(
                    request,
                    projected,
                    minMax,
                    claim,
                    new ResourceBudget()),
            AbsoluteCompositionProofCertificate absolute =>
                AbsoluteCompositionCertificateReplay.Check(
                    request,
                    projected,
                    absolute,
                    claim,
                    new ResourceBudget()),
            UnaryCompositionProofCertificate unaryComposition =>
                UnaryCompositionCertificateReplay.Check(
                    request,
                    projected,
                    unaryComposition,
                    claim,
                    new ResourceBudget()),
            AffinePhaseSineCompositionProofCertificate affinePhase =>
                AffinePhaseSineCompositionCertificateReplay.Check(
                    request,
                    projected,
                    affinePhase,
                    claim,
                    new ResourceBudget()),
            MonotoneTrigonometricPhaseProofCertificate phase =>
                MonotoneTrigonometricPhaseCertificateReplay.Check(
                    request,
                    projected,
                    phase,
                    claim,
                    new ResourceBudget()),
            _ => true
        };
    }

    private static IEnumerable<ProofOutcome<object>> Outcomes(AnalysisReport report)
    {
        yield return Box(report.Domain);
        yield return Box(report.Range);
        yield return Box(report.Parity);
        yield return Box(report.Zeros);
        yield return Box(report.YIntercept);
        yield return Box(report.Minima);
        yield return Box(report.Maxima);
        yield return Box(report.InflectionPoints);
        yield return Box(report.VerticalAsymptotes);
        yield return Box(report.HorizontalAsymptotes);
        yield return Box(report.ObliqueAsymptotes);
        yield return Box(report.Monotonicity);
        yield return Box(report.Period);
    }

    private static ProofOutcome<object> Box<T>(ProofOutcome<T> outcome)
    {
        return outcome.State == ProofState.Unknown
            ? ProofOutcome<object>.Unknown(outcome.UnknownReason!.Value)
            : ProofOutcome<object>.Proved(outcome.Value!, outcome.Certificate!);
    }

    private static AnalysisRequest Request(InputExpression input)
    {
        return new AnalysisRequest(input, AnalysisFeatures.All, AngleUnit.Radians, "x", static () => true);
    }

    private static SemanticExpression Build(InputExpression input)
    {
        return new SemanticGraphBuilder(new ResourceBudget()).Build(input);
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
        return InputExpression.Binary(
            InputExpressionKind.Power,
            basis,
            Number(exponent),
            Source);
    }

    private static InputExpression Function(
        string name,
        params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
