using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Independently replays certificates for the finite elementary-composition
/// portfolio. The replay recognizes the semantic source chain, proves its
/// exact regularity guard, and reconstructs the claim without invoking the
/// producer's recognizer or feature dispatcher.
/// </summary>
internal static class ElementaryCompositionCertificateReplay
{
    private const string CertificateRule = "exact-elementary-composition-pullback-v1";
    private const string FirstParameter = "n₁";
    private const string SecondParameter = "n₂";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, ElementaryCompositionProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge(4);
        if (certificate.Rule != CertificateRule || certificate.ProvenFeature != certificate.Feature || !CertificateFeatureBinding.IsSingleRequested(request, certificate.Feature) || certificate.AngleUnit != request.AngleUnit || request.AngleUnit is not (AngleUnit.Radians or AngleUnit.Degrees or AngleUnit.Grads) || !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !TryRecognize(request, expression, budget, out ElementaryCompositionCertificateReplayReplayPattern pattern) || pattern.Kind != certificate.Kind || !string.Equals(pattern.Canonical, certificate.PatternCanonical, StringComparison.Ordinal) || !TryReconstructClaim(pattern.Kind, certificate.Feature, budget, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryRecognize(AnalysisRequest request, SemanticExpression expression, ResourceBudget budget, out ElementaryCompositionCertificateReplayReplayPattern pattern)
    {
        budget.Charge(4);
        if (TryRecognizeNestedSine(expression, request.Variable, budget, out ElementaryCompositionKind nestedKind))
        {
            if (request.AngleUnit != AngleUnit.Radians)
            {
                pattern = default;
                return false;
            }

            pattern = CreatePattern(nestedKind, expression.Value);
            return true;
        }

        if (TryRecognizeExponentialPlusIdentity(expression, request.Variable, budget))
        {
            pattern = CreatePattern(ElementaryCompositionKind.ExponentialPlusIdentity, expression.Value);
            return true;
        }

        pattern = default;
        return false;
    }

    private static bool TryRecognizeNestedSine(SemanticExpression expression, string variable, ResourceBudget budget, out ElementaryCompositionKind kind)
    {
        budget.Charge(4);
        if (expression.RewriteHistory.Length != 0 || expression.SourceOperands is not [var innerExpression] || expression.Value is not { Kind: ValueKind.Function, Name: "sin", Operands: [var innerValue] } || innerExpression.RewriteHistory.Length != 0 || innerExpression.SourceOperands is not [var argumentExpression] || innerExpression.Value is not { Kind: ValueKind.Function, Operands: [var argumentValue] } || !string.Equals(innerValue.Canonical, innerExpression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(argumentValue.Canonical, argumentExpression.Value.Canonical, StringComparison.Ordinal) || !IsBareVariable(argumentExpression, variable))
        {
            kind = default;
            return false;
        }

        kind = innerExpression.Value.Name switch
        {
            "sqrt" => ElementaryCompositionKind.SineOfSquareRoot,
            "ln" => ElementaryCompositionKind.SineOfLogarithm,
            "log" => ElementaryCompositionKind.SineOfCommonLogarithm,
            "exp" => ElementaryCompositionKind.SineOfExponential,
            "tan" => ElementaryCompositionKind.SineOfTangent,
            _ => (ElementaryCompositionKind)(-1)
        };
        if ((int)kind < 0 || !TryBuildNestedRegularity(innerExpression.Value.Name, argumentExpression.Value, out Formula defined, out Formula continuous, out Formula differentiable))
        {
            kind = default;
            return false;
        }

        return RegularityMatches(innerExpression, defined, continuous, differentiable) && RegularityMatches(expression, defined, continuous, differentiable);
    }

    private static bool TryRecognizeExponentialPlusIdentity(SemanticExpression expression, string variable, ResourceBudget budget)
    {
        budget.Charge(4);
        if (expression.RewriteHistory.Length != 0 || expression.SourceOperands is not [var leftExpression, var rightExpression] || expression.Value is not { Kind: ValueKind.Add, Operands: [var leftValue, var rightValue] } || !string.Equals(leftValue.Canonical, leftExpression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(rightValue.Canonical, rightExpression.Value.Canonical, StringComparison.Ordinal))
        {
            return false;
        }

        bool recognized = IsBareVariable(leftExpression, variable) && IsBareExponential(rightExpression, variable) || IsBareExponential(leftExpression, variable) && IsBareVariable(rightExpression, variable);
        return recognized && RegularityMatches(expression, Formula.True, Formula.True, Formula.True);
    }

    private static bool IsBareVariable(SemanticExpression expression, string variable)
    {
        return expression.Value.Kind == ValueKind.Variable &&
               string.Equals(expression.Value.Name, variable, StringComparison.Ordinal) &&
               expression.SourceOperands.Length == 0 && expression.RewriteHistory.Length == 0 &&
               RegularityMatches(expression, Formula.True, Formula.True, Formula.True);
    }

    private static bool IsBareExponential(SemanticExpression expression, string variable)
    {
        return expression.RewriteHistory.Length == 0 && expression.SourceOperands is [var argumentExpression] &&
               expression.Value is { Kind: ValueKind.Function, Name: "exp", Operands: [var argumentValue] } &&
               string.Equals(argumentValue.Canonical, argumentExpression.Value.Canonical, StringComparison.Ordinal) &&
               IsBareVariable(argumentExpression, variable) &&
               RegularityMatches(expression, Formula.True, Formula.True, Formula.True);
    }

    private static bool TryBuildNestedRegularity(string innerFunction, ValueTerm variable, out Formula defined, out Formula continuous, out Formula differentiable)
    {
        ValueTerm zero = ConstantTerm(BigRational.Zero, -1);
        switch (innerFunction)
        {
            case "sqrt":
                defined = Formula.Compare(variable, Comparison.GreaterOrEqual, zero);
                continuous = defined;
                differentiable = Formula.Compare(variable, Comparison.Greater, zero);
                return true;
            case "ln":
            case "log":
                defined = Formula.Compare(variable, Comparison.Greater, zero);
                continuous = defined;
                differentiable = defined;
                return true;
            case "exp":
                defined = Formula.True;
                continuous = Formula.True;
                differentiable = Formula.True;
                return true;
            case "tan":
                defined = Formula.Compare(FunctionTerm("cos", variable, -2), Comparison.NotEqual, zero);
                continuous = defined;
                differentiable = defined;
                return true;
            default:
                defined = null!;
                continuous = null!;
                differentiable = null!;
                return false;
        }
    }

    private static bool RegularityMatches(SemanticExpression expression, Formula defined, Formula continuous, Formula differentiable)
    {
        return CanonicalEquals(expression.DefinedWhen, defined) &&
               CanonicalEquals(expression.ContinuousWhen, continuous) &&
               CanonicalEquals(expression.DifferentiableWhen, differentiable);
    }

    private static bool CanonicalEquals(Formula left, Formula right)
    {
        return string.Equals(left.Canonical, right.Canonical, StringComparison.Ordinal);
    }

    private static ElementaryCompositionCertificateReplayReplayPattern CreatePattern(ElementaryCompositionKind kind, ValueTerm value)
    {
        return new ElementaryCompositionCertificateReplayReplayPattern(kind, $"elementary-composition:{(int)kind}:{value.Canonical}");
    }

    private static bool TryReconstructClaim(ElementaryCompositionKind kind, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge(8);
        return kind switch
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

    private static ValueTerm ConstantTerm(BigRational value, int id)
    {
        return new ValueTerm(id, ValueKind.Constant, value, string.Empty, [], "q:" + value);
    }

    private static ValueTerm FunctionTerm(string function, ValueTerm argument, int id)
    {
        return new ValueTerm(id, ValueKind.Function, default, function, [argument],
            $"{(int)ValueKind.Function}:{function}({argument.Canonical})");
    }

    private static bool Fail(out object value)
    {
        value = null!;
        return false;
    }
}
