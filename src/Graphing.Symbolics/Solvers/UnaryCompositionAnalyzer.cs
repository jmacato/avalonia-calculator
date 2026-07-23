using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class UnaryCompositionAnalyzer
{
    private const string CertificateRule = "exact-unary-composition-cells-v1";
    private const string Parameter = "m";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!TryCompute(request, expression, feature, budget, out object? value, out UnaryCompositionPattern? pattern) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new UnaryCompositionProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), pattern.Kind, pattern.OuterFunction, pattern.InnerFunction, pattern.Canonical, expression.DefinedWhen.Canonical, CertificateRule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    private static bool TryCompute(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out object value, out UnaryCompositionPattern pattern)
    {
        budget.Charge(8);
        if (!TryExtractPattern(expression, request.Variable, budget, out pattern))
        {
            value = null!;
            return false;
        }

        budget.CheckCoefficient(pattern.Frequency);
        budget.CheckCoefficient(pattern.Phase);
        RealSet domain = Domain(pattern, request.AngleUnit);
        if (!DefinednessMatches(expression, pattern, request, domain, budget))
        {
            value = null!;
            return false;
        }

        return pattern.Kind switch
        {
            UnaryCompositionKind.GuardedInverseIdentity => TryComputeIdentity(pattern, feature, domain, out value),
            UnaryCompositionKind.PrincipalInverseOfAffineTrigonometric or UnaryCompositionKind.PrimitiveOfAffineTrigonometric => TryComputePeriodic(pattern, request.AngleUnit, feature, domain, out value),
            UnaryCompositionKind.PrimitiveOfAbsoluteAffine => TryComputeAbsoluteLogarithm(pattern, feature, domain, budget, out value),
            _ => Fail(out value)
        };
    }

    private static bool TryExtractPattern(SemanticExpression expression, string variable, ResourceBudget budget, out UnaryCompositionPattern pattern)
    {
        if (TryExtractGuardedIdentity(expression, variable, budget, out pattern))
        {
            return true;
        }

        ValueTerm term = expression.Value;
        if (TryExtractAbsoluteLogarithm(term, variable, budget, out pattern))
        {
            return true;
        }

        if (term is not { Kind: ValueKind.Function, Operands.Length: 1 } || term.Operands[0] is not { Kind: ValueKind.Function, Operands.Length: 1 } inner || !TryAffineArgument(inner.Operands[0], variable, budget, out BigRational slope, out BigRational intercept))
        {
            pattern = null!;
            return false;
        }

        UnaryCompositionKind kind;
        if ((term.Name, inner.Name) is ("asin", "sin") or ("acos", "cos") or ("atan", "tan"))
        {
            kind = UnaryCompositionKind.PrincipalInverseOfAffineTrigonometric;
        }
        else if (inner.Name == "sin" && term.Name is "exp" or "sqrt" or "ln" or "tan" or "sin" or "cos")
        {
            kind = UnaryCompositionKind.PrimitiveOfAffineTrigonometric;
        }
        else
        {
            pattern = null!;
            return false;
        }

        string outer = term.Name;
        int innerSign = 1;
        if (slope.Sign < 0)
        {
            slope = -slope;
            intercept = -intercept;
            if (inner.Name is "sin" or "tan")
            {
                innerSign = -1;
            }
        }

        pattern = new UnaryCompositionPattern(kind, outer, inner.Name, slope, intercept, innerSign, string.Empty);
        return true;
    }

    private static bool TryExtractAbsoluteLogarithm(ValueTerm term, string variable, ResourceBudget budget, out UnaryCompositionPattern pattern)
    {
        if (term is not { Kind: ValueKind.Function, Name: "ln" or "log", Operands: [{ Kind: ValueKind.Function, Name: "abs", Operands.Length: 1 } absolute] } || !TryAffineArgument(absolute.Operands[0], variable, budget, out BigRational slope, out BigRational intercept))
        {
            pattern = null!;
            return false;
        }

        if (slope.Sign < 0)
        {
            slope = -slope;
            intercept = -intercept;
        }

        pattern = new UnaryCompositionPattern(UnaryCompositionKind.PrimitiveOfAbsoluteAffine, term.Name, "abs", slope, intercept, 1, string.Empty);
        return true;
    }

    private static bool TryExtractGuardedIdentity(SemanticExpression expression, string variable, ResourceBudget budget, out UnaryCompositionPattern pattern)
    {
        RewriteStep? identity = null;
        foreach (RewriteStep rewrite in expression.RewriteHistory)
        {
            if (rewrite.Rule is "sine-arcsine-identity" or "cosine-arccosine-identity" or "tangent-arctangent-identity" or "exponential-logarithm-identity" or "logarithm-exponential-identity")
            {
                if (identity is not null)
                {
                    pattern = null!;
                    return false;
                }

                identity = rewrite;
            }
        }

        if (identity is null || !TryAffineArgument(expression.Value, variable, budget, out BigRational slope, out BigRational intercept))
        {
            pattern = null!;
            return false;
        }

        (string outer, string inner) = identity.Value.Rule switch
        {
            "sine-arcsine-identity" => ("sin", "asin"),
            "cosine-arccosine-identity" => ("cos", "acos"),
            "tangent-arctangent-identity" => ("tan", "atan"),
            "exponential-logarithm-identity" => ("exp", "ln"),
            "logarithm-exponential-identity" => ("ln", "exp"),
            _ => throw new InvalidOperationException()
        };
        int sign = 1;
        if (slope.Sign < 0)
        {
            slope = -slope;
            intercept = -intercept;
            sign = -1;
        }

        pattern = new UnaryCompositionPattern(UnaryCompositionKind.GuardedInverseIdentity, outer, inner, slope, intercept, sign, identity.Value.Rule);
        return true;
    }

    private static bool TryAffineArgument(ValueTerm term, string variable, ResourceBudget budget, out BigRational slope, out BigRational intercept)
    {
        if (!RationalFunctionExtractor.TryExtract(term, variable, budget, out RationalExtraction extraction) || extraction.Function.Denominator.Degree != 0 || extraction.Function.Numerator.Degree != 1)
        {
            slope = default;
            intercept = default;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        slope = extraction.Function.Numerator[1] / denominator;
        intercept = extraction.Function.Numerator[0] / denominator;
        return !slope.IsZero;
    }

    private static bool DefinednessMatches(SemanticExpression expression, UnaryCompositionPattern pattern, AnalysisRequest request, RealSet expectedDomain, ResourceBudget budget)
    {
        if (pattern.Kind == UnaryCompositionKind.GuardedInverseIdentity)
        {
            return TrigonometricAndLatticeAnalyzer.DomainMatches(expression, request.Variable, request.AngleUnit, expectedDomain, budget);
        }

        if (pattern.OuterFunction is not ("sqrt" or "ln" or "log" or "tan" or "asin" or "acos" or "atan"))
        {
            return string.Equals(expression.DefinedWhen.Canonical, Formula.True.Canonical, StringComparison.Ordinal);
        }

        ValueTerm inner = expression.Value.Operands[0];
        ValueTerm zero = ConstantTerm(BigRational.Zero, -1);
        Formula expected;
        if (pattern.OuterFunction == "sqrt")
        {
            expected = Formula.Compare(inner, Comparison.GreaterOrEqual, zero);
        }
        else if (pattern.OuterFunction is "ln" or "log")
        {
            expected = Formula.Compare(inner, Comparison.Greater, zero);
        }
        else if (pattern.OuterFunction is "asin" or "acos")
        {
            expected = Formula.And(Formula.Compare(inner, Comparison.GreaterOrEqual, ConstantTerm(BigRational.MinusOne, -2)), Formula.Compare(inner, Comparison.LessOrEqual, ConstantTerm(BigRational.One, -3)));
        }
        else
        {
            ValueTerm cosineArgument = pattern.OuterFunction == "atan" ? inner.Operands[0] : inner;
            ValueTerm cosine = FunctionTerm("cos", cosineArgument, -4);
            expected = Formula.Compare(cosine, Comparison.NotEqual, zero);
        }

        return string.Equals(expression.DefinedWhen.Canonical, expected.Canonical, StringComparison.Ordinal);
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

    private static RealSet Domain(UnaryCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Kind == UnaryCompositionKind.GuardedInverseIdentity)
        {
            return IdentityDomain(pattern);
        }

        if (pattern.Kind == UnaryCompositionKind.PrimitiveOfAbsoluteAffine)
        {
            return new DifferenceSet(AllRealSet.Instance, RealSets.Points([Center(pattern)]));
        }

        if (pattern.OuterFunction is "sqrt" or "ln")
        {
            bool closed = pattern.OuterFunction == "sqrt";
            BigRational lowerFraction = pattern.InnerSign > 0 ? BigRational.Zero : BigRational.One;
            BigRational upperFraction = lowerFraction + BigRational.One;
            return PeriodicIntervals(pattern, angleUnit, lowerFraction, closed, upperFraction, closed, 2);
        }

        if (pattern.OuterFunction == "atan" && pattern.InnerFunction == "tan")
        {
            return PeriodicIntervals(pattern, angleUnit, new BigRational(-1, 2), false, new BigRational(1, 2), false, 1);
        }

        return AllRealSet.Instance;
    }

    private static bool TryComputeAbsoluteLogarithm(UnaryCompositionPattern pattern, AnalysisFeatures feature, RealSet domain, ResourceBudget budget, out object value)
    {
        ExactReal center = Center(pattern);
        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => AllRealSet.Instance,
            AnalysisFeatures.Parity => pattern.Phase.IsZero ? FunctionParity.Even : FunctionParity.Neither,
            AnalysisFeatures.Zeros => RealSets.Points([Rational((-BigRational.One - pattern.Phase) / pattern.Frequency), Rational((BigRational.One - pattern.Phase) / pattern.Frequency)]),
            AnalysisFeatures.YIntercept => pattern.Phase.IsZero ? OptionalValue<ExactReal>.None : OptionalValue<ExactReal>.Some(LogAbsoluteRational(pattern.Phase, pattern.OuterFunction, budget)),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Vertical, new SingletonReal(center), null, null)),
            AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(center), false), Monotonicity.Decreasing), new MonotoneRegion(new IntervalSet(RealBound.Finite(center), false, RealBound.PositiveInfinity, false), Monotonicity.Increasing)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static ExactReal LogAbsoluteRational(BigRational value, string logarithm, ResourceBudget budget)
    {
        BigRational absolute = value.Abs();
        if (absolute.IsOne)
        {
            return Rational(BigRational.Zero);
        }

        if (logarithm == "log" && TryExactCommonLogarithm(absolute, budget, out BigRational exponent))
        {
            return Rational(exponent);
        }

        return new FunctionReal(logarithm, [Rational(absolute)]);
    }

    private static bool TryExactCommonLogarithm(BigRational positive, ResourceBudget budget, out BigRational exponent)
    {
        if (TryPowerOfTen(positive.Numerator, budget, out int numeratorExponent) && TryPowerOfTen(positive.Denominator, budget, out int denominatorExponent))
        {
            exponent = new BigRational(numeratorExponent - denominatorExponent);
            return true;
        }

        exponent = default;
        return false;
    }

    private static bool TryPowerOfTen(ExactInteger value, ResourceBudget budget, out int exponent)
    {
        exponent = 0;
        while (value > ExactInteger.One)
        {
            budget.Charge(1);
            value = ExactInteger.DivRem(value, 10, out ExactInteger remainder);
            if (!remainder.IsZero)
            {
                exponent = 0;
                return false;
            }

            exponent = checked(exponent + 1);
        }

        return value.IsOne;
    }

    private static RealSet IdentityDomain(UnaryCompositionPattern pattern)
    {
        if (pattern.InnerFunction is "asin" or "acos")
        {
            ExactReal lower = SolveAffineValue(pattern, BigRational.MinusOne);
            ExactReal upper = SolveAffineValue(pattern, BigRational.One);
            return OrderedInterval(lower, true, upper, true);
        }

        if (pattern.InnerFunction == "ln")
        {
            ExactReal boundary = Center(pattern);
            return pattern.InnerSign > 0 ? new IntervalSet(RealBound.Finite(boundary), false, RealBound.PositiveInfinity, false) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(boundary), false);
        }

        return AllRealSet.Instance;
    }

    private static bool TryComputeIdentity(UnaryCompositionPattern pattern, AnalysisFeatures feature, RealSet domain, out object value)
    {
        bool bounded = pattern.InnerFunction is "asin" or "acos";
        bool positiveDomain = pattern.InnerFunction == "ln";
        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => bounded ? ClosedInterval(Rational(BigRational.MinusOne), Rational(BigRational.One)) : positiveDomain ? new IntervalSet(RealBound.Finite(Rational(BigRational.Zero)), false, RealBound.PositiveInfinity, false) : AllRealSet.Instance,
            AnalysisFeatures.Parity => positiveDomain ? FunctionParity.Neither : pattern.Phase.IsZero ? FunctionParity.Odd : FunctionParity.Neither,
            AnalysisFeatures.Zeros => positiveDomain ? EmptySet.Instance : RealSets.Points([Center(pattern)]),
            AnalysisFeatures.YIntercept => IdentityYIntercept(pattern, domain),
            AnalysisFeatures.Minima => bounded ? [SingletonFeature(SolveAffineValue(pattern, BigRational.MinusOne), Rational(BigRational.MinusOne))] : ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.Maxima => bounded ? [SingletonFeature(SolveAffineValue(pattern, BigRational.One), Rational(BigRational.One))] : ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.ObliqueAsymptotes => IdentityObliqueAsymptotes(pattern, bounded),
            AnalysisFeatures.Monotonicity => IdentityMonotonicity(pattern, domain),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static OptionalValue<ExactReal> IdentityYIntercept(UnaryCompositionPattern pattern, RealSet domain)
    {
        BigRational atZero = pattern.InnerSign * pattern.Phase;
        if (pattern.InnerFunction is "asin" or "acos" && (atZero < BigRational.MinusOne || atZero > BigRational.One))
        {
            return OptionalValue<ExactReal>.None;
        }

        if (pattern.InnerFunction == "ln" && atZero <= BigRational.Zero)
        {
            return OptionalValue<ExactReal>.None;
        }

        _ = domain;
        return OptionalValue<ExactReal>.Some(Rational(atZero));
    }

    private static ImmutableArray<Asymptote> IdentityObliqueAsymptotes(UnaryCompositionPattern pattern, bool bounded)
    {
        if (bounded)
        {
            return [];
        }

        BigRational slope = pattern.InnerSign * pattern.Frequency;
        BigRational intercept = pattern.InnerSign * pattern.Phase;
        ExactReal interceptReal = Rational(intercept);
        return [new Asymptote(AsymptoteOrientation.Oblique, new SingletonReal(interceptReal), Rational(slope), interceptReal)];
    }

    private static ImmutableArray<MonotoneRegion> IdentityMonotonicity(UnaryCompositionPattern pattern, RealSet domain)
    {
        RealSet region = domain switch
        {
            IntervalSet interval => interval with
            {
                IncludesLower = false,
                IncludesUpper = false
            },
            _ => domain
        };
        return [new MonotoneRegion(region, pattern.InnerSign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing)];
    }

    private static bool TryComputePeriodic(UnaryCompositionPattern pattern, AngleUnit angleUnit, AnalysisFeatures feature, RealSet domain, out object value)
    {
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = domain;
                return true;
            case AnalysisFeatures.Range:
                value = PeriodicRange(pattern, angleUnit);
                return true;
            case AnalysisFeatures.Parity:
                value = PeriodicParity(pattern, angleUnit);
                return true;
            case AnalysisFeatures.Zeros:
                value = PeriodicZeros(pattern, angleUnit);
                return true;
            case AnalysisFeatures.YIntercept:
                return TryPeriodicYIntercept(pattern, angleUnit, out value);
            case AnalysisFeatures.Minima:
                value = PeriodicExtrema(pattern, angleUnit, minimum: true);
                return true;
            case AnalysisFeatures.Maxima:
                value = PeriodicExtrema(pattern, angleUnit, minimum: false);
                return true;
            case AnalysisFeatures.InflectionPoints:
                return TryPeriodicInflections(pattern, angleUnit, out value);
            case AnalysisFeatures.VerticalAsymptotes:
                value = PeriodicVerticalAsymptotes(pattern, angleUnit);
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.Monotonicity:
                value = PeriodicMonotonicity(pattern, angleUnit);
                return true;
            case AnalysisFeatures.Period:
                value = PeriodicPeriod(pattern, angleUnit);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static IntervalSet PeriodicRange(UnaryCompositionPattern pattern, AngleUnit angleUnit)
    {
        ExactReal minusOne = Rational(BigRational.MinusOne);
        ExactReal zero = Rational(BigRational.Zero);
        ExactReal one = Rational(BigRational.One);
        return pattern.OuterFunction switch
        {
            "asin" => ClosedInterval(Angle(angleUnit, new BigRational(-1, 2)), Angle(angleUnit, new BigRational(1, 2))),
            "acos" => ClosedInterval(zero, Angle(angleUnit, BigRational.One)),
            "atan" => new IntervalSet(RealBound.Finite(Angle(angleUnit, new BigRational(-1, 2))), false, RealBound.Finite(Angle(angleUnit, new BigRational(1, 2))), false),
            "exp" => ClosedInterval(ExpMinusOne(), new NamedReal("e")),
            "sqrt" => ClosedInterval(zero, one),
            "ln" => new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(zero), true),
            "tan" => ClosedInterval(ExactRealArithmetic.Negate(FunctionAtOne("tan", angleUnit)), FunctionAtOne("tan", angleUnit)),
            "sin" => ClosedInterval(ExactRealArithmetic.Negate(FunctionAtOne("sin", angleUnit)), FunctionAtOne("sin", angleUnit)),
            "cos" => ClosedInterval(FunctionAtOne("cos", angleUnit), one),
            _ => throw new InvalidOperationException()
        };
    }

    private static FunctionParity PeriodicParity(UnaryCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (!TryQuarterTurnMultiple(pattern.Phase, angleUnit, out bool oddQuarter))
        {
            return FunctionParity.Neither;
        }

        if (pattern.InnerFunction == "cos")
        {
            return oddQuarter ? FunctionParity.Neither : FunctionParity.Even;
        }

        if (pattern.InnerFunction == "tan")
        {
            return FunctionParity.Odd;
        }

        if (oddQuarter)
        {
            return FunctionParity.Even;
        }

        return pattern.OuterFunction switch
        {
            "asin" or "sin" or "tan" => FunctionParity.Odd,
            "cos" => FunctionParity.Even,
            _ => FunctionParity.Neither
        };
    }

    private static RealSet PeriodicZeros(UnaryCompositionPattern pattern, AngleUnit angleUnit)
    {
        return pattern.OuterFunction switch
        {
            "exp" or "cos" => EmptySet.Instance,
            "acos" => PeriodicPoints(pattern, angleUnit, 0, 2),
            "ln" => PeriodicPoints(pattern, angleUnit,
                pattern.InnerSign > 0 ? new BigRational(1, 2) : new BigRational(3, 2), 2),
            _ => PeriodicPoints(pattern, angleUnit, 0, 1)
        };
    }

    private static bool TryPeriodicYIntercept(UnaryCompositionPattern pattern, AngleUnit angleUnit, out object value)
    {
        if (pattern.Phase.IsZero)
        {
            value = pattern.OuterFunction switch
            {
                "acos" => OptionalValue<ExactReal>.Some(Rational(BigRational.Zero)),
                "exp" or "cos" => OptionalValue<ExactReal>.Some(Rational(BigRational.One)),
                "ln" => OptionalValue<ExactReal>.None,
                _ => OptionalValue<ExactReal>.Some(Rational(BigRational.Zero))
            };
            return true;
        }

        if (pattern.OuterFunction is "sqrt" or "ln")
        {
            value = null!;
            return false;
        }

        if (pattern.InnerFunction == "tan" && TryQuarterTurnMultiple(pattern.Phase, angleUnit, out bool oddQuarter) && oddQuarter)
        {
            value = OptionalValue<ExactReal>.None;
            return true;
        }

        ExactReal inner = new FunctionReal(pattern.InnerFunction, [ExactAngleArithmetic.ToRadians(Rational(pattern.Phase), angleUnit)]);
        if (pattern.InnerSign < 0)
        {
            inner = ExactRealArithmetic.Negate(inner);
        }

        value = OptionalValue<ExactReal>.Some(ApplyOuterAtValue(pattern.OuterFunction, inner, angleUnit));
        return true;
    }

    private static ImmutableArray<FeaturePoint> PeriodicExtrema(UnaryCompositionPattern pattern, AngleUnit angleUnit, bool minimum)
    {
        if (pattern.OuterFunction == "atan")
        {
            return [];
        }

        if (pattern.OuterFunction == "ln")
        {
            return minimum ? [] : [PeriodicFeature(pattern, angleUnit, pattern.InnerSign > 0 ? new BigRational(1, 2) : new BigRational(3, 2), 2, Rational(BigRational.Zero))];
        }

        if (pattern.OuterFunction == "sqrt")
        {
            if (!minimum)
            {
                return [PeriodicFeature(pattern, angleUnit, pattern.InnerSign > 0 ? new BigRational(1, 2) : new BigRational(3, 2), 2, Rational(BigRational.One))];
            }

            BigRational start = pattern.InnerSign > 0 ? BigRational.Zero : BigRational.One;
            return [PeriodicFeature(pattern, angleUnit, start, 2, Rational(BigRational.Zero)), PeriodicFeature(pattern, angleUnit, start + BigRational.One, 2, Rational(BigRational.Zero))];
        }

        if (pattern.OuterFunction == "acos")
        {
            return [PeriodicFeature(pattern, angleUnit, minimum ? BigRational.Zero : BigRational.One, 2, minimum ? Rational(BigRational.Zero) : Angle(angleUnit, BigRational.One))];
        }

        if (pattern.OuterFunction == "cos")
        {
            return [PeriodicFeature(pattern, angleUnit, minimum ? new BigRational(1, 2) : BigRational.Zero, 1, minimum ? FunctionAtOne("cos", angleUnit) : Rational(BigRational.One))];
        }

        bool increasingOuter = pattern.OuterFunction is "asin" or "exp" or "sin" or "tan";
        int desiredInner = minimum == increasingOuter ? -1 : 1;
        BigRational fraction = RawSineExtremumFraction(pattern, desiredInner);
        return [PeriodicFeature(pattern, angleUnit, fraction, 2, OuterAtSignedUnit(pattern, desiredInner, angleUnit))];
    }

    private static bool TryPeriodicInflections(UnaryCompositionPattern pattern, AngleUnit angleUnit, out object value)
    {
        switch (pattern.OuterFunction)
        {
            case "asin":
            case "acos":
            case "atan":
            case "sqrt":
            case "ln":
                value = ImmutableArray<FeaturePoint>.Empty;
                return true;
            case "sin":
                value = ImmutableArray.Create<FeaturePoint>(PeriodicFeature(pattern, angleUnit, 0, 1, Rational(BigRational.Zero)));
                return true;
            case "exp":
                value = ExponentialInflections(pattern, angleUnit);
                return true;
            case "tan":
            case "cos":
                // Their exact inflection equations are transcendental even
                // after reducing to s = sin(u). The validated analytic
                // backend must isolate every root before this can be proved.
                value = null!;
                return false;
            default:
                value = null!;
                return false;
        }
    }

    private static ImmutableArray<FeaturePoint> ExponentialInflections(UnaryCompositionPattern pattern, AngleUnit angleUnit)
    {
        ExactReal sqrtFive = new FunctionReal("sqrt", [Rational(new BigRational(5))]);
        ExactReal positiveConjugate = ExactRealArithmetic.Scale(ExactRealArithmetic.AddRational(sqrtFive, BigRational.MinusOne), new BigRational(1, 2));
        ExactReal negativeConjugate = ExactRealArithmetic.Negate(positiveConjugate);
        ExactReal beta = ExactAngleArithmetic.FromRadians(new FunctionReal("asin", [negativeConjugate]), angleUnit);
        ExactReal firstAngle;
        ExactReal secondAngle;
        if (pattern.InnerSign > 0)
        {
            firstAngle = ExactRealArithmetic.Add(Angle(angleUnit, BigRational.One), beta);
            secondAngle = ExactRealArithmetic.Negate(beta);
        }
        else
        {
            firstAngle = beta;
            secondAngle = ExactRealArithmetic.Subtract(Angle(angleUnit, BigRational.One), beta);
        }

        ExactReal period = TrigStep(pattern, angleUnit, 2);
        ExactReal y = new FunctionReal("exp", [positiveConjugate]);
        return [PeriodicFeature(pattern, firstAngle, period, y), PeriodicFeature(pattern, secondAngle, period, y)];
    }

    private static ImmutableArray<Asymptote> PeriodicVerticalAsymptotes(UnaryCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.OuterFunction != "ln")
        {
            return [];
        }

        return [new Asymptote(AsymptoteOrientation.Vertical, new PeriodicReal(SolveAngle(pattern, Angle(angleUnit, BigRational.Zero)), TrigStep(pattern, angleUnit, 1), Parameter, IntegerConstraint.All(Parameter)), null, null)];
    }

    private static ImmutableArray<MonotoneRegion> PeriodicMonotonicity(UnaryCompositionPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.OuterFunction == "atan")
        {
            return [new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, new BigRational(-1, 2), false, new BigRational(1, 2), false, 1), pattern.InnerSign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing)];
        }

        if (pattern.OuterFunction == "acos")
        {
            return [new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, 0, false, 1, false, 2), Monotonicity.Increasing), new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, 1, false, 2, false, 2), Monotonicity.Decreasing)];
        }

        if (pattern.OuterFunction == "cos")
        {
            return [new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, 0, false, new BigRational(1, 2), false, 1), Monotonicity.Decreasing), new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, new BigRational(1, 2), false, 1, false, 1), Monotonicity.Increasing)];
        }

        if (pattern.OuterFunction is "sqrt" or "ln")
        {
            BigRational start = pattern.InnerSign > 0 ? BigRational.Zero : BigRational.One;
            return [new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, start, false, start + new BigRational(1, 2), false, 2), Monotonicity.Increasing), new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, start + new BigRational(1, 2), false, start + BigRational.One, false, 2), Monotonicity.Decreasing)];
        }

        bool reverse = pattern.InnerSign < 0;
        return [new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, new BigRational(1, 2), false, new BigRational(3, 2), false, 2), reverse ? Monotonicity.Increasing : Monotonicity.Decreasing), new MonotoneRegion(PeriodicIntervals(pattern, angleUnit, new BigRational(3, 2), false, new BigRational(5, 2), false, 2), reverse ? Monotonicity.Decreasing : Monotonicity.Increasing)];
    }

    private static Periodicity PeriodicPeriod(UnaryCompositionPattern pattern, AngleUnit angleUnit)
    {
        BigRational fraction = pattern.OuterFunction switch
        {
            "atan" => BigRational.One,
            "cos" when pattern.InnerFunction == "sin" => BigRational.One,
            _ => new BigRational(2)
        };
        return new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, TrigStep(pattern, angleUnit, fraction));
    }

    private static ExactReal OuterAtSignedUnit(UnaryCompositionPattern pattern, int value, AngleUnit angleUnit)
    {
        BigRational rational = new(value);
        return pattern.OuterFunction switch
        {
            "asin" or "acos" or "atan" => ExactInverseTrigonometry.PrincipalAngle(pattern.OuterFunction, ExactScalar.FromRational(rational), angleUnit),
            "exp" => value < 0 ? ExpMinusOne() : new NamedReal("e"),
            _ => ApplyOuterAtValue(pattern.OuterFunction, Rational(rational), angleUnit)
        };
    }

    private static BigRational RawSineExtremumFraction(UnaryCompositionPattern pattern, int desiredInner)
    {
        int desiredRaw = desiredInner * pattern.InnerSign;
        return desiredRaw > 0 ? new BigRational(1, 2) : new BigRational(3, 2);
    }

    private static ConstantYFeaturePoint SingletonFeature(ExactReal x, ExactReal y)
    {
        return new ConstantYFeaturePoint(new SingletonReal(x), y);
    }

    private static ConstantYFeaturePoint PeriodicFeature(UnaryCompositionPattern pattern, AngleUnit angleUnit, BigRational angleFraction, BigRational periodFraction, ExactReal y)
    {
        return PeriodicFeature(pattern, Angle(angleUnit, angleFraction), TrigStep(pattern, angleUnit, periodFraction),
            y);
    }

    private static ConstantYFeaturePoint PeriodicFeature(UnaryCompositionPattern pattern, ExactReal angle, ExactReal period, ExactReal y)
    {
        return new ConstantYFeaturePoint(new PeriodicReal(SolveAngle(pattern, angle), period, Parameter, IntegerConstraint.All(Parameter)),
            y);
    }

    private static PeriodicPointSet PeriodicPoints(UnaryCompositionPattern pattern, AngleUnit angleUnit, BigRational angleFraction, BigRational periodFraction)
    {
        return new PeriodicPointSet(SolveAngle(pattern, Angle(angleUnit, angleFraction)),
            TrigStep(pattern, angleUnit, periodFraction), Parameter, IntegerConstraint.All(Parameter));
    }

    private static PeriodicIntervalSet PeriodicIntervals(UnaryCompositionPattern pattern, AngleUnit angleUnit, BigRational lowerFraction, bool includesLower, BigRational upperFraction, bool includesUpper, BigRational periodFraction)
    {
        return new PeriodicIntervalSet(TrigStep(pattern, angleUnit, periodFraction), Parameter, IntegerConstraint.All(Parameter),
        [
            new PeriodicInterval(SolveAngle(pattern, Angle(angleUnit, lowerFraction)), includesLower,
                SolveAngle(pattern, Angle(angleUnit, upperFraction)), includesUpper)
        ]);
    }

    private static ExactReal SolveAngle(UnaryCompositionPattern pattern, ExactReal angle)
    {
        return ExactRealArithmetic.Scale(ExactRealArithmetic.AddRational(angle, -pattern.Phase),
            pattern.Frequency.Reciprocal());
    }

    private static ExactReal TrigStep(UnaryCompositionPattern pattern, AngleUnit angleUnit, BigRational fraction)
    {
        return ExactRealArithmetic.Scale(Angle(angleUnit, fraction), pattern.Frequency.Reciprocal());
    }

    private static RationalReal SolveAffineValue(UnaryCompositionPattern pattern, BigRational target)
    {
        BigRational rawTarget = pattern.InnerSign * target;
        return Rational((rawTarget - pattern.Phase) / pattern.Frequency);
    }

    private static RationalReal Center(UnaryCompositionPattern pattern)
    {
        return Rational(-pattern.Phase / pattern.Frequency);
    }

    private static IntervalSet OrderedInterval(ExactReal first, bool includesFirst, ExactReal second, bool includesSecond)
    {
        if (first is RationalReal left && second is RationalReal right && left.Value > right.Value)
        {
            return new IntervalSet(RealBound.Finite(second), includesSecond, RealBound.Finite(first), includesFirst);
        }

        return new IntervalSet(RealBound.Finite(first), includesFirst, RealBound.Finite(second), includesSecond);
    }

    private static IntervalSet ClosedInterval(ExactReal lower, ExactReal upper)
    {
        return new IntervalSet(RealBound.Finite(lower), true, RealBound.Finite(upper), true);
    }

    private static FunctionReal ExpMinusOne()
    {
        return new FunctionReal("divide", [Rational(BigRational.One), new NamedReal("e")]);
    }

    private static ExactReal FunctionAtOne(string function, AngleUnit angleUnit)
    {
        return ApplyOuterAtValue(function, Rational(BigRational.One), angleUnit);
    }

    private static ExactReal ApplyOuterAtValue(string function, ExactReal value, AngleUnit angleUnit)
    {
        return function switch
        {
            "asin" or "acos" or "atan" => ExactAngleArithmetic.FromRadians(new FunctionReal(function, [value]),
                angleUnit),
            "sin" or "cos" or "tan" => new FunctionReal(function, [ExactAngleArithmetic.ToRadians(value, angleUnit)]),
            _ => new FunctionReal(function, [value])
        };
    }

    private static RationalReal Rational(BigRational value)
    {
        return new RationalReal(value);
    }

    private static ExactReal Angle(AngleUnit unit, BigRational piFraction)
    {
        return ExactAngleArithmetic.PiFraction(unit, piFraction);
    }

    private static bool TryQuarterTurnMultiple(BigRational phase, AngleUnit angleUnit, out bool oddMultiple)
    {
        if (angleUnit == AngleUnit.Radians)
        {
            oddMultiple = false;
            return phase.IsZero;
        }

        BigRational quarter = angleUnit == AngleUnit.Degrees ? new BigRational(90) : new BigRational(100);
        BigRational multiple = phase / quarter;
        if (!multiple.IsInteger)
        {
            oddMultiple = false;
            return false;
        }

        oddMultiple = !multiple.Numerator.IsEven;
        return true;
    }

    private static bool Fail(out object value)
    {
        value = null!;
        return false;
    }
}
