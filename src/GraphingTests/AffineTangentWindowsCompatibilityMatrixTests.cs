using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AffineTangentWindowsCompatibilityMatrixTests
{
    private static readonly SourceRange Source = new(0, 1);
    private static readonly AffineTangentWindowsCompatibilityMatrixTestsFrequencyCase[] Frequencies = [new("p1", "x", "π", false, false), new("n1", "-x", "π", true, false), new("p2", "2*x", "π/2", false, false), new("n2", "-2*x", "π/2", true, false), new("half", "x/2", "2π", false, false), new("pi", "pi*x", "1", false, true)];
    private static readonly AffineTangentWindowsCompatibilityMatrixTestsPhaseCase[] Phases = [new("zero", string.Empty), new("p1", "+1"), new("n1", "-1"), new("halfpi", "+pi/2"), new("quarterpi", "+pi/4")];
    private static readonly AffineTangentWindowsCompatibilityMatrixTestsAmplitudeCase[] Amplitudes = [new("p1", string.Empty), new("n1", "-"), new("p2", "2*"), new("pi", "pi*")];
    [Fact]
    public void CompleteCapturedTangentMatrixUsesOnlySettledWindowsOmissions()
    {
        const PerformAnalysisType requested = PerformAnalysisType.CriticalPoints | PerformAnalysisType.Asymptotes | PerformAnalysisType.Period;
        int rows = 0;
        foreach (AffineTangentWindowsCompatibilityMatrixTestsFrequencyCase frequency in Frequencies)
        {
            foreach (AffineTangentWindowsCompatibilityMatrixTestsPhaseCase phase in Phases)
            {
                foreach (AffineTangentWindowsCompatibilityMatrixTestsAmplitudeCase amplitude in Amplitudes)
                {
                    foreach (bool quotient in new[]
                    {
                        false,
                        true
                    }

                    )
                    {
                        string formula = Formula(frequency, phase, amplitude, quotient);
                        GraphFunctionAnalysisData result = AnalyzePublic(formula, requested);
                        bool publishesPoles = PublishesPoles(frequency, phase);
                        Assert.Empty(result.InflectionPoints);
                        if (publishesPoles)
                        {
                            Assert.Single(result.VerticalAsymptotes);
                        }
                        else
                        {
                            Assert.Empty(result.VerticalAsymptotes);
                        }

                        Assert.Equal((int)FunctionPeriodicityType.Periodic, result.PeriodicityDirection);
                        Assert.Equal(frequency.ExpectedPeriod, result.PeriodicityExpression);
                        Assert.Equal(Bits(AnalysisType.InflectionPoints, publishesPoles ? null : AnalysisType.VerticalAsymptotes), result.TooComplexFeatures);
                        AssertAcceptedTangentCertificates(CreateExpression(frequency, phase, amplitude, quotient), amplitude.Id == "pi" || frequency.IsPi || phase.Id is "halfpi" or "quarterpi");
                        rows++;
                    }
                }
            }
        }

        Assert.Equal(240, rows);
    }

    [Fact]
    public void OnlyTheUnanimousDirectParityOmissionIsCopied()
    {
        GraphFunctionAnalysisData direct = AnalyzePublic("-tan(2*x-1)", PerformAnalysisType.Parity);
        Assert.Equal((int)FunctionParityType.Unknown, direct.Parity);
        Assert.Equal(Bits(AnalysisType.Parity), direct.TooComplexFeatures);
        GraphFunctionAnalysisData quotient = AnalyzePublic("-(sin(2*x-1)/cos(2*x-1))", PerformAnalysisType.Parity);
        Assert.Equal((int)FunctionParityType.None, quotient.Parity);
        Assert.Equal(0, quotient.TooComplexFeatures);
        // Identical captures disagreed on this direct row. A two-of-three
        // modal UI result is not a stable compatibility rule.
        GraphFunctionAnalysisData unstableModal = AnalyzePublic("tan(2*x+pi/4)", PerformAnalysisType.Parity);
        Assert.Equal((int)FunctionParityType.None, unstableModal.Parity);
        Assert.Equal(0, unstableModal.TooComplexFeatures);
        AnalysisRequest request = Request(Negate(Tan(Subtract(Multiply(Number(2), Variable()), Number(1)))), AnalysisFeatures.Parity);
        AnalysisReport internalReport = AnalysisEngine.Analyze(request);
        Assert.Equal(ProofState.Proved, internalReport.Parity.State);
        Assert.Equal(FunctionParity.Neither, internalReport.Parity.Value);
        Assert.True(CertificateChecker.Check(request, internalReport.Expression!, internalReport.Parity));
    }

    [Theory]
    [InlineData("tan(pi*x+1)", "x ≠ n₁ − 1/π + 1/2, ∀ n₁ ∈ ℤ", "x = n₁ + (π − 1)/π, n₁ ∈ ℤ", null, "(n₁ − 1/π + 1/2, n₁ − 1/π + 3/2), n₁ ∈ ℤ")]
    [InlineData("tan(pi*x-1)", "x ≠ n₁ + 1/π + 1/2, ∀ n₁ ∈ ℤ", "x = n₁ + 1/π, n₁ ∈ ℤ", null, "(n₁ + 1/π + 1/2, n₁ + 1/π + 3/2), n₁ ∈ ℤ")]
    [InlineData("tan(x+1)", "x ≠ πn₁ + π/2 − 1, ∀ n₁ ∈ ℤ", "x = πn₁ + π − 1, n₁ ∈ ℤ", null, "(πn₁ + π/2 − 1, πn₁ + 3π/2 − 1), n₁ ∈ ℤ")]
    [InlineData("tan(2*x+1)", "x ≠ π/2n₁ + π/4 − 1/2, ∀ n₁ ∈ ℤ", "x = π/2n₁ + (π − 1)/2, n₁ ∈ ℤ", null, "(π/2n₁ + π/4 − 1/2, π/2n₁ + 3π/4 − 1/2), n₁ ∈ ℤ")]
    [InlineData("tan(x/2+1)", "x ≠ 2πn₁ + π − 2, ∀ n₁ ∈ ℤ", "x = 2πn₁ + 2(π − 1), n₁ ∈ ℤ", null, "(2πn₁ + π − 2, 2πn₁ + 3π − 2), n₁ ∈ ℤ")]
    [InlineData("tan(pi*x+pi/4)", "x ≠ n₁ + 1/4, ∀ n₁ ∈ ℤ", "x = n₁ + 3/4, n₁ ∈ ℤ", "x = n₁ + 1/4, n₁ ∈ ℤ", "(n₁ + 1/4, n₁ + 5/4), n₁ ∈ ℤ")]
    [InlineData("tan(x-1)", "x ≠ πn₁ + π/2 + 1, ∀ n₁ ∈ ℤ", "x = πn₁ + 1, n₁ ∈ ℤ", "x = πn₁ + (π + 2)/2, n₁ ∈ ℤ", "(πn₁ + π/2 + 1, πn₁ + 3π/2 + 1), n₁ ∈ ℤ")]
    [InlineData("tan(2*x-1)", "x ≠ π/2n₁ + π/4 + 1/2, ∀ n₁ ∈ ℤ", "x = π/2n₁ + 1/2, n₁ ∈ ℤ", "x = π/2n₁ + (π + 2)/4, n₁ ∈ ℤ", "(π/2n₁ + π/4 + 1/2, π/2n₁ + 3π/4 + 1/2), n₁ ∈ ℤ")]
    [InlineData("tan(-x+pi/2)", "x ≠ πn₁, ∀ n₁ ∈ ℤ", "x = πn₁ + π/2, n₁ ∈ ℤ", null, "(πn₁, πn₁ + π), n₁ ∈ ℤ")]
    public void CapturedPeriodicFamiliesUseWindowsRepresentativeChoices(string formula, string expectedDomain, string expectedZeros, string? expectedVertical, string expectedMonotonicity)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula, PerformAnalysisType.Domain | PerformAnalysisType.InterceptionPointsWithXAndYAxis | PerformAnalysisType.Asymptotes | PerformAnalysisType.Monotonicity);
        Assert.Equal(expectedDomain, result.Domain);
        Assert.Equal(expectedZeros, result.Zeros);
        if (expectedVertical is null)
        {
            Assert.Empty(result.VerticalAsymptotes);
        }
        else
        {
            Assert.Equal(expectedVertical, Assert.Single(result.VerticalAsymptotes));
        }

        Assert.Equal(expectedMonotonicity, Assert.Single(result.MonotoneIntervals).Key);
    }

    [Theory]
    [InlineData("tan(3*x-1)")]
    [InlineData("3*tan(2*x-1)")]
    [InlineData("tan(2*x+2)")]
    [InlineData("tan(2*x-1)+1")]
    public void CapturedCoefficientRulesDoNotLeakToUnmeasuredForms(string formula)
    {
        GraphFunctionAnalysisData result = AnalyzePublic(formula, PerformAnalysisType.CriticalPoints | PerformAnalysisType.Asymptotes);
        Assert.NotEmpty(result.InflectionPoints);
        Assert.NotEmpty(result.VerticalAsymptotes);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void RadianCaptureRulesDoNotLeakToDegreeMode()
    {
        GraphFunctionAnalysisData result = AnalyzePublic("tan(x)", PerformAnalysisType.CriticalPoints | PerformAnalysisType.Asymptotes, EvalTrigUnitMode.Degrees);
        Assert.NotEmpty(result.InflectionPoints);
        Assert.NotEmpty(result.VerticalAsymptotes);
        Assert.Equal(0, result.TooComplexFeatures);
    }

    [Fact]
    public void BoundedRadicalTangentProductCopiesOnlyTheCapturedPeriodOmission()
    {
        const string formula = "sqrt(x)*sqrt(0.1-x)*tan(x)";
        GraphFunctionAnalysisData result = AnalyzePublic(formula, PerformAnalysisType.All);
        Assert.Equal("0 ≤ x ≤ 1/10", result.Domain);
        Assert.Empty(result.Range);
        Assert.Equal("x = 0 ∨ x = 1/10", result.Zeros);
        Assert.Equal("y = 0", result.YIntercept);
        Assert.Empty(result.Minima);
        Assert.Empty(result.Maxima);
        Assert.Empty(result.InflectionPoints);
        Assert.Empty(result.VerticalAsymptotes);
        Assert.Empty(result.HorizontalAsymptotes);
        Assert.Empty(result.ObliqueAsymptotes);
        Assert.Equal((int)FunctionParityType.None, result.Parity);
        Assert.Equal((int)FunctionPeriodicityType.Unknown, result.PeriodicityDirection);
        Assert.Empty(result.PeriodicityExpression);
        Assert.Empty(result.MonotoneIntervals);
        Assert.Equal(Bits(AnalysisType.Range, AnalysisType.Period, AnalysisType.Minima, AnalysisType.Maxima, AnalysisType.InflectionPoints, AnalysisType.Monotonicity), result.TooComplexFeatures);
        InputExpression expression = BoundedProduct();
        AnalysisRequest request = Request(expression, AnalysisFeatures.Period);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        Assert.Equal(ProofState.Proved, report.Period.State);
        Assert.Equal(PeriodicityKind.NotPeriodic, report.Period.Value!.Kind);
        Assert.IsType<BoundedRadicalTangentProductProofCertificate>(report.Period.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Period));
    }

    private static void AssertAcceptedTangentCertificates(InputExpression expression, bool usesExactCoefficientCertificate)
    {
        const AnalysisFeatures features = AnalysisFeatures.InflectionPoints | AnalysisFeatures.VerticalAsymptotes | AnalysisFeatures.Period;
        AnalysisRequest request = Request(expression, features);
        AnalysisReport report = AnalysisEngine.Analyze(request);
        AssertProof(report.InflectionPoints);
        AssertProof(report.VerticalAsymptotes);
        AssertProof(report.Period);
        string inflectionPattern = Pattern(report.InflectionPoints.Certificate!);
        Assert.Equal(inflectionPattern, Pattern(report.VerticalAsymptotes.Certificate!));
        Assert.Equal(inflectionPattern, Pattern(report.Period.Certificate!));
        return;
        void AssertProof<T>(ProofOutcome<T> outcome)
        {
            Assert.Equal(ProofState.Proved, outcome.State);
            if (usesExactCoefficientCertificate)
            {
                var certificate = Assert.IsType<ExactCoefficientProofCertificate>(outcome.Certificate);
                Assert.Equal(ExactCoefficientPatternKind.AffineTangent, certificate.PatternKind);
            }
            else
            {
                var certificate = Assert.IsType<TheoremProofCertificate>(outcome.Certificate);
                Assert.Equal(TheoremRule.AffineTangent, certificate.Theorem);
                Assert.Equal(AngleUnit.Radians.ToString(), certificate.Parameters[1]);
            }

            Assert.True(CertificateChecker.Check(request, report.Expression!, outcome));
        }
    }

    private static string Pattern(ProofCertificate certificate)
    {
        return certificate switch
        {
            ExactCoefficientProofCertificate exact => exact.PatternCanonical,
            TheoremProofCertificate theorem => theorem.Parameters[0],
            _ => throw new InvalidOperationException("Unexpected affine-tangent certificate.")
        };
    }

    private static bool PublishesPoles(AffineTangentWindowsCompatibilityMatrixTestsFrequencyCase frequency, AffineTangentWindowsCompatibilityMatrixTestsPhaseCase phase)
    {
        return frequency.IsPi
            ? phase.Id is "zero" or "halfpi" or "quarterpi"
            : phase.Id == (frequency.IsNegative ? "p1" : "n1");
    }

    private static string Formula(AffineTangentWindowsCompatibilityMatrixTestsFrequencyCase frequency, AffineTangentWindowsCompatibilityMatrixTestsPhaseCase phase, AffineTangentWindowsCompatibilityMatrixTestsAmplitudeCase amplitude, bool quotient)
    {
        string argument = frequency.Argument + phase.Suffix;
        string core = quotient ? $"sin({argument})/cos({argument})" : $"tan({argument})";
        return amplitude.Id switch
        {
            "p1" => core,
            "n1" => $"-({core})",
            _ => $"{amplitude.Prefix}({core})"
        };
    }

    private static InputExpression CreateExpression(AffineTangentWindowsCompatibilityMatrixTestsFrequencyCase frequency, AffineTangentWindowsCompatibilityMatrixTestsPhaseCase phase, AffineTangentWindowsCompatibilityMatrixTestsAmplitudeCase amplitude, bool quotient)
    {
        InputExpression x = Variable();
        InputExpression argument = frequency.Id switch
        {
            "p1" => x,
            "n1" => Negate(x),
            "p2" => Multiply(Number(2), x),
            "n2" => Multiply(Number(-2), x),
            "half" => Divide(x, Number(2)),
            "pi" => Multiply(Symbol("pi"), x),
            _ => throw new InvalidOperationException("Unexpected frequency.")
        };
        argument = phase.Id switch
        {
            "zero" => argument,
            "p1" => Add(argument, Number(1)),
            "n1" => Subtract(argument, Number(1)),
            "halfpi" => Add(argument, Divide(Symbol("pi"), Number(2))),
            "quarterpi" => Add(argument, Divide(Symbol("pi"), Number(4))),
            _ => throw new InvalidOperationException("Unexpected phase.")
        };
        InputExpression core = quotient ? Divide(Sin(argument), Cos(argument)) : Tan(argument);
        return amplitude.Id switch
        {
            "p1" => core,
            "n1" => Negate(core),
            "p2" => Multiply(Number(2), core),
            "pi" => Multiply(Symbol("pi"), core),
            _ => throw new InvalidOperationException("Unexpected amplitude.")
        };
    }

    private static InputExpression BoundedProduct()
    {
        InputExpression x = Variable();
        return Multiply(Multiply(Function("sqrt", x), Function("sqrt", Subtract(Number(new BigRational(1, 10)), x))), Tan(x));
    }

    private static GraphFunctionAnalysisData AnalyzePublic(string formula, PerformAnalysisType requested, EvalTrigUnitMode angleUnit = EvalTrigUnitMode.Radians)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.EvalOptions().SetTrigUnitMode(angleUnit);
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType) ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));
        IGraphAnalyzer analyzer = graph.GetAnalyzer();
        Assert.True(analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX));
        Assert.False(variableIsNotX);
        Assert.Equal(GraphStatus.Ok, analyzer.PerformFunctionAnalysis((uint)requested));
        return solver.Analyze(analyzer);
    }

    private static AnalysisRequest Request(InputExpression expression, AnalysisFeatures features)
    {
        return new AnalysisRequest(expression, features, AngleUnit.Radians, "x", static () => true);
    }

    private static int Bits(params AnalysisType?[] features)
    {
        return features.Where(static feature => feature.HasValue)
            .Aggregate(0, static (bits, feature) => bits | FeatureBit(feature!.Value));
    }

    private static int FeatureBit(AnalysisType type)
    {
        return type switch
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
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Number(int value)
    {
        return Number(new BigRational(value));
    }

    private static InputExpression Number(BigRational value)
    {
        return InputExpression.Number(value, Source);
    }

    private static InputExpression Symbol(string name)
    {
        return InputExpression.Variable(name, Source);
    }

    private static InputExpression Negate(InputExpression value)
    {
        return InputExpression.Unary(InputExpressionKind.Negate, value, Source);
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

    private static InputExpression Sin(InputExpression argument)
    {
        return Function("sin", argument);
    }

    private static InputExpression Cos(InputExpression argument)
    {
        return Function("cos", argument);
    }

    private static InputExpression Tan(InputExpression argument)
    {
        return Function("tan", argument);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
