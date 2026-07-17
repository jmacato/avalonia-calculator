using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Checker-owned reconstruction of the Puiseux phase, its exact monotonicity
/// evidence, and the requested trigonometric pullback claim. It never calls the
/// producer context factory or producer feature dispatcher.
/// </summary>
internal static class MonotoneTrigonometricPhaseCertificateReplay
{
    private const string Parameter = "n₁";
    private const string Rule = "strict-monotone-puiseux-trigonometric-pullback-v2";
    public static bool Check(AnalysisRequest request, SemanticExpression expression, MonotoneTrigonometricPhaseProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge(8);
        if (request.AngleUnit is not (AngleUnit.Radians or AngleUnit.Degrees or AngleUnit.Grads) || !string.Equals(certificate.Rule, Rule, StringComparison.Ordinal) || certificate.Feature != certificate.ProvenFeature || !CertificateFeatureBinding.IsSingleRequested(request, certificate.Feature) || !string.Equals(certificate.Variable, request.Variable, StringComparison.Ordinal) || certificate.AngleUnit != request.AngleUnit || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) || !string.Equals(certificate.DefinednessCanonical, expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.ContinuityCanonical, expression.ContinuousWhen.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DifferentiabilityCanonical, expression.DifferentiableWhen.Canonical, StringComparison.Ordinal) || !TryCreateReplayContext(request, expression, certificate, budget, out MonotoneTrigonometricPhaseCertificateReplayReplayContext context) || !TryReconstructFeature(context, certificate.Feature, budget, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryCreateReplayContext(AnalysisRequest request, SemanticExpression expression, MonotoneTrigonometricPhaseProofCertificate certificate, ResourceBudget budget, out MonotoneTrigonometricPhaseCertificateReplayReplayContext context)
    {
        if (!TryReplayOuter(expression, certificate, out string outerFunction, out ValueTerm phaseValue, out bool isDirectComposition, out ValueTerm? quotientDenominator) || !TryReplayDomain(request, expression, certificate, budget, out bool parameterIsNonnegative, out bool boundaryIncluded, out BigRational coordinateOffset) || !TryReplayPhase(request, phaseValue, certificate, coordinateOffset, parameterIsNonnegative, budget, out PuiseuxPolynomial phase, out int substitutionDegree, out UnivariatePolynomial parameterPolynomial))
        {
            context = default;
            return false;
        }

        UnivariatePolynomial derivative = parameterPolynomial.Derivative(budget);
        if (derivative.IsZero)
        {
            context = default;
            return false;
        }

        PhaseOrientation parameterOrientation = parameterPolynomial.LeadingCoefficient.Sign > 0 ? PhaseOrientation.Increasing : PhaseOrientation.Decreasing;
        PolynomialFormula violationFormula = BuildDerivativeViolationFormula(derivative, parameterIsNonnegative, boundaryIncluded, parameterOrientation);
        PhaseOrientation orientation = substitutionDegree > 0 ? parameterOrientation : parameterOrientation == PhaseOrientation.Increasing ? PhaseOrientation.Decreasing : PhaseOrientation.Increasing;
        if (parameterOrientation != certificate.ParameterOrientation || orientation != certificate.Orientation || !string.Equals(certificate.DerivativeViolationCells.Formula.Canonical, violationFormula.Canonical, StringComparison.Ordinal) || !CellDecomposer.Verify(certificate.DerivativeViolationCells, budget) || certificate.DerivativeViolationCells.Result is not EmptySet || !parameterIsNonnegative && (parameterPolynomial.Degree & 1) == 0 || substitutionDegree < 0 && boundaryIncluded)
        {
            context = default;
            return false;
        }

        context = new MonotoneTrigonometricPhaseCertificateReplayReplayContext(request.Variable, request.AngleUnit, outerFunction, isDirectComposition, quotientDenominator, phaseValue, phase, coordinateOffset, certificate.DomainCells.Result, parameterIsNonnegative, boundaryIncluded, substitutionDegree, parameterPolynomial, parameterOrientation, orientation);
        return true;
    }

    private static bool TryReplayOuter(SemanticExpression expression, MonotoneTrigonometricPhaseProofCertificate certificate, out string outerFunction, out ValueTerm phaseValue, out bool isDirectComposition, out ValueTerm? quotientDenominator) => TryExtractOuterFunction(expression.Value, certificate.Feature, out outerFunction, out phaseValue, out isDirectComposition, out quotientDenominator) && string.Equals(outerFunction, certificate.OuterFunction, StringComparison.Ordinal) && string.Equals(certificate.PhaseCanonical, phaseValue.Canonical, StringComparison.Ordinal);
    private static bool TryReplayDomain(AnalysisRequest request, SemanticExpression expression, MonotoneTrigonometricPhaseProofCertificate certificate, ResourceBudget budget, out bool parameterIsNonnegative, out bool boundaryIncluded, out BigRational coordinateOffset)
    {
        if (!PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, request.Variable, budget, out PolynomialFormula domainFormula) || !string.Equals(domainFormula.Canonical, certificate.DomainFormula.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.DomainCells.Formula.Canonical, domainFormula.Canonical, StringComparison.Ordinal) || !CellDecomposer.Verify(certificate.DomainCells, budget) || !TryClassifyDomain(certificate.DomainCells.Result, out parameterIsNonnegative, out boundaryIncluded, out coordinateOffset) || parameterIsNonnegative != certificate.ParameterIsNonnegative || boundaryIncluded != certificate.BoundaryIncluded || coordinateOffset != certificate.CoordinateOffset)
        {
            parameterIsNonnegative = default;
            boundaryIncluded = default;
            coordinateOffset = default;
            return false;
        }

        return true;
    }

    private static bool TryReplayPhase(AnalysisRequest request, ValueTerm phaseValue, MonotoneTrigonometricPhaseProofCertificate certificate, BigRational coordinateOffset, bool parameterIsNonnegative, ResourceBudget budget, out PuiseuxPolynomial phase, out int substitutionDegree, out UnivariatePolynomial parameterPolynomial)
    {
        if (!PuiseuxPolynomial.TryExtractInCoordinate(phaseValue, request.Variable, coordinateOffset, budget, out phase) || !phase.Terms.SequenceEqual(certificate.PhaseTerms) || !phase.TryLowerToPolynomial(parameterIsNonnegative, budget, out substitutionDegree, out parameterPolynomial) || substitutionDegree != certificate.SubstitutionDegree || !parameterPolynomial.Equals(certificate.ParameterPolynomial) || parameterPolynomial.Degree <= 0)
        {
            phase = null!;
            substitutionDegree = default;
            parameterPolynomial = null!;
            return false;
        }

        return true;
    }

    private static bool TryReconstructFeature(MonotoneTrigonometricPhaseCertificateReplayReplayContext context, AnalysisFeatures feature, ResourceBudget budget, out object value)
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
                value = new IntervalSet(RealBound.Finite(new RationalReal(BigRational.MinusOne)), true, RealBound.Finite(new RationalReal(BigRational.One)), true);
                return true;
            case AnalysisFeatures.Parity:
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

                value = null!;
                return false;
            case AnalysisFeatures.Zeros:
                if (!TryCreatePreimage(context, context.OuterFunction == "sin" ? BigRational.Zero : new BigRational(1, 2), BigRational.One, budget, out PolynomialPhasePreimage zeroPreimage))
                {
                    value = null!;
                    return false;
                }

                value = new PolynomialPhasePreimageSet(zeroPreimage);
                return true;
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

    private static bool TryBuildExtrema(MonotoneTrigonometricPhaseCertificateReplayReplayContext context, bool minimum, ResourceBudget budget, out object value)
    {
        if (context.ParameterIsNonnegative && context.BoundaryIncluded && !context.Phase.ConstantCoefficient.IsZero)
        {
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

    private static bool TryCreatePreimage(MonotoneTrigonometricPhaseCertificateReplayReplayContext context, BigRational offsetFraction, BigRational periodFraction, ResourceBudget budget, out PolynomialPhasePreimage preimage)
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

    private static bool TryBuildConstraint(MonotoneTrigonometricPhaseCertificateReplayReplayContext context, ExactReal targetOffset, ExactReal targetPeriod, ResourceBudget budget, out IntegerConstraint constraint)
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

    private static bool TryEvaluateAtOrigin(MonotoneTrigonometricPhaseCertificateReplayReplayContext context, ResourceBudget budget, out object value)
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

    private static ExactReal EvaluateOuter(MonotoneTrigonometricPhaseCertificateReplayReplayContext context, ExactReal phase)
    {
        if (phase is RationalReal { Value.IsZero: true })
        {
            return new RationalReal(context.OuterFunction == "sin" ? BigRational.Zero : BigRational.One);
        }

        return new FunctionReal(context.OuterFunction, [ExactAngleArithmetic.ToRadians(phase, context.AngleUnit)]);
    }

    private static bool TryBuildHorizontalAsymptotes(MonotoneTrigonometricPhaseCertificateReplayReplayContext context, ResourceBudget budget, out object value)
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

    private static ImmutableArray<MonotoneRegion> BuildMonotonicity(MonotoneTrigonometricPhaseCertificateReplayReplayContext context)
    {
        string factorFunction = context.OuterFunction == "sin" ? "cos" : "sin";
        int factorSign = context.OuterFunction == "sin" ? 1 : -1;
        var positive = new PolynomialPhaseSignSet(context.Phase.Canonical, context.Phase.Terms, context.Variable, context.CoordinateOffset, context.Domain, context.AngleUnit, factorFunction, factorSign, Comparison.Greater);
        var negative = positive with
        {
            Comparison = Comparison.Less
        };
        return context.Orientation == PhaseOrientation.Increasing ? [new MonotoneRegion(positive, Monotonicity.Increasing), new MonotoneRegion(negative, Monotonicity.Decreasing)] : [new MonotoneRegion(positive, Monotonicity.Decreasing), new MonotoneRegion(negative, Monotonicity.Increasing)];
    }

    private static PolynomialFormula BuildDerivativeViolationFormula(UnivariatePolynomial derivative, bool parameterIsNonnegative, bool boundaryIncluded, PhaseOrientation orientation)
    {
        var violation = new PolynomialAtom(derivative, orientation == PhaseOrientation.Increasing ? Comparison.Less : Comparison.Greater);
        return parameterIsNonnegative ? new PolynomialJunction(true, [new PolynomialAtom(UnivariatePolynomial.Variable, boundaryIncluded ? Comparison.GreaterOrEqual : Comparison.Greater), violation]) : violation;
    }

    private static Periodicity BuildPeriodicity(MonotoneTrigonometricPhaseCertificateReplayReplayContext context)
    {
        if (!context.ParameterIsNonnegative && context.Phase.IsAffine)
        {
            ExactReal fullTurn = ExactAngleArithmetic.PiFraction(context.AngleUnit, new BigRational(2));
            ExactReal period = ExactRealArithmetic.Scale(fullTurn, context.Phase.LinearCoefficient.Abs().Reciprocal());
            return new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, period);
        }

        return new Periodicity(PeriodicityKind.NotPeriodic, null);
    }

    private static bool TryClassifyDomain(RealSet domain, out bool parameterIsNonnegative, out bool boundaryIncluded, out BigRational coordinateOffset)
    {
        if (domain is AllRealSet)
        {
            parameterIsNonnegative = false;
            boundaryIncluded = true;
            coordinateOffset = BigRational.Zero;
            return true;
        }

        if (domain is IntervalSet { Lower: { Kind: BoundKind.Finite, Value: RationalReal { Value: var lower } }, IncludesLower: var includesLower, Upper.Kind: BoundKind.PositiveInfinity })
        {
            parameterIsNonnegative = true;
            boundaryIncluded = includesLower;
            coordinateOffset = lower;
            return true;
        }

        parameterIsNonnegative = default;
        boundaryIncluded = default;
        coordinateOffset = default;
        return false;
    }

    private static bool TryExtractOuterFunction(ValueTerm subject, AnalysisFeatures feature, out string outerFunction, out ValueTerm phase, out bool isDirectComposition, out ValueTerm? quotientDenominator)
    {
        isDirectComposition = true;
        quotientDenominator = null;
        ValueTerm candidate = subject;
        if (feature is (AnalysisFeatures.Zeros or AnalysisFeatures.HorizontalAsymptotes) && candidate is { Kind: ValueKind.Divide, Operands: [var numerator, var denominator] })
        {
            candidate = numerator;
            quotientDenominator = denominator;
            isDirectComposition = false;
        }

        if (candidate is { Kind: ValueKind.Function, Name: "sin" or "cos", Operands: [var phaseValue] })
        {
            outerFunction = candidate.Name;
            phase = phaseValue;
            return true;
        }

        outerFunction = string.Empty;
        phase = null!;
        return false;
    }
}
