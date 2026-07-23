using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffineDiscontinuousCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [MemberData(nameof(AffineUnitMatrix))]
    public void SignAndFloorReplayEveryFeatureAcrossUnitsAndAffineOrientations(
        string function,
        int angleUnitValue,
        int slope,
        int intercept)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression affine = Add(
            Multiply(Number(slope), Variable()),
            Number(intercept));
        InputExpression expression = Function(function, affine);
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.All,
            angleUnit);
        SemanticExpression semantic = Build(expression);

        if (function == "sign")
        {
            AssertSignReplay<RealSet>(request, semantic, AnalysisFeatures.Domain);
            AssertSignReplay<RealSet>(request, semantic, AnalysisFeatures.Range);
            AssertSignReplay<FunctionParity>(request, semantic, AnalysisFeatures.Parity);
            AssertSignReplay<RealSet>(request, semantic, AnalysisFeatures.Zeros);
            AssertSignReplay<OptionalValue<ExactReal>>(
                request,
                semantic,
                AnalysisFeatures.YIntercept);
            AssertSignReplay<ImmutableArray<FeaturePoint>>(
                request,
                semantic,
                AnalysisFeatures.Minima);
            AssertSignReplay<ImmutableArray<FeaturePoint>>(
                request,
                semantic,
                AnalysisFeatures.Maxima);
            AssertSignReplay<ImmutableArray<FeaturePoint>>(
                request,
                semantic,
                AnalysisFeatures.InflectionPoints);
            AssertSignReplay<ImmutableArray<Asymptote>>(
                request,
                semantic,
                AnalysisFeatures.VerticalAsymptotes);
            AssertSignReplay<ImmutableArray<Asymptote>>(
                request,
                semantic,
                AnalysisFeatures.HorizontalAsymptotes);
            AssertSignReplay<ImmutableArray<Asymptote>>(
                request,
                semantic,
                AnalysisFeatures.ObliqueAsymptotes);
            AssertSignReplay<ImmutableArray<MonotoneRegion>>(
                request,
                semantic,
                AnalysisFeatures.Monotonicity);
            AssertSignReplay<Periodicity>(request, semantic, AnalysisFeatures.Period);
            return;
        }

        AssertFloorReplay<RealSet>(request, semantic, AnalysisFeatures.Domain);
        AssertFloorReplay<RealSet>(request, semantic, AnalysisFeatures.Range);
        AssertFloorReplay<FunctionParity>(request, semantic, AnalysisFeatures.Parity);
        AssertFloorReplay<RealSet>(request, semantic, AnalysisFeatures.Zeros);
        AssertFloorReplay<OptionalValue<ExactReal>>(
            request,
            semantic,
            AnalysisFeatures.YIntercept);
        AssertFloorReplay<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Minima);
        AssertFloorReplay<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Maxima);
        AssertFloorReplay<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.InflectionPoints);
        AssertFloorReplay<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.VerticalAsymptotes);
        AssertFloorReplay<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes);
        AssertFloorReplay<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.ObliqueAsymptotes);
        AssertFloorReplay<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity);
        AssertFloorReplay<Periodicity>(request, semantic, AnalysisFeatures.Period);
    }

    [Theory]
    [InlineData("sign")]
    [InlineData("floor")]
    public void RetainedArgumentHolesRejectTextuallyAdaptedCertificates(string function)
    {
        InputExpression affine = Add(Multiply(Number(2), Variable()), Number(-4));
        InputExpression cleanInput = Function(function, affine);
        AnalysisRequest cleanRequest = Request(
            cleanInput,
            AnalysisFeatures.Range,
            AngleUnit.Radians);
        SemanticExpression clean = Build(cleanInput);

        InputExpression hiddenAffine = Add(
            affine,
            Multiply(
                Number(0),
                Divide(Number(1), Subtract(Variable(), Number(1)))));
        InputExpression holedInput = Function(function, hiddenAffine);
        AnalysisRequest holedRequest = Request(
            holedInput,
            AnalysisFeatures.Range,
            AngleUnit.Radians);
        SemanticExpression holed = Build(holedInput);
        Assert.Equal(clean.Value.Canonical, holed.Value.Canonical);
        Assert.NotEqual(clean.DefinedWhen.Canonical, holed.DefinedWhen.Canonical);

        if (function == "sign")
        {
            Assert.True(AffineSignAnalyzer.TryAnalyze(
                cleanRequest,
                clean,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> outcome));
            var certificate = Assert.IsType<AffineSignProofCertificate>(
                outcome.Certificate);
            AffineSignProofCertificate forged = certificate with
            {
                DefinednessCanonical = holed.DefinedWhen.Canonical
            };
            Assert.False(AffineSignCertificateChecker.Check(
                holedRequest,
                holed,
                forged,
                ClaimCanonical.For(outcome.Value!),
                new ResourceBudget()));
            Assert.False(CertificateChecker.Check(
                holedRequest,
                holed,
                ProofOutcome<RealSet>.Proved(outcome.Value!, forged)));
            return;
        }

        Assert.True(AffineFloorAnalyzer.TryAnalyze(
            cleanRequest,
            clean,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> floorOutcome));
        var floorCertificate = Assert.IsType<AffineFloorProofCertificate>(
            floorOutcome.Certificate);
        AffineFloorProofCertificate forgedFloor = floorCertificate with
        {
            ArgumentDefinednessCanonical = holed.SourceOperands[0].DefinedWhen.Canonical,
            DefinednessCanonical = holed.DefinedWhen.Canonical
        };
        Assert.False(AffineFloorCertificateChecker.Check(
            holedRequest,
            holed,
            forgedFloor,
            ClaimCanonical.For(floorOutcome.Value!),
            new ResourceBudget()));
        Assert.False(CertificateChecker.Check(
            holedRequest,
            holed,
            ProofOutcome<RealSet>.Proved(floorOutcome.Value!, forgedFloor)));
    }

    [Theory]
    [InlineData("sign")]
    [InlineData("floor")]
    public void WrongOrDuplicateDiscontinuityRegularityIsRejected(string function)
    {
        InputExpression input = Function(
            function,
            Add(Multiply(Number(-3), Variable()), Number(2)));
        AnalysisRequest request = Request(
            input,
            AnalysisFeatures.Range,
            AngleUnit.Grads);
        SemanticExpression semantic = Build(input);
        Formula correctRegularity = semantic.ContinuousWhen;
        Formula wrongRegularity = Formula.Predicate(
            ExactPredicate.IsLocallyConstant,
            semantic.SourceOperands[0].Value);
        Formula duplicateRegularity = new JunctionFormula(
            true,
            [correctRegularity, correctRegularity]);

        if (function == "sign")
        {
            Assert.True(AffineSignAnalyzer.TryAnalyze(
                request,
                semantic,
                AnalysisFeatures.Range,
                new ResourceBudget(),
                out ProofOutcome<RealSet> outcome));
            var certificate = Assert.IsType<AffineSignProofCertificate>(
                outcome.Certificate);
            AssertSignRejected(semantic with { ContinuousWhen = Formula.True });
            AssertSignRejected(semantic with { ContinuousWhen = wrongRegularity });
            AssertSignRejected(semantic with { ContinuousWhen = duplicateRegularity });
            AssertSignRejected(semantic with { DifferentiableWhen = Formula.True });
            return;

            void AssertSignRejected(SemanticExpression changed) =>
                Assert.False(AffineSignCertificateChecker.Check(
                    request,
                    changed,
                    certificate,
                    ClaimCanonical.For(outcome.Value!),
                    new ResourceBudget()));
        }

        Assert.True(AffineFloorAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> floorOutcome));
        var floorCertificate = Assert.IsType<AffineFloorProofCertificate>(
            floorOutcome.Certificate);
        AssertFloorRejected(semantic with { ContinuousWhen = Formula.True });
        AssertFloorRejected(semantic with { ContinuousWhen = wrongRegularity });
        AssertFloorRejected(semantic with { ContinuousWhen = duplicateRegularity });
        AssertFloorRejected(semantic with { DifferentiableWhen = Formula.True });
        return;

        void AssertFloorRejected(SemanticExpression changed) =>
            Assert.False(AffineFloorCertificateChecker.Check(
                request,
                changed,
                floorCertificate,
                ClaimCanonical.For(floorOutcome.Value!),
                new ResourceBudget()));
    }

    public static IEnumerable<object[]> AffineUnitMatrix()
    {
        int[] units =
        [
            (int)AngleUnit.Radians,
            (int)AngleUnit.Degrees,
            (int)AngleUnit.Grads
        ];
        foreach (int unit in units)
        {
            yield return ["sign", unit, 2, -4];
            yield return ["sign", unit, -3, 2];
            yield return ["floor", unit, 2, -4];
            yield return ["floor", unit, -3, 2];
        }
    }

    private static void AssertSignReplay<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.True(AffineSignAnalyzer.TryAnalyze(
            request,
            semantic,
            feature,
            new ResourceBudget(),
            out ProofOutcome<T> outcome));
        var certificate = Assert.IsType<AffineSignProofCertificate>(outcome.Certificate);
        Assert.True(AffineSignCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget()));
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
    }

    private static void AssertFloorReplay<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.True(AffineFloorAnalyzer.TryAnalyze(
            request,
            semantic,
            feature,
            new ResourceBudget(),
            out ProofOutcome<T> outcome));
        var certificate = Assert.IsType<AffineFloorProofCertificate>(outcome.Certificate);
        Assert.True(AffineFloorCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget()));
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
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
        return InputExpression.Function(name, [argument], Source);
    }
}
