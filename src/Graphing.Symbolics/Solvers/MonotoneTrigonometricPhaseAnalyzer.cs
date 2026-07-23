using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class MonotoneTrigonometricPhaseAnalyzer
{
    private const string Rule = "strict-monotone-puiseux-trigonometric-pullback-v2";
    private const string Parameter = "n₁";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!MonotoneTrigonometricPhaseContext.TryCreate(request, expression, feature, budget, out MonotoneTrigonometricPhaseContext context) || !TryComputeFeature(context, feature, budget, out object value) || value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new MonotoneTrigonometricPhaseProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), request.Variable, request.AngleUnit, context.OuterFunction, context.PhaseValue.Canonical, context.Phase.Terms, context.CoordinateOffset, context.SubstitutionDegree, context.ParameterPolynomial, context.ParameterIsNonnegative, context.BoundaryIncluded, context.DomainFormula, context.DomainCells, context.ParameterOrientation, context.Orientation, context.DerivativeViolationCells, expression.DefinedWhen.Canonical, expression.ContinuousWhen.Canonical, expression.DifferentiableWhen.Canonical, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    private static bool TryComputeFeature(MonotoneTrigonometricPhaseContext context, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        if (!context.IsDirectComposition && feature is not (AnalysisFeatures.Zeros or AnalysisFeatures.HorizontalAsymptotes))
        {
            value = null!;
            return false;
        }

        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = context.Domain;
                return true;
            case AnalysisFeatures.Range:
                value = UnitRange();
                return true;
            case AnalysisFeatures.Parity:
                return TryComputeParity(context, out value);
            case AnalysisFeatures.Zeros:
                return TryBuildPreimage(context, context.OuterFunction == "sin" ? BigRational.Zero : new BigRational(1, 2), BigRational.One, budget, out value);
            case AnalysisFeatures.YIntercept:
                return TryEvaluateAtOrigin(context, budget, out value);
            case AnalysisFeatures.Minima:
                return TryBuildExtrema(context, minimum: true, budget, out value);
            case AnalysisFeatures.Maxima:
                return TryBuildExtrema(context, minimum: false, budget, out value);
            case AnalysisFeatures.HorizontalAsymptotes:
                return TryBuildHorizontalAsymptotes(context, budget, out value);
            case AnalysisFeatures.VerticalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.Monotonicity:
                value = BuildMonotonicity(context);
                return true;
            case AnalysisFeatures.Period:
                value = BuildPeriodicity(context);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static bool TryComputeParity(MonotoneTrigonometricPhaseContext context, out object value)
    {
        if (context.ParameterIsNonnegative)
        {
            value = FunctionParity.Neither;
            return true;
        }

        if (context.OuterFunction == "cos" && (context.Phase.IsOdd || context.Phase.IsEven))
        {
            value = FunctionParity.Even;
            return true;
        }

        if (context.Phase.IsOdd)
        {
            value = FunctionParity.Odd;
            return true;
        }

        if (context.Phase.IsEven)
        {
            value = FunctionParity.Even;
            return true;
        }

        // Nonzero phase shifts require exact unit-aware symmetry analysis;
        // leave them unknown instead of guessing from the polynomial shape.
        value = null!;
        return false;
    }

    private static bool TryBuildExtrema(MonotoneTrigonometricPhaseContext context, bool minimum, ResourceBudget budget, out object value)
    {
        if (context.ParameterIsNonnegative && context.BoundaryIncluded && !context.Phase.ConstantCoefficient.IsZero)
        {
            // The periodic interior extrema remain exact, but the included
            // endpoint may itself be a one-sided extremum.  Until its
            // unit-aware trig sign is certified, publishing only the interior
            // family would be incomplete.
            value = null!;
            return false;
        }

        BigRational offset = context.OuterFunction == "sin" ? minimum ? new BigRational(3, 2) : new BigRational(1, 2) : minimum ? BigRational.One : BigRational.Zero;
        if (!TryCreatePreimage(context, offset, new BigRational(2), budget, out PolynomialPhasePreimage preimage))
        {
            value = null!;
            return false;
        }

        var points = ImmutableArray.CreateBuilder<FeaturePoint>(2);
        if (context.OuterFunction == "sin" && context.ParameterIsNonnegative && context.BoundaryIncluded && context.Phase.ConstantCoefficient.IsZero && (minimum && context.Orientation == PhaseOrientation.Increasing || !minimum && context.Orientation == PhaseOrientation.Decreasing))
        {
            points.Add(new ConstantYFeaturePoint(new SingletonReal(new RationalReal(context.CoordinateOffset)), new RationalReal(BigRational.Zero)));
        }

        points.Add(new ConstantYFeaturePoint(new PolynomialPhasePreimageReal(preimage), new RationalReal(minimum ? BigRational.MinusOne : BigRational.One)));
        value = points.ToImmutable();
        return true;
    }

    private static bool TryBuildPreimage(MonotoneTrigonometricPhaseContext context, BigRational offsetFraction, BigRational periodFraction, ResourceBudget budget, out object value)
    {
        if (!TryCreatePreimage(context, offsetFraction, periodFraction, budget, out PolynomialPhasePreimage preimage))
        {
            value = null!;
            return false;
        }

        value = new PolynomialPhasePreimageSet(preimage);
        return true;
    }

    private static bool TryCreatePreimage(MonotoneTrigonometricPhaseContext context, BigRational offsetFraction, BigRational periodFraction, ResourceBudget budget, out PolynomialPhasePreimage preimage)
    {
        ExactReal targetOffset = ExactAngleArithmetic.PiFraction(context.AngleUnit, offsetFraction);
        ExactReal targetPeriod = ExactAngleArithmetic.PiFraction(context.AngleUnit, periodFraction);
        if (!TryBuildConstraint(context, targetOffset, targetPeriod, budget, out IntegerConstraint constraint))
        {
            preimage = null!;
            return false;
        }

        preimage = new PolynomialPhasePreimage(context.Phase.Canonical, context.Phase.Terms, context.Variable, context.CoordinateOffset, context.ParameterPolynomial, context.SubstitutionDegree, context.ParameterIsNonnegative, context.BoundaryIncluded, targetOffset, targetPeriod, Parameter, constraint);
        return true;
    }

    private static bool TryBuildConstraint(MonotoneTrigonometricPhaseContext context, ExactReal targetOffset, ExactReal targetPeriod, ResourceBudget budget, out IntegerConstraint constraint)
    {
        if (!context.ParameterIsNonnegative)
        {
            constraint = IntegerConstraint.All(Parameter);
            return true;
        }

        var boundary = new RationalReal(context.Phase.ConstantCoefficient);
        Comparison thresholdComparison;
        bool lowerBound;
        if (context.ParameterOrientation == PhaseOrientation.Increasing)
        {
            thresholdComparison = context.BoundaryIncluded ? Comparison.GreaterOrEqual : Comparison.Greater;
            lowerBound = true;
        }
        else
        {
            // Find the first lattice value outside the decreasing phase
            // image, then step back once to obtain its greatest valid index.
            thresholdComparison = context.BoundaryIncluded ? Comparison.Greater : Comparison.GreaterOrEqual;
            lowerBound = false;
        }

        if (!TryFindFirstLatticeIndex(targetOffset, targetPeriod, boundary, thresholdComparison, budget, out ExactInteger first))
        {
            constraint = default;
            return false;
        }

        ExactInteger bound = lowerBound ? first : first - ExactInteger.One;
        constraint = new IntegerConstraint(Parameter, lowerBound ? Comparison.GreaterOrEqual : Comparison.LessOrEqual, new ExactIntegerConstant(bound, false));
        return true;
    }

    private static bool TryFindFirstLatticeIndex(ExactReal offset, ExactReal period, ExactReal boundary, Comparison comparison, ResourceBudget budget, out ExactInteger first)
    {
        bool Test(ExactInteger index, out bool result)
        {
            budget.CheckCoefficient(new BigRational(index));
            ExactReal target = ExactRealArithmetic.Add(offset, ExactRealArithmetic.Scale(period, new BigRational(index)));
            return ExactRealSubstitutionArithmetic.TryCompare(target, comparison, boundary, budget, out result);
        }

        if (!Test(ExactInteger.Zero, out bool zeroMatches))
        {
            first = default;
            return false;
        }

        ExactInteger low;
        ExactInteger high;
        if (zeroMatches)
        {
            high = ExactInteger.Zero;
            low = ExactInteger.MinusOne;
            while (true)
            {
                if (!Test(low, out bool matches))
                {
                    first = default;
                    return false;
                }

                if (!matches)
                {
                    break;
                }

                high = low;
                low <<= 1;
            }
        }
        else
        {
            low = ExactInteger.Zero;
            high = ExactInteger.One;
            while (true)
            {
                if (!Test(high, out bool matches))
                {
                    first = default;
                    return false;
                }

                if (matches)
                {
                    break;
                }

                low = high;
                high <<= 1;
            }
        }

        while (high - low > ExactInteger.One)
        {
            ExactInteger middle = (low + high) / new ExactInteger(2);
            if (!Test(middle, out bool matches))
            {
                first = default;
                return false;
            }

            if (matches)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        first = high;
        return true;
    }

    private static bool TryEvaluateAtOrigin(MonotoneTrigonometricPhaseContext context, ResourceBudget budget, out object value)
    {
        if (context.ParameterIsNonnegative && (context.CoordinateOffset.Sign > 0 || context.CoordinateOffset.IsZero && !context.BoundaryIncluded))
        {
            value = OptionalValue<ExactReal>.None;
            return true;
        }

        BigRational coordinate = -context.CoordinateOffset;
        if (coordinate.Sign < 0)
        {
            value = null!;
            return false;
        }

        ExactReal phase = new RationalReal(BigRational.Zero);
        foreach (PuiseuxTerm term in context.Phase.Terms)
        {
            ExactReal power = coordinate.IsZero && term.Exponent.Sign > 0 ? new RationalReal(BigRational.Zero) : term.Exponent.IsZero ? new RationalReal(BigRational.One) : term.Exponent.IsInteger && term.Exponent.Numerator >= int.MinValue && term.Exponent.Numerator <= int.MaxValue ? new RationalReal(coordinate.Pow(checked((int)term.Exponent.Numerator))) : FixedRationalPowerValue.Compose(new RationalReal(coordinate), term.Exponent, budget);
            ExactReal scaled = ExactRealArithmetic.Scale(power, term.Coefficient);
            phase = ExactRealArithmetic.Add(phase, scaled);
        }

        value = OptionalValue<ExactReal>.Some(EvaluateOuter(context, phase));
        return true;
    }

    private static ExactReal EvaluateOuter(MonotoneTrigonometricPhaseContext context, ExactReal phase)
    {
        if (phase is RationalReal { Value.IsZero: true })
        {
            return new RationalReal(context.OuterFunction == "sin" ? BigRational.Zero : BigRational.One);
        }

        return new FunctionReal(context.OuterFunction, [ExactAngleArithmetic.ToRadians(phase, context.AngleUnit)]);
    }

    private static bool TryBuildHorizontalAsymptotes(MonotoneTrigonometricPhaseContext context, ResourceBudget budget, out object value)
    {
        if (!context.IsDirectComposition)
        {
            if (context.QuotientDenominator is null || !PuiseuxPolynomial.TryExtractInCoordinate(context.QuotientDenominator, context.Variable, context.CoordinateOffset, budget, out PuiseuxPolynomial denominator) || !denominator.TryGetLeadingTerm(out PuiseuxTerm leading) || leading.Exponent.Sign <= 0)
            {
                value = null!;
                return false;
            }

            ExactReal zero = new RationalReal(BigRational.Zero);
            value = ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(zero), null, zero));
            return true;
        }

        if (context.SubstitutionDegree > 0)
        {
            value = ImmutableArray<Asymptote>.Empty;
            return true;
        }

        ExactReal limit = EvaluateOuter(context, new RationalReal(context.Phase.ConstantCoefficient));
        ExactRealSubstitutionArithmetic.ValidateCoefficients(limit, budget);
        value = ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(limit), null, limit));
        return true;
    }

    private static ImmutableArray<MonotoneRegion> BuildMonotonicity(MonotoneTrigonometricPhaseContext context)
    {
        string factorFunction = context.OuterFunction == "sin" ? "cos" : "sin";
        int factorSign = context.OuterFunction == "sin" ? 1 : -1;
        var positiveDerivative = new PolynomialPhaseSignSet(context.Phase.Canonical, context.Phase.Terms, context.Variable, context.CoordinateOffset, context.Domain, context.AngleUnit, factorFunction, factorSign, Comparison.Greater);
        var negativeDerivative = positiveDerivative with
        {
            Comparison = Comparison.Less
        };
        return context.Orientation == PhaseOrientation.Increasing ? [new MonotoneRegion(positiveDerivative, Monotonicity.Increasing), new MonotoneRegion(negativeDerivative, Monotonicity.Decreasing)] : [new MonotoneRegion(positiveDerivative, Monotonicity.Decreasing), new MonotoneRegion(negativeDerivative, Monotonicity.Increasing)];
    }

    private static IntervalSet UnitRange()
    {
        return new IntervalSet(RealBound.Finite(new RationalReal(BigRational.MinusOne)), true,
            RealBound.Finite(new RationalReal(BigRational.One)), true);
    }

    private static Periodicity BuildPeriodicity(MonotoneTrigonometricPhaseContext context)
    {
        if (!context.ParameterIsNonnegative && context.Phase.IsAffine)
        {
            ExactReal fullTurn = ExactAngleArithmetic.PiFraction(context.AngleUnit, new BigRational(2));
            ExactReal period = ExactRealArithmetic.Scale(fullTurn, context.Phase.LinearCoefficient.Abs().Reciprocal());
            return new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, period);
        }

        return new Periodicity(PeriodicityKind.NotPeriodic, null);
    }
}
