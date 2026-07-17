using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class WindowsFunctionAnalysisCompatibilityTests
{
    private static readonly SourceRange Source = new(0, 1);
    private static readonly string[] ShiftedDoubleSineMinima =
        ["(πn₁ + 3π/4, 0), n₁ ∈ ℤ", "(πn₁ + 5π/4, 0), n₁ ∈ ℤ"];

    [Fact]
    public void MixedNestedExpressionUsesGeneralExactOriginSubstitution()
    {
        const string formula = "sin(x+sqrt(x))-cos(x)+tan(x)";
        GraphFunctionAnalysisData result = AnalyzePublic(
            formula,
            PerformAnalysisType.InterceptionPointsWithXAndYAxis);

        Assert.Equal("y = −1", result.YIntercept);
        Assert.Equal(Bits(AnalysisType.Zeros), result.TooComplexFeatures);

        InputExpression variable = Variable();
        InputExpression input = Add(
            Subtract(
                Sin(Add(variable, Sqrt(variable))),
                Cos(variable)),
            Tan(variable));
        AnalysisRequest request = Request(input);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.Equal(ProofState.Proved, report.YIntercept.State);
        OptionalValue<ExactReal> intercept = report.YIntercept.Value!;
        Assert.True(intercept.HasValue);
        Assert.Equal("q:-1", ExactRealCanonical.Format(intercept.Value!));
        var certificate = Assert.IsType<ExactOriginProofCertificate>(
            report.YIntercept.Certificate);
        Assert.True(CertificateChecker.Check(
            request,
            report.Expression!,
            report.YIntercept));

        var tampered = ProofOutcome<OptionalValue<ExactReal>>.Proved(
            OptionalValue<ExactReal>.Some(new RationalReal(BigRational.Zero)),
            certificate with
            {
                Claim = ClaimCanonical.ForObject(
                OptionalValue<ExactReal>.Some(new RationalReal(BigRational.Zero)))
            });
        Assert.False(CertificateChecker.Check(
            request,
            report.Expression!,
            tampered));
    }

    [Fact]
    public void AbsoluteSineMatchesEveryStableWindowsRowAndFooterOmission()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("abs(sin(x))");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = πn₁, n₁ ∈ ℤ", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Even, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Equal("π", result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void SineAbsoluteMatchesEveryStableWindowsRowAndFooterOmission()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sin(abs(x))");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x ∈ {πn₁, −πn₁}, n₁ ∈ ℤ, n₁ ≥ 0", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Even, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Period,
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.InflectionPoints,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void AbsoluteHyperbolicSineMatchesEveryStableWindowsRowAndFooterOmission()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("abs(sinh(x))");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = 0", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Equal("(0, 0)", Assert.Single(result.Minima));
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Unknown, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Parity,
                AnalysisType.HorizontalAsymptotes,
                AnalysisType.ObliqueAsymptotes,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void HyperbolicCosineAbsoluteMatchesEveryStableWindowsRowAndFooterOmission()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("cosh(abs(x))");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal("y = 1", result.YIntercept);
        Assert.Equal("(0, 1)", Assert.Single(result.Minima));
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Even, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.HorizontalAsymptotes,
                AnalysisType.ObliqueAsymptotes,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void AbsoluteCompatibilityNeverDowngradesInternalProofOutcomes()
    {
        AnalysisRequest absoluteSineRequest = Request(Abs(Sin(Variable())));
        AnalysisReport absoluteSine = AnalysisEngine.Analyze(absoluteSineRequest);
        AssertAbsoluteProof(absoluteSineRequest, absoluteSine, absoluteSine.Range);
        AssertAbsoluteProof(absoluteSineRequest, absoluteSine, absoluteSine.Minima);
        AssertAbsoluteProof(absoluteSineRequest, absoluteSine, absoluteSine.Maxima);
        AssertAbsoluteProof(absoluteSineRequest, absoluteSine, absoluteSine.Monotonicity);

        AnalysisRequest sineAbsoluteRequest = Request(Sin(Abs(Variable())));
        AnalysisReport sineAbsolute = AnalysisEngine.Analyze(sineAbsoluteRequest);
        AssertAbsoluteProof(sineAbsoluteRequest, sineAbsolute, sineAbsolute.Range);
        AssertAbsoluteProof(sineAbsoluteRequest, sineAbsolute, sineAbsolute.Period);
        AssertAbsoluteProof(sineAbsoluteRequest, sineAbsolute, sineAbsolute.Minima);
        AssertAbsoluteProof(sineAbsoluteRequest, sineAbsolute, sineAbsolute.Maxima);
        AssertAbsoluteProof(sineAbsoluteRequest, sineAbsolute, sineAbsolute.InflectionPoints);
        AssertAbsoluteProof(sineAbsoluteRequest, sineAbsolute, sineAbsolute.Monotonicity);

        AnalysisRequest absoluteSinhRequest = Request(Abs(Sinh(Variable())));
        AnalysisReport absoluteSinh = AnalysisEngine.Analyze(absoluteSinhRequest);
        AssertAbsoluteProof(absoluteSinhRequest, absoluteSinh, absoluteSinh.Range);
        AssertAbsoluteProof(absoluteSinhRequest, absoluteSinh, absoluteSinh.Parity);
        AssertAbsoluteProof(absoluteSinhRequest, absoluteSinh, absoluteSinh.HorizontalAsymptotes);
        AssertAbsoluteProof(absoluteSinhRequest, absoluteSinh, absoluteSinh.ObliqueAsymptotes);
        AssertAbsoluteProof(absoluteSinhRequest, absoluteSinh, absoluteSinh.Monotonicity);

        AnalysisRequest coshAbsoluteRequest = Request(Cosh(Abs(Variable())));
        AnalysisReport coshAbsolute = AnalysisEngine.Analyze(coshAbsoluteRequest);
        AssertAbsoluteProof(coshAbsoluteRequest, coshAbsolute, coshAbsolute.Range);
        AssertAbsoluteProof(coshAbsoluteRequest, coshAbsolute, coshAbsolute.HorizontalAsymptotes);
        AssertAbsoluteProof(coshAbsoluteRequest, coshAbsolute, coshAbsolute.ObliqueAsymptotes);
        AssertAbsoluteProof(coshAbsoluteRequest, coshAbsolute, coshAbsolute.Monotonicity);
    }

    [Fact]
    public void CotangentMatchesStableWindowsOmissionsWithoutDiscardingProofs()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("cot(x)");
        Assert.Equal("x ≠ πn₁, ∀ n₁ ∈ ℤ", result.Domain);
        Assert.Equal("y ∈ ℝ", result.Range);
        Assert.Equal("x = πn₁ + π/2, n₁ ∈ ℤ", result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Equal("π", result.PeriodicityExpression);
        Assert.Equal(
            (int)FunctionMonotonicityType.Descending,
            Assert.Single(result.MonotoneIntervals).Value);
        Assert.Equal(
            Bits(AnalysisType.InflectionPoints, AnalysisType.VerticalAsymptotes),
            result.TooComplexFeatures);

        AssertReciprocalProofs(
            Function("cot", Variable()),
            static report => report.InflectionPoints,
            static report => report.VerticalAsymptotes);
    }

    [Fact]
    public void SecantMatchesStableWindowsVerticalOmissionWithoutDiscardingProof()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sec(x)");
        Assert.Equal("x ≠ πn₁ + π/2, ∀ n₁ ∈ ℤ", result.Domain);
        Assert.Equal("y ∈ (−∞, −1] ∪ [1, ∞)", result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal("y = 1", result.YIntercept);
        Assert.Equal("(2πn₁, 1), n₁ ∈ ℤ", Assert.Single(result.Minima));
        Assert.Equal("(2πn₁ + π, −1), n₁ ∈ ℤ", Assert.Single(result.Maxima));
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Even, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Equal("2π", result.PeriodicityExpression);
        Assert.Equal(4, result.MonotoneIntervals.Count);
        Assert.Equal(Bits(AnalysisType.VerticalAsymptotes), result.TooComplexFeatures);

        AssertReciprocalProofs(
            Function("sec", Variable()),
            static report => report.VerticalAsymptotes);
    }

    [Fact]
    public void CosecantMatchesStableWindowsVerticalOmissionWithoutDiscardingProof()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("csc(x)");
        Assert.Equal("x ≠ πn₁, ∀ n₁ ∈ ℤ", result.Domain);
        Assert.Equal("y ∈ (−∞, −1] ∪ [1, ∞)", result.Range);
        Assert.Empty(result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Equal("(2πn₁ + π/2, 1), n₁ ∈ ℤ", Assert.Single(result.Minima));
        Assert.Equal("(2πn₁ + 3π/2, −1), n₁ ∈ ℤ", Assert.Single(result.Maxima));
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Equal("2π", result.PeriodicityExpression);
        Assert.Equal(4, result.MonotoneIntervals.Count);
        Assert.Equal(Bits(AnalysisType.VerticalAsymptotes), result.TooComplexFeatures);

        AssertReciprocalProofs(
            Function("csc", Variable()),
            static report => report.VerticalAsymptotes);
    }

    [Theory]
    [InlineData("0*tan(x)")]
    [InlineData("tan(x)-tan(x)")]
    [InlineData("(pi-pi)*tan(x)")]
    public void ZeroOnTangentMatchesStableWindowsRowsWithoutDiscardingProofs(
        string formula)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal("x ≠ πn₁ + π/2, ∀ n₁ ∈ ℤ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x ≠ πn₁ + π/2, ∀ n₁ ∈ ℤ", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Unknown, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Equal("π", result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(AnalysisType.Range, AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void SineSelfDivisionMatchesStableWindowsPuncturedConstantPresentation()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sin(x)/sin(x)");

        Assert.Equal("x ≠ πn₁, ∀ n₁ ∈ ℤ", result.Domain);
        Assert.Equal("y ∈ {1}", result.Range);
        Assert.Empty(result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Equal("y = 1", Assert.Single(result.HorizontalAsymptotes));
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Even, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(πn₁, ∞)"] = (int)FunctionMonotonicityType.Constant,
                ["(−∞, πn₁)"] = (int)FunctionMonotonicityType.Constant
            },
            result.MonotoneIntervals);
        Assert.Equal(Bits(AnalysisType.Period), result.TooComplexFeatures);
    }

    [Fact]
    public void CosineSelfDivisionMatchesReplayedWindowsPuncturedConstantPresentation()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("cos(x)/cos(x)");

        Assert.Equal("x ≠ πn₁ + π/2, ∀ n₁ ∈ ℤ", result.Domain);
        Assert.Equal("y ∈ {1}", result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal("y = 1", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Equal("y = 1", Assert.Single(result.HorizontalAsymptotes));
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Even, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(πn₁ + π/2, ∞)"] = (int)FunctionMonotonicityType.Constant,
                ["(−∞, πn₁ + π/2)"] = (int)FunctionMonotonicityType.Constant
            },
            result.MonotoneIntervals);
        Assert.Equal(Bits(AnalysisType.Period), result.TooComplexFeatures);
    }

    [Fact]
    public void GuardedConstantCompatibilityNeverDowngradesInternalProofOutcomes()
    {
        InputExpression tangent = Function("tan", Variable());
        AssertGuardedConstantProofs(
            InputExpression.Binary(
                InputExpressionKind.Multiply,
                InputExpression.Number(BigRational.Zero, Source),
                tangent,
                Source));

        InputExpression sine = Function("sin", Variable());
        AssertGuardedConstantProofs(InputExpression.Binary(
            InputExpressionKind.Divide,
            sine,
            sine,
            Source));
    }

    [Fact]
    public void ZeroSecantUsesWindowsPrimitivePeriodWithoutChangingCheckedPeriod()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("0*sec(x)");

        Assert.Equal("x ≠ πn₁ + π/2, ∀ n₁ ∈ ℤ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x ≠ πn₁ + π/2, ∀ n₁ ∈ ℤ", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Unknown, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Equal("2π", result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(AnalysisType.Range, AnalysisType.Monotonicity),
            result.TooComplexFeatures);

        AnalysisRequest request = Request(Multiply(
            Number(0),
            Function("sec", Variable())));
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.Equal(ProofState.Proved, report.Period.State);
        Assert.IsType<GuardedConstantProofCertificate>(report.Period.Certificate);
        Assert.Equal(
            "pi:1:0",
            ExactRealCanonical.Format(report.Period.Value!.FundamentalPeriod!));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Period));
    }

    [Theory]
    [InlineData("0*cos(x)", "")]
    [InlineData("0*tan(x)", "π")]
    [InlineData("0*cot(x)", "π")]
    [InlineData("0*csc(x)", "π")]
    public void SecantPeriodProjectionDoesNotLeakToOtherZeroProducts(
        string formula,
        string expectedPeriod)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal(expectedPeriod, result.PeriodicityExpression);
    }

    [Fact]
    public void AffineSignAliasMatchesStableWindowsRowsAndFooter()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sgn(x)");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = 0", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Equal(["y = 1", "y = −1"], result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(AnalysisType.Range, AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void ShiftedAffineSignPublishesAllRowsExceptWindowsOmissions()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sgn(2*x-4)");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = 2", result.Zeros);
        Assert.Equal("y = −1", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Equal(["y = 1", "y = −1"], result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(AnalysisType.Range, AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void AffineSignCompatibilityNeverDowngradesInternalProofOutcomes()
    {
        AnalysisRequest request = Request(Function(
            "sign",
            Add(Multiply(Number(2), Variable()), Number(-4))));
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertAffineSign(report.Range);
        AssertAffineSign(report.Monotonicity);
        AssertAffineSign(report.Zeros);
        AssertAffineSign(report.HorizontalAsymptotes);
        return;

        void AssertAffineSign<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<AffineSignProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    [Fact]
    public void ZeroToVariableMatchesEveryStableWindowsRowWithoutFalseFooterFlags()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("0^x");

        Assert.Equal("x > 0", result.Domain);
        Assert.Equal("y ∈ {0}", result.Range);
        Assert.Equal("x > 0", result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Equal("y = 0", Assert.Single(result.HorizontalAsymptotes));
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Unknown, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Equal(
            (int)FunctionMonotonicityType.Constant,
            Assert.Single(result.MonotoneIntervals).Value);
        Assert.Equal(0, result.TooComplexFeatures);

        AnalysisRequest request = Request(Power(Number(0), Variable()));
        AnalysisReport report = AnalysisEngine.Analyze(request);
        AssertZeroBase(report.Parity);
        AssertZeroBase(report.Range);
        AssertZeroBase(report.Zeros);
        AssertZeroBase(report.Monotonicity);
        AssertZeroBase(report.Period);
        return;

        void AssertZeroBase<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<ZeroBaseAffinePowerProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    [Fact]
    public void ZeroToSymbolicAffineExponentMatchesEveryStableWindowsRow()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("0^(pi*x-1)");

        Assert.Equal("x > 1/π", result.Domain);
        Assert.Equal("y ∈ {0}", result.Range);
        Assert.Equal("x > 1/π", result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Equal("y = 0", Assert.Single(result.HorizontalAsymptotes));
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Unknown, result.Parity);
        Assert.Equal(
            (int)FunctionPeriodicityType.NotPeriodic,
            result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        KeyValuePair<string, int> monotonicity = Assert.Single(
            result.MonotoneIntervals);
        Assert.Equal("(1/π, ∞)", monotonicity.Key);
        Assert.Equal((int)FunctionMonotonicityType.Constant, monotonicity.Value);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Theory]
    [InlineData("floor(x)", false, "y = 0")]
    [InlineData("floor(2*x+1)", true, "y = 1")]
    public void AffineFloorMatchesEveryStableWindowsRowAndFooterOmission(
        string formula,
        bool shifted,
        string expectedYIntercept)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal(expectedYIntercept, result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Zeros,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);

        InputExpression argument = shifted
            ? Add(Multiply(Number(2), Variable()), Number(1))
            : Variable();
        AnalysisRequest request = Request(Function("floor", argument));
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Assert.IsType<IntegerLatticeSet>(Proved(report.Range));
        Assert.Equal(
            shifted ? "interval[q:-1/2,1,q:0,0]" : "interval[q:0,1,q:1,0]",
            Proved(report.Zeros).Canonical);
        Assert.IsType<PeriodicIntervalSet>(
            Assert.Single(Proved(report.Monotonicity)).Region);
        Assert.Equal(
            PeriodicityKind.NotPeriodic,
            Proved(report.Period).Kind);

        AssertAffineFloor(report.Range);
        AssertAffineFloor(report.Zeros);
        AssertAffineFloor(report.Monotonicity);
        AssertAffineFloor(report.Period);
        return;

        void AssertAffineFloor<T>(ProofOutcome<T> outcome)
        {
            Assert.IsType<AffineFloorProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }

        static T Proved<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            return outcome.Value!;
        }
    }

    [Fact]
    public void ZeroToTangentMatchesEveryStableWindowsRowAndFooterOmission()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("0^tan(x)");
        const string positiveTangentCells =
            "x ∈ (πn₁, πn₁ + π/2), n₁ ∈ ℤ";

        Assert.Equal(positiveTangentCells, result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal(positiveTangentCells, result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Unknown, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
        Assert.Equal("π", result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.VerticalAsymptotes,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);

        AnalysisRequest request = Request(Power(
            Number(0),
            Function("tan", Variable())));
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Asymptote horizontal = Assert.Single(Proved(report.HorizontalAsymptotes));
        Assert.Equal(AsymptoteOrientation.Horizontal, horizontal.Orientation);
        Assert.Equal(
            "q:0",
            ExactRealCanonical.Format(
                Assert.IsType<SingletonReal>(horizontal.Coordinate).Value));
        Assert.Equal(FunctionParity.Neither, Proved(report.Parity));
        MonotoneRegion monotone = Assert.Single(Proved(report.Monotonicity));
        Assert.Equal(Monotonicity.Constant, monotone.Direction);

        AssertTangentPower(report.Domain);
        AssertTangentPower(report.HorizontalAsymptotes);
        AssertTangentPower(report.Parity);
        AssertTangentPower(report.Monotonicity);
        return;

        void AssertTangentPower<T>(ProofOutcome<T> outcome)
        {
            Assert.IsType<ZeroBaseTangentPowerProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }

        static T Proved<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            return outcome.Value!;
        }
    }

    [Fact]
    public void GuardedCotangentIdentityMatchesEveryStableWindowsRowAndFooterOmission()
    {
        const string formula = "(sin(x)^2+cos(x)^2)/tan(x)";
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal("x ≠ π/2n₁, ∀ n₁ ∈ ℤ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Empty(result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Period,
                AnalysisType.ObliqueAsymptotes,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);

        InputExpression x = Variable();
        InputExpression numerator = Add(
            Power(Function("sin", x), Number(2)),
            Power(Function("cos", x), Number(2)));
        AnalysisRequest request = Request(Divide(
            numerator,
            Function("tan", x)));
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Assert.IsType<UnionSet>(Proved(report.Range));
        Assert.Equal(FunctionParity.Odd, Proved(report.Parity));
        Asymptote vertical = Assert.Single(Proved(report.VerticalAsymptotes));
        Assert.Equal(AsymptoteOrientation.Vertical, vertical.Orientation);
        Assert.IsType<PeriodicReal>(vertical.Coordinate);
        Assert.Equal(
            PeriodicityKind.PeriodicWithFundamentalPeriod,
            Proved(report.Period).Kind);
        Assert.Single(Proved(report.Monotonicity));

        AssertGuardedCotangent(report.Domain);
        AssertGuardedCotangent(report.Range);
        AssertGuardedCotangent(report.VerticalAsymptotes);
        AssertGuardedCotangent(report.Period);
        AssertGuardedCotangent(report.Monotonicity);
        return;

        void AssertGuardedCotangent<T>(ProofOutcome<T> outcome)
        {
            Assert.IsType<GuardedCotangentIdentityProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }

        static T Proved<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            return outcome.Value!;
        }
    }

    [Fact]
    public void SingleHoleSineMatchesWindowsRowsExceptItsUnsoundPeriodClaim()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sin(x)*(x/x)");

        Assert.Equal("x ≠ 0", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = πn₁, n₁ ∈ ℤ, n₁ ≠ 0", result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Equal("(2πn₁ + 3π/2, −1), n₁ ∈ ℤ", Assert.Single(result.Minima));
        Assert.Equal("(2πn₁ + π/2, 1), n₁ ∈ ℤ", Assert.Single(result.Maxima));
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);

        // Windows displays 2π by looking only at the simplified sine value.
        // That translation does not preserve R\{0}; publish the checked
        // mathematical function instead of reproducing a false period.
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.InflectionPoints,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);

        InputExpression x = Variable();
        AnalysisRequest request = Request(Multiply(
            Function("sin", x),
            Divide(x, x)));
        AnalysisReport report = AnalysisEngine.Analyze(request);
        SemanticExpression semantic = Assert.IsType<SemanticExpression>(report.Expression);

        Assert.IsType<IntervalSet>(Proved(report.Range));
        Assert.IsType<PeriodicPointSet>(Proved(report.Zeros));
        Assert.Single(Proved(report.InflectionPoints));
        Assert.Equal(4, Proved(report.Monotonicity).Length);
        Periodicity period = Proved(report.Period);
        Assert.Equal(PeriodicityKind.NotPeriodic, period.Kind);
        Assert.Null(period.FundamentalPeriod);

        AssertSingleHoleSine(report.Range);
        AssertSingleHoleSine(report.Zeros);
        AssertSingleHoleSine(report.InflectionPoints);
        AssertSingleHoleSine(report.Monotonicity);
        AssertSingleHoleSine(report.Period);
        return;

        void AssertSingleHoleSine<T>(ProofOutcome<T> outcome)
        {
            Assert.IsType<SingleHoleSineProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, semantic, outcome));
        }

        static T Proved<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            return outcome.Value!;
        }
    }

    [Theory]
    [InlineData("sinh(x)", false, false)]
    [InlineData("sinh(-x)", false, true)]
    [InlineData("cosh(x)", true, false)]
    [InlineData("cosh(-x)", true, true)]
    public void CenteredHyperbolicPrimitivesMatchStableWindowsOmissions(
        string formula,
        bool cosine,
        bool reflected)
    {
        ArgumentNullException.ThrowIfNull(formula);
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal(cosine ? string.Empty : "x = 0", result.Zeros);
        Assert.Equal(cosine ? "y = 1" : "y = 0", result.YIntercept);
        Assert.Equal(cosine ? ["(0, 1)"] : [], result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Equal(cosine ? [] : ["(0, 0)"], result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Unknown, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Parity,
                AnalysisType.HorizontalAsymptotes,
                AnalysisType.ObliqueAsymptotes,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);

        // Reflection changes the proved direction internally but not the
        // installed Calculator's public omission contract.
        Assert.Equal(reflected, formula.Contains("-x", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("2*sinh(2*x)+1")]
    [InlineData("2*cosh(x-1)-3")]
    public void ShiftedHyperbolicPrimitivesKeepWindowsNeitherParity(
        string formula)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Empty(result.Range);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.HorizontalAsymptotes,
                AnalysisType.ObliqueAsymptotes,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void HyperbolicTangentMatchesStableWindowsRowsAndFooter()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("tanh(x)");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal("y = tanh(0)", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Unknown, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Zeros,
                AnalysisType.Parity,
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.InflectionPoints,
                AnalysisType.HorizontalAsymptotes,
                AnalysisType.ObliqueAsymptotes,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Theory]
    [InlineData("sinh")]
    [InlineData("cosh")]
    [InlineData("tanh")]
    public void HyperbolicCompatibilityNeverDowngradesInternalProofOutcomes(
        string function)
    {
        AnalysisRequest request = Request(Function(function, Variable()));
        AnalysisReport report = AnalysisEngine.Analyze(request);

        AssertElementary(report.Range);
        AssertElementary(report.Parity);
        AssertElementary(report.HorizontalAsymptotes);
        AssertElementary(report.ObliqueAsymptotes);
        AssertElementary(report.Monotonicity);
        return;

        void AssertElementary<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);
            Assert.Equal(TheoremRule.ElementaryPrimitive, certificate.Theorem);
            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    [Theory]
    [InlineData("sqrt(sin(x)^2)", 2 | 64 | 128 | 4096)]
    [InlineData("sin(sqrt(x^2))", 2 | 8 | 64 | 128 | 256 | 4096)]
    [InlineData("cos(x)/sin(x)", 256 | 512)]
    [InlineData("x+exp(x)", 16)]
    [InlineData("sin(sqrt(x))", 2 | 8 | 64 | 128 | 256 | 4096)]
    [InlineData("sin(exp(x))", 2 | 8 | 256 | 4096)]
    [InlineData("sin(tan(x))", 2 | 8 | 16 | 64 | 128 | 256 | 512 | 4096)]
    public void CompatibilityClassificationUsesCheckedTheoremPatternsNotSourceSpelling(
        string formula,
        int expectedTooComplex)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal(expectedTooComplex, result.TooComplexFeatures);
    }

    [Theory]
    [InlineData("ln(abs(x))", 0)]
    [InlineData("sin(asin(x))", 8)]
    [InlineData("cos(acos(x))", 8)]
    [InlineData("tan(atan(x))", 8)]
    [InlineData("asin(sin(x))", 5578)]
    [InlineData("acos(cos(x))", 4546)]
    [InlineData("atan(tan(x))", 512)]
    [InlineData("sqrt(sin(x))", 4866)]
    [InlineData("ln(sin(x))", 4610)]
    [InlineData("exp(sin(x))", 0)]
    [InlineData("sin(sin(x))", 4570)]
    [InlineData("cos(sin(x))", 4554)]
    [InlineData("tan(sin(x))", 272)]
    public void UnaryCompositionFooterMatchesStableWindowsExceptUnprovedCurvature(
        string formula,
        int expectedTooComplex)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal(expectedTooComplex, result.TooComplexFeatures);
    }

    [Theory]
    [InlineData("sin(asin(x))")]
    [InlineData("cos(acos(x))")]
    public void BoundedGuardedInverseUsesWindowsOpenRangeAndEndpointOmissions(string formula)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal("−1 ≤ x ≤ 1", result.Domain);
        Assert.Equal("y ∈ (−1, 1)", result.Range);
        Assert.Equal("x = 0", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["(−1, 1)"] = (int)FunctionMonotonicityType.Ascending
            },
            result.MonotoneIntervals);
        Assert.Equal(Bits(AnalysisType.Period), result.TooComplexFeatures);
    }

    [Fact]
    public void TangentArctangentUsesIdentityRowsAndWindowsPeriodFooter()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("tan(atan(x))");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Equal("y ∈ ℝ", result.Range);
        Assert.Equal("x = 0", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Equal("y = x", Assert.Single(result.ObliqueAsymptotes));
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Equal(
            (int)FunctionMonotonicityType.Ascending,
            Assert.Single(result.MonotoneIntervals).Value);
        Assert.Equal(Bits(AnalysisType.Period), result.TooComplexFeatures);
    }

    [Fact]
    public void PrincipalInverseBranchesMatchStableWindowsRowsAndOmissions()
    {
        GraphFunctionAnalysisData arcsine = AnalyzePublic("asin(sin(x))");
        Assert.Equal("x ∈ ℝ", arcsine.Domain);
        Assert.Empty(arcsine.Range);
        Assert.Equal("x = πn₁, n₁ ∈ ℤ", arcsine.Zeros);
        Assert.Equal("y = 0", arcsine.YIntercept);
        Assert.Empty(arcsine.Minima);
        Assert.Empty(arcsine.Maxima);
        Assert.Empty(arcsine.InflectionPoints);
        AssertAllAsymptotesEmpty(arcsine);
        Assert.Equal((int)FunctionParityType.Odd, arcsine.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, arcsine.PeriodicityDirection);
        Assert.Empty(arcsine.MonotoneIntervals);
        Assert.Equal(5578, arcsine.TooComplexFeatures);

        GraphFunctionAnalysisData arccosine = AnalyzePublic("acos(cos(x))");
        Assert.Equal("x ∈ ℝ", arccosine.Domain);
        Assert.Empty(arccosine.Range);
        Assert.Equal("x = 2πn₁, n₁ ∈ ℤ", arccosine.Zeros);
        Assert.Equal("y = 0", arccosine.YIntercept);
        Assert.Empty(arccosine.Minima);
        Assert.Empty(arccosine.Maxima);
        Assert.Empty(arccosine.InflectionPoints);
        AssertAllAsymptotesEmpty(arccosine);
        Assert.Equal((int)FunctionParityType.Even, arccosine.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, arccosine.PeriodicityDirection);
        Assert.Equal("2π", arccosine.PeriodicityExpression);
        Assert.Empty(arccosine.MonotoneIntervals);
        Assert.Equal(4546, arccosine.TooComplexFeatures);

        GraphFunctionAnalysisData arctangent = AnalyzePublic("atan(tan(x))");
        Assert.Equal("x ≠ πn₁ + π/2, ∀ n₁ ∈ ℤ", arctangent.Domain);
        Assert.Equal("y ∈ (−π/2, π/2)", arctangent.Range);
        Assert.Equal("x = πn₁, n₁ ∈ ℤ", arctangent.Zeros);
        Assert.Equal("y = 0", arctangent.YIntercept);
        Assert.Empty(arctangent.Minima);
        Assert.Empty(arctangent.Maxima);
        Assert.Empty(arctangent.InflectionPoints);
        AssertAllAsymptotesEmpty(arctangent);
        Assert.Equal((int)FunctionParityType.Odd, arctangent.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, arctangent.PeriodicityDirection);
        Assert.Equal("π", arctangent.PeriodicityExpression);
        Assert.Equal(
            "(πn₁ + π/2, πn₁ + 3π/2), n₁ ∈ ℤ",
            Assert.Single(arctangent.MonotoneIntervals).Key);
        Assert.Equal(Bits(AnalysisType.VerticalAsymptotes), arctangent.TooComplexFeatures);
    }

    [Fact]
    public void PartialSineCompositionsMatchStableWindowsDomainEndpointAndFooterRows()
    {
        GraphFunctionAnalysisData squareRoot = AnalyzePublic("sqrt(sin(x))");
        Assert.Equal("x ∈ [2πn₁, 2πn₁ + π], n₁ ∈ ℤ", squareRoot.Domain);
        Assert.Empty(squareRoot.Range);
        Assert.Equal("x = πn₁, n₁ ∈ ℤ", squareRoot.Zeros);
        Assert.Equal("y = 0", squareRoot.YIntercept);
        Assert.Equal(2, squareRoot.Minima.Count);
        Assert.Equal("(2πn₁ + π/2, 1), n₁ ∈ ℤ", Assert.Single(squareRoot.Maxima));
        Assert.Empty(squareRoot.InflectionPoints);
        AssertAllAsymptotesEmpty(squareRoot);
        Assert.Equal((int)FunctionParityType.None, squareRoot.Parity);
        Assert.Equal("2π", squareRoot.PeriodicityExpression);
        Assert.Empty(squareRoot.MonotoneIntervals);
        Assert.Equal(4866, squareRoot.TooComplexFeatures);

        GraphFunctionAnalysisData logarithm = AnalyzePublic("ln(sin(x))");
        Assert.Equal("x ∈ (2πn₁, 2πn₁ + π), n₁ ∈ ℤ", logarithm.Domain);
        Assert.Empty(logarithm.Range);
        Assert.Equal("x = 2πn₁ + π/2, n₁ ∈ ℤ", logarithm.Zeros);
        Assert.Empty(logarithm.YIntercept);
        Assert.Empty(logarithm.Minima);
        Assert.Equal("(2πn₁ + π/2, 0), n₁ ∈ ℤ", Assert.Single(logarithm.Maxima));
        Assert.Empty(logarithm.InflectionPoints);
        AssertAllAsymptotesEmpty(logarithm);
        Assert.Equal((int)FunctionParityType.None, logarithm.Parity);
        Assert.Equal("2π", logarithm.PeriodicityExpression);
        Assert.Empty(logarithm.MonotoneIntervals);
        Assert.Equal(4610, logarithm.TooComplexFeatures);

        GraphFunctionAnalysisData commonLogarithm = AnalyzePublic(
            "log(sin(2*x+pi/2))");
        Assert.Equal(
            "x ∈ (πn₁ + 3π/4, πn₁ + 5π/4), n₁ ∈ ℤ",
            commonLogarithm.Domain);
        Assert.Empty(commonLogarithm.Range);
        Assert.Equal("x = πn₁, n₁ ∈ ℤ", commonLogarithm.Zeros);
        Assert.Equal("y = 0", commonLogarithm.YIntercept);
        Assert.Empty(commonLogarithm.Minima);
        Assert.Empty(commonLogarithm.Maxima);
        Assert.Empty(commonLogarithm.InflectionPoints);
        AssertAllAsymptotesEmpty(commonLogarithm);
        Assert.Equal((int)FunctionParityType.Even, commonLogarithm.Parity);
        Assert.Equal(
            (int)FunctionPeriodicityType.Periodic,
            commonLogarithm.PeriodicityDirection);
        Assert.Equal("π", commonLogarithm.PeriodicityExpression);
        Assert.Empty(commonLogarithm.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.VerticalAsymptotes,
                AnalysisType.Monotonicity),
            commonLogarithm.TooComplexFeatures);

        InputExpression variable = Variable();
        InputExpression pi = InputExpression.Variable("pi", Source);
        InputExpression commonLogExpression = Function(
            "log",
            Function(
                "sin",
                Add(
                    Multiply(Number(2), variable),
                    Divide(pi, Number(2)))));
        AnalysisRequest request = Request(commonLogExpression);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        AssertAffinePhaseProof(report.Range);
        AssertAffinePhaseProof(report.Minima);
        AssertAffinePhaseProof(report.Maxima);
        AssertAffinePhaseProof(report.VerticalAsymptotes);
        AssertAffinePhaseProof(report.Monotonicity);
        return;

        void AssertAffinePhaseProof<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<AffinePhaseSineCompositionProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    [Fact]
    public void ShiftedDoubleSineCompositionsMatchEveryStableWindowsRowAndFooter()
    {
        GraphFunctionAnalysisData squareRoot = AnalyzePublic(
            "sqrt(sin(2*x+pi/2))");
        Assert.Equal(
            "x ∈ [πn₁ + 3π/4, πn₁ + 5π/4], n₁ ∈ ℤ",
            squareRoot.Domain);
        Assert.Empty(squareRoot.Range);
        Assert.Equal("x = π/2n₁ + π/4, n₁ ∈ ℤ", squareRoot.Zeros);
        Assert.Equal("y = 1", squareRoot.YIntercept);
        Assert.Equal(
            ShiftedDoubleSineMinima,
            squareRoot.Minima);
        Assert.Equal("(πn₁, 1), n₁ ∈ ℤ", Assert.Single(squareRoot.Maxima));
        Assert.Empty(squareRoot.InflectionPoints);
        AssertAllAsymptotesEmpty(squareRoot);
        Assert.Equal((int)FunctionParityType.Even, squareRoot.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, squareRoot.PeriodicityDirection);
        Assert.Equal("π", squareRoot.PeriodicityExpression);
        Assert.Empty(squareRoot.MonotoneIntervals);
        Assert.Equal(4866, squareRoot.TooComplexFeatures);

        GraphFunctionAnalysisData logarithm = AnalyzePublic(
            "ln(sin(2*x+pi/2))");
        Assert.Equal(
            "x ∈ (πn₁ + 3π/4, πn₁ + 5π/4), n₁ ∈ ℤ",
            logarithm.Domain);
        Assert.Empty(logarithm.Range);
        Assert.Equal("x = πn₁, n₁ ∈ ℤ", logarithm.Zeros);
        Assert.Equal("y = 0", logarithm.YIntercept);
        Assert.Empty(logarithm.Minima);
        Assert.Equal("(πn₁, 0), n₁ ∈ ℤ", Assert.Single(logarithm.Maxima));
        Assert.Empty(logarithm.InflectionPoints);
        AssertAllAsymptotesEmpty(logarithm);
        Assert.Equal((int)FunctionParityType.Even, logarithm.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Periodic, logarithm.PeriodicityDirection);
        Assert.Equal("π", logarithm.PeriodicityExpression);
        Assert.Empty(logarithm.MonotoneIntervals);
        Assert.Equal(4610, logarithm.TooComplexFeatures);
    }

    [Theory]
    [InlineData("sqrt(sin(3*x+pi/2))")]
    [InlineData("sqrt(sin(2*x+pi/3))")]
    public void ShiftedDoubleSineCompatibilityDoesNotBlanketOtherProvedPatterns(
        string formula)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.NotEmpty(result.Range);
        Assert.Empty(result.InflectionPoints);
        Assert.Equal(2, result.MonotoneIntervals.Count);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void ShiftedDoubleSineFooterOmissionsRespectRequestedFeatureGroups()
    {
        const string formula = "sqrt(sin(2*x+pi/2))";
        GraphFunctionAnalysisData range = AnalyzePublic(
            formula,
            PerformAnalysisType.Range);
        Assert.Empty(range.Range);
        Assert.Equal(Bits(AnalysisType.Range), range.TooComplexFeatures);

        GraphFunctionAnalysisData period = AnalyzePublic(
            formula,
            PerformAnalysisType.Period);
        Assert.Equal("π", period.PeriodicityExpression);
        Assert.Equal(0, period.TooComplexFeatures);
    }

    [Fact]
    public void NestedSineOuterRowsUseStableWindowsOmissionsWithoutDiscardingProofs()
    {
        GraphFunctionAnalysisData sine = AnalyzePublic("sin(sin(x))");
        Assert.Equal("x ∈ ℝ", sine.Domain);
        Assert.Empty(sine.Range);
        Assert.Empty(sine.Zeros);
        Assert.Equal("y = 0", sine.YIntercept);
        Assert.Empty(sine.Minima);
        Assert.Empty(sine.Maxima);
        Assert.Empty(sine.InflectionPoints);
        AssertAllAsymptotesEmpty(sine);
        Assert.Equal((int)FunctionParityType.Odd, sine.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, sine.PeriodicityDirection);
        Assert.Empty(sine.MonotoneIntervals);
        Assert.Equal(4570, sine.TooComplexFeatures);

        GraphFunctionAnalysisData cosine = AnalyzePublic("cos(sin(x))");
        Assert.Equal("x ∈ ℝ", cosine.Domain);
        Assert.Empty(cosine.Range);
        Assert.Empty(cosine.Zeros);
        Assert.Equal("y = 1", cosine.YIntercept);
        Assert.Empty(cosine.Minima);
        Assert.Empty(cosine.Maxima);
        Assert.Empty(cosine.InflectionPoints);
        AssertAllAsymptotesEmpty(cosine);
        Assert.Equal((int)FunctionParityType.Even, cosine.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, cosine.PeriodicityDirection);
        Assert.Empty(cosine.MonotoneIntervals);
        Assert.Equal(4554, cosine.TooComplexFeatures);

        GraphFunctionAnalysisData tangent = AnalyzePublic("tan(sin(x))");
        Assert.Equal("x ∈ ℝ", tangent.Domain);
        Assert.Equal("y ∈ [−tan(1), tan(1)]", tangent.Range);
        Assert.Empty(tangent.Zeros);
        Assert.Equal("y = 0", tangent.YIntercept);
        Assert.Single(tangent.Minima);
        Assert.Single(tangent.Maxima);
        Assert.Empty(tangent.InflectionPoints);
        AssertAllAsymptotesEmpty(tangent);
        Assert.Equal((int)FunctionParityType.Odd, tangent.Parity);
        Assert.Equal("2π", tangent.PeriodicityExpression);
        Assert.Equal(2, tangent.MonotoneIntervals.Count);
        // Windows publishes an incomplete inflection set here. The checked
        // analytic obligation stays unknown and therefore adds that bit.
        Assert.Equal(272, tangent.TooComplexFeatures);
    }

    [Fact]
    public void ExponentialPlusIdentityUsesWindowsEmptyZeroPresentationWithoutLosingRoot()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("exp(x)+x");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Equal("y ∈ ℝ", result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal("y = 1", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Equal("y = x", Assert.Single(result.ObliqueAsymptotes));
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Equal(
            (int)FunctionMonotonicityType.Ascending,
            Assert.Single(result.MonotoneIntervals).Value);
        Assert.Equal(Bits(AnalysisType.Zeros), result.TooComplexFeatures);

        InputExpression variable = Variable();
        AnalysisRequest request = Request(InputExpression.Binary(
            InputExpressionKind.Add,
            Function("exp", variable),
            variable,
            Source));
        AnalysisReport report = AnalysisEngine.Analyze(request);
        AssertElementaryCompositionProof(request, report, report.Zeros);
        Assert.IsType<PointSet>(report.Zeros.Value);
    }

    [Fact]
    public void SineSquareRootMatchesEveryStableWindowsRowAndFooterOmission()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sin(sqrt(x))");

        Assert.Equal("x ≥ 0", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = (πn₁)^2, n₁ ∈ ℤ, n₁ ≥ 0", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Period,
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.InflectionPoints,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void SineLogarithmMatchesEveryStableWindowsRowAndFooterOmission()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sin(ln(x))");

        Assert.Equal("x > 0", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = e^(πn₁), n₁ ∈ ℤ", result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Equal("(e^(2πn₁ + 3π/2), −1), n₁ ∈ ℤ", Assert.Single(result.Minima));
        Assert.Equal("(e^(2πn₁ + π/2), 1), n₁ ∈ ℤ", Assert.Single(result.Maxima));
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Period,
                AnalysisType.InflectionPoints,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void SineCommonLogarithmMatchesEveryStableWindowsRowAndFooterOmission()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sin(log(x))");

        Assert.Equal("x > 0", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = 10^(πn₁), n₁ ∈ ℤ", result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Equal("(10^(2πn₁ + 3π/2), −1), n₁ ∈ ℤ", Assert.Single(result.Minima));
        Assert.Equal("(10^(2πn₁ + π/2), 1), n₁ ∈ ℤ", Assert.Single(result.Maxima));
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Period,
                AnalysisType.InflectionPoints,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void SineExponentialKeepsCertifiedHorizontalLimitAndSafeLatticeConstraints()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sin(exp(x))");

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = ln(n₁) + ln(π), n₁ ∈ ℤ, n₁ ≥ 1", result.Zeros);
        Assert.Equal("y = sin(1)", result.YIntercept);
        Assert.Equal(
            "(ln(2πn₁ + 3π/2), −1), n₁ ∈ ℤ, n₁ ≥ 0",
            Assert.Single(result.Minima));
        Assert.Equal(
            "(ln(2πn₁ + π/2), 1), n₁ ∈ ℤ, n₁ ≥ 0",
            Assert.Single(result.Maxima));
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Equal("y = 0", Assert.Single(result.HorizontalAsymptotes));
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Period,
                AnalysisType.InflectionPoints,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);

        // Windows omits the real limit lim[x→−∞] sin(exp(x)) = 0 and emits
        // extrema for unrestricted integer parameters, which makes some log
        // arguments nonpositive. Mathematical truth remains authoritative for
        // both rows; only the safe Windows spelling of the zero lattice is used.
    }

    [Fact]
    public void SineTangentMatchesEveryStableWindowsRowAndFooterOmission()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("sin(tan(x))");

        Assert.Equal("x ≠ πn₁ + π/2, ∀ n₁ ∈ ℤ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Odd, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Zeros,
                AnalysisType.Period,
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.InflectionPoints,
                AnalysisType.VerticalAsymptotes,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);
    }

    [Fact]
    public void ElementaryCompositionCompatibilityNeverDowngradesCheckedProofOutcomes()
    {
        AssertProofs(
            Function("sin", Function("sqrt", Variable())),
            static (request, report) =>
            {
                AssertElementaryCompositionProof(request, report, report.Range);
                AssertElementaryCompositionProof(request, report, report.Zeros);
                AssertElementaryCompositionProof(request, report, report.Minima);
                AssertElementaryCompositionProof(request, report, report.Maxima);
                AssertElementaryCompositionProof(request, report, report.Monotonicity);
                AssertElementaryCompositionProof(request, report, report.Period);
            });
        AssertProofs(
            Function("sin", Function("ln", Variable())),
            static (request, report) =>
            {
                AssertElementaryCompositionProof(request, report, report.Range);
                AssertElementaryCompositionProof(request, report, report.Zeros);
                AssertElementaryCompositionProof(request, report, report.InflectionPoints);
                AssertElementaryCompositionProof(request, report, report.Monotonicity);
                AssertElementaryCompositionProof(request, report, report.Period);
            });
        AssertProofs(
            Function("sin", Function("log", Variable())),
            static (request, report) =>
            {
                AssertElementaryCompositionProof(request, report, report.Range);
                AssertElementaryCompositionProof(request, report, report.Zeros);
                AssertElementaryCompositionProof(request, report, report.InflectionPoints);
                AssertElementaryCompositionProof(request, report, report.Monotonicity);
                AssertElementaryCompositionProof(request, report, report.Period);
            });
        AssertProofs(
            Function("sin", Function("exp", Variable())),
            static (request, report) =>
            {
                AssertElementaryCompositionProof(request, report, report.Range);
                AssertElementaryCompositionProof(request, report, report.Zeros);
                AssertElementaryCompositionProof(request, report, report.HorizontalAsymptotes);
                AssertElementaryCompositionProof(request, report, report.Monotonicity);
                AssertElementaryCompositionProof(request, report, report.Period);
            });
        AssertProofs(
            Function("sin", Function("tan", Variable())),
            static (request, report) =>
            {
                AssertElementaryCompositionProof(request, report, report.Range);
                AssertElementaryCompositionProof(request, report, report.Zeros);
                AssertElementaryCompositionProof(request, report, report.Minima);
                AssertElementaryCompositionProof(request, report, report.Maxima);
                AssertElementaryCompositionProof(request, report, report.VerticalAsymptotes);
                AssertElementaryCompositionProof(request, report, report.Monotonicity);
                AssertElementaryCompositionProof(request, report, report.Period);
            });
        return;

        static void AssertProofs(
            InputExpression expression,
            Action<AnalysisRequest, AnalysisReport> assertion)
        {
            AnalysisRequest request = Request(expression);
            assertion(request, AnalysisEngine.Analyze(request));
        }
    }

    [Fact]
    public void NaturalLogAbsoluteMatchesEveryStableWindowsRow()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("ln(abs(x))");

        Assert.Equal("x ≠ 0", result.Domain);
        Assert.Equal("y ∈ ℝ", result.Range);
        Assert.Equal("x = −1 ∨ x = 1", result.Zeros);
        Assert.Empty(result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Equal("x = 0", Assert.Single(result.VerticalAsymptotes));
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.Even, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.NotPeriodic, result.PeriodicityDirection);
        Assert.Equal(2, result.MonotoneIntervals.Count);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Theory]
    [InlineData("min(x,1)", "y = 0")]
    [InlineData("min(1,x)", "y = 0")]
    [InlineData("max(x,1)", "y = 1")]
    [InlineData("max(1,x)", "y = 1")]
    public void CapturedUnitAffineMinMaxBaselinesMatchWindowsWithoutLosingProofs(
        string formula,
        string expectedYIntercept)
    {
        ArgumentNullException.ThrowIfNull(formula);
        ArgumentNullException.ThrowIfNull(expectedYIntercept);
        GraphFunctionAnalysisData result = AnalyzePublic(formula);

        Assert.Equal("x ∈ ℝ", result.Domain);
        Assert.Empty(result.Range);
        Assert.Empty(result.Zeros);
        Assert.Equal(expectedYIntercept, result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        AssertAllAsymptotesEmpty(result);
        Assert.Equal((int)FunctionParityType.Unknown, result.Parity);
        Assert.Equal(
            (int)FunctionPeriodicityType.NotPeriodic,
            result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Parity,
                AnalysisType.Zeros,
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.InflectionPoints,
                AnalysisType.HorizontalAsymptotes,
                AnalysisType.ObliqueAsymptotes,
                AnalysisType.Monotonicity),
            result.TooComplexFeatures);

        InputExpression function = Function(
            formula.StartsWith("min", StringComparison.Ordinal) ? "min" : "max",
            formula.Contains("(1,x)", StringComparison.Ordinal) ? Number(1) : Variable(),
            formula.Contains("(1,x)", StringComparison.Ordinal) ? Variable() : Number(1));
        AnalysisRequest request = Request(function);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        AssertAffineMinMaxProof(request, report, report.Domain);
        AssertAffineMinMaxProof(request, report, report.Range);
        AssertAffineMinMaxProof(request, report, report.Parity);
        AssertAffineMinMaxProof(request, report, report.Zeros);
        AssertAffineMinMaxProof(request, report, report.YIntercept);
        AssertAffineMinMaxProof(request, report, report.Minima);
        AssertAffineMinMaxProof(request, report, report.Maxima);
        AssertAffineMinMaxProof(request, report, report.InflectionPoints);
        AssertAffineMinMaxProof(request, report, report.VerticalAsymptotes);
        AssertAffineMinMaxProof(request, report, report.HorizontalAsymptotes);
        AssertAffineMinMaxProof(request, report, report.ObliqueAsymptotes);
        AssertAffineMinMaxProof(request, report, report.Monotonicity);
        AssertAffineMinMaxProof(request, report, report.Period);
    }

    [Fact]
    public void AffineMinMaxCompatibilityIsExactAndRequestedMaskAware()
    {
        GraphFunctionAnalysisData domain = AnalyzePublic(
            "min(x,1)",
            PerformAnalysisType.Domain);
        Assert.Equal("x ∈ ℝ", domain.Domain);
        Assert.Equal(0, domain.TooComplexFeatures);

        GraphFunctionAnalysisData range = AnalyzePublic(
            "min(x,1)",
            PerformAnalysisType.Range);
        Assert.Empty(range.Range);
        Assert.Equal(Bits(AnalysisType.Range), range.TooComplexFeatures);

        GraphFunctionAnalysisData parity = AnalyzePublic(
            "min(x,1)",
            PerformAnalysisType.Parity);
        Assert.Equal((int)FunctionParityType.Unknown, parity.Parity);
        Assert.Equal(Bits(AnalysisType.Parity), parity.TooComplexFeatures);

        GraphFunctionAnalysisData intercepts = AnalyzePublic(
            "max(x,1)",
            PerformAnalysisType.InterceptionPointsWithXAndYAxis);
        Assert.Empty(intercepts.Zeros);
        Assert.Equal("y = 1", intercepts.YIntercept);
        Assert.Equal(Bits(AnalysisType.Zeros), intercepts.TooComplexFeatures);

        GraphFunctionAnalysisData critical = AnalyzePublic(
            "min(x,1)",
            PerformAnalysisType.CriticalPoints);
        Assert.Empty(critical.Minima);
        Assert.Empty(critical.Maxima);
        Assert.Empty(critical.InflectionPoints);
        Assert.Equal(
            Bits(
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.InflectionPoints),
            critical.TooComplexFeatures);

        GraphFunctionAnalysisData asymptotes = AnalyzePublic(
            "max(x,1)",
            PerformAnalysisType.Asymptotes);
        AssertAllAsymptotesEmpty(asymptotes);
        Assert.Equal(
            Bits(
                AnalysisType.HorizontalAsymptotes,
                AnalysisType.ObliqueAsymptotes),
            asymptotes.TooComplexFeatures);

        GraphFunctionAnalysisData monotonicity = AnalyzePublic(
            "min(x,1)",
            PerformAnalysisType.Monotonicity);
        Assert.Empty(monotonicity.MonotoneIntervals);
        Assert.Equal(
            Bits(AnalysisType.Monotonicity),
            monotonicity.TooComplexFeatures);

        GraphFunctionAnalysisData period = AnalyzePublic(
            "max(x,1)",
            PerformAnalysisType.Period);
        Assert.Equal(
            (int)FunctionPeriodicityType.NotPeriodic,
            period.PeriodicityDirection);
        Assert.Empty(period.PeriodicityExpression);
        Assert.Equal(0, period.TooComplexFeatures);

        GraphFunctionAnalysisData structurallyCovered = AnalyzePublic("min(x,2)");
        Assert.Empty(structurallyCovered.Range);
        Assert.Empty(structurallyCovered.Zeros);
        Assert.Empty(structurallyCovered.MonotoneIntervals);
        Assert.Equal(
            (int)FunctionPeriodicityType.NotPeriodic,
            structurallyCovered.PeriodicityDirection);
        Assert.Equal(
            Bits(
                AnalysisType.Range,
                AnalysisType.Parity,
                AnalysisType.Zeros,
                AnalysisType.Minima,
                AnalysisType.Maxima,
                AnalysisType.InflectionPoints,
                AnalysisType.HorizontalAsymptotes,
                AnalysisType.ObliqueAsymptotes,
                AnalysisType.Monotonicity),
            structurallyCovered.TooComplexFeatures);
    }

    [Fact]
    public void FooterOmissionsAreMaskedByTheActuallyRequestedFeatureGroups()
    {
        GraphFunctionAnalysisData domain = AnalyzePublic(
            "abs(sin(x))",
            PerformAnalysisType.Domain);
        Assert.Equal("x ∈ ℝ", domain.Domain);
        Assert.Equal(0, domain.TooComplexFeatures);

        GraphFunctionAnalysisData range = AnalyzePublic(
            "abs(sin(x))",
            PerformAnalysisType.Range);
        Assert.Empty(range.Range);
        Assert.Equal(Bits(AnalysisType.Range), range.TooComplexFeatures);

        GraphFunctionAnalysisData cotCritical = AnalyzePublic(
            "cot(x)",
            PerformAnalysisType.CriticalPoints);
        Assert.Empty(cotCritical.InflectionPoints);
        Assert.Equal(Bits(AnalysisType.InflectionPoints), cotCritical.TooComplexFeatures);

        GraphFunctionAnalysisData secCritical = AnalyzePublic(
            "sec(x)",
            PerformAnalysisType.CriticalPoints);
        Assert.Equal(0, secCritical.TooComplexFeatures);

        GraphFunctionAnalysisData nestedRange = AnalyzePublic(
            "sin(sqrt(x))",
            PerformAnalysisType.Range);
        Assert.Equal(Bits(AnalysisType.Range), nestedRange.TooComplexFeatures);

        GraphFunctionAnalysisData nestedCritical = AnalyzePublic(
            "sin(ln(x))",
            PerformAnalysisType.CriticalPoints);
        Assert.Equal(
            Bits(AnalysisType.InflectionPoints),
            nestedCritical.TooComplexFeatures);

        GraphFunctionAnalysisData nestedIntercepts = AnalyzePublic(
            "sin(tan(x))",
            PerformAnalysisType.InterceptionPointsWithXAndYAxis);
        Assert.Equal(Bits(AnalysisType.Zeros), nestedIntercepts.TooComplexFeatures);

        GraphFunctionAnalysisData nestedAsymptotes = AnalyzePublic(
            "sin(tan(x))",
            PerformAnalysisType.Asymptotes);
        Assert.Equal(
            Bits(AnalysisType.VerticalAsymptotes),
            nestedAsymptotes.TooComplexFeatures);

        GraphFunctionAnalysisData signRange = AnalyzePublic(
            "sgn(x)",
            PerformAnalysisType.Range);
        Assert.Equal(Bits(AnalysisType.Range), signRange.TooComplexFeatures);

        GraphFunctionAnalysisData signDomain = AnalyzePublic(
            "sgn(x)",
            PerformAnalysisType.Domain);
        Assert.Equal(0, signDomain.TooComplexFeatures);

        GraphFunctionAnalysisData zeroPowerParity = AnalyzePublic(
            "0^x",
            PerformAnalysisType.Parity);
        Assert.Equal((int)FunctionParityType.Unknown, zeroPowerParity.Parity);
        Assert.Equal(0, zeroPowerParity.TooComplexFeatures);

        GraphFunctionAnalysisData floorRange = AnalyzePublic(
            "floor(x)",
            PerformAnalysisType.Range);
        Assert.Empty(floorRange.Range);
        Assert.Equal(Bits(AnalysisType.Range), floorRange.TooComplexFeatures);

        GraphFunctionAnalysisData floorIntercepts = AnalyzePublic(
            "floor(x)",
            PerformAnalysisType.InterceptionPointsWithXAndYAxis);
        Assert.Empty(floorIntercepts.Zeros);
        Assert.Equal("y = 0", floorIntercepts.YIntercept);
        Assert.Equal(Bits(AnalysisType.Zeros), floorIntercepts.TooComplexFeatures);

        GraphFunctionAnalysisData floorPeriod = AnalyzePublic(
            "floor(x)",
            PerformAnalysisType.Period);
        Assert.Equal(
            (int)FunctionPeriodicityType.NotPeriodic,
            floorPeriod.PeriodicityDirection);
        Assert.Equal(0, floorPeriod.TooComplexFeatures);

        GraphFunctionAnalysisData floorMonotonicity = AnalyzePublic(
            "floor(x)",
            PerformAnalysisType.Monotonicity);
        Assert.Empty(floorMonotonicity.MonotoneIntervals);
        Assert.Equal(
            Bits(AnalysisType.Monotonicity),
            floorMonotonicity.TooComplexFeatures);

        GraphFunctionAnalysisData zeroTangentRange = AnalyzePublic(
            "0^tan(x)",
            PerformAnalysisType.Range);
        Assert.Empty(zeroTangentRange.Range);
        Assert.Equal(
            Bits(AnalysisType.Range),
            zeroTangentRange.TooComplexFeatures);

        GraphFunctionAnalysisData zeroTangentParity = AnalyzePublic(
            "0^tan(x)",
            PerformAnalysisType.Parity);
        Assert.Equal((int)FunctionParityType.Unknown, zeroTangentParity.Parity);
        Assert.Equal(0, zeroTangentParity.TooComplexFeatures);

        GraphFunctionAnalysisData zeroTangentAsymptotes = AnalyzePublic(
            "0^tan(x)",
            PerformAnalysisType.Asymptotes);
        AssertAllAsymptotesEmpty(zeroTangentAsymptotes);
        Assert.Equal(
            Bits(AnalysisType.VerticalAsymptotes),
            zeroTangentAsymptotes.TooComplexFeatures);

        GraphFunctionAnalysisData zeroTangentPeriod = AnalyzePublic(
            "0^tan(x)",
            PerformAnalysisType.Period);
        Assert.Equal("π", zeroTangentPeriod.PeriodicityExpression);
        Assert.Equal(0, zeroTangentPeriod.TooComplexFeatures);

        GraphFunctionAnalysisData zeroTangentMonotonicity = AnalyzePublic(
            "0^tan(x)",
            PerformAnalysisType.Monotonicity);
        Assert.Empty(zeroTangentMonotonicity.MonotoneIntervals);
        Assert.Equal(
            Bits(AnalysisType.Monotonicity),
            zeroTangentMonotonicity.TooComplexFeatures);

        const string guardedCotangent = "(sin(x)^2+cos(x)^2)/tan(x)";
        GraphFunctionAnalysisData guardedCotangentRange = AnalyzePublic(
            guardedCotangent,
            PerformAnalysisType.Range);
        Assert.Empty(guardedCotangentRange.Range);
        Assert.Equal(
            Bits(AnalysisType.Range),
            guardedCotangentRange.TooComplexFeatures);

        GraphFunctionAnalysisData guardedCotangentPeriod = AnalyzePublic(
            guardedCotangent,
            PerformAnalysisType.Period);
        Assert.Equal(
            (int)FunctionPeriodicityType.Unknown,
            guardedCotangentPeriod.PeriodicityDirection);
        Assert.Equal(
            Bits(AnalysisType.Period),
            guardedCotangentPeriod.TooComplexFeatures);

        GraphFunctionAnalysisData guardedCotangentAsymptotes = AnalyzePublic(
            guardedCotangent,
            PerformAnalysisType.Asymptotes);
        AssertAllAsymptotesEmpty(guardedCotangentAsymptotes);
        Assert.Equal(
            Bits(AnalysisType.ObliqueAsymptotes),
            guardedCotangentAsymptotes.TooComplexFeatures);

        GraphFunctionAnalysisData guardedCotangentMonotonicity = AnalyzePublic(
            guardedCotangent,
            PerformAnalysisType.Monotonicity);
        Assert.Empty(guardedCotangentMonotonicity.MonotoneIntervals);
        Assert.Equal(
            Bits(AnalysisType.Monotonicity),
            guardedCotangentMonotonicity.TooComplexFeatures);

        const string singleHoleSine = "sin(x)*(x/x)";
        GraphFunctionAnalysisData singleHoleRange = AnalyzePublic(
            singleHoleSine,
            PerformAnalysisType.Range);
        Assert.Empty(singleHoleRange.Range);
        Assert.Equal(Bits(AnalysisType.Range), singleHoleRange.TooComplexFeatures);

        GraphFunctionAnalysisData singleHoleCritical = AnalyzePublic(
            singleHoleSine,
            PerformAnalysisType.CriticalPoints);
        Assert.Single(singleHoleCritical.Minima);
        Assert.Single(singleHoleCritical.Maxima);
        Assert.Empty(singleHoleCritical.InflectionPoints);
        Assert.Equal(
            Bits(AnalysisType.InflectionPoints),
            singleHoleCritical.TooComplexFeatures);

        GraphFunctionAnalysisData singleHolePeriod = AnalyzePublic(
            singleHoleSine,
            PerformAnalysisType.Period);
        Assert.Equal(
            (int)FunctionPeriodicityType.NotPeriodic,
            singleHolePeriod.PeriodicityDirection);
        Assert.Empty(singleHolePeriod.PeriodicityExpression);
        Assert.Equal(0, singleHolePeriod.TooComplexFeatures);

        GraphFunctionAnalysisData singleHoleMonotonicity = AnalyzePublic(
            singleHoleSine,
            PerformAnalysisType.Monotonicity);
        Assert.Empty(singleHoleMonotonicity.MonotoneIntervals);
        Assert.Equal(
            Bits(AnalysisType.Monotonicity),
            singleHoleMonotonicity.TooComplexFeatures);
    }

    private static void AssertAbsoluteProof<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.IsType<AbsoluteCompositionProofCertificate>(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
    }

    private static void AssertElementaryCompositionProof<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.IsType<ElementaryCompositionProofCertificate>(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
    }

    private static void AssertAffineMinMaxProof<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.IsType<AffineMinMaxProofCertificate>(outcome.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
    }

    private static void AssertGuardedConstantProofs(InputExpression expression)
    {
        AnalysisRequest request = Request(expression);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        AssertGuarded(report.Range);
        AssertGuarded(report.Parity);
        AssertGuarded(report.Zeros);
        AssertGuarded(report.YIntercept);
        AssertGuarded(report.Minima);
        AssertGuarded(report.Maxima);
        AssertGuarded(report.InflectionPoints);
        AssertGuarded(report.VerticalAsymptotes);
        AssertGuarded(report.HorizontalAsymptotes);
        AssertGuarded(report.ObliqueAsymptotes);
        AssertGuarded(report.Monotonicity);
        AssertGuarded(report.Period);
        return;

        void AssertGuarded<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            Assert.IsType<GuardedConstantProofCertificate>(outcome.Certificate);
            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    private static void AssertReciprocalProofs<TFirst>(
        InputExpression expression,
        Func<AnalysisReport, ProofOutcome<TFirst>> first)
    {
        AnalysisRequest request = Request(expression);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        AssertReciprocalProof(request, report, first(report));
    }

    private static void AssertReciprocalProofs<TFirst, TSecond>(
        InputExpression expression,
        Func<AnalysisReport, ProofOutcome<TFirst>> first,
        Func<AnalysisReport, ProofOutcome<TSecond>> second)
    {
        AnalysisRequest request = Request(expression);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        AssertReciprocalProof(request, report, first(report));
        AssertReciprocalProof(request, report, second(report));
    }

    private static void AssertReciprocalProof<T>(
        AnalysisRequest request,
        AnalysisReport report,
        ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);
        Assert.Equal(TheoremRule.AffineReciprocalTrigonometric, certificate.Theorem);
        Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
    }

    private static void AssertAllAsymptotesEmpty(GraphFunctionAnalysisData result)
    {
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
    }

    private static GraphFunctionAnalysisData AnalyzePublic(
        string formula,
        PerformAnalysisType requested = PerformAnalysisType.All)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)requested));
        return solver.Analyze(analyzer);
    }

    private static AnalysisRequest Request(InputExpression expression) =>
        new(expression, AnalysisFeatures.All, AngleUnit.Radians, "x", static () => true);

    private static int Bits(params AnalysisType[] features) =>
        features.Aggregate(0, static (bits, feature) => bits | FeatureBit(feature));

    private static int FeatureBit(AnalysisType type) => type switch
    {
        AnalysisType.Domain => 1,
        AnalysisType.Range => 2,
        AnalysisType.Parity => 4,
        AnalysisType.Period => 8,
        AnalysisType.Zeros => 16,
        AnalysisType.YIntercept => 32,
        AnalysisType.Minima => 64,
        AnalysisType.Maxima => 128,
        AnalysisType.InflectionPoints => 256,
        AnalysisType.VerticalAsymptotes => 512,
        AnalysisType.HorizontalAsymptotes => 1024,
        AnalysisType.ObliqueAsymptotes => 2048,
        AnalysisType.Monotonicity => 4096,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static InputExpression Variable() => InputExpression.Variable("x", Source);

    private static InputExpression Abs(InputExpression value) => Function("abs", value);

    private static InputExpression Sin(InputExpression value) => Function("sin", value);

    private static InputExpression Cos(InputExpression value) => Function("cos", value);

    private static InputExpression Tan(InputExpression value) => Function("tan", value);

    private static InputExpression Sqrt(InputExpression value) => Function("sqrt", value);

    private static InputExpression Sinh(InputExpression value) => Function("sinh", value);

    private static InputExpression Cosh(InputExpression value) => Function("cosh", value);

    private static InputExpression Number(int value) =>
        InputExpression.Number(new BigRational(value), Source);

    private static InputExpression Add(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Add, left, right, Source);

    private static InputExpression Subtract(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);

    private static InputExpression Multiply(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Multiply, left, right, Source);

    private static InputExpression Divide(InputExpression left, InputExpression right) =>
        InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);

    private static InputExpression Power(InputExpression basis, InputExpression exponent) =>
        InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);

    private static InputExpression Function(string name, params InputExpression[] arguments) =>
        InputExpression.Function(name, arguments.ToImmutableArray(), Source);
}
