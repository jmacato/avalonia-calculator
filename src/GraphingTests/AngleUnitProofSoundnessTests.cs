using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class AngleUnitProofSoundnessTests
{
    private static readonly SourceRange Source = new(0, 1);

    [Theory]
    [InlineData((int)AngleUnit.Radians, "fn:asin(fn:divide(q:1,pi:1:0))", "pi:2:0")]
    [InlineData((int)AngleUnit.Degrees, "fn:divide(fn:scale(fn:asin(fn:divide(q:1,pi:1:0)),q:180),pi:1:0)", "q:360")]
    [InlineData((int)AngleUnit.Grads, "fn:divide(fn:scale(fn:asin(fn:divide(q:1,pi:1:0)),q:200),pi:1:0)", "q:400")]
    public void ExactSymbolicInverseZerosUseTheRequestedAngleUnit(
        int unitValue,
        string expectedPrincipal,
        string expectedPeriod)
    {
        AngleUnit unit = (AngleUnit)unitValue;
        InputExpression expression = Subtract(
            Multiply(Symbol("pi"), Function("sin", Variable())),
            Number(1));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Zeros, unit);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        UnionSet zeros = Assert.IsType<UnionSet>(Proved(report.Zeros));
        PeriodicPointSet[] families = zeros.Operands.Cast<PeriodicPointSet>().ToArray();
        Assert.Equal(2, families.Length);
        Assert.Contains(
            families,
            family => ExactRealCanonical.Format(family.Offset) == expectedPrincipal);
        Assert.All(families, family => Assert.Equal(
            expectedPeriod,
            ExactRealCanonical.Format(family.Period)));
        Assert.IsType<ExactCoefficientProofCertificate>(report.Zeros.Certificate);
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Zeros));

        AngleUnit other = unit == AngleUnit.Degrees ? AngleUnit.Grads : AngleUnit.Degrees;
        Assert.False(CertificateChecker.Check(
            request with { AngleUnit = other },
            report.Expression!,
            report.Zeros));
    }

    [Theory]
    [InlineData((int)AngleUnit.Radians, "q:1")]
    [InlineData((int)AngleUnit.Degrees, "pi:1/180:0")]
    [InlineData((int)AngleUnit.Grads, "pi:1/200:0")]
    public void ExactSymbolicForwardValuesUseRadianFunctionArguments(
        int unitValue,
        string expectedArgument)
    {
        AngleUnit unit = (AngleUnit)unitValue;
        InputExpression sine = Multiply(
            Symbol("pi"),
            Function("sin", Add(Variable(), Number(1))));
        AnalysisRequest sineRequest = Request(sine, AnalysisFeatures.YIntercept, unit);
        AnalysisReport sineReport = AnalysisEngine.Analyze(sineRequest);
        Assert.Equal(
            $"fn:multiply(pi:1:0,fn:sin({expectedArgument}))",
            ExactRealCanonical.Format(Proved(sineReport.YIntercept).Value!));
        Assert.True(CertificateChecker.Check(
            sineRequest,
            sineReport.Expression!,
            sineReport.YIntercept));

        InputExpression tangent = Multiply(
            Symbol("pi"),
            Function("tan", Subtract(Variable(), Number(1))));
        AnalysisRequest tangentRequest = Request(
            tangent,
            AnalysisFeatures.YIntercept,
            unit);
        AnalysisReport tangentReport = AnalysisEngine.Analyze(tangentRequest);
        Assert.Equal(
            $"fn:multiply(pi:-1:0,fn:tan({expectedArgument}))",
            ExactRealCanonical.Format(Proved(tangentReport.YIntercept).Value!));
        Assert.True(CertificateChecker.Check(
            tangentRequest,
            tangentReport.Expression!,
            tangentReport.YIntercept));
    }

    [Theory]
    [InlineData((int)AngleUnit.Radians, "pi:1/6:0", "pi:5/6:0", "pi:2:0")]
    [InlineData((int)AngleUnit.Degrees, "q:30", "q:150", "q:360")]
    [InlineData((int)AngleUnit.Grads, "q:100/3", "q:500/3", "q:400")]
    public void RationalAffineTrigUsesExactSpecialInverseAngles(
        int unitValue,
        string expectedFirst,
        string expectedSecond,
        string expectedPeriod)
    {
        AngleUnit unit = (AngleUnit)unitValue;
        InputExpression expression = Subtract(
            Multiply(Number(2), Function("sin", Variable())),
            Number(1));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Zeros, unit);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        UnionSet zeros = Assert.IsType<UnionSet>(Proved(report.Zeros));
        Assert.Equal(
            new[] { expectedFirst, expectedSecond }.Order(StringComparer.Ordinal),
            zeros.Operands
                .Cast<PeriodicPointSet>()
                .Select(static family => ExactRealCanonical.Format(family.Offset))
                .Order(StringComparer.Ordinal));
        Assert.All(zeros.Operands.Cast<PeriodicPointSet>(), family => Assert.Equal(
            expectedPeriod,
            ExactRealCanonical.Format(family.Period)));
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Zeros));
    }

    [Theory]
    [InlineData((int)AngleUnit.Radians, 1, "q:1")]
    [InlineData((int)AngleUnit.Degrees, 90, "q:1")]
    [InlineData((int)AngleUnit.Grads, 100, "q:1")]
    public void RationalAffineQuarterTurnsProveParityValuesAndPoles(
        int unitValue,
        int quarterTurn,
        string expectedSine)
    {
        AngleUnit unit = (AngleUnit)unitValue;
        InputExpression sine = Function(
            "sin",
            Add(Variable(), unit == AngleUnit.Radians
                ? Divide(Symbol("pi"), Number(2))
                : Number(quarterTurn)));
        AnalysisRequest sineRequest = Request(
            sine,
            AnalysisFeatures.Parity | AnalysisFeatures.YIntercept,
            unit);
        AnalysisReport sineReport = AnalysisEngine.Analyze(sineRequest);
        Assert.Equal(FunctionParity.Even, Proved(sineReport.Parity));
        Assert.Equal(
            expectedSine,
            ExactRealCanonical.Format(Proved(sineReport.YIntercept).Value!));

        InputExpression tangent = Function(
            "tan",
            Add(Variable(), unit == AngleUnit.Radians
                ? Divide(Symbol("pi"), Number(2))
                : Number(quarterTurn)));
        AnalysisRequest tangentRequest = Request(
            tangent,
            AnalysisFeatures.Parity | AnalysisFeatures.YIntercept,
            unit);
        AnalysisReport tangentReport = AnalysisEngine.Analyze(tangentRequest);
        Assert.Equal(FunctionParity.Odd, Proved(tangentReport.Parity));
        Assert.False(Proved(tangentReport.YIntercept).HasValue);

        Assert.True(CertificateChecker.Check(
            sineRequest,
            sineReport.Expression!,
            sineReport.Parity));
        Assert.True(CertificateChecker.Check(
            sineRequest,
            sineReport.Expression!,
            sineReport.YIntercept));
        Assert.True(CertificateChecker.Check(
            tangentRequest,
            tangentReport.Expression!,
            tangentReport.Parity));
        Assert.True(CertificateChecker.Check(
            tangentRequest,
            tangentReport.Expression!,
            tangentReport.YIntercept));
    }

    [Theory]
    [InlineData((int)AngleUnit.Radians, "q:1")]
    [InlineData((int)AngleUnit.Degrees, "pi:1/180:0")]
    [InlineData((int)AngleUnit.Grads, "pi:1/200:0")]
    public void ReciprocalTrigYInterceptsUseRadianFunctionArguments(
        int unitValue,
        string expectedArgument)
    {
        AngleUnit unit = (AngleUnit)unitValue;
        foreach (string function in new[] { "sec", "csc", "cot" })
        {
            InputExpression expression = Function(
                function,
                Add(Variable(), Number(1)));
            AnalysisRequest request = Request(
                expression,
                AnalysisFeatures.YIntercept,
                unit);
            AnalysisReport report = AnalysisEngine.Analyze(request);

            Assert.Equal(
                $"fn:{function}({expectedArgument})",
                ExactRealCanonical.Format(Proved(report.YIntercept).Value!));
            Assert.True(CertificateChecker.Check(
                request,
                report.Expression!,
                report.YIntercept));
        }
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, "pi:1/180:0", "q:180")]
    [InlineData((int)AngleUnit.Grads, "pi:1/200:0", "q:200")]
    public void NestedTrigAndInverseTrigPreserveBothUnitBoundaries(
        int unitValue,
        string expectedPhase,
        string expectedHalfTurn)
    {
        AngleUnit unit = (AngleUnit)unitValue;
        InputExpression inner = Function(
            "sin",
            Add(Variable(), Number(1)));

        AnalysisRequest forwardRequest = Request(
            Function("sin", inner),
            AnalysisFeatures.Range | AnalysisFeatures.YIntercept,
            unit);
        AnalysisReport forward = AnalysisEngine.Analyze(forwardRequest);
        string expectedInner = $"fn:sin({expectedPhase})";
        string expectedForward =
            $"fn:sin(fn:multiply({expectedInner},{(unit == AngleUnit.Degrees ? "pi:1/180:0" : "pi:1/200:0")}))";
        Assert.Equal(
            expectedForward,
            ExactRealCanonical.Format(Proved(forward.YIntercept).Value!));
        IntervalSet range = Assert.IsType<IntervalSet>(Proved(forward.Range));
        Assert.Contains("fn:sin", range.Canonical, StringComparison.Ordinal);

        AnalysisRequest inverseRequest = Request(
            Function("asin", inner),
            AnalysisFeatures.Range | AnalysisFeatures.YIntercept,
            unit);
        AnalysisReport inverse = AnalysisEngine.Analyze(inverseRequest);
        Assert.Equal(
            $"interval[{(unit == AngleUnit.Degrees ? "q:-90" : "q:-100")},1,{(unit == AngleUnit.Degrees ? "q:90" : "q:100")},1]",
            Proved(inverse.Range).Canonical);
        Assert.Equal(
            $"fn:divide(fn:scale(fn:asin({expectedInner}),{expectedHalfTurn}),pi:1:0)",
            ExactRealCanonical.Format(Proved(inverse.YIntercept).Value!));

        Assert.True(CertificateChecker.Check(
            forwardRequest,
            forward.Expression!,
            forward.YIntercept));
        Assert.True(CertificateChecker.Check(
            inverseRequest,
            inverse.Expression!,
            inverse.YIntercept));
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, "pi:1/180:0")]
    [InlineData((int)AngleUnit.Grads, "pi:1/200:0")]
    public void AbsoluteAndPartialSineCompositionsUseRadianArguments(
        int unitValue,
        string expectedPhase)
    {
        AngleUnit unit = (AngleUnit)unitValue;
        InputExpression shiftedSine = Function(
            "sin",
            Add(Variable(), Number(1)));

        AnalysisRequest absoluteRequest = Request(
            Function("abs", shiftedSine),
            AnalysisFeatures.YIntercept,
            unit);
        AnalysisReport absolute = AnalysisEngine.Analyze(absoluteRequest);
        Assert.Equal(
            $"fn:abs(fn:sin({expectedPhase}))",
            ExactRealCanonical.Format(Proved(absolute.YIntercept).Value!));
        Assert.True(CertificateChecker.Check(
            absoluteRequest,
            absolute.Expression!,
            absolute.YIntercept));

        AnalysisRequest squareRootRequest = Request(
            Function("sqrt", shiftedSine),
            AnalysisFeatures.YIntercept,
            unit);
        AnalysisReport squareRoot = AnalysisEngine.Analyze(squareRootRequest);
        Assert.Equal(
            $"fn:sqrt(fn:sin({expectedPhase}))",
            ExactRealCanonical.Format(Proved(squareRoot.YIntercept).Value!));
        Assert.True(CertificateChecker.Check(
            squareRootRequest,
            squareRoot.Expression!,
            squareRoot.YIntercept));
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, "q:30")]
    [InlineData((int)AngleUnit.Grads, "q:100/3")]
    public void InversePrimitiveYInterceptUsesSelectedOutputUnit(
        int unitValue,
        string expected)
    {
        AngleUnit unit = (AngleUnit)unitValue;
        InputExpression expression = Function(
            "asin",
            Add(Variable(), Divide(Number(1), Number(2))));
        AnalysisRequest request = Request(
            expression,
            AnalysisFeatures.YIntercept,
            unit);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        Assert.Equal(
            expected,
            ExactRealCanonical.Format(Proved(report.YIntercept).Value!));
        Assert.True(CertificateChecker.Check(
            request,
            report.Expression!,
            report.YIntercept));
    }

    [Theory]
    [InlineData((int)AngleUnit.Degrees, "q:180")]
    [InlineData((int)AngleUnit.Grads, "q:200")]
    public void HalfAngleRootFamiliesConvertGenericAtanCoordinates(
        int unitValue,
        string expectedScale)
    {
        AngleUnit unit = (AngleUnit)unitValue;
        InputExpression expression = Add(
            Function("sin", Variable()),
            Function("cos", Variable()));
        AnalysisRequest request = Request(expression, AnalysisFeatures.Zeros, unit);
        AnalysisReport report = AnalysisEngine.Analyze(request);

        RealSet zeros = Proved(report.Zeros);
        PeriodicPointSet[] families = Assert.IsType<UnionSet>(zeros)
            .Operands
            .Cast<PeriodicPointSet>()
            .ToArray();
        Assert.Equal(2, families.Length);
        Assert.All(families, family =>
        {
            string offset = ExactRealCanonical.Format(family.Offset);
            Assert.Contains("fn:twice-atan", offset, StringComparison.Ordinal);
            Assert.Contains(expectedScale, offset, StringComparison.Ordinal);
            Assert.Contains("pi:1:0", offset, StringComparison.Ordinal);
        });
        Assert.True(CertificateChecker.Check(request, report.Expression!, report.Zeros));
    }

    private static AnalysisRequest Request(
        InputExpression expression,
        AnalysisFeatures features,
        AngleUnit angleUnit)
    {
        return new AnalysisRequest(expression, features, angleUnit, "x", static () => true);
    }

    private static T Proved<T>(ProofOutcome<T> outcome)
    {
        Assert.Equal(ProofState.Proved, outcome.State);
        Assert.NotNull(outcome.Certificate);
        return outcome.Value!;
    }

    private static InputExpression Variable()
    {
        return InputExpression.Variable("x", Source);
    }

    private static InputExpression Symbol(string name)
    {
        return InputExpression.Variable(name, Source);
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

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }
}
