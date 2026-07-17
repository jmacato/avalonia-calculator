using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class GuardedCotangentIdentityCertificateReplayTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [MemberData(nameof(IdentityAliasMatrix))]
    public void EveryFeatureReplaysForIdentityAndTangentAliasesAcrossAngleUnits(
        int angleUnitValue,
        string identityAlias,
        string tangentAlias)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression argument = Multiply(Number(2), Variable());
        InputExpression expression = Divide(
            Identity(identityAlias, Variable()),
            Tangent(tangentAlias, argument));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.All,
            angleUnit);
        SemanticExpression semantic = Build(expression);

        AssertReplay<RealSet>(request, semantic, AnalysisFeatures.Domain);
        AssertReplay<RealSet>(request, semantic, AnalysisFeatures.Range);
        AssertReplay<FunctionParity>(request, semantic, AnalysisFeatures.Parity);
        AssertReplay<RealSet>(request, semantic, AnalysisFeatures.Zeros);
        AssertReplay<OptionalValue<ExactReal>>(
            request,
            semantic,
            AnalysisFeatures.YIntercept);
        AssertReplay<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Minima);
        AssertReplay<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.Maxima);
        AssertReplay<ImmutableArray<FeaturePoint>>(
            request,
            semantic,
            AnalysisFeatures.InflectionPoints);
        AssertReplay<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.VerticalAsymptotes);
        AssertReplay<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.HorizontalAsymptotes);
        AssertReplay<ImmutableArray<Asymptote>>(
            request,
            semantic,
            AnalysisFeatures.ObliqueAsymptotes);
        AssertReplay<ImmutableArray<MonotoneRegion>>(
            request,
            semantic,
            AnalysisFeatures.Monotonicity);
        AssertReplay<Periodicity>(request, semantic, AnalysisFeatures.Period);
    }

    [Fact]
    public void ReplayRejectsRetainedIncorrectAndDuplicateGuards()
    {
        InputExpression expression = Divide(
            Identity("ordered", Variable()),
            Function("tan", Variable()));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Range,
            AngleUnit.Radians);
        SemanticExpression semantic = Build(expression);
        Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        RealSet range = outcome.Value!;
        var certificate = Assert.IsType<GuardedCotangentIdentityProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(range);
        SemanticExpression numerator = semantic.SourceOperands[0];
        SemanticExpression denominator = semantic.SourceOperands[1];
        Formula cosineGuard = denominator.DefinedWhen;
        Formula denominatorGuard = Formula.Compare(
            denominator.Value,
            Comparison.NotEqual,
            Build(Number(0)).Value);

        Formula constantTrue = Formula.Compare(
            Build(Number(1)).Value,
            Comparison.NotEqual,
            Build(Number(0)).Value);
        Formula denominatorWithTrue = Formula.And(cosineGuard, constantTrue);
        Formula outerWithTrue = Formula.And(
            cosineGuard,
            denominatorGuard,
            constantTrue);
        SemanticExpression allowed = ReplaceGuards(
            semantic,
            numerator,
            denominator,
            denominatorWithTrue,
            outerWithTrue);
        Assert.True(GuardedCotangentIdentityCertificateChecker.Check(
            request,
            allowed,
            WithDefinedness(certificate, outerWithTrue),
            claim,
            new ResourceBudget()));

        Formula retainedHole = Formula.Compare(
            Build(Variable()).Value,
            Comparison.NotEqual,
            Build(Number(1)).Value);
        Formula holedDenominator = Formula.And(cosineGuard, retainedHole);
        Formula holedOuter = Formula.And(
            cosineGuard,
            denominatorGuard,
            retainedHole);
        AssertRejected(ReplaceGuards(
            semantic,
            numerator,
            denominator,
            holedDenominator,
            holedOuter));

        Formula wrongGuard = Formula.Compare(
            Build(Function("sin", Variable())).Value,
            Comparison.NotEqual,
            Build(Number(0)).Value);
        Formula wrongOuter = Formula.And(wrongGuard, denominatorGuard);
        AssertRejected(ReplaceGuards(
            semantic,
            numerator,
            denominator,
            wrongGuard,
            wrongOuter));

        Formula duplicateDenominator = new JunctionFormula(
            true,
            [cosineGuard, cosineGuard]);
        Formula duplicateOuter = new JunctionFormula(
            true,
            [cosineGuard, cosineGuard, denominatorGuard]);
        AssertRejected(ReplaceGuards(
            semantic,
            numerator,
            denominator,
            duplicateDenominator,
            duplicateOuter));

        Formula duplicateZeroGuard = new JunctionFormula(
            true,
            [cosineGuard, denominatorGuard, denominatorGuard]);
        AssertRejected(ReplaceGuards(
            semantic,
            numerator,
            denominator,
            cosineGuard,
            duplicateZeroGuard));
        return;

        void AssertRejected(SemanticExpression forgedExpression)
        {
            GuardedCotangentIdentityProofCertificate forgedCertificate =
                WithDefinedness(certificate, forgedExpression.DefinedWhen);
            Assert.False(GuardedCotangentIdentityCertificateChecker.Check(
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
    public void ReplayRejectsFieldClaimAndRequestContextMutations()
    {
        InputExpression expression = Divide(
            Identity("reordered", Variable()),
            Tangent("ratio", Variable()));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Range,
            AngleUnit.Radians);
        SemanticExpression semantic = Build(expression);
        Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        RealSet range = outcome.Value!;
        var certificate = Assert.IsType<GuardedCotangentIdentityProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(range);

        AssertRejected(certificate with { Feature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { ProvenFeature = AnalysisFeatures.Zeros });
        AssertRejected(certificate with { NumeratorCanonical = "changed" });
        AssertRejected(certificate with { DenominatorCanonical = "changed" });
        AssertRejected(certificate with { PatternCanonical = "changed" });
        AssertRejected(certificate with { DomainCanonical = AllRealSet.Instance.Canonical });
        AssertRejected(certificate with { DefinednessCanonical = Formula.True.Canonical });
        AssertRejected(certificate with { Rule = "untrusted" });
        AssertRejected(certificate with { Subject = certificate.Subject + ":changed" });
        AssertRejected(certificate with { Claim = EmptySet.Instance.Canonical });

        AnalysisRequest unrequested = Request(
            expression,
            AnalysisFeatures.Zeros,
            AngleUnit.Radians);
        Assert.False(GuardedCotangentIdentityCertificateChecker.Check(
            unrequested,
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        AnalysisRequest wrongVariable = new(
            expression,
            AnalysisFeatures.Range,
            AngleUnit.Radians,
            "t",
            static () => true);
        Assert.False(GuardedCotangentIdentityCertificateChecker.Check(
            wrongVariable,
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        AnalysisRequest wrongUnit = Request(
            expression,
            AnalysisFeatures.Range,
            AngleUnit.Degrees);
        Assert.False(GuardedCotangentIdentityCertificateChecker.Check(
            wrongUnit,
            semantic,
            certificate,
            claim,
            new ResourceBudget()));
        return;

        void AssertRejected(GuardedCotangentIdentityProofCertificate changed) =>
            Assert.False(GuardedCotangentIdentityCertificateChecker.Check(
                request,
                semantic,
                changed,
                claim,
                new ResourceBudget()));
    }

    [Fact]
    public void ReplayHonorsBudgetAndRevisionCancellation()
    {
        InputExpression expression = Divide(
            Identity("ordered", Variable()),
            Function("tan", Variable()));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.Range,
            AngleUnit.Radians);
        SemanticExpression semantic = Build(expression);
        Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
            request,
            semantic,
            AnalysisFeatures.Range,
            new ResourceBudget(),
            out ProofOutcome<RealSet> outcome));
        var certificate = Assert.IsType<GuardedCotangentIdentityProofCertificate>(
            outcome.Certificate);
        string claim = ClaimCanonical.For(outcome.Value!);

        Assert.Throws<AnalysisCancelledException>(() =>
            GuardedCotangentIdentityCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                new ResourceBudget(static () => false)));

        var exhausted = new ResourceBudget();
        exhausted.Charge(AnalysisLimits.WorkUnits);
        Assert.Throws<BudgetExceededException>(() =>
            GuardedCotangentIdentityCertificateChecker.Check(
                request,
                semantic,
                certificate,
                claim,
                exhausted));
    }

    public static IEnumerable<object[]> IdentityAliasMatrix()
    {
        int[] units =
        [
            (int)AngleUnit.Radians,
            (int)AngleUnit.Degrees,
            (int)AngleUnit.Grads
        ];
        foreach (int unit in units)
        {
            yield return [unit, "ordered", "tan"];
            yield return [unit, "reordered", "ratio"];
            yield return [unit, "one", "scaled"];
        }
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        SemanticExpression semantic,
        AnalysisFeatures feature)
    {
        Assert.True(GuardedCotangentIdentityAnalyzer.TryAnalyze(
            request,
            semantic,
            feature,
            new ResourceBudget(),
            out ProofOutcome<T> outcome));
        var certificate = Assert.IsType<GuardedCotangentIdentityProofCertificate>(
            outcome.Certificate);
        Assert.True(GuardedCotangentIdentityCertificateChecker.Check(
            request,
            semantic,
            certificate,
            ClaimCanonical.ForObject(outcome.Value!),
            new ResourceBudget()));
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
    }

    private static SemanticExpression ReplaceGuards(
        SemanticExpression expression,
        SemanticExpression numerator,
        SemanticExpression denominator,
        Formula denominatorDefinedWhen,
        Formula outerDefinedWhen)
    {
        SemanticExpression changedDenominator = denominator with
        {
            DefinedWhen = denominatorDefinedWhen
        };
        return expression with
        {
            DefinedWhen = outerDefinedWhen,
            SourceOperands = [numerator, changedDenominator]
        };
    }

    private static GuardedCotangentIdentityProofCertificate WithDefinedness(
        GuardedCotangentIdentityProofCertificate certificate,
        Formula definedWhen) =>
        certificate with { DefinednessCanonical = definedWhen.Canonical };

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features,
        AngleUnit angleUnit) =>
        new(expression, features, angleUnit, "x", static () => true);

    private static SemanticExpression Build(InputExpression input) =>
        new SemanticGraphBuilder(new ResourceBudget()).Build(input);

    private static InputExpression Identity(string alias, InputExpression argument) =>
        alias switch
        {
            "ordered" => Add(Power(Sin(argument), 2), Power(Cos(argument), 2)),
            "reordered" => Add(Power(Cos(argument), 2), Power(Sin(argument), 2)),
            "one" => Number(1),
            _ => throw new ArgumentOutOfRangeException(nameof(alias))
        };

    private static InputExpression Tangent(string alias, InputExpression argument) =>
        alias switch
        {
            "tan" => Function("tan", argument),
            "ratio" => Divide(Sin(argument), Cos(argument)),
            "scaled" => Multiply(Number(-3), Function("tan", argument)),
            _ => throw new ArgumentOutOfRangeException(nameof(alias))
        };

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Power(InputExpression basis, int exponent) =>
        InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);

    private static InputExpression Sin(InputExpression argument) => Function("sin", argument);

    private static InputExpression Cos(InputExpression argument) => Function("cos", argument);

    private static InputExpression Function(string name, InputExpression argument) =>
        InputExpression.Function(name, [argument], Source);
}
