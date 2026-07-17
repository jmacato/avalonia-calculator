using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Re-derives claims for a nonzero rational linear drift plus one affine sine
/// or cosine.  Pattern recognition is shared; derivative classification and
/// every claim constructor are replayed independently.
/// </summary>
internal static class LinearDriftTrigCertificateReplay
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, TheoremProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (request.AngleUnit != AngleUnit.Radians || certificate.Parameters.Length != 3 || !LinearDriftTrigAnalyzer.TryCreatePattern(expression.Value, request.Variable, budget, out LinearDriftTrigPattern pattern) || !string.Equals(certificate.Parameters[0], pattern.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Parameters[1], nameof(AngleUnit.Radians), StringComparison.Ordinal) || !string.Equals(certificate.Parameters[2], expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !ExactFormulaVerifier.IsAlwaysTrue(expression.DefinedWhen, budget) || !TryReconstruct(pattern, certificate.Feature, budget, out object expected))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool TryReconstruct(LinearDriftTrigPattern pattern, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge();
        switch (feature)
        {
            case AnalysisFeatures.Domain:
            case AnalysisFeatures.Range:
                value = AllRealSet.Instance;
                return true;
            case AnalysisFeatures.Parity:
                value = BuildParity(pattern);
                return true;
            case AnalysisFeatures.Zeros:
                return TryBuildZeros(pattern, out value);
            case AnalysisFeatures.YIntercept:
                value = OptionalValue<ExactReal>.Some(BuildYIntercept(pattern, budget));
                return true;
            case AnalysisFeatures.Minima:
                return TryBuildExtrema(pattern, minimum: true, budget, out value);
            case AnalysisFeatures.Maxima:
                return TryBuildExtrema(pattern, minimum: false, budget, out value);
            case AnalysisFeatures.InflectionPoints:
                value = BuildInflections(pattern, budget);
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
            case AnalysisFeatures.HorizontalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.Monotonicity:
                return TryBuildMonotonicity(pattern, out value);
            case AnalysisFeatures.Period:
                value = new Periodicity(PeriodicityKind.NotPeriodic, null);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static FunctionParity BuildParity(LinearDriftTrigPattern pattern)
    {
        if (!pattern.Trig.Phase.IsZero)
        {
            return FunctionParity.Neither;
        }

        return pattern.Trig.Function == "sin" && pattern.Intercept.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
    }

    private static bool TryBuildZeros(LinearDriftTrigPattern pattern, out object value)
    {
        if (pattern.DerivativeRegime is not (LinearDriftDerivativeRegime.Increasing or LinearDriftDerivativeRegime.Decreasing) || pattern.Trig.Function != "sin" || !pattern.Trig.Phase.IsZero || !pattern.Intercept.IsZero)
        {
            value = null!;
            return false;
        }

        value = RealSets.Points([new RationalReal(BigRational.Zero)]);
        return true;
    }

    private static ExactReal BuildYIntercept(LinearDriftTrigPattern pattern, ResourceBudget budget)
    {
        if (pattern.Trig.Phase.IsZero)
        {
            ExactReal trigValue = pattern.Trig.Function == "cos" ? pattern.Trig.Amplitude.Value : new RationalReal(BigRational.Zero);
            return Checked(ExactRealArithmetic.Add(trigValue, pattern.Intercept.Value), budget);
        }

        ExactReal primitive = new FunctionReal(pattern.Trig.Function, [new RationalReal(pattern.Trig.Phase)]);
        return Checked(ExactRealArithmetic.Add(ExactRealArithmetic.Multiply(pattern.Trig.Amplitude.Value, primitive), pattern.Intercept.Value), budget);
    }

    private static bool TryBuildExtrema(LinearDriftTrigPattern pattern, bool minimum, ResourceBudget budget, out object value)
    {
        if (pattern.DerivativeRegime == LinearDriftDerivativeRegime.Unknown)
        {
            value = null!;
            return false;
        }

        if (pattern.DerivativeRegime != LinearDriftDerivativeRegime.Oscillatory)
        {
            value = ImmutableArray<FeaturePoint>.Empty;
            return true;
        }

        ExactScalar derivativeAmplitude = pattern.Trig.Amplitude.Multiply(ExactScalar.FromRational(pattern.Trig.Frequency), budget);
        BigRational targetNumerator = pattern.Trig.Function == "sin" ? -pattern.Slope : pattern.Slope;
        ExactScalar target = ExactScalar.FromRational(targetNumerator).Multiply(derivativeAmplitude.Reciprocal(budget), budget);
        LinearDriftTrigCertificateReplayCriticalAnglePair angles = CriticalAngles(pattern.Trig.Function, target, budget);
        bool principalIsMaximum = pattern.Trig.Amplitude.Sign > 0;
        ExactReal angle = minimum == principalIsMaximum ? angles.Reflected : angles.Principal;
        IntegerAffineFeaturePoint point;
        if (pattern.Trig.Amplitude.RationalValue is { } rationalAmplitude && target.RationalValue is { } rationalTarget)
        {
            BigRational square = rationalTarget * rationalTarget;
            budget.CheckCoefficient(square);
            BigRational radicand = BigRational.One - square;
            budget.CheckCoefficient(radicand);
            ExactReal positiveTrig = SquareRoot(radicand);
            Checked(positiveTrig, budget);
            point = RationalExtremumPoint(pattern, angle, positiveTrig, minimum ? -rationalAmplitude.Abs() : rationalAmplitude.Abs(), budget);
        }
        else
        {
            ExactReal magnitude = OscillationMagnitude(pattern, derivativeAmplitude, budget);
            point = ExactExtremumPoint(pattern, angle, magnitude, minimum, budget);
        }

        value = ImmutableArray.Create<FeaturePoint>(point);
        return true;
    }

    private static IntegerAffineFeaturePoint RationalExtremumPoint(LinearDriftTrigPattern pattern, ExactReal angle, ExactReal positiveTrig, BigRational amplitudeFactor, ResourceBudget budget)
    {
        BigRational reciprocalFrequency = pattern.Trig.Frequency.Reciprocal();
        budget.CheckCoefficient(reciprocalFrequency);
        BigRational rationalOffset = -pattern.Trig.Phase * reciprocalFrequency;
        budget.CheckCoefficient(rationalOffset);
        ExactReal xOffset = Checked(ExactRealArithmetic.AddRational(ExactRealArithmetic.Scale(angle, reciprocalFrequency), rationalOffset), budget);
        ExactReal xStep = Checked(new AffinePiReal(new BigRational(2) * reciprocalFrequency, BigRational.Zero), budget);
        ExactReal trigValue = Checked(ScaleRadical(positiveTrig, amplitudeFactor), budget);
        ExactReal lineValue = Checked(ExactRealArithmetic.Add(ExactRealArithmetic.Scale(xOffset, pattern.Slope), pattern.Intercept.Value), budget);
        ExactReal yOffset = Checked(ExactRealArithmetic.Add(lineValue, trigValue), budget);
        ExactReal yStep = Checked(ExactRealArithmetic.Scale(xStep, pattern.Slope), budget);
        return new IntegerAffineFeaturePoint(xOffset, xStep, yOffset, yStep, "m", IntegerConstraint.All("m"));
    }

    private static IntegerAffineFeaturePoint ExactExtremumPoint(LinearDriftTrigPattern pattern, ExactReal angle, ExactReal magnitude, bool minimum, ResourceBudget budget)
    {
        BigRational reciprocalFrequency = pattern.Trig.Frequency.Reciprocal();
        budget.CheckCoefficient(reciprocalFrequency);
        BigRational rationalOffset = -pattern.Trig.Phase * reciprocalFrequency;
        budget.CheckCoefficient(rationalOffset);
        ExactReal xOffset = Checked(TransformOrdered(angle, reciprocalFrequency, rationalOffset), budget);
        ExactReal xStep = Checked(new AffinePiReal(new BigRational(2) * reciprocalFrequency, BigRational.Zero), budget);
        var ordinateTerms = ImmutableArray.CreateBuilder<ExactReal>();
        ordinateTerms.Add(minimum ? ExactRealArithmetic.Negate(magnitude) : magnitude);
        foreach (ExactReal term in FlattenAddition(xOffset))
        {
            ordinateTerms.Add(ExactRealArithmetic.Scale(term, pattern.Slope));
        }

        ordinateTerms.AddRange(FlattenAddition(pattern.Intercept.Value));
        ExactReal yOffset = Checked(OrderedAdd(ordinateTerms), budget);
        ExactReal yStep = Checked(ExactRealArithmetic.Scale(xStep, pattern.Slope), budget);
        return new IntegerAffineFeaturePoint(xOffset, xStep, yOffset, yStep, "m", IntegerConstraint.All("m"));
    }

    private static ExactReal OscillationMagnitude(LinearDriftTrigPattern pattern, ExactScalar derivativeAmplitude, ResourceBudget budget)
    {
        ExactReal derivativeSquare = ExactRealArithmetic.Power(derivativeAmplitude.Abs().Value, 2);
        BigRational slopeSquare = pattern.Slope * pattern.Slope;
        budget.CheckCoefficient(slopeSquare);
        ExactReal radicand = ExactRealArithmetic.Subtract(derivativeSquare, new RationalReal(slopeSquare));
        ExactReal root = new FunctionReal("sqrt", [radicand]);
        ExactReal magnitude = ExactRealArithmetic.Divide(root, new RationalReal(pattern.Trig.Frequency.Abs()));
        return Checked(magnitude, budget);
    }

    private static LinearDriftTrigCertificateReplayCriticalAnglePair CriticalAngles(string function, ExactScalar target, ResourceBudget budget)
    {
        budget.Charge();
        if (target.RationalValue is { } rational)
        {
            ExactReal principal = function == "sin" ? InverseCosine(rational) : InverseSine(rational);
            ExactReal reflected = function == "sin" ? SubtractFromTwoPi(principal) : SubtractFromPi(principal);
            return new LinearDriftTrigCertificateReplayCriticalAnglePair(principal, reflected);
        }

        ExactReal inverse = new FunctionReal(function == "sin" ? "acos" : "asin", [target.Abs().Value]);
        ExactReal pi = new AffinePiReal(BigRational.One, BigRational.Zero);
        ExactReal twoPi = new AffinePiReal(new BigRational(2), BigRational.Zero);
        if (function == "sin")
        {
            return target.Sign < 0 ? new LinearDriftTrigCertificateReplayCriticalAnglePair(OrderedAdd([ExactRealArithmetic.Negate(inverse), pi]), OrderedAdd([inverse, pi])) : new LinearDriftTrigCertificateReplayCriticalAnglePair(inverse, OrderedAdd([twoPi, ExactRealArithmetic.Negate(inverse)]));
        }

        return target.Sign < 0 ? new LinearDriftTrigCertificateReplayCriticalAnglePair(ExactRealArithmetic.Negate(inverse), OrderedAdd([inverse, pi])) : new LinearDriftTrigCertificateReplayCriticalAnglePair(inverse, OrderedAdd([pi, ExactRealArithmetic.Negate(inverse)]));
    }

    private static ExactReal TransformOrdered(ExactReal value, BigRational scale, BigRational addend)
    {
        var terms = ImmutableArray.CreateBuilder<ExactReal>();
        foreach (ExactReal term in FlattenAddition(value))
        {
            terms.Add(ExactRealArithmetic.Scale(term, scale));
        }

        if (!addend.IsZero)
        {
            terms.Add(new RationalReal(addend));
        }

        return OrderedAdd(terms);
    }

    private static ImmutableArray<ExactReal> FlattenAddition(ExactReal value)
    {
        var result = ImmutableArray.CreateBuilder<ExactReal>();
        var pending = new Stack<ExactReal>();
        pending.Push(value);
        while (pending.Count != 0)
        {
            ExactReal current = pending.Pop();
            if (current is FunctionReal { Function: "add", Arguments: [var left, var right] })
            {
                pending.Push(right);
                pending.Push(left);
            }
            else if (current is not RationalReal { Value.IsZero: true })
            {
                result.Add(current);
            }
        }

        return result.ToImmutable();
    }

    private static ExactReal OrderedAdd(IEnumerable<ExactReal> values)
    {
        using IEnumerator<ExactReal> enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return new RationalReal(BigRational.Zero);
        }

        ExactReal result = enumerator.Current;
        while (enumerator.MoveNext())
        {
            result = ExactRealArithmetic.Add(result, enumerator.Current);
        }

        return result;
    }

    private static ImmutableArray<FeaturePoint> BuildInflections(LinearDriftTrigPattern pattern, ResourceBudget budget)
    {
        BigRational reciprocalFrequency = pattern.Trig.Frequency.Reciprocal();
        budget.CheckCoefficient(reciprocalFrequency);
        BigRational piOffset = pattern.Trig.Function == "cos" ? new BigRational(1, 2) * reciprocalFrequency : BigRational.Zero;
        budget.CheckCoefficient(piOffset);
        BigRational rationalOffset = -pattern.Trig.Phase * reciprocalFrequency;
        budget.CheckCoefficient(rationalOffset);
        ExactReal xOffset = Checked(new AffinePiReal(piOffset, rationalOffset), budget);
        ExactReal xStep = Checked(new AffinePiReal(reciprocalFrequency, BigRational.Zero), budget);
        ExactReal yOffset = Checked(ExactRealArithmetic.Add(ExactRealArithmetic.Scale(xOffset, pattern.Slope), pattern.Intercept.Value), budget);
        ExactReal yStep = Checked(ExactRealArithmetic.Scale(xStep, pattern.Slope), budget);
        return [new IntegerAffineFeaturePoint(xOffset, xStep, yOffset, yStep, "m", IntegerConstraint.All("m"))];
    }

    private static ExactReal InverseCosine(BigRational value) => value switch
    {
        _ when value == BigRational.MinusOne => new AffinePiReal(BigRational.One, BigRational.Zero),
        _ when value == new BigRational(-1, 2) => new AffinePiReal(new BigRational(2, 3), BigRational.Zero),
        _ when value.IsZero => new AffinePiReal(new BigRational(1, 2), BigRational.Zero),
        _ when value == new BigRational(1, 2) => new AffinePiReal(new BigRational(1, 3), BigRational.Zero),
        _ when value == BigRational.One => new RationalReal(BigRational.Zero),
        _ => new FunctionReal("acos", [new RationalReal(value)])
    };
    private static ExactReal InverseSine(BigRational value) => value switch
    {
        _ when value == BigRational.MinusOne => new AffinePiReal(new BigRational(-1, 2), BigRational.Zero),
        _ when value == new BigRational(-1, 2) => new AffinePiReal(new BigRational(-1, 6), BigRational.Zero),
        _ when value.IsZero => new RationalReal(BigRational.Zero),
        _ when value == new BigRational(1, 2) => new AffinePiReal(new BigRational(1, 6), BigRational.Zero),
        _ when value == BigRational.One => new AffinePiReal(new BigRational(1, 2), BigRational.Zero),
        _ => new FunctionReal("asin", [new RationalReal(value)])
    };
    private static ExactReal SubtractFromPi(ExactReal value) => value switch
    {
        RationalReal rational => new AffinePiReal(BigRational.One, -rational.Value),
        AffinePiReal affine => new AffinePiReal(BigRational.One - affine.PiCoefficient, -affine.Constant),
        _ => new FunctionReal("pi-minus", [value])
    };
    private static ExactReal SubtractFromTwoPi(ExactReal value) => value switch
    {
        RationalReal rational => new AffinePiReal(new BigRational(2), -rational.Value),
        AffinePiReal affine => new AffinePiReal(new BigRational(2) - affine.PiCoefficient, -affine.Constant),
        _ => ExactRealArithmetic.Subtract(new AffinePiReal(new BigRational(2), BigRational.Zero), value)
    };
    private static ExactReal SquareRoot(BigRational value)
    {
        if (BigRational.TrySquareRoot(value, out BigRational rational))
        {
            return new RationalReal(rational);
        }

        BigRational numerator = new(value.Numerator);
        BigRational denominator = new(value.Denominator);
        ExactReal numeratorRoot = BigRational.TrySquareRoot(numerator, out BigRational rationalNumerator) ? new RationalReal(rationalNumerator) : new FunctionReal("sqrt", [new RationalReal(numerator)]);
        if (!BigRational.TrySquareRoot(denominator, out BigRational rationalDenominator))
        {
            return new FunctionReal("sqrt", [new RationalReal(value)]);
        }

        return rationalDenominator.IsOne ? numeratorRoot : new FunctionReal("divide", [numeratorRoot, new RationalReal(rationalDenominator)]);
    }

    private static ExactReal ScaleRadical(ExactReal value, BigRational factor)
    {
        if (value is FunctionReal { Function: "divide", Arguments: [var numerator, RationalReal denominator] })
        {
            return ScaleIrrational(numerator, factor / denominator.Value);
        }

        return ExactRealArithmetic.Scale(value, factor);
    }

    private static ExactReal ScaleIrrational(ExactReal value, BigRational factor)
    {
        if (factor.Sign < 0)
        {
            return ExactRealArithmetic.Negate(ScaleIrrational(value, -factor));
        }

        ExactReal numerator = factor.Numerator.IsOne ? value : new FunctionReal("scale", [value, new RationalReal(new BigRational(factor.Numerator))]);
        return factor.Denominator.IsOne ? numerator : new FunctionReal("divide", [numerator, new RationalReal(new BigRational(factor.Denominator))]);
    }

    private static ExactReal Checked(ExactReal value, ResourceBudget budget)
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

    private static bool TryBuildMonotonicity(LinearDriftTrigPattern pattern, out object value)
    {
        switch (pattern.DerivativeRegime)
        {
            case LinearDriftDerivativeRegime.Increasing:
                value = ImmutableArray.Create(new MonotoneRegion(AllRealSet.Instance, Monotonicity.Increasing));
                return true;
            case LinearDriftDerivativeRegime.Decreasing:
                value = ImmutableArray.Create(new MonotoneRegion(AllRealSet.Instance, Monotonicity.Decreasing));
                return true;
            default:
                value = null!;
                return false;
        }
    }
}
