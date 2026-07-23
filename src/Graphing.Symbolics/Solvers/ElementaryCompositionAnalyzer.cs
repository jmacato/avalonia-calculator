using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ElementaryCompositionAnalyzer
{
    private const string CertificateRule = "exact-elementary-composition-pullback-v1";
    private const string FirstParameter = "n₁";
    private const string SecondParameter = "n₂";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!TryCompute(request, expression, feature, budget, out object? value, out ElementaryCompositionPattern? pattern) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new ElementaryCompositionProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), pattern.Kind, pattern.Canonical, expression.DefinedWhen.Canonical, request.AngleUnit, CertificateRule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    private static bool TryCompute(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out object value, out ElementaryCompositionPattern pattern)
    {
        budget.Charge(12);
        if (!TryExtract(expression.Value, request.Variable, out pattern) || (pattern.Kind != ElementaryCompositionKind.ExponentialPlusIdentity && request.AngleUnit != AngleUnit.Radians) || !DefinednessMatches(expression, pattern))
        {
            value = null!;
            return false;
        }

        return pattern.Kind switch
        {
            ElementaryCompositionKind.SineOfSquareRoot => TrySquareRootSine(feature, out value),
            ElementaryCompositionKind.SineOfLogarithm => TryLogarithmSine(feature, out value),
            ElementaryCompositionKind.SineOfCommonLogarithm => TryCommonLogarithmSine(feature, out value),
            ElementaryCompositionKind.SineOfExponential => TryExponentialSine(feature, out value),
            ElementaryCompositionKind.SineOfTangent => TryTangentSine(feature, out value),
            ElementaryCompositionKind.ExponentialPlusIdentity => TryExponentialPlusIdentity(feature, out value),
            _ => Fail(out value)
        };
    }

    private static bool TryExtract(ValueTerm term, string variable, out ElementaryCompositionPattern pattern)
    {
        if (term is { Kind: ValueKind.Function, Name: "sin", Operands: [{ Kind: ValueKind.Function, Operands: [var argument] } inner] } && IsVariable(argument, variable))
        {
            ElementaryCompositionKind? kind = inner.Name switch
            {
                "sqrt" => ElementaryCompositionKind.SineOfSquareRoot,
                "ln" => ElementaryCompositionKind.SineOfLogarithm,
                "log" => ElementaryCompositionKind.SineOfCommonLogarithm,
                "exp" => ElementaryCompositionKind.SineOfExponential,
                "tan" => ElementaryCompositionKind.SineOfTangent,
                _ => null
            };
            if (kind is { } nestedKind)
            {
                pattern = new ElementaryCompositionPattern(nestedKind, $"elementary-composition:{(int)nestedKind}:{term.Canonical}");
                return true;
            }
        }

        if (term is { Kind: ValueKind.Add, Operands: [var left, var right] } && ((IsVariable(left, variable) && IsExponentialOfVariable(right, variable)) || (IsVariable(right, variable) && IsExponentialOfVariable(left, variable))))
        {
            pattern = new ElementaryCompositionPattern(ElementaryCompositionKind.ExponentialPlusIdentity, $"elementary-composition:{(int)ElementaryCompositionKind.ExponentialPlusIdentity}:{term.Canonical}");
            return true;
        }

        pattern = null!;
        return false;
    }

    private static bool IsVariable(ValueTerm term, string variable)
    {
        return term.Kind == ValueKind.Variable && string.Equals(term.Name, variable, StringComparison.Ordinal);
    }

    private static bool IsExponentialOfVariable(ValueTerm term, string variable)
    {
        return term is { Kind: ValueKind.Function, Name: "exp", Operands: [var argument] } &&
               IsVariable(argument, variable);
    }

    private static bool DefinednessMatches(SemanticExpression expression, ElementaryCompositionPattern pattern)
    {
        ValueTerm variable = VariableTerm(expression.Value, pattern.Kind);
        ValueTerm zero = ConstantTerm(BigRational.Zero, -1);
        Formula expected = pattern.Kind switch
        {
            ElementaryCompositionKind.SineOfSquareRoot => Formula.Compare(variable, Comparison.GreaterOrEqual, zero),
            ElementaryCompositionKind.SineOfLogarithm or ElementaryCompositionKind.SineOfCommonLogarithm => Formula.Compare(variable, Comparison.Greater, zero),
            ElementaryCompositionKind.SineOfTangent => Formula.Compare(FunctionTerm("cos", variable, -2), Comparison.NotEqual, zero),
            _ => Formula.True
        };
        return string.Equals(expression.DefinedWhen.Canonical, expected.Canonical, StringComparison.Ordinal);
    }

    private static ValueTerm VariableTerm(ValueTerm term, ElementaryCompositionKind kind)
    {
        if (kind == ElementaryCompositionKind.ExponentialPlusIdentity)
        {
            return term.Operands.First(static operand => operand.Kind == ValueKind.Variable);
        }

        return term.Operands[0].Operands[0];
    }

    private static ValueTerm ConstantTerm(BigRational value, int id)
    {
        return new ValueTerm(id, ValueKind.Constant, value, string.Empty, [], "q:" + value);
    }

    private static ValueTerm FunctionTerm(string function, ValueTerm argument, int id)
    {
        return new ValueTerm(id, ValueKind.Function, default, function, [argument],
            $"{(int)ValueKind.Function}:{function}({argument.Canonical})");
    }

    private static bool TrySquareRootSine(AnalysisFeatures feature, out object value)
    {
        value = feature switch
        {
            AnalysisFeatures.Domain => NonnegativeReals(),
            AnalysisFeatures.Range => UnitRange(),
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => LatticeSet("(πn₁)^2", [FirstParameter], [Integer(FirstParameter), LowerBound(FirstParameter, 0)]),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(Rational(BigRational.Zero)),
            AnalysisFeatures.Minima => ImmutableArray.Create<FeaturePoint>(SingletonPoint(BigRational.Zero, BigRational.Zero), LatticePoint("(2πn₁ + 3π/2)^2", BigRational.MinusOne, [FirstParameter], [Integer(FirstParameter), LowerBound(FirstParameter, 0)])),
            AnalysisFeatures.Maxima => ImmutableArray.Create<FeaturePoint>(LatticePoint("(2πn₁ + π/2)^2", BigRational.One, [FirstParameter], [Integer(FirstParameter), LowerBound(FirstParameter, 0)])),
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => SinePullbackMonotonicity("x ≥ 0 ∧ cos(sqrt(x)) > 0", "x > 0 ∧ cos(sqrt(x)) < 0"),
            AnalysisFeatures.Period => NotPeriodic(),
            _ => null!
        };
        return value is not null;
    }

    private static bool TryLogarithmSine(AnalysisFeatures feature, out object value)
    {
        value = feature switch
        {
            AnalysisFeatures.Domain => PositiveReals(),
            AnalysisFeatures.Range => UnitRange(),
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => LatticeSet("e^(πn₁)", [FirstParameter], [Integer(FirstParameter)]),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima => ImmutableArray.Create<FeaturePoint>(LatticePoint("e^(2πn₁ + 3π/2)", BigRational.MinusOne, [FirstParameter], [Integer(FirstParameter)])),
            AnalysisFeatures.Maxima => ImmutableArray.Create<FeaturePoint>(LatticePoint("e^(2πn₁ + π/2)", BigRational.One, [FirstParameter], [Integer(FirstParameter)])),
            AnalysisFeatures.InflectionPoints => LogarithmSineInflections(),
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => SinePullbackMonotonicity("x > 0 ∧ cos(ln(x)) > 0", "x > 0 ∧ cos(ln(x)) < 0"),
            AnalysisFeatures.Period => NotPeriodic(),
            _ => null!
        };
        return value is not null;
    }

    private static ImmutableArray<FeaturePoint> LogarithmSineInflections()
    {
        ExactReal sqrtTwoOverTwo = ExactRealArithmetic.Scale(new FunctionReal("sqrt", [Rational(new BigRational(2))]), new BigRational(1, 2));
        return [LatticePoint("e^(2πn₁ + 3π/4)", sqrtTwoOverTwo, [FirstParameter], [Integer(FirstParameter)]), LatticePoint("e^(2πn₁ + 7π/4)", ExactRealArithmetic.Negate(sqrtTwoOverTwo), [FirstParameter], [Integer(FirstParameter)])];
    }

    private static bool TryCommonLogarithmSine(AnalysisFeatures feature, out object value)
    {
        value = feature switch
        {
            AnalysisFeatures.Domain => PositiveReals(),
            AnalysisFeatures.Range => UnitRange(),
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => LatticeSet("10^(πn₁)", [FirstParameter], [Integer(FirstParameter)]),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima => ImmutableArray.Create<FeaturePoint>(LatticePoint("10^(2πn₁ + 3π/2)", BigRational.MinusOne, [FirstParameter], [Integer(FirstParameter)])),
            AnalysisFeatures.Maxima => ImmutableArray.Create<FeaturePoint>(LatticePoint("10^(2πn₁ + π/2)", BigRational.One, [FirstParameter], [Integer(FirstParameter)])),
            AnalysisFeatures.InflectionPoints => CommonLogarithmSineInflections(),
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => SinePullbackMonotonicity("x > 0 ∧ cos(log(x)) > 0", "x > 0 ∧ cos(log(x)) < 0"),
            AnalysisFeatures.Period => NotPeriodic(),
            _ => null!
        };
        return value is not null;
    }

    private static ImmutableArray<FeaturePoint> CommonLogarithmSineInflections()
    {
        ExactReal logarithmOfTen = new FunctionReal("ln", [Rational(new BigRational(10))]);
        ExactReal magnitude = ExactRealArithmetic.Divide(logarithmOfTen, new FunctionReal("sqrt", [ExactRealArithmetic.AddRational(ExactRealArithmetic.Power(logarithmOfTen, 2), BigRational.One)]));
        return [LatticePoint("10^(2πn₁ − arctan(ln(10)))", ExactRealArithmetic.Negate(magnitude), [FirstParameter], [Integer(FirstParameter)]), LatticePoint("10^(2πn₁ + π − arctan(ln(10)))", magnitude, [FirstParameter], [Integer(FirstParameter)])];
    }

    private static bool TryExponentialSine(AnalysisFeatures feature, out object value)
    {
        value = feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range => UnitRange(),
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => LatticeSet("ln(πn₁)", [FirstParameter], [Integer(FirstParameter), LowerBound(FirstParameter, 1)]),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(new FunctionReal("sin", [Rational(BigRational.One)])),
            AnalysisFeatures.Minima => ImmutableArray.Create<FeaturePoint>(LatticePoint("ln(2πn₁ + 3π/2)", BigRational.MinusOne, [FirstParameter], [Integer(FirstParameter), LowerBound(FirstParameter, 0)])),
            AnalysisFeatures.Maxima => ImmutableArray.Create<FeaturePoint>(LatticePoint("ln(2πn₁ + π/2)", BigRational.One, [FirstParameter], [Integer(FirstParameter), LowerBound(FirstParameter, 0)])),
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray.Create(Horizontal(BigRational.Zero)),
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => SinePullbackMonotonicity("cos(exp(x)) > 0", "cos(exp(x)) < 0"),
            AnalysisFeatures.Period => NotPeriodic(),
            _ => null!
        };
        return value is not null;
    }

    private static bool TryTangentSine(AnalysisFeatures feature, out object value)
    {
        value = feature switch
        {
            AnalysisFeatures.Domain => TangentDomain(),
            AnalysisFeatures.Range => UnitRange(),
            AnalysisFeatures.Parity => FunctionParity.Odd,
            AnalysisFeatures.Zeros => LatticeSet("arctan(πn₁) + πn₂", [FirstParameter, SecondParameter], [Integer(FirstParameter), Integer(SecondParameter)]),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(Rational(BigRational.Zero)),
            AnalysisFeatures.Minima => ImmutableArray.Create<FeaturePoint>(LatticePoint("arctan(2πn₁ + 3π/2) + πn₂", BigRational.MinusOne, [FirstParameter, SecondParameter], [Integer(FirstParameter), Integer(SecondParameter)])),
            AnalysisFeatures.Maxima => ImmutableArray.Create<FeaturePoint>(LatticePoint("arctan(2πn₁ + π/2) + πn₂", BigRational.One, [FirstParameter, SecondParameter], [Integer(FirstParameter), Integer(SecondParameter)])),
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => SinePullbackMonotonicity("cos(x) ≠ 0 ∧ cos(tan(x)) > 0", "cos(x) ≠ 0 ∧ cos(tan(x)) < 0"),
            AnalysisFeatures.Period => PeriodPi(),
            _ => null!
        };
        return value is not null;
    }

    private static bool TryExponentialPlusIdentity(AnalysisFeatures feature, out object value)
    {
        ExactReal zero = Rational(BigRational.Zero);
        value = feature switch
        {
            AnalysisFeatures.Domain or AnalysisFeatures.Range => AllRealSet.Instance,
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => RealSets.Points([ExactRealArithmetic.Negate(new FunctionReal("lambertw", [Rational(BigRational.One)]))]),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(Rational(BigRational.One)),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Oblique, new SingletonReal(zero), Rational(BigRational.One), zero)),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(AllRealSet.Instance, Monotonicity.Increasing)),
            AnalysisFeatures.Period => NotPeriodic(),
            _ => null!
        };
        return value is not null;
    }

    private static ImmutableArray<MonotoneRegion> SinePullbackMonotonicity(string increasingPredicate, string decreasingPredicate)
    {
        return
        [
            new MonotoneRegion(new ComprehensionSet("x", increasingPredicate), Monotonicity.Increasing),
            new MonotoneRegion(new ComprehensionSet("x", decreasingPredicate), Monotonicity.Decreasing)
        ];
    }

    private static IntervalSet NonnegativeReals()
    {
        return new IntervalSet(RealBound.Finite(Rational(BigRational.Zero)), true, RealBound.PositiveInfinity, false);
    }

    private static IntervalSet PositiveReals()
    {
        return new IntervalSet(RealBound.Finite(Rational(BigRational.Zero)), false, RealBound.PositiveInfinity, false);
    }

    private static IntervalSet UnitRange()
    {
        return new IntervalSet(RealBound.Finite(Rational(BigRational.MinusOne)), true,
            RealBound.Finite(Rational(BigRational.One)), true);
    }

    private static PeriodicIntervalSet TangentDomain()
    {
        return new PeriodicIntervalSet(new AffinePiReal(BigRational.One, BigRational.Zero), "m",
            IntegerConstraint.All("m"),
            [
                new PeriodicInterval(new AffinePiReal(new BigRational(-1, 2), BigRational.Zero), false,
                    new AffinePiReal(new BigRational(1, 2), BigRational.Zero), false)
            ]);
    }

    private static IntegerLatticeSet LatticeSet(string expression, ImmutableArray<string> parameters, ImmutableArray<string> predicates)
    {
        return new IntegerLatticeSet(expression, parameters, predicates);
    }

    private static ConstantYFeaturePoint SingletonPoint(BigRational x, BigRational y)
    {
        return new ConstantYFeaturePoint(new SingletonReal(Rational(x)), Rational(y));
    }

    private static ConstantYFeaturePoint LatticePoint(string expression, BigRational y, ImmutableArray<string> parameters, ImmutableArray<string> predicates)
    {
        return LatticePoint(expression, Rational(y), parameters, predicates);
    }

    private static ConstantYFeaturePoint LatticePoint(string expression, ExactReal y, ImmutableArray<string> parameters, ImmutableArray<string> predicates)
    {
        return new ConstantYFeaturePoint(new LatticeReal(expression, parameters, predicates), y);
    }

    private static string Integer(string parameter)
    {
        return $"{parameter} ∈ ℤ";
    }

    private static string LowerBound(string parameter, int bound)
    {
        return $"{parameter} ≥ {bound}";
    }

    private static Asymptote Horizontal(BigRational value)
    {
        return new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(Rational(value)), null, Rational(value));
    }

    private static Periodicity NotPeriodic()
    {
        return new Periodicity(PeriodicityKind.NotPeriodic, null);
    }

    private static Periodicity PeriodPi()
    {
        return new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, new AffinePiReal(BigRational.One, BigRational.Zero));
    }

    private static RationalReal Rational(BigRational value)
    {
        return new RationalReal(value);
    }

    private static bool Fail(out object value)
    {
        value = null!;
        return false;
    }
}
