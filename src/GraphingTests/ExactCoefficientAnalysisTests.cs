using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ExactCoefficientAnalysisTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Fact]
    public void SymbolicCoefficientCorpusProvesEveryFeatureWithReplayableCertificates()
    {
        foreach ((string name, InputExpression expression) in Corpus())
        {
            AnalysisRequest request = Request(expression, AnalysisFeatures.All);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            AssertAllProved(report, name);
            AssertAllReplay(request, report, name);
            foreach (ProofCertificate certificate in Certificates(report).Skip(1))
            {
                Assert.IsType<ExactCoefficientProofCertificate>(certificate);
            }
        }
    }

    [Fact]
    public void AffineQuadraticAndMobiusClaimsUseExactOrderedValues()
    {
        AnalysisReport affine = AnalysisEngine.Analyze(Request(
            Multiply(Symbol("e"), Variable()),
            AnalysisFeatures.All));
        Assert.IsType<AllRealSet>(Proved(affine.Range));
        Assert.Equal("points[q:0]", Proved(affine.Zeros).Canonical);
        Assert.Equal(FunctionParity.Odd, Proved(affine.Parity));
        Asymptote line = Assert.Single(Proved(affine.ObliqueAsymptotes));
        Assert.Equal("named:e", ExactRealCanonical.Format(line.Slope!));
        Assert.Equal(Monotonicity.Increasing, Assert.Single(Proved(affine.Monotonicity)).Direction);

        AnalysisReport quadratic = AnalysisEngine.Analyze(Request(
            Subtract(Power(Variable(), 2), Symbol("pi")),
            AnalysisFeatures.All));
        Assert.Equal("interval[pi:-1:0,1,+inf,0]", Proved(quadratic.Range).Canonical);
        Assert.Equal(
            "points[fn:negate(fn:sqrt(pi:1:0)),fn:sqrt(pi:1:0)]",
            Proved(quadratic.Zeros).Canonical);
        Assert.Equal(FunctionParity.Even, Proved(quadratic.Parity));
        var minimum = Assert.IsType<ConstantYFeaturePoint>(Assert.Single(Proved(quadratic.Minima)));
        Assert.Equal("q:0", ExactRealCanonical.Format(Assert.IsType<SingletonReal>(minimum.X).Value));
        Assert.Equal("pi:-1:0", ExactRealCanonical.Format(minimum.Y));

        AnalysisReport pole = AnalysisEngine.Analyze(Request(
            Divide(Number(1), Subtract(Variable(), Symbol("pi"))),
            AnalysisFeatures.All));
        Assert.Equal(
            "difference[reals,points[pi:1:0]]",
            Proved(pole.Domain).Canonical);
        Assert.Equal(
            "difference[reals,points[q:0]]",
            Proved(pole.Range).Canonical);
        Assert.Equal(FunctionParity.Neither, Proved(pole.Parity));
        Assert.Equal("pi:1:0", Coordinate(Assert.Single(Proved(pole.VerticalAsymptotes))));
        Assert.Equal("q:0", Coordinate(Assert.Single(Proved(pole.HorizontalAsymptotes))));
        Assert.All(Proved(pole.Monotonicity), static region =>
            Assert.Equal(Monotonicity.Decreasing, region.Direction));

        AnalysisReport numerator = AnalysisEngine.Analyze(Request(
            Add(
                Divide(Symbol("pi"), Subtract(Variable(), Number(1))),
                Symbol("e")),
            AnalysisFeatures.All));
        Assert.Equal(
            "difference[reals,points[named:e]]",
            Proved(numerator.Range).Canonical);
        Assert.Equal("q:1", Coordinate(Assert.Single(Proved(numerator.VerticalAsymptotes))));
        Assert.Equal("named:e", Coordinate(Assert.Single(Proved(numerator.HorizontalAsymptotes))));
    }

    [Fact]
    public void ExactAffineTrigOffsetsFrequenciesAndPhasesHaveFundamentalPeriods()
    {
        AnalysisReport sineFrequency = AnalysisEngine.Analyze(Request(
            Function("sin", Multiply(Symbol("pi"), Variable())),
            AnalysisFeatures.All));
        Assert.Equal("q:2", ExactRealCanonical.Format(Proved(sineFrequency.Period).FundamentalPeriod!));
        Assert.Equal(FunctionParity.Odd, Proved(sineFrequency.Parity));
        Assert.Equal("points[q:0]", Proved(sineFrequency.YIntercept).HasValue
            ? RealSets.Points([Proved(sineFrequency.YIntercept).Value!]).Canonical
            : "missing");

        AnalysisReport cosineFrequency = AnalysisEngine.Analyze(Request(
            Function("cos", Multiply(Symbol("e"), Variable())),
            AnalysisFeatures.All));
        Assert.Equal(
            "fn:divide(pi:2:0,named:e)",
            ExactRealCanonical.Format(Proved(cosineFrequency.Period).FundamentalPeriod!));
        Assert.Equal(FunctionParity.Even, Proved(cosineFrequency.Parity));

        AnalysisReport shiftedSine = AnalysisEngine.Analyze(Request(
            Add(Function("sin", Variable()), Symbol("pi")),
            AnalysisFeatures.All));
        Assert.Equal("interval[pi:1:-1,1,pi:1:1,1]", Proved(shiftedSine.Range).Canonical);
        Assert.IsType<EmptySet>(Proved(shiftedSine.Zeros));
        Assert.Equal(FunctionParity.Neither, Proved(shiftedSine.Parity));

        AnalysisReport phase = AnalysisEngine.Analyze(Request(
            Function("sin", Add(Variable(), Divide(Symbol("pi"), Number(2)))),
            AnalysisFeatures.All));
        Assert.Equal(FunctionParity.Even, Proved(phase.Parity));
        Assert.Equal("q:1", ExactRealCanonical.Format(Proved(phase.YIntercept).Value!));
        Assert.Equal("pi:2:0", ExactRealCanonical.Format(Proved(phase.Period).FundamentalPeriod!));

        AnalysisReport tangent = AnalysisEngine.Analyze(Request(
            Function("tan", Add(Variable(), Divide(Symbol("pi"), Number(2)))),
            AnalysisFeatures.All));
        Assert.Equal(FunctionParity.Odd, Proved(tangent.Parity));
        Assert.False(Proved(tangent.YIntercept).HasValue);
        Assert.Equal("pi:1:0", ExactRealCanonical.Format(Proved(tangent.Period).FundamentalPeriod!));
        Assert.All(Proved(tangent.Monotonicity), static region =>
            Assert.Equal(Monotonicity.Increasing, region.Direction));
    }

    [Fact]
    public void ExactAmplitudeAndRationalShiftProveInverseZeroFamilies()
    {
        InputExpression x = Variable();
        InputExpression expression = Add(
            Multiply(
                Symbol("pi"),
                Function(
                    "sin",
                    Subtract(Multiply(Number(2), x), Number(1)))),
            Number(1));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Zeros);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(ProofState.Proved, report.Zeros.State);
        var zeros = Assert.IsType<UnionSet>(report.Zeros.Value);
        Assert.Equal(2, zeros.Operands.OfType<PeriodicPointSet>().Count());
        Assert.All(
            zeros.Operands.OfType<PeriodicPointSet>(),
            static family => Assert.Contains(
                "asin",
                family.Canonical,
                StringComparison.Ordinal));
        var certificate = Assert.IsType<ExactCoefficientProofCertificate>(
            report.Zeros.Certificate);
        Assert.Equal(ExactCoefficientPatternKind.AffineSine, certificate.PatternKind);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Zeros));

        ProofOutcome<RealSet> mutated = ProofOutcome<RealSet>.Proved(
            zeros,
            certificate with
            {
                PatternCanonical = certificate.PatternCanonical + ":changed"
            });
        Assert.False(CertificateChecker.Check(request, report.Expression!, mutated));
    }

    [Fact]
    public void DedicatedCertificateRejectsPatternOrderRuleAndClaimMutations()
    {
        InputExpression expression = Subtract(Power(Variable(), 2), Symbol("pi"));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Range);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        RealSet range = Proved(report.Range);
        var certificate = Assert.IsType<ExactCoefficientProofCertificate>(report.Range.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Range));
        Assert.NotEmpty(certificate.OrderWitnesses);

        AssertRejected(certificate with { PatternCanonical = certificate.PatternCanonical + ":mutated" });
        AssertRejected(certificate with { Rule = "untrusted" });
        ExactOrderWitness first = certificate.OrderWitnesses[0];
        AssertRejected(certificate with
        {
            OrderWitnesses = certificate.OrderWitnesses.SetItem(
                0,
                first with { Lower = first.Lower - BigRational.One })
        });
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            ProofOutcome<RealSet>.Proved(EmptySet.Instance, certificate)));

        void AssertRejected(ExactCoefficientProofCertificate changed) =>
            Assert.False(CertificateChecker.Check(
                request,
                report.Expression!,
                ProofOutcome<RealSet>.Proved(range, changed)));
    }

    [Fact]
    public void GuardedSimplificationNeverErasesAnOriginalHole()
    {
        InputExpression hole = Divide(Number(1), Subtract(Variable(), Number(1)));
        InputExpression expression = Add(
            Multiply(Symbol("pi"), Variable()),
            Multiply(Number(0), hole));
        AnalysisReport report = AnalysisEngine.Analyze(Request(expression, AnalysisFeatures.All));

        Assert.Equal(ProofState.Proved, report.Domain.State);
        Assert.Equal("union[interval[-inf,0,q:1,0],interval[q:1,0,+inf,0]]", Proved(report.Domain).Canonical);
        Assert.Equal(ProofState.Unknown, report.Range.State);
        Assert.Equal(UnknownReason.UnsupportedFragment, report.Range.UnknownReason);
        Assert.Equal(ProofState.Unknown, report.Parity.State);
        Assert.Equal(ProofState.Unknown, report.Monotonicity.State);
        Assert.DoesNotContain(
            Certificates(report),
            static certificate => certificate is ExactCoefficientProofCertificate);

        InputExpression cleanExpression = Multiply(Symbol("pi"), Variable());
        AnalysisRequest cleanRequest = Request(cleanExpression, AnalysisFeatures.Range);
        AnalysisReport clean = AnalysisEngine.Analyze(cleanRequest);
        RealSet cleanRange = Proved(clean.Range);
        var cleanCertificate = Assert.IsType<ExactCoefficientProofCertificate>(
            clean.Range.Certificate);
        SemanticExpression hiddenSemantic = Assert.IsType<SemanticExpression>(report.Expression);
        Assert.Equal(clean.Expression!.Value.Canonical, hiddenSemantic.Value.Canonical);
        Assert.False(CertificateChecker.Check(
            Request(expression, AnalysisFeatures.Range),
            hiddenSemantic,
            ProofOutcome<RealSet>.Proved(cleanRange, cleanCertificate)));
    }

    [Fact]
    public void ConstantTruePremisesCoexistWithExactPoleAndDenominatorGuards()
    {
        InputExpression squareRootTwo = Function("sqrt", Number(2));
        InputExpression tangent = Multiply(
            squareRootTwo,
            Function("tan", Variable()));
        AnalysisRequest tangentRequest = Request(tangent, AnalysisFeatures.Range);
        AnalysisReport tangentReport = AnalysisEngine.Analyze(tangentRequest);
        Assert.Equal(ProofState.Proved, tangentReport.Range.State);
        Assert.IsType<ExactCoefficientProofCertificate>(tangentReport.Range.Certificate);
        Assert.True(CertificateChecker.Check(
            tangentRequest,
            tangentReport.Expression!,
            tangentReport.Range));

        InputExpression reciprocal = Divide(
            squareRootTwo,
            Subtract(Variable(), Number(1)));
        AnalysisRequest reciprocalRequest = Request(reciprocal, AnalysisFeatures.Range);
        AnalysisReport reciprocalReport = AnalysisEngine.Analyze(reciprocalRequest);
        Assert.Equal(ProofState.Proved, reciprocalReport.Range.State);
        Assert.IsType<ExactCoefficientProofCertificate>(reciprocalReport.Range.Certificate);
        Assert.True(CertificateChecker.Check(
            reciprocalRequest,
            reciprocalReport.Expression!,
            reciprocalReport.Range));
    }

    [Fact]
    public void ExactCoefficientLimitsAndCancellationAreDeterministic()
    {
        InputExpression tooHigh = Multiply(
            Symbol("pi"),
            InputExpression.Binary(
                InputExpressionKind.Power,
                Variable(),
                Number(AnalysisLimits.UnivariateDegree + 1),
                Source));
        AnalysisReport degree = AnalysisEngine.Analyze(Request(tooHigh, AnalysisFeatures.Range));
        Assert.Equal(ProofState.Unknown, degree.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, degree.Range.UnknownReason);

        ExactInteger oversized = ExactInteger.One << AnalysisLimits.CoefficientBits;
        AnalysisReport coefficient = AnalysisEngine.Analyze(Request(
            Multiply(Multiply(Number(new BigRational(oversized)), Symbol("pi")), Variable()),
            AnalysisFeatures.Range));
        Assert.Equal(ProofState.Unknown, coefficient.Range.State);
        Assert.Equal(UnknownReason.BudgetExceeded, coefficient.Range.UnknownReason);

        AnalysisRequest cancelled = new(
            Multiply(Symbol("e"), Variable()),
            AnalysisFeatures.All,
            AngleUnit.Radians,
            "x",
            static () => false);
        Assert.Throws<AnalysisCancelledException>(() => AnalysisEngine.Analyze(cancelled));
    }

    private static IEnumerable<(string Name, InputExpression Expression)> Corpus()
    {
        InputExpression x = Variable();
        yield return ("e*x", Multiply(Symbol("e"), x));
        yield return ("x+pi", Add(x, Symbol("pi")));
        yield return ("pi*x", Multiply(Symbol("pi"), x));
        yield return ("sqrt(2)*x", Multiply(Function("sqrt", Number(2)), x));
        yield return ("pi*x+e", Add(Multiply(Symbol("pi"), x), Symbol("e")));
        yield return ("x^2-pi", Subtract(Power(x, 2), Symbol("pi")));
        yield return ("1/(x-pi)", Divide(Number(1), Subtract(x, Symbol("pi"))));
        yield return ("pi/(x-1)+e", Add(Divide(Symbol("pi"), Subtract(x, Number(1))), Symbol("e")));
        yield return ("e+cos(x)", Add(Symbol("e"), Function("cos", x)));
        yield return ("sin(x)+pi", Add(Function("sin", x), Symbol("pi")));
        yield return ("cos(e*x)", Function("cos", Multiply(Symbol("e"), x)));
        yield return ("sin(pi*x)", Function("sin", Multiply(Symbol("pi"), x)));
        yield return ("cos(x-pi/2)", Function("cos", Subtract(x, Divide(Symbol("pi"), Number(2)))));
        yield return ("sin(x+pi/2)", Function("sin", Add(x, Divide(Symbol("pi"), Number(2)))));
        yield return ("sin(x+pi)", Function("sin", Add(x, Symbol("pi"))));
        yield return ("tan(x+pi/2)", Function("tan", Add(x, Divide(Symbol("pi"), Number(2)))));
    }

    private static void AssertAllProved(AnalysisReport report, string name)
    {
        AssertProved(report.Domain, name);
        AssertProved(report.Range, name);
        AssertProved(report.Parity, name);
        AssertProved(report.Zeros, name);
        AssertProved(report.YIntercept, name);
        AssertProved(report.Minima, name);
        AssertProved(report.Maxima, name);
        AssertProved(report.InflectionPoints, name);
        AssertProved(report.VerticalAsymptotes, name);
        AssertProved(report.HorizontalAsymptotes, name);
        AssertProved(report.ObliqueAsymptotes, name);
        AssertProved(report.Monotonicity, name);
        AssertProved(report.Period, name);
    }

    private static void AssertAllReplay(
        AnalysisRequest request,
        AnalysisReport report,
        string name)
    {
        AssertReplay(request, report, report.Domain, name);
        AssertReplay(request, report, report.Range, name);
        AssertReplay(request, report, report.Parity, name);
        AssertReplay(request, report, report.Zeros, name);
        AssertReplay(request, report, report.YIntercept, name);
        AssertReplay(request, report, report.Minima, name);
        AssertReplay(request, report, report.Maxima, name);
        AssertReplay(request, report, report.InflectionPoints, name);
        AssertReplay(request, report, report.VerticalAsymptotes, name);
        AssertReplay(request, report, report.HorizontalAsymptotes, name);
        AssertReplay(request, report, report.ObliqueAsymptotes, name);
        AssertReplay(request, report, report.Monotonicity, name);
        AssertReplay(request, report, report.Period, name);
    }

    private static IEnumerable<ProofCertificate> Certificates(AnalysisReport report)
    {
        ProofCertificate?[] certificates =
        [
            report.Domain.Certificate,
            report.Range.Certificate,
            report.Parity.Certificate,
            report.Zeros.Certificate,
            report.YIntercept.Certificate,
            report.Minima.Certificate,
            report.Maxima.Certificate,
            report.InflectionPoints.Certificate,
            report.VerticalAsymptotes.Certificate,
            report.HorizontalAsymptotes.Certificate,
            report.ObliqueAsymptotes.Certificate,
            report.Monotonicity.Certificate,
            report.Period.Certificate
        ];
        return certificates.OfType<ProofCertificate>();
    }

    private static void AssertReplay<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome,
        string name)
    {
        Assert.True(
            CertificateChecker.Check(request, report.Expression!, outcome),
            $"Certificate replay failed for {name}/{typeof(T).Name}.");
    }

    private static void AssertProved<T>(ProofOutcome<T> outcome, string name) =>
        Assert.True(
            outcome.State == ProofState.Proved && outcome.Certificate is not null,
            $"{name} was {outcome.State}/{outcome.UnknownReason} for {typeof(T).Name}.");

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        return outcome.Value!;
    }

    private static string Coordinate(Asymptote asymptote) =>
        ExactRealCanonical.Format(Assert.IsType<SingletonReal>(asymptote.Coordinate).Value);

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features) =>
        new(expression, features, AngleUnit.Radians, "x", static () => true);

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Symbol(string name) => InputExpression.Variable(name, Source);

    private static InputExpression Number(int value) => Number(new BigRational(value));

    private static InputExpression Number(BigRational value) => InputExpression.Number(value, Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Power(InputExpression basis, int exponent) =>
        InputExpression.Binary(InputExpressionKind.Power, basis, Number(exponent), Source);

    private static InputExpression Function(string name, params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
