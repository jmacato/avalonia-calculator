using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class ExactOriginSubstitutionCoverageTests
{
    private static readonly SourceRange Source = new(0, 1);
    [Theory]
    [InlineData((int)AngleUnit.Radians, "pi:1/2:0")]
    [InlineData((int)AngleUnit.Degrees, "q:90")]
    [InlineData((int)AngleUnit.Grads, "q:100")]
    public void InverseTrigonometryReturnsTheExactValueInTheRequestedUnit(int angleUnitValue, string expectedCanonical)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression x = Variable();
        InputExpression expression = Add(Function("asin", Add(x, Number(1))), Function("sin", x));
        AssertOrigin(expression, angleUnit, expectedHasValue: true, expectedCanonical);
    }

    [Theory]
    [InlineData((int)AngleUnit.Radians)]
    [InlineData((int)AngleUnit.Degrees)]
    [InlineData((int)AngleUnit.Grads)]
    public void ForwardTrigonometryConvertsTheRequestedUnitExactly(int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression halfTurn = angleUnit switch
        {
            AngleUnit.Radians => Symbol("pi"),
            AngleUnit.Degrees => Number(180),
            AngleUnit.Grads => Number(200),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnitValue))
        };
        AssertOrigin(Function("cos", Add(Variable(), halfTurn)), angleUnit, expectedHasValue: true, expectedCanonical: "q:-1");
    }

    [Fact]
    public void AngleConversionCannotGrowPastTheExactCoefficientBudget()
    {
        ExactInteger denominator = ExactInteger.One << (AnalysisLimits.CoefficientBits - 1);
        InputExpression tinyAngle = InputExpression.Number(new BigRational(ExactInteger.One, denominator), Source);
        var request = new AnalysisRequest(Function("sin", Add(Variable(), tinyAngle)), AnalysisFeatures.YIntercept, AngleUnit.Degrees, "x", static () => true);
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(request.Expression);
        Assert.Throws<BudgetExceededException>(() => ExactOriginAnalyzer.TryAnalyze(request, semantic, AnalysisFeatures.YIntercept, new ResourceBudget(), out ProofOutcome<OptionalValue<ExactReal>> _));
        BigRational oversizedPiCoefficient = BigRational.One / (new BigRational(180) * new BigRational(denominator));
        var claimed = OptionalValue<ExactReal>.Some(new FunctionReal("sin", [new AffinePiReal(oversizedPiCoefficient, BigRational.Zero)]));
        string claim = ClaimCanonical.For(claimed);
        var certificate = new ExactOriginProofCertificate(semantic.Value.Canonical, claim, semantic.DefinedWhen.Canonical, AngleUnit.Degrees, ExactOriginAnalyzer.Rule);
        Assert.Throws<BudgetExceededException>(() => ExactOriginCertificateReplay.Check(request, semantic, certificate, claim, new ResourceBudget()));
        Assert.False(CertificateChecker.Check(request, semantic, ProofOutcome<OptionalValue<ExactReal>>.Proved(claimed, certificate)));
    }

    [Theory]
    [InlineData((int)AngleUnit.Radians)]
    [InlineData((int)AngleUnit.Degrees)]
    [InlineData((int)AngleUnit.Grads)]
    public void QuarterTurnsReduceExactlyAndRetainTangentPoles(int angleUnitValue)
    {
        AngleUnit angleUnit = (AngleUnit)angleUnitValue;
        InputExpression quarterTurn = angleUnit switch
        {
            AngleUnit.Radians => Divide(Symbol("pi"), Number(2)),
            AngleUnit.Degrees => Number(90),
            AngleUnit.Grads => Number(100),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnitValue))
        };
        InputExpression phase = Add(Variable(), quarterTurn);
        AssertOrigin(Function("sin", phase), angleUnit, expectedHasValue: true, expectedCanonical: "q:1");
        AssertOrigin(Function("cos", phase), angleUnit, expectedHasValue: true, expectedCanonical: "q:0");
        AssertOrigin(Function("tan", phase), angleUnit, expectedHasValue: false);
    }

    [Fact]
    public void CertificateReplayRejectsAnAngleUnitTamper()
    {
        InputExpression expression = Function("asin", Add(Variable(), Number(1)));
        ExactOriginSubstitutionCoverageTestsProducedOrigin produced = Produce(expression, AngleUnit.Radians);
        var certificate = Assert.IsType<ExactOriginProofCertificate>(produced.Outcome.Certificate);
        var changedCertificate = certificate with
        {
            AngleUnit = AngleUnit.Degrees
        };
        ProofOutcome<OptionalValue<ExactReal>> changedOutcome = ProofOutcome<OptionalValue<ExactReal>>.Proved(produced.Outcome.Value!, changedCertificate);
        Assert.False(ExactOriginCertificateReplay.Check(produced.Request, produced.Semantic, changedCertificate, ClaimCanonical.For(produced.Outcome.Value!), new ResourceBudget()));
        Assert.False(CertificateChecker.Check(produced.Request, produced.Semantic, changedOutcome));
        Assert.False(CertificateChecker.Check(produced.Request with { AngleUnit = AngleUnit.Degrees }, produced.Semantic, produced.Outcome));
    }

    [Fact]
    public void VariableExpressionsAreEvaluatedExactlyWhenUsedAsExponents()
    {
        InputExpression x = Variable();
        AssertOrigin(Power(Add(x, Number(2)), Add(x, Number(1))), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "q:2");
        AssertOrigin(Power(Add(x, Number(4)), Add(x, Number(1, 2))), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "q:2");
        AssertOrigin(Power(Add(x, Number(2)), Subtract(x, Number(1))), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "q:1/2");
    }

    [Fact]
    public void NonRationalPowerAndRootParametersRemainFormalExactValues()
    {
        InputExpression shifted = Add(Variable(), Number(2));
        InputExpression irrationalParameter = Function("sqrt", shifted);
        AssertOrigin(Power(shifted, irrationalParameter), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "fn:power(q:2,fn:sqrt(q:2))");
        AssertOrigin(Function("root", shifted, irrationalParameter), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "fn:root(q:2,fn:sqrt(q:2))");
    }

    [Fact]
    public void TotalDiscreteFunctionsRemainFormalWhenTheyCannotBeReduced()
    {
        InputExpression irrational = Function("sqrt", Add(Variable(), Number(2)));
        AssertOrigin(Function("floor", irrational), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "fn:floor(fn:sqrt(q:2))");
        AssertOrigin(Function("ceil", irrational), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "fn:ceil(fn:sqrt(q:2))");
        AssertOrigin(Function("sign", Function("floor", irrational)), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "fn:sign(fn:floor(fn:sqrt(q:2)))");
        AssertOrigin(Function("factorial", Number((long)int.MaxValue + 1)), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "fn:factorial(q:2147483648)");
    }

    [Fact]
    public void ExactFractionalPowerPreflightKeepsTheCoefficientBoundary()
    {
        Assert.True(FixedRationalPowerValue.TryComposeRational(new BigRational(4), new BigRational(16_383, 2), new ResourceBudget(), out BigRational boundary));
        Assert.Equal(AnalysisLimits.CoefficientBits, boundary.Numerator.GetBitLength());
        Assert.True(boundary.Denominator.IsOne);
        Assert.False(FixedRationalPowerValue.TryComposeRational(new BigRational(4), new BigRational(16_385, 2), new ResourceBudget(), out _));
    }

    [Fact]
    public void OversizedExactPowersRemainSymbolicWithoutAllocatingTheirExpansion()
    {
        AssertOrigin(Power(Number(4), Number(int.MaxValue, 2)), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "fn:power(q:4,q:2147483647/2)");
        AssertOrigin(Function("root", Number(2), Number(int.MaxValue)), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "fn:root(q:2,q:2147483647)");
    }

    [Fact]
    public void PiAndEParticipateInExactStructuralNormalization()
    {
        InputExpression x = Variable();
        InputExpression expression = Add(Function("sin", Add(x, Symbol("pi"))), Subtract(Symbol("e"), Function("exp", Add(x, Number(1)))));
        AssertOrigin(expression, AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "q:0");
    }

    [Fact]
    public void ExactOriginSupportsTheGeneralElementaryFunctionVocabulary()
    {
        InputExpression x = Variable();
        InputExpression expression = Sum(Function("sign", Subtract(x, Number(2))), Function("floor", Add(x, Number(3, 2))), Function("ceil", Add(x, Number(3, 2))), Function("factorial", Add(x, Number(4))), Function("log", Add(x, Number(10))), Function("ln", Add(x, Number(1))), Function("exp", x), Function("sinh", x), Function("cosh", x), Function("tanh", x), Function("root", Add(x, Number(27)), Number(3)));
        AssertOrigin(expression, AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "q:32");
    }

    [Fact]
    public void AcceptedGraphVmOperatorFunctionFormsLowerToExactOriginSemantics()
    {
        InputExpression x = Variable();
        InputExpression expression = Function("sum", Function("log10", Add(x, Number(10))), Function("power", Add(x, Number(2)), Number(2)), Function("pow", Add(x, Number(3)), Number(2)), Function("sum", x, Number(1), Number(2)), Function("product", Add(x, Number(1)), Number(2), Number(3)), Function("subtract", x, Number(2)), Function("divide", Add(x, Number(4)), Number(2)), Function("root", Add(x, Number(4))), Function("min", Add(x, Number(1)), Number(2), Number(3)), Function("max", Add(x, Number(1)), Number(2), Number(3)));
        AssertOrigin(expression, AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "q:29");
    }

    [Fact]
    public void VariadicLoweringStaysBalancedNearThePublicAstLimit()
    {
        InputExpression expression = Variable();
        long expected = 0;
        const int layers = 63;
        const int arity = 128;
        for (int layer = 0; layer < layers; layer++)
        {
            var arguments = ImmutableArray.CreateBuilder<InputExpression>(arity);
            arguments.Add(expression);
            for (int index = 1; index < arity; index++)
            {
                int value = checked(layer * (arity - 1) + index);
                expected += value;
                arguments.Add(Number(value));
            }

            expression = InputExpression.Function("sum", arguments.MoveToImmutable(), Source);
        }

        var request = new AnalysisRequest(expression, AnalysisFeatures.YIntercept, AngleUnit.Radians, "x", static () => true);
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
        Assert.InRange(MaximumValueDepth(semantic.Value), 1, 512);
        Assert.True(ExactOriginAnalyzer.TryAnalyze(request, semantic, AnalysisFeatures.YIntercept, new ResourceBudget(), out ProofOutcome<OptionalValue<ExactReal>> outcome));
        Assert.Equal($"q:{expected}", ExactRealCanonical.Format(outcome.Value!.Value!));
        Assert.True(CertificateChecker.Check(request, semantic, outcome));
    }

    [Fact]
    public void NestedExtremaPreserveExactChildFunctionEvaluationAtTheOrigin()
    {
        InputExpression x = Variable();
        InputExpression expression = Add(Function("min", Function("sqrt", Add(x, Number(1))), Number(2)), Function("sin", x));
        AssertOrigin(expression, AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "q:1");
    }

    [Fact]
    public void SquareRootValueAndNonzeroGuardsAreEvaluatedAtTheOrigin()
    {
        InputExpression x = Variable();
        AssertOrigin(Divide(Number(1), Function("sqrt", Add(x, Number(1)))), AngleUnit.Radians, expectedHasValue: true, expectedCanonical: "q:1");
        AssertOrigin(Divide(Number(1), Function("sqrt", x)), AngleUnit.Radians, expectedHasValue: false);
    }

    [Fact]
    public void ValueCancellationNeverErasesAnOriginHole()
    {
        InputExpression x = Variable();
        InputExpression squareRoot = Function("sqrt", x);
        InputExpression logarithm = Function("ln", x);
        AssertOrigin(Divide(x, x), AngleUnit.Radians, expectedHasValue: false);
        AssertOrigin(Divide(squareRoot, squareRoot), AngleUnit.Radians, expectedHasValue: false);
        AssertOrigin(Subtract(logarithm, logarithm), AngleUnit.Radians, expectedHasValue: false);
    }

    [Fact]
    public void ParentGuardsCannotCrashBeforeAChildRootGuardRejectsTheOrigin()
    {
        InputExpression x = Variable();
        InputExpression undefinedRoot = Function("root", x, Number(-1));
        AssertOrigin(Divide(Number(1), undefinedRoot), AngleUnit.Radians, expectedHasValue: false);
    }

    private static void AssertOrigin(InputExpression expression, AngleUnit angleUnit, bool expectedHasValue, string? expectedCanonical = null)
    {
        ExactOriginSubstitutionCoverageTestsProducedOrigin produced = Produce(expression, angleUnit);
        OptionalValue<ExactReal> intercept = produced.Outcome.Value!;
        Assert.Equal(expectedHasValue, intercept.HasValue);
        if (expectedCanonical is not null)
        {
            Assert.True(intercept.HasValue);
            Assert.Equal(expectedCanonical, ExactRealCanonical.Format(intercept.Value!));
        }

        var certificate = Assert.IsType<ExactOriginProofCertificate>(produced.Outcome.Certificate);
        string claim = ClaimCanonical.For(intercept);
        Assert.True(ExactOriginCertificateReplay.Check(produced.Request, produced.Semantic, certificate, claim, new ResourceBudget()));
        Assert.True(CertificateChecker.Check(produced.Request, produced.Semantic, produced.Outcome));
        AnalysisReport integrated = AnalysisEngine.Analyze(produced.Request);
        Assert.Equal(ProofState.Proved, integrated.YIntercept.State);
        Assert.Equal(claim, ClaimCanonical.For(integrated.YIntercept.Value!));
        Assert.NotNull(integrated.Expression);
        Assert.True(CertificateChecker.Check(produced.Request, integrated.Expression, integrated.YIntercept));
    }

    private static ExactOriginSubstitutionCoverageTestsProducedOrigin Produce(InputExpression expression, AngleUnit angleUnit)
    {
        var request = new AnalysisRequest(expression, AnalysisFeatures.YIntercept, angleUnit, "x", static () => true);
        SemanticExpression semantic = new SemanticGraphBuilder(new ResourceBudget()).Build(expression);
        Assert.True(ExactOriginAnalyzer.TryAnalyze(request, semantic, AnalysisFeatures.YIntercept, new ResourceBudget(), out ProofOutcome<OptionalValue<ExactReal>> outcome));
        Assert.Equal(ProofState.Proved, outcome.State);
        return new ExactOriginSubstitutionCoverageTestsProducedOrigin(request, semantic, outcome);
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

    private static InputExpression Number(long value)
    {
        return InputExpression.Number(new BigRational(value), Source);
    }

    private static InputExpression Number(int numerator, int denominator)
    {
        return InputExpression.Number(new BigRational(numerator, denominator), Source);
    }

    private static InputExpression Add(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Add, left, right, Source);
    }

    private static InputExpression Subtract(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Subtract, left, right, Source);
    }

    private static InputExpression Divide(InputExpression left, InputExpression right)
    {
        return InputExpression.Binary(InputExpressionKind.Divide, left, right, Source);
    }

    private static InputExpression Power(InputExpression basis, InputExpression exponent)
    {
        return InputExpression.Binary(InputExpressionKind.Power, basis, exponent, Source);
    }

    private static InputExpression Function(string name, params InputExpression[] arguments)
    {
        return InputExpression.Function(name, arguments.ToImmutableArray(), Source);
    }

    private static InputExpression Sum(InputExpression first, params InputExpression[] remaining)
    {
        return remaining.Aggregate(first, Add);
    }

    private static int MaximumValueDepth(ValueTerm root)
    {
        var pending = new Stack<(ValueTerm Term, int Depth)>();
        var deepest = new Dictionary<int, int>();
        pending.Push((root, 1));
        int maximum = 0;
        while (pending.TryPop(out (ValueTerm Term, int Depth) current))
        {
            if (deepest.TryGetValue(current.Term.Id, out int previous) && previous >= current.Depth)
            {
                continue;
            }

            deepest[current.Term.Id] = current.Depth;
            maximum = Math.Max(maximum, current.Depth);
            foreach (ValueTerm operand in current.Term.Operands)
            {
                pending.Push((operand, current.Depth + 1));
            }
        }

        return maximum;
    }
}
