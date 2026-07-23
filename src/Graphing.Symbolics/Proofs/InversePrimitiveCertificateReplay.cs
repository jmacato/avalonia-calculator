using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Independent replay for affine transforms of asin, acos, and atan.
/// Recognition may be shared with the semantic pattern extractor; every
/// published set and value is reconstructed here.
/// </summary>
internal static class InversePrimitiveCertificateReplay
{
    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Parameters.Length != 3 ||
            !TrigonometricAndLatticeAnalyzer.TryGetAffinePrimitive(
                expression.Value,
                request.Variable,
                budget,
                out AffinePrimitivePattern pattern) ||
            pattern.Function is not ("asin" or "acos" or "atan") ||
            !string.Equals(certificate.Parameters[0], pattern.Canonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Parameters[1], request.AngleUnit.ToString(), StringComparison.Ordinal) ||
            !string.Equals(
                certificate.Parameters[2],
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal))
        {
            return false;
        }

        RealSet domain = BuildDomain(pattern, budget);
        if (!DefinednessMatches(expression, pattern, budget) ||
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

    private static bool TryReconstruct(
        AffinePrimitivePattern pattern,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        RealSet domain,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = domain;
                return true;
            case AnalysisFeatures.Range:
                value = BuildRange(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.Parity:
                value = BuildParity(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.Zeros:
                return TryBuildZeros(pattern, budget, out value);
            case AnalysisFeatures.YIntercept:
                value = BuildYIntercept(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.Minima:
                value = BuildExtrema(pattern, angleUnit, minimum: true, budget);
                return true;
            case AnalysisFeatures.Maxima:
                value = BuildExtrema(pattern, angleUnit, minimum: false, budget);
                return true;
            case AnalysisFeatures.InflectionPoints:
                value = BuildInflections(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
                value = BuildHorizontalAsymptotes(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.Monotonicity:
                value = BuildMonotonicity(pattern, domain);
                return true;
            case AnalysisFeatures.Period:
                value = new Periodicity(PeriodicityKind.NotPeriodic, null);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static RealSet BuildDomain(
        AffinePrimitivePattern pattern,
        ResourceBudget budget)
    {
        if (pattern.Function == "atan")
        {
            return AllRealSet.Instance;
        }

        BigRational atMinusOne = SolveInnerRational(
            pattern,
            BigRational.MinusOne,
            budget);
        BigRational atOne = SolveInnerRational(pattern, BigRational.One, budget);
        BigRational lower = pattern.InnerSlope.Sign > 0 ? atMinusOne : atOne;
        BigRational upper = pattern.InnerSlope.Sign > 0 ? atOne : atMinusOne;
        return new IntervalSet(
            RealBound.Finite(new RationalReal(lower)),
            true,
            RealBound.Finite(new RationalReal(upper)),
            true);
    }

    private static bool DefinednessMatches(
        SemanticExpression expression,
        AffinePrimitivePattern pattern,
        ResourceBudget budget)
    {
        if (pattern.Function == "atan")
        {
            return ExactFormulaVerifier.IsAlwaysTrue(expression.DefinedWhen, budget);
        }

        if (pattern.Function is not ("asin" or "acos") ||
            pattern.Core.Operands is not [var argument])
        {
            return false;
        }

        ValueTerm minusOne = Constant(BigRational.MinusOne);
        ValueTerm one = Constant(BigRational.One);
        return ExactFormulaVerifier.MatchesRequiredGuards(
            expression.DefinedWhen,
            [
                Formula.Compare(argument, Comparison.GreaterOrEqual, minusOne),
                Formula.Compare(argument, Comparison.LessOrEqual, one)
            ],
            budget);
    }

    private static ValueTerm Constant(BigRational value)
    {
        return new ValueTerm(
            int.MinValue + value.Sign + 1,
            ValueKind.Constant,
            value,
            string.Empty,
            [],
            $"q:{value}");
    }

    private static IntervalSet BuildRange(
        AffinePrimitivePattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        ExactReal zero = new RationalReal(BigRational.Zero);
        ExactReal negativeHalfTurn = ExactAngleArithmetic.PiFraction(
            angleUnit,
            new BigRational(-1, 2));
        ExactReal halfTurn = ExactAngleArithmetic.PiFraction(
            angleUnit,
            new BigRational(1, 2));
        return pattern.Function switch
        {
            "asin" => TransformInterval(
                pattern,
                negativeHalfTurn,
                true,
                halfTurn,
                true,
                budget),
            "acos" => TransformInterval(
                pattern,
                zero,
                true,
                ExactAngleArithmetic.PiFraction(angleUnit, BigRational.One),
                true,
                budget),
            "atan" => TransformInterval(
                pattern,
                negativeHalfTurn,
                false,
                halfTurn,
                false,
                budget),
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
    }

    private static IntervalSet TransformInterval(
        AffinePrimitivePattern pattern,
        ExactReal lower,
        bool includesLower,
        ExactReal upper,
        bool includesUpper,
        ResourceBudget budget)
    {
        ExactReal transformedLower = TransformOutput(pattern, lower, budget);
        ExactReal transformedUpper = TransformOutput(pattern, upper, budget);
        return pattern.OuterScale.Sign > 0
            ? new IntervalSet(
                RealBound.Finite(transformedLower),
                includesLower,
                RealBound.Finite(transformedUpper),
                includesUpper)
            : new IntervalSet(
                RealBound.Finite(transformedUpper),
                includesUpper,
                RealBound.Finite(transformedLower),
                includesLower);
    }

    private static FunctionParity BuildParity(
        AffinePrimitivePattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        if (!pattern.InnerIntercept.IsZero)
        {
            return FunctionParity.Neither;
        }

        if (pattern.Function == "acos")
        {
            ExactReal center = TransformOutput(
                pattern,
                ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(1, 2)),
                budget);
            return IsZero(center) ? FunctionParity.Odd : FunctionParity.Neither;
        }

        return pattern.OuterShift.IsZero
            ? FunctionParity.Odd
            : FunctionParity.Neither;
    }

    private static bool TryBuildZeros(
        AffinePrimitivePattern pattern,
        ResourceBudget budget,
        out object value)
    {
        BigRational target = Checked(
            -pattern.OuterShift / pattern.OuterScale,
            budget);
        if (pattern.Function is "asin" or "atan" && target.IsZero)
        {
            value = PointAtInnerValue(
                pattern,
                new RationalReal(BigRational.Zero),
                budget);
            return true;
        }

        if (pattern.Function == "acos" && target.IsZero)
        {
            value = PointAtInnerValue(
                pattern,
                new RationalReal(BigRational.One),
                budget);
            return true;
        }

        value = null!;
        return false;
    }

    private static OptionalValue<ExactReal> BuildYIntercept(
        AffinePrimitivePattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        BigRational argument = pattern.InnerIntercept;
        if (pattern.Function is "asin" or "acos" &&
            (argument < BigRational.MinusOne || argument > BigRational.One))
        {
            return OptionalValue<ExactReal>.None;
        }

        return OptionalValue<ExactReal>.Some(
            TransformOutput(
                pattern,
                PrimitiveAt(pattern, argument, angleUnit),
                budget));
    }

    private static ImmutableArray<FeaturePoint> BuildExtrema(
        AffinePrimitivePattern pattern,
        AngleUnit angleUnit,
        bool minimum,
        ResourceBudget budget)
    {
        if (pattern.Function == "atan")
        {
            return [];
        }

        bool useBaseMinimum = pattern.OuterScale.Sign > 0 == minimum;
        BigRational inner = (pattern.Function, useBaseMinimum) switch
        {
            ("asin", true) => BigRational.MinusOne,
            ("asin", false) => BigRational.One,
            ("acos", true) => BigRational.One,
            _ => BigRational.MinusOne
        };
        ExactReal x = new RationalReal(SolveInnerRational(pattern, inner, budget));
        ExactReal y = TransformOutput(
            pattern,
            PrimitiveAt(pattern, inner, angleUnit),
            budget);
        return [new ConstantYFeaturePoint(new SingletonReal(x), y)];
    }

    private static ImmutableArray<FeaturePoint> BuildInflections(
        AffinePrimitivePattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        ExactReal x = new RationalReal(
            SolveInnerRational(pattern, BigRational.Zero, budget));
        ExactReal y = TransformOutput(
            pattern,
            PrimitiveAt(pattern, BigRational.Zero, angleUnit),
            budget);
        return [new ConstantYFeaturePoint(new SingletonReal(x), y)];
    }

    private static ImmutableArray<Asymptote> BuildHorizontalAsymptotes(
        AffinePrimitivePattern pattern,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        if (pattern.Function != "atan")
        {
            return [];
        }

        ExactReal positive = TransformOutput(
            pattern,
            ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(1, 2)),
            budget);
        ExactReal negative = TransformOutput(
            pattern,
            ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(-1, 2)),
            budget);
        return [Horizontal(positive), Horizontal(negative)];
    }

    private static Asymptote Horizontal(ExactReal y)
    {
        return new Asymptote(
            AsymptoteOrientation.Horizontal,
            new SingletonReal(y),
            null,
            y);
    }

    private static ImmutableArray<MonotoneRegion> BuildMonotonicity(
        AffinePrimitivePattern pattern,
        RealSet domain)
    {
        int primitiveDirection = pattern.Function == "acos" ? -1 : 1;
        int direction = primitiveDirection *
                        pattern.InnerSlope.Sign *
                        pattern.OuterScale.Sign;
        return
        [
            new MonotoneRegion(
                domain,
                direction > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing)
        ];
    }

    private static ExactReal PrimitiveAt(
        AffinePrimitivePattern pattern,
        BigRational argument,
        AngleUnit angleUnit)
    {
        return ExactInverseTrigonometry.PrincipalAngle(
            pattern.Function,
            ExactScalar.FromRational(argument),
            angleUnit,
            normalizeOddNegative: false);
    }

    private static RealSet PointAtInnerValue(
        AffinePrimitivePattern pattern,
        ExactReal innerValue,
        ResourceBudget budget)
    {
        return RealSets.Points([SolveInnerExact(pattern, innerValue, budget)]);
    }

    private static ExactReal SolveInnerExact(
        AffinePrimitivePattern pattern,
        ExactReal innerValue,
        ResourceBudget budget)
    {
        budget.CheckCoefficient(pattern.InnerIntercept);
        budget.CheckCoefficient(pattern.InnerSlope);
        return Checked(
            ExactRealArithmetic.Scale(
                ExactRealArithmetic.AddRational(innerValue, -pattern.InnerIntercept),
                pattern.InnerSlope.Reciprocal()),
            budget);
    }

    private static BigRational SolveInnerRational(
        AffinePrimitivePattern pattern,
        BigRational innerValue,
        ResourceBudget budget)
    {
        return Checked(
            (innerValue - pattern.InnerIntercept) / pattern.InnerSlope,
            budget);
    }

    private static ExactReal TransformOutput(
        AffinePrimitivePattern pattern,
        ExactReal primitive,
        ResourceBudget budget)
    {
        return Checked(
            ExactRealArithmetic.AddRational(
                ExactRealArithmetic.Scale(primitive, pattern.OuterScale),
                pattern.OuterShift),
            budget);
    }

    private static bool IsZero(ExactReal value)
    {
        return value switch
        {
            RationalReal rational => rational.Value.IsZero,
            AffinePiReal affine => affine.PiCoefficient.IsZero && affine.Constant.IsZero,
            _ => false
        };
    }

    private static BigRational Checked(
        BigRational value,
        ResourceBudget budget)
    {
        budget.Charge();
        budget.CheckCoefficient(value);
        return value;
    }

    private static ExactReal Checked(
        ExactReal value,
        ResourceBudget budget)
    {
        budget.Charge();
        switch (value)
        {
            case RationalReal rational:
                budget.CheckCoefficient(rational.Value);
                break;
            case AffinePiReal affine:
                budget.CheckCoefficient(affine.PiCoefficient);
                budget.CheckCoefficient(affine.Constant);
                break;
            case FunctionReal function:
                foreach (ExactReal argument in function.Arguments)
                {
                    Checked(argument, budget);
                }

                break;
        }

        return value;
    }
}
