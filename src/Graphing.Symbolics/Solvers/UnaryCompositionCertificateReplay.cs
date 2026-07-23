using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Production replay kernel for unary-composition certificates. This checker
/// independently recognizes the source expression, checks its exact domain,
/// and reconstructs the claimed feature without calling the producer.
/// </summary>
internal static class UnaryCompositionCertificateReplay
{
    private const string CertificateRule = "exact-unary-composition-cells-v1";
    private const string Parameter = "m";

    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        UnaryCompositionProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Rule != CertificateRule ||
            certificate.ProvenFeature != certificate.Feature ||
            !CertificateFeatureBinding.IsSingleRequested(request, certificate.Feature) ||
            !string.Equals(
                certificate.Subject,
                certificate.SubjectCanonical,
                StringComparison.Ordinal) ||
            !string.Equals(
                certificate.Subject,
                expression.Value.Canonical,
                StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) ||
            !string.Equals(
                certificate.DefinednessCanonical,
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal) ||
            !TryRecognize(
                expression,
                request.Variable,
                budget,
                out UnaryCompositionPattern pattern) ||
            pattern.Kind != certificate.Kind ||
            !string.Equals(
                pattern.OuterFunction,
                certificate.OuterFunction,
                StringComparison.Ordinal) ||
            !string.Equals(
                pattern.InnerFunction,
                certificate.InnerFunction,
                StringComparison.Ordinal) ||
            !string.Equals(
                pattern.Canonical,
                certificate.PatternCanonical,
                StringComparison.Ordinal))
        {
            return false;
        }

        budget.CheckCoefficient(pattern.Frequency);
        budget.CheckCoefficient(pattern.Phase);
        RealSet domain = BuildDomain(pattern, request.AngleUnit);
        if (!CheckDefinedness(
                expression,
                pattern,
                request,
                domain,
                budget) ||
            !TryReconstruct(
                pattern,
                request.AngleUnit,
                certificate.Feature,
                domain,
                budget,
                out object expected))
        {
            return false;
        }

        return string.Equals(
            ClaimCanonical.ForObject(expected),
            claim,
            StringComparison.Ordinal);
    }

    private static bool TryRecognize(
        SemanticExpression expression,
        string variable,
        ResourceBudget budget,
        out UnaryCompositionPattern pattern)
    {
        budget.Charge();
        if (TryRecognizeGuardedIdentity(
                expression,
                variable,
                budget,
                out pattern))
        {
            return true;
        }

        if (TryRecognizeAbsoluteLogarithm(
                expression.Value,
                variable,
                budget,
                out pattern))
        {
            return true;
        }

        ValueTerm subject = expression.Value;
        if (subject is not
            {
                Kind: ValueKind.Function,
                Operands: [var inner]
            } ||
            inner is not
            {
                Kind: ValueKind.Function,
                Operands: [var argument]
            } ||
            !TryReadAffine(
                argument,
                variable,
                budget,
                out BigRational frequency,
                out BigRational phase))
        {
            pattern = null!;
            return false;
        }

        UnaryCompositionKind kind;
        if ((subject.Name, inner.Name) is
            ("asin", "sin") or
            ("acos", "cos") or
            ("atan", "tan"))
        {
            kind = UnaryCompositionKind.PrincipalInverseOfAffineTrigonometric;
        }
        else if (inner.Name == "sin" &&
                 subject.Name is "exp" or "sqrt" or "ln" or "tan" or "sin" or "cos")
        {
            kind = UnaryCompositionKind.PrimitiveOfAffineTrigonometric;
        }
        else
        {
            pattern = null!;
            return false;
        }

        int innerSign = 1;
        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phase = -phase;
            if (inner.Name is "sin" or "tan")
            {
                innerSign = -1;
            }
        }

        pattern = new UnaryCompositionPattern(
            kind,
            subject.Name,
            inner.Name,
            frequency,
            phase,
            innerSign,
            string.Empty);
        return true;
    }

    private static bool TryRecognizeAbsoluteLogarithm(
        ValueTerm subject,
        string variable,
        ResourceBudget budget,
        out UnaryCompositionPattern pattern)
    {
        if (subject is not
            {
                Kind: ValueKind.Function,
                Name: "ln" or "log",
                Operands:
                [
                    {
                        Kind: ValueKind.Function,
                        Name: "abs",
                        Operands: [var argument]
                    }
                ]
            } ||
            !TryReadAffine(
                argument,
                variable,
                budget,
                out BigRational frequency,
                out BigRational phase))
        {
            pattern = null!;
            return false;
        }

        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phase = -phase;
        }

        pattern = new UnaryCompositionPattern(
            UnaryCompositionKind.PrimitiveOfAbsoluteAffine,
            subject.Name,
            "abs",
            frequency,
            phase,
            1,
            string.Empty);
        return true;
    }

    private static bool TryRecognizeGuardedIdentity(
        SemanticExpression expression,
        string variable,
        ResourceBudget budget,
        out UnaryCompositionPattern pattern)
    {
        RewriteStep? identity = null;
        foreach (RewriteStep step in expression.RewriteHistory)
        {
            budget.Charge();
            if (step.Rule is not (
                "sine-arcsine-identity" or
                "cosine-arccosine-identity" or
                "tangent-arctangent-identity" or
                "exponential-logarithm-identity" or
                "logarithm-exponential-identity"))
            {
                continue;
            }

            if (identity is not null)
            {
                pattern = null!;
                return false;
            }

            identity = step;
        }

        if (identity is null ||
            !TryReadAffine(
                expression.Value,
                variable,
                budget,
                out BigRational frequency,
                out BigRational phase))
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

        int innerSign = 1;
        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phase = -phase;
            innerSign = -1;
        }

        pattern = new UnaryCompositionPattern(
            UnaryCompositionKind.GuardedInverseIdentity,
            outer,
            inner,
            frequency,
            phase,
            innerSign,
            identity.Value.Rule);
        return true;
    }

    private static bool TryReadAffine(
        ValueTerm value,
        string variable,
        ResourceBudget budget,
        out BigRational frequency,
        out BigRational phase)
    {
        budget.Charge();
        if (!RationalFunctionExtractor.TryExtract(
                value,
                variable,
                budget,
                out RationalExtraction extraction) ||
            !extraction.DomainExclusions.IsEmpty ||
            extraction.Function.Denominator.Degree != 0 ||
            extraction.Function.Numerator.Degree != 1)
        {
            frequency = default;
            phase = default;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        frequency = extraction.Function.Numerator[1] / denominator;
        phase = extraction.Function.Numerator[0] / denominator;
        return !frequency.IsZero;
    }

    private static bool CheckDefinedness(
        SemanticExpression expression,
        UnaryCompositionPattern pattern,
        AnalysisRequest request,
        RealSet expectedDomain,
        ResourceBudget budget)
    {
        budget.Charge();
        if (pattern.Kind == UnaryCompositionKind.GuardedInverseIdentity)
        {
            _ = request;
            _ = expectedDomain;
            return CheckGuardedSemanticChain(expression, pattern, budget);
        }

        if (pattern.OuterFunction is not (
            "sqrt" or "ln" or "log" or "tan" or "asin" or "acos" or "atan"))
        {
            return string.Equals(
                expression.DefinedWhen.Canonical,
                Formula.True.Canonical,
                StringComparison.Ordinal);
        }

        ValueTerm inner = expression.Value.Operands[0];
        ValueTerm zero = ConstantTerm(BigRational.Zero, -101);
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
            expected = Formula.And(
                Formula.Compare(
                    inner,
                    Comparison.GreaterOrEqual,
                    ConstantTerm(BigRational.MinusOne, -102)),
                Formula.Compare(
                    inner,
                    Comparison.LessOrEqual,
                    ConstantTerm(BigRational.One, -103)));
        }
        else
        {
            ValueTerm cosineArgument = pattern.OuterFunction == "atan"
                ? inner.Operands[0]
                : inner;
            ValueTerm cosine = FunctionTerm("cos", cosineArgument, -104);
            expected = Formula.Compare(cosine, Comparison.NotEqual, zero);
        }

        return string.Equals(
            expression.DefinedWhen.Canonical,
            expected.Canonical,
            StringComparison.Ordinal);
    }

    private static bool CheckGuardedSemanticChain(
        SemanticExpression expression,
        UnaryCompositionPattern pattern,
        ResourceBudget budget)
    {
        budget.Charge();
        if (!TryReadGuardedChain(
                expression,
                pattern,
                out RewriteStep rewrite,
                out SemanticExpression innerExpression,
                out SemanticExpression argument) ||
            !HasEverywhereRegularity(argument) ||
            !TryBuildGuardedInnerRegularity(
                pattern.InnerFunction,
                argument.Value,
                out Formula expectedDefined,
                out Formula expectedContinuous,
                out Formula expectedDifferentiable))
        {
            return false;
        }

        ValueTerm expectedBefore = FunctionTerm(
            pattern.OuterFunction,
            innerExpression.Value,
            -114);
        return GuardedRewriteMatches(
                   rewrite,
                   pattern,
                   expectedBefore,
                   argument.Value,
                   expectedDefined) &&
               GuardedRegularityMatches(
                   expression,
                   innerExpression,
                   expectedDefined,
                   expectedContinuous,
                   expectedDifferentiable);
    }

    private static bool TryReadGuardedChain(
        SemanticExpression expression,
        UnaryCompositionPattern pattern,
        out RewriteStep rewrite,
        out SemanticExpression innerExpression,
        out SemanticExpression argument)
    {
        if (expression.RewriteHistory is not [var onlyRewrite] ||
            expression.SourceOperands is not [var onlyInner] ||
            onlyInner.RewriteHistory.Length != 0 ||
            onlyInner.SourceOperands is not [var onlyArgument] ||
            onlyInner.Value is not
            {
                Kind: ValueKind.Function,
                Operands: [var innerArgument]
            })
        {
            return FailGuardedChain(
                out rewrite,
                out innerExpression,
                out argument);
        }

        if (!string.Equals(
                onlyInner.Value.Name,
                pattern.InnerFunction,
                StringComparison.Ordinal) ||
            !string.Equals(
                innerArgument.Canonical,
                onlyArgument.Value.Canonical,
                StringComparison.Ordinal) ||
            !string.Equals(
                expression.Value.Canonical,
                onlyArgument.Value.Canonical,
                StringComparison.Ordinal))
        {
            return FailGuardedChain(
                out rewrite,
                out innerExpression,
                out argument);
        }

        rewrite = onlyRewrite;
        innerExpression = onlyInner;
        argument = onlyArgument;
        return true;
    }

    private static bool FailGuardedChain(
        out RewriteStep rewrite,
        out SemanticExpression innerExpression,
        out SemanticExpression argument)
    {
        rewrite = default;
        innerExpression = null!;
        argument = null!;
        return false;
    }

    private static bool HasEverywhereRegularity(SemanticExpression expression)
    {
        return CanonicalEquals(expression.DefinedWhen, Formula.True) &&
               CanonicalEquals(expression.ContinuousWhen, Formula.True) &&
               CanonicalEquals(expression.DifferentiableWhen, Formula.True);
    }

    private static bool TryBuildGuardedInnerRegularity(
        string innerFunction,
        ValueTerm argument,
        out Formula defined,
        out Formula continuous,
        out Formula differentiable)
    {
        ValueTerm zero = ConstantTerm(BigRational.Zero, -111);
        ValueTerm minusOne = ConstantTerm(BigRational.MinusOne, -112);
        ValueTerm one = ConstantTerm(BigRational.One, -113);
        switch (innerFunction)
        {
            case "asin":
            case "acos":
                defined = Formula.And(
                    Formula.Compare(
                        argument,
                        Comparison.GreaterOrEqual,
                        minusOne),
                    Formula.Compare(
                        argument,
                        Comparison.LessOrEqual,
                        one));
                continuous = defined;
                differentiable = Formula.And(
                    Formula.Compare(
                        argument,
                        Comparison.Greater,
                        minusOne),
                    Formula.Compare(
                        argument,
                        Comparison.Less,
                        one));
                return true;
            case "ln":
                defined = Formula.Compare(
                    argument,
                    Comparison.Greater,
                    zero);
                continuous = defined;
                differentiable = defined;
                return true;
            case "atan":
            case "exp":
                defined = Formula.True;
                continuous = Formula.True;
                differentiable = Formula.True;
                return true;
            default:
                defined = null!;
                continuous = null!;
                differentiable = null!;
                return false;
        }
    }

    private static bool GuardedRewriteMatches(
        RewriteStep rewrite,
        UnaryCompositionPattern pattern,
        ValueTerm expectedBefore,
        ValueTerm expectedAfter,
        Formula expectedGuard)
    {
        return string.Equals(
                   rewrite.Rule,
                   pattern.IdentityRule,
                   StringComparison.Ordinal) &&
               string.Equals(
                   rewrite.Before,
                   expectedBefore.Canonical,
                   StringComparison.Ordinal) &&
               string.Equals(
                   rewrite.After,
                   expectedAfter.Canonical,
                   StringComparison.Ordinal) &&
               CanonicalEquals(rewrite.Guard, expectedGuard);
    }

    private static bool GuardedRegularityMatches(
        SemanticExpression expression,
        SemanticExpression innerExpression,
        Formula expectedDefined,
        Formula expectedContinuous,
        Formula expectedDifferentiable)
    {
        return CanonicalEquals(innerExpression.DefinedWhen, expectedDefined) &&
               CanonicalEquals(
                   innerExpression.ContinuousWhen,
                   expectedContinuous) &&
               CanonicalEquals(
                   innerExpression.DifferentiableWhen,
                   expectedDifferentiable) &&
               CanonicalEquals(expression.DefinedWhen, expectedDefined) &&
               CanonicalEquals(expression.ContinuousWhen, expectedContinuous) &&
               CanonicalEquals(
                   expression.DifferentiableWhen,
                   expectedDifferentiable);
    }

    private static bool CanonicalEquals(Formula left, Formula right)
    {
        return string.Equals(left.Canonical, right.Canonical, StringComparison.Ordinal);
    }

    private static ValueTerm ConstantTerm(BigRational value, int id)
    {
        return new ValueTerm(
            id,
            ValueKind.Constant,
            value,
            string.Empty,
            [],
            "q:" + value);
    }

    private static ValueTerm FunctionTerm(
        string function,
        ValueTerm argument,
        int id)
    {
        return new ValueTerm(
            id,
            ValueKind.Function,
            default,
            function,
            [argument],
            $"{(int)ValueKind.Function}:{function}({argument.Canonical})");
    }

    private static RealSet BuildDomain(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.Kind == UnaryCompositionKind.GuardedInverseIdentity)
        {
            return BuildIdentityDomain(pattern);
        }

        if (pattern.Kind == UnaryCompositionKind.PrimitiveOfAbsoluteAffine)
        {
            return new DifferenceSet(
                AllRealSet.Instance,
                RealSets.Points([AffineCenter(pattern)]));
        }

        if (pattern.OuterFunction is "sqrt" or "ln")
        {
            bool closed = pattern.OuterFunction == "sqrt";
            BigRational lower = pattern.InnerSign > 0
                ? BigRational.Zero
                : BigRational.One;
            return BuildPeriodicIntervals(
                pattern,
                angleUnit,
                lower,
                closed,
                lower + BigRational.One,
                closed,
                2);
        }

        if (pattern.OuterFunction == "atan" &&
            pattern.InnerFunction == "tan")
        {
            return BuildPeriodicIntervals(
                pattern,
                angleUnit,
                new BigRational(-1, 2),
                false,
                new BigRational(1, 2),
                false,
                1);
        }

        return AllRealSet.Instance;
    }

    private static bool TryReconstruct(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        RealSet domain,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        return pattern.Kind switch
        {
            UnaryCompositionKind.GuardedInverseIdentity =>
                TryReconstructIdentity(pattern, feature, domain, out value),
            UnaryCompositionKind.PrincipalInverseOfAffineTrigonometric or
            UnaryCompositionKind.PrimitiveOfAffineTrigonometric =>
                TryReconstructPeriodic(
                    pattern,
                    angleUnit,
                    feature,
                    domain,
                    out value),
            UnaryCompositionKind.PrimitiveOfAbsoluteAffine =>
                TryReconstructAbsoluteLogarithm(
                    pattern,
                    feature,
                    domain,
                    budget,
                    out value),
            _ => Fail(out value)
        };
    }

    private static bool TryReconstructAbsoluteLogarithm(
        UnaryCompositionPattern pattern,
        AnalysisFeatures feature,
        RealSet domain,
        ResourceBudget budget,
        out object value)
    {
        ExactReal center = AffineCenter(pattern);
        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => AllRealSet.Instance,
            AnalysisFeatures.Parity => pattern.Phase.IsZero
                ? FunctionParity.Even
                : FunctionParity.Neither,
            AnalysisFeatures.Zeros => RealSets.Points(
            [
                Rational((-BigRational.One - pattern.Phase) / pattern.Frequency),
                Rational((BigRational.One - pattern.Phase) / pattern.Frequency)
            ]),
            AnalysisFeatures.YIntercept => pattern.Phase.IsZero
                ? OptionalValue<ExactReal>.None
                : OptionalValue<ExactReal>.Some(
                    EvaluateAbsoluteLogarithmAtRational(
                        pattern.Phase,
                        pattern.OuterFunction,
                        budget)),
            AnalysisFeatures.Minima or
            AnalysisFeatures.Maxima or
            AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray.Create(
                new Asymptote(
                    AsymptoteOrientation.Vertical,
                    new SingletonReal(center),
                    null,
                    null)),
            AnalysisFeatures.HorizontalAsymptotes or
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(
                new MonotoneRegion(
                    new IntervalSet(
                        RealBound.NegativeInfinity,
                        false,
                        RealBound.Finite(center),
                        false),
                    Monotonicity.Decreasing),
                new MonotoneRegion(
                    new IntervalSet(
                        RealBound.Finite(center),
                        false,
                        RealBound.PositiveInfinity,
                        false),
                    Monotonicity.Increasing)),
            AnalysisFeatures.Period =>
                new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static ExactReal EvaluateAbsoluteLogarithmAtRational(
        BigRational argument,
        string logarithm,
        ResourceBudget budget)
    {
        BigRational magnitude = argument.Abs();
        if (magnitude.IsOne)
        {
            return Rational(BigRational.Zero);
        }

        if (logarithm == "log" &&
            TryReadPowerOfTen(magnitude.Numerator, budget, out int numeratorPower) &&
            TryReadPowerOfTen(magnitude.Denominator, budget, out int denominatorPower))
        {
            return Rational(new BigRational(numeratorPower - denominatorPower));
        }

        return new FunctionReal(logarithm, [Rational(magnitude)]);
    }

    private static bool TryReadPowerOfTen(
        ExactInteger value,
        ResourceBudget budget,
        out int power)
    {
        power = 0;
        while (value > ExactInteger.One)
        {
            budget.Charge();
            value = ExactInteger.DivRem(value, 10, out ExactInteger remainder);
            if (!remainder.IsZero)
            {
                power = 0;
                return false;
            }

            power = checked(power + 1);
        }

        return value.IsOne;
    }

    private static RealSet BuildIdentityDomain(UnaryCompositionPattern pattern)
    {
        if (pattern.InnerFunction is "asin" or "acos")
        {
            return OrderedInterval(
                SolveAffineOutput(pattern, BigRational.MinusOne),
                true,
                SolveAffineOutput(pattern, BigRational.One),
                true);
        }

        if (pattern.InnerFunction == "ln")
        {
            ExactReal boundary = AffineCenter(pattern);
            return pattern.InnerSign > 0
                ? new IntervalSet(
                    RealBound.Finite(boundary),
                    false,
                    RealBound.PositiveInfinity,
                    false)
                : new IntervalSet(
                    RealBound.NegativeInfinity,
                    false,
                    RealBound.Finite(boundary),
                    false);
        }

        return AllRealSet.Instance;
    }

    private static bool TryReconstructIdentity(
        UnaryCompositionPattern pattern,
        AnalysisFeatures feature,
        RealSet domain,
        out object value)
    {
        bool hasClosedBranch = pattern.InnerFunction is "asin" or "acos";
        bool hasPositiveDomain = pattern.InnerFunction == "ln";
        value = feature switch
        {
            AnalysisFeatures.Domain => domain,
            AnalysisFeatures.Range => hasClosedBranch
                ? ClosedInterval(
                    Rational(BigRational.MinusOne),
                    Rational(BigRational.One))
                : hasPositiveDomain
                    ? new IntervalSet(
                        RealBound.Finite(Rational(BigRational.Zero)),
                        false,
                        RealBound.PositiveInfinity,
                        false)
                    : AllRealSet.Instance,
            AnalysisFeatures.Parity => hasPositiveDomain
                ? FunctionParity.Neither
                : pattern.Phase.IsZero
                    ? FunctionParity.Odd
                    : FunctionParity.Neither,
            AnalysisFeatures.Zeros => hasPositiveDomain
                ? EmptySet.Instance
                : RealSets.Points([AffineCenter(pattern)]),
            AnalysisFeatures.YIntercept => IdentityIntercept(pattern),
            AnalysisFeatures.Minima => hasClosedBranch
                ? [SingletonFeature(
                    SolveAffineOutput(pattern, BigRational.MinusOne),
                    Rational(BigRational.MinusOne))]
                : ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.Maxima => hasClosedBranch
                ? [SingletonFeature(
                    SolveAffineOutput(pattern, BigRational.One),
                    Rational(BigRational.One))]
                : ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.InflectionPoints =>
                ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or
            AnalysisFeatures.HorizontalAsymptotes =>
                ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.ObliqueAsymptotes =>
                BuildIdentityObliqueAsymptotes(pattern, hasClosedBranch),
            AnalysisFeatures.Monotonicity =>
                BuildIdentityMonotonicity(pattern, domain),
            AnalysisFeatures.Period =>
                new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static OptionalValue<ExactReal> IdentityIntercept(
        UnaryCompositionPattern pattern)
    {
        BigRational value = pattern.InnerSign * pattern.Phase;
        if (pattern.InnerFunction is "asin" or "acos" &&
            (value < BigRational.MinusOne || value > BigRational.One))
        {
            return OptionalValue<ExactReal>.None;
        }

        if (pattern.InnerFunction == "ln" && value <= BigRational.Zero)
        {
            return OptionalValue<ExactReal>.None;
        }

        return OptionalValue<ExactReal>.Some(Rational(value));
    }

    private static ImmutableArray<Asymptote> BuildIdentityObliqueAsymptotes(
        UnaryCompositionPattern pattern,
        bool bounded)
    {
        if (bounded)
        {
            return [];
        }

        ExactReal intercept = Rational(pattern.InnerSign * pattern.Phase);
        return
        [
            new Asymptote(
                AsymptoteOrientation.Oblique,
                new SingletonReal(intercept),
                Rational(pattern.InnerSign * pattern.Frequency),
                intercept)
        ];
    }

    private static ImmutableArray<MonotoneRegion> BuildIdentityMonotonicity(
        UnaryCompositionPattern pattern,
        RealSet domain)
    {
        RealSet openRegion = domain is IntervalSet interval
            ? interval with
            {
                IncludesLower = false,
                IncludesUpper = false
            }
            : domain;
        return
        [
            new MonotoneRegion(
                openRegion,
                pattern.InnerSign > 0
                    ? Monotonicity.Increasing
                    : Monotonicity.Decreasing)
        ];
    }

    private static bool TryReconstructPeriodic(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        RealSet domain,
        out object value)
    {
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = domain;
                return true;
            case AnalysisFeatures.Range:
                value = BuildPeriodicRange(pattern, angleUnit);
                return true;
            case AnalysisFeatures.Parity:
                value = ClassifyPeriodicParity(pattern, angleUnit);
                return true;
            case AnalysisFeatures.Zeros:
                value = BuildPeriodicZeros(pattern, angleUnit);
                return true;
            case AnalysisFeatures.YIntercept:
                return TryBuildPeriodicIntercept(pattern, angleUnit, out value);
            case AnalysisFeatures.Minima:
                value = BuildPeriodicExtrema(
                    pattern,
                    angleUnit,
                    minimum: true);
                return true;
            case AnalysisFeatures.Maxima:
                value = BuildPeriodicExtrema(
                    pattern,
                    angleUnit,
                    minimum: false);
                return true;
            case AnalysisFeatures.InflectionPoints:
                return TryBuildPeriodicInflections(pattern, angleUnit, out value);
            case AnalysisFeatures.VerticalAsymptotes:
                value = BuildPeriodicVerticalAsymptotes(pattern, angleUnit);
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.Monotonicity:
                value = BuildPeriodicMonotonicity(pattern, angleUnit);
                return true;
            case AnalysisFeatures.Period:
                value = BuildPeriodicPeriod(pattern, angleUnit);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static IntervalSet BuildPeriodicRange(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        ExactReal zero = Rational(BigRational.Zero);
        ExactReal one = Rational(BigRational.One);
        return pattern.OuterFunction switch
        {
            "asin" => ClosedInterval(
                UnitAngle(angleUnit, new BigRational(-1, 2)),
                UnitAngle(angleUnit, new BigRational(1, 2))),
            "acos" => ClosedInterval(
                zero,
                UnitAngle(angleUnit, BigRational.One)),
            "atan" => new IntervalSet(
                RealBound.Finite(
                    UnitAngle(angleUnit, new BigRational(-1, 2))),
                false,
                RealBound.Finite(
                    UnitAngle(angleUnit, new BigRational(1, 2))),
                false),
            "exp" => ClosedInterval(ReciprocalE(), new NamedReal("e")),
            "sqrt" => ClosedInterval(zero, one),
            "ln" => new IntervalSet(
                RealBound.NegativeInfinity,
                false,
                RealBound.Finite(zero),
                true),
            "tan" => SymmetricUnitInputRange("tan", angleUnit),
            "sin" => SymmetricUnitInputRange("sin", angleUnit),
            "cos" => ClosedInterval(
                EvaluateUnitInput("cos", angleUnit),
                one),
            _ => throw new InvalidOperationException()
        };
    }

    private static IntervalSet SymmetricUnitInputRange(
        string function,
        AngleUnit angleUnit)
    {
        ExactReal upper = EvaluateUnitInput(function, angleUnit);
        return ClosedInterval(ExactRealArithmetic.Negate(upper), upper);
    }

    private static FunctionParity ClassifyPeriodicParity(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        if (!TryClassifyQuarterTurn(
                pattern.Phase,
                angleUnit,
                out bool oddQuarter))
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

    private static RealSet BuildPeriodicZeros(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        return pattern.OuterFunction switch
        {
            "exp" or "cos" => EmptySet.Instance,
            "acos" => BuildPeriodicPoints(pattern, angleUnit, 0, 2),
            "ln" => BuildPeriodicPoints(
                pattern,
                angleUnit,
                pattern.InnerSign > 0
                    ? new BigRational(1, 2)
                    : new BigRational(3, 2),
                2),
            _ => BuildPeriodicPoints(pattern, angleUnit, 0, 1)
        };
    }

    private static bool TryBuildPeriodicIntercept(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit,
        out object value)
    {
        if (pattern.Phase.IsZero)
        {
            value = pattern.OuterFunction switch
            {
                "acos" => OptionalValue<ExactReal>.Some(
                    Rational(BigRational.Zero)),
                "exp" or "cos" => OptionalValue<ExactReal>.Some(
                    Rational(BigRational.One)),
                "ln" => OptionalValue<ExactReal>.None,
                _ => OptionalValue<ExactReal>.Some(
                    Rational(BigRational.Zero))
            };
            return true;
        }

        if (pattern.OuterFunction is "sqrt" or "ln")
        {
            value = null!;
            return false;
        }

        if (pattern.InnerFunction == "tan" &&
            TryClassifyQuarterTurn(
                pattern.Phase,
                angleUnit,
                out bool oddQuarter) &&
            oddQuarter)
        {
            value = OptionalValue<ExactReal>.None;
            return true;
        }

        ExactReal innerValue = new FunctionReal(
            pattern.InnerFunction,
            [ExactAngleArithmetic.ToRadians(
                Rational(pattern.Phase),
                angleUnit)]);
        if (pattern.InnerSign < 0)
        {
            innerValue = ExactRealArithmetic.Negate(innerValue);
        }

        value = OptionalValue<ExactReal>.Some(
            EvaluateOuter(pattern.OuterFunction, innerValue, angleUnit));
        return true;
    }

    private static ImmutableArray<FeaturePoint> BuildPeriodicExtrema(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit,
        bool minimum)
    {
        if (pattern.OuterFunction == "atan")
        {
            return [];
        }

        if (pattern.OuterFunction == "ln")
        {
            return minimum
                ? []
                : [BuildPeriodicFeature(
                    pattern,
                    angleUnit,
                    pattern.InnerSign > 0
                        ? new BigRational(1, 2)
                        : new BigRational(3, 2),
                    2,
                    Rational(BigRational.Zero))];
        }

        if (pattern.OuterFunction == "sqrt")
        {
            if (!minimum)
            {
                return
                [
                    BuildPeriodicFeature(
                        pattern,
                        angleUnit,
                        pattern.InnerSign > 0
                            ? new BigRational(1, 2)
                            : new BigRational(3, 2),
                        2,
                        Rational(BigRational.One))
                ];
            }

            BigRational start = pattern.InnerSign > 0
                ? BigRational.Zero
                : BigRational.One;
            return
            [
                BuildPeriodicFeature(
                    pattern,
                    angleUnit,
                    start,
                    2,
                    Rational(BigRational.Zero)),
                BuildPeriodicFeature(
                    pattern,
                    angleUnit,
                    start + BigRational.One,
                    2,
                    Rational(BigRational.Zero))
            ];
        }

        if (pattern.OuterFunction == "acos")
        {
            return
            [
                BuildPeriodicFeature(
                    pattern,
                    angleUnit,
                    minimum ? BigRational.Zero : BigRational.One,
                    2,
                    minimum
                        ? Rational(BigRational.Zero)
                        : UnitAngle(angleUnit, BigRational.One))
            ];
        }

        if (pattern.OuterFunction == "cos")
        {
            return
            [
                BuildPeriodicFeature(
                    pattern,
                    angleUnit,
                    minimum ? new BigRational(1, 2) : BigRational.Zero,
                    1,
                    minimum
                        ? EvaluateUnitInput("cos", angleUnit)
                        : Rational(BigRational.One))
            ];
        }

        bool increasingOuter = pattern.OuterFunction is
            "asin" or "exp" or "sin" or "tan";
        int desiredInnerValue = minimum == increasingOuter ? -1 : 1;
        int desiredRawSine = desiredInnerValue * pattern.InnerSign;
        BigRational fraction = desiredRawSine > 0
            ? new BigRational(1, 2)
            : new BigRational(3, 2);
        return
        [
            BuildPeriodicFeature(
                pattern,
                angleUnit,
                fraction,
                2,
                EvaluateOuterAtSignedUnit(
                    pattern,
                    desiredInnerValue,
                    angleUnit))
        ];
    }

    private static bool TryBuildPeriodicInflections(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit,
        out object value)
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
                value = ImmutableArray.Create<FeaturePoint>(
                    BuildPeriodicFeature(
                        pattern,
                        angleUnit,
                        0,
                        1,
                        Rational(BigRational.Zero)));
                return true;
            case "exp":
                value = BuildExponentialInflections(pattern, angleUnit);
                return true;
            case "tan":
            case "cos":
                value = null!;
                return false;
            default:
                value = null!;
                return false;
        }
    }

    private static ImmutableArray<FeaturePoint> BuildExponentialInflections(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        ExactReal squareRootFive = new FunctionReal(
            "sqrt",
            [Rational(new BigRational(5))]);
        ExactReal positiveConjugate = ExactRealArithmetic.Scale(
            ExactRealArithmetic.AddRational(
                squareRootFive,
                BigRational.MinusOne),
            new BigRational(1, 2));
        ExactReal negativeConjugate = ExactRealArithmetic.Negate(
            positiveConjugate);
        ExactReal inverseSine = ExactAngleArithmetic.FromRadians(
            new FunctionReal("asin", [negativeConjugate]),
            angleUnit);

        ExactReal firstAngle;
        ExactReal secondAngle;
        if (pattern.InnerSign > 0)
        {
            firstAngle = ExactRealArithmetic.Add(
                UnitAngle(angleUnit, BigRational.One),
                inverseSine);
            secondAngle = ExactRealArithmetic.Negate(inverseSine);
        }
        else
        {
            firstAngle = inverseSine;
            secondAngle = ExactRealArithmetic.Subtract(
                UnitAngle(angleUnit, BigRational.One),
                inverseSine);
        }

        ExactReal period = BuildTrigStep(pattern, angleUnit, 2);
        ExactReal ordinate = new FunctionReal("exp", [positiveConjugate]);
        return
        [
            BuildPeriodicFeature(pattern, firstAngle, period, ordinate),
            BuildPeriodicFeature(pattern, secondAngle, period, ordinate)
        ];
    }

    private static ImmutableArray<Asymptote> BuildPeriodicVerticalAsymptotes(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.OuterFunction != "ln")
        {
            return [];
        }

        return
        [
            new Asymptote(
                AsymptoteOrientation.Vertical,
                new PeriodicReal(
                    SolveAngleCoordinate(
                        pattern,
                        UnitAngle(angleUnit, BigRational.Zero)),
                    BuildTrigStep(pattern, angleUnit, 1),
                    Parameter,
                    IntegerConstraint.All(Parameter)),
                null,
                null)
        ];
    }

    private static ImmutableArray<MonotoneRegion> BuildPeriodicMonotonicity(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        if (pattern.OuterFunction == "atan")
        {
            return
            [
                new MonotoneRegion(
                    BuildPeriodicIntervals(
                        pattern,
                        angleUnit,
                        new BigRational(-1, 2),
                        false,
                        new BigRational(1, 2),
                        false,
                        1),
                    pattern.InnerSign > 0
                        ? Monotonicity.Increasing
                        : Monotonicity.Decreasing)
            ];
        }

        if (pattern.OuterFunction == "acos")
        {
            return
            [
                new MonotoneRegion(
                    BuildPeriodicIntervals(
                        pattern,
                        angleUnit,
                        0,
                        false,
                        1,
                        false,
                        2),
                    Monotonicity.Increasing),
                new MonotoneRegion(
                    BuildPeriodicIntervals(
                        pattern,
                        angleUnit,
                        1,
                        false,
                        2,
                        false,
                        2),
                    Monotonicity.Decreasing)
            ];
        }

        if (pattern.OuterFunction == "cos")
        {
            return
            [
                new MonotoneRegion(
                    BuildPeriodicIntervals(
                        pattern,
                        angleUnit,
                        0,
                        false,
                        new BigRational(1, 2),
                        false,
                        1),
                    Monotonicity.Decreasing),
                new MonotoneRegion(
                    BuildPeriodicIntervals(
                        pattern,
                        angleUnit,
                        new BigRational(1, 2),
                        false,
                        1,
                        false,
                        1),
                    Monotonicity.Increasing)
            ];
        }

        if (pattern.OuterFunction is "sqrt" or "ln")
        {
            BigRational start = pattern.InnerSign > 0
                ? BigRational.Zero
                : BigRational.One;
            return
            [
                new MonotoneRegion(
                    BuildPeriodicIntervals(
                        pattern,
                        angleUnit,
                        start,
                        false,
                        start + new BigRational(1, 2),
                        false,
                        2),
                    Monotonicity.Increasing),
                new MonotoneRegion(
                    BuildPeriodicIntervals(
                        pattern,
                        angleUnit,
                        start + new BigRational(1, 2),
                        false,
                        start + BigRational.One,
                        false,
                        2),
                    Monotonicity.Decreasing)
            ];
        }

        bool reverse = pattern.InnerSign < 0;
        return
        [
            new MonotoneRegion(
                BuildPeriodicIntervals(
                    pattern,
                    angleUnit,
                    new BigRational(1, 2),
                    false,
                    new BigRational(3, 2),
                    false,
                    2),
                reverse
                    ? Monotonicity.Increasing
                    : Monotonicity.Decreasing),
            new MonotoneRegion(
                BuildPeriodicIntervals(
                    pattern,
                    angleUnit,
                    new BigRational(3, 2),
                    false,
                    new BigRational(5, 2),
                    false,
                    2),
                reverse
                    ? Monotonicity.Decreasing
                    : Monotonicity.Increasing)
        ];
    }

    private static Periodicity BuildPeriodicPeriod(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit)
    {
        BigRational turns = pattern.OuterFunction switch
        {
            "atan" => BigRational.One,
            "cos" when pattern.InnerFunction == "sin" => BigRational.One,
            _ => new BigRational(2)
        };
        return new Periodicity(
            PeriodicityKind.PeriodicWithFundamentalPeriod,
            BuildTrigStep(pattern, angleUnit, turns));
    }

    private static ExactReal EvaluateOuterAtSignedUnit(
        UnaryCompositionPattern pattern,
        int signedUnit,
        AngleUnit angleUnit)
    {
        BigRational rational = new(signedUnit);
        return pattern.OuterFunction switch
        {
            "asin" or "acos" or "atan" =>
                ExactInverseTrigonometry.PrincipalAngle(
                    pattern.OuterFunction,
                    ExactScalar.FromRational(rational),
                    angleUnit),
            "exp" => signedUnit < 0
                ? ReciprocalE()
                : new NamedReal("e"),
            _ => EvaluateOuter(
                pattern.OuterFunction,
                Rational(rational),
                angleUnit)
        };
    }

    private static ConstantYFeaturePoint SingletonFeature(
        ExactReal abscissa,
        ExactReal ordinate)
    {
        return new ConstantYFeaturePoint(new SingletonReal(abscissa), ordinate);
    }

    private static ConstantYFeaturePoint BuildPeriodicFeature(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit,
        BigRational angleFraction,
        BigRational periodFraction,
        ExactReal ordinate)
    {
        return BuildPeriodicFeature(
            pattern,
            UnitAngle(angleUnit, angleFraction),
            BuildTrigStep(pattern, angleUnit, periodFraction),
            ordinate);
    }

    private static ConstantYFeaturePoint BuildPeriodicFeature(
        UnaryCompositionPattern pattern,
        ExactReal angle,
        ExactReal period,
        ExactReal ordinate)
    {
        return new ConstantYFeaturePoint(
            new PeriodicReal(
                SolveAngleCoordinate(pattern, angle),
                period,
                Parameter,
                IntegerConstraint.All(Parameter)),
            ordinate);
    }

    private static PeriodicPointSet BuildPeriodicPoints(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit,
        BigRational angleFraction,
        BigRational periodFraction)
    {
        return new PeriodicPointSet(
            SolveAngleCoordinate(
                pattern,
                UnitAngle(angleUnit, angleFraction)),
            BuildTrigStep(pattern, angleUnit, periodFraction),
            Parameter,
            IntegerConstraint.All(Parameter));
    }

    private static PeriodicIntervalSet BuildPeriodicIntervals(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit,
        BigRational lowerFraction,
        bool includeLower,
        BigRational upperFraction,
        bool includeUpper,
        BigRational periodFraction)
    {
        return new PeriodicIntervalSet(
            BuildTrigStep(pattern, angleUnit, periodFraction),
            Parameter,
            IntegerConstraint.All(Parameter),
            [
                new PeriodicInterval(
                    SolveAngleCoordinate(
                        pattern,
                        UnitAngle(angleUnit, lowerFraction)),
                    includeLower,
                    SolveAngleCoordinate(
                        pattern,
                        UnitAngle(angleUnit, upperFraction)),
                    includeUpper)
            ]);
    }

    private static ExactReal SolveAngleCoordinate(
        UnaryCompositionPattern pattern,
        ExactReal angle)
    {
        return ExactRealArithmetic.Scale(
            ExactRealArithmetic.AddRational(angle, -pattern.Phase),
            pattern.Frequency.Reciprocal());
    }

    private static ExactReal BuildTrigStep(
        UnaryCompositionPattern pattern,
        AngleUnit angleUnit,
        BigRational fraction)
    {
        return ExactRealArithmetic.Scale(
            UnitAngle(angleUnit, fraction),
            pattern.Frequency.Reciprocal());
    }

    private static RationalReal SolveAffineOutput(
        UnaryCompositionPattern pattern,
        BigRational target)
    {
        BigRational rawTarget = pattern.InnerSign * target;
        return Rational((rawTarget - pattern.Phase) / pattern.Frequency);
    }

    private static RationalReal AffineCenter(UnaryCompositionPattern pattern)
    {
        return Rational(-pattern.Phase / pattern.Frequency);
    }

    private static IntervalSet OrderedInterval(
        ExactReal first,
        bool includeFirst,
        ExactReal second,
        bool includeSecond)
    {
        if (first is RationalReal left &&
            second is RationalReal right &&
            left.Value > right.Value)
        {
            return new IntervalSet(
                RealBound.Finite(second),
                includeSecond,
                RealBound.Finite(first),
                includeFirst);
        }

        return new IntervalSet(
            RealBound.Finite(first),
            includeFirst,
            RealBound.Finite(second),
            includeSecond);
    }

    private static IntervalSet ClosedInterval(
        ExactReal lower,
        ExactReal upper)
    {
        return new IntervalSet(RealBound.Finite(lower), true, RealBound.Finite(upper), true);
    }

    private static FunctionReal ReciprocalE()
    {
        return new FunctionReal(
            "divide",
            [Rational(BigRational.One), new NamedReal("e")]);
    }

    private static ExactReal EvaluateUnitInput(
        string function,
        AngleUnit angleUnit)
    {
        return EvaluateOuter(function, Rational(BigRational.One), angleUnit);
    }

    private static ExactReal EvaluateOuter(
        string function,
        ExactReal value,
        AngleUnit angleUnit)
    {
        return function switch
        {
            "asin" or "acos" or "atan" => ExactAngleArithmetic.FromRadians(
                new FunctionReal(function, [value]),
                angleUnit),
            "sin" or "cos" or "tan" => new FunctionReal(
                function,
                [ExactAngleArithmetic.ToRadians(value, angleUnit)]),
            _ => new FunctionReal(function, [value])
        };
    }

    private static ExactReal UnitAngle(
        AngleUnit angleUnit,
        BigRational piFraction)
    {
        return ExactAngleArithmetic.PiFraction(angleUnit, piFraction);
    }

    private static bool TryClassifyQuarterTurn(
        BigRational phase,
        AngleUnit angleUnit,
        out bool oddMultiple)
    {
        if (angleUnit == AngleUnit.Radians)
        {
            oddMultiple = false;
            return phase.IsZero;
        }

        BigRational quarterTurn = angleUnit == AngleUnit.Degrees
            ? new BigRational(90)
            : new BigRational(100);
        BigRational multiple = phase / quarterTurn;
        if (!multiple.IsInteger)
        {
            oddMultiple = false;
            return false;
        }

        oddMultiple = !multiple.Numerator.IsEven;
        return true;
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
