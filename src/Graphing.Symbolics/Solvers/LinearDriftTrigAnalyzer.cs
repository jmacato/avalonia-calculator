using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Proves features of a nonzero linear drift plus one bounded affine sine or
/// cosine. The bounded oscillation and the linear term are kept distinct so
/// range, asymptotic, and derivative arguments do not require an unsound
/// transcendental-to-polynomial reduction.
/// </summary>
internal static class LinearDriftTrigAnalyzer
{
    public static bool TryCompute(SemanticExpression expression, string variable, AngleUnit angleUnit, AnalysisFeatures feature, ResourceBudget budget, out object value, out ImmutableArray<string> parameters)
    {
        if (angleUnit != AngleUnit.Radians || !TryCreatePattern(expression.Value, variable, budget, out LinearDriftTrigPattern? pattern) || !TrigonometricAndLatticeAnalyzer.DomainMatches(expression, variable, angleUnit, AllRealSet.Instance, budget))
        {
            value = null!;
            parameters = [];
            return false;
        }

        value = feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range => AllRealSet.Instance,
            AnalysisFeatures.Parity => TryParity(pattern, out FunctionParity parity) ? parity : null!,
            AnalysisFeatures.Zeros => TryZeros(pattern, out RealSet zeros) ? zeros : null!,
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(YIntercept(pattern, budget)),
            AnalysisFeatures.Minima => TryExtrema(pattern, budget, minimum: true, out ImmutableArray<FeaturePoint> minima) ? minima : null!,
            AnalysisFeatures.Maxima => TryExtrema(pattern, budget, minimum: false, out ImmutableArray<FeaturePoint> maxima) ? maxima : null!,
            AnalysisFeatures.InflectionPoints => Inflections(pattern, budget),
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => TryMonotonicity(pattern, out ImmutableArray<MonotoneRegion> monotonicity) ? monotonicity : null!,
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        parameters = [pattern.Canonical, angleUnit.ToString(), expression.DefinedWhen.Canonical];
        return value is not null;
    }

    internal static bool TryCreatePattern(ValueTerm term, string variable, ResourceBudget budget, out LinearDriftTrigPattern pattern)
    {
        ImmutableArray<(ValueTerm Term, BigRational Coefficient)> terms = CollectLinearCombination(term, budget);
        BigRational slope = BigRational.Zero;
        LinearDriftExactOffset intercept = LinearDriftExactOffset.Zero;
        string? trigFunction = null;
        BigRational trigFrequency = default;
        BigRational trigPhase = default;
        ExactScalar trigAmplitude = ExactScalar.Zero;
        foreach ((ValueTerm candidate, BigRational coefficient) in terms)
        {
            budget.Charge();
            if (TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(candidate, variable, budget, out AffineTrigPattern candidateTrig) && candidateTrig.Function is "sin" or "cos")
            {
                if (trigFunction is not null && (trigFunction != candidateTrig.Function || trigFrequency != candidateTrig.Frequency || trigPhase != candidateTrig.Phase))
                {
                    pattern = null!;
                    return false;
                }

                trigFunction ??= candidateTrig.Function;
                trigFrequency = candidateTrig.Frequency;
                trigPhase = candidateTrig.Phase;
                ExactScalar scaledAmplitude = candidateTrig.Amplitude.Multiply(ExactScalar.FromRational(coefficient), budget);
                if (!ExactScalar.TryAdd(trigAmplitude, scaledAmplitude, budget, out trigAmplitude))
                {
                    pattern = null!;
                    return false;
                }

                BigRational shiftedIntercept = coefficient * candidateTrig.Shift;
                budget.CheckCoefficient(shiftedIntercept);
                if (!intercept.TryAdd(LinearDriftExactOffset.FromRational(shiftedIntercept), budget, out intercept))
                {
                    pattern = null!;
                    return false;
                }

                continue;
            }

            if (TryGetRationalAffine(candidate, variable, budget, out BigRational candidateSlope, out BigRational candidateIntercept))
            {
                slope += coefficient * candidateSlope;
                budget.CheckCoefficient(slope);
                BigRational scaledIntercept = coefficient * candidateIntercept;
                budget.CheckCoefficient(scaledIntercept);
                if (!intercept.TryAdd(LinearDriftExactOffset.FromRational(scaledIntercept), budget, out intercept))
                {
                    pattern = null!;
                    return false;
                }

                continue;
            }

            if (!ExactScalar.TryCreate(candidate, budget, out ExactScalar scalar) || !intercept.TryAdd(LinearDriftExactOffset.FromScalar(scalar, coefficient, budget), budget, out intercept))
            {
                pattern = null!;
                return false;
            }
        }

        if (trigFunction is null || slope.IsZero || trigAmplitude.IsZero)
        {
            pattern = null!;
            return false;
        }

        var trig = new AffineTrigPattern(trigFunction, trigAmplitude, trigFrequency, trigPhase, BigRational.Zero);
        ExactScalar derivativeAmplitude = trigAmplitude.Multiply(ExactScalar.FromRational(trig.Frequency), budget);
        LinearDriftDerivativeRegime regime = derivativeAmplitude.TryCompareAbsoluteTo(slope.Abs(), budget, out int magnitudeComparison) ? magnitudeComparison > 0 ? LinearDriftDerivativeRegime.Oscillatory : slope.Sign > 0 ? LinearDriftDerivativeRegime.Increasing : LinearDriftDerivativeRegime.Decreasing : LinearDriftDerivativeRegime.Unknown;
        pattern = new LinearDriftTrigPattern(slope, intercept, trig, regime);
        return true;
    }

    private static ImmutableArray<(ValueTerm Term, BigRational Coefficient)> CollectLinearCombination(ValueTerm root, ResourceBudget budget)
    {
        var descendingIds = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var pending = new SortedDictionary<int, (ValueTerm Term, BigRational Coefficient)>(descendingIds);
        var atoms = new SortedDictionary<string, (ValueTerm Term, BigRational Coefficient)>(StringComparer.Ordinal);
        var scalarValues = new Dictionary<int, RationalScalarEvaluation>();
        AddPending(root, BigRational.One, pending, budget);
        int visited = 0;
        while (pending.Count != 0)
        {
            budget.Charge();
            if (++visited > AnalysisLimits.SemanticNodes)
            {
                throw new BudgetExceededException(nameof(AnalysisLimits.SemanticNodes));
            }

            KeyValuePair<int, (ValueTerm Term, BigRational Coefficient)> entry = pending.First();
            pending.Remove(entry.Key);
            (ValueTerm term, BigRational coefficient) = entry.Value;
            if (coefficient.IsZero)
            {
                continue;
            }

            switch (term.Kind)
            {
                case ValueKind.Add:
                    AddPending(term.Operands[0], coefficient, pending, budget);
                    AddPending(term.Operands[1], coefficient, pending, budget);
                    continue;
                case ValueKind.Subtract:
                    AddPending(term.Operands[0], coefficient, pending, budget);
                    AddPending(term.Operands[1], -coefficient, pending, budget);
                    continue;
                case ValueKind.Negate:
                    AddPending(term.Operands[0], -coefficient, pending, budget);
                    continue;
                case ValueKind.Multiply when TryGetRationalFactor(term, scalarValues, budget, out ValueTerm factorTarget, out BigRational factor):
                    AddPending(factorTarget, CheckedMultiply(coefficient, factor, budget), pending, budget);
                    continue;
                case ValueKind.Divide when TryGetRationalScalar(term.Operands[1], scalarValues, budget, out BigRational denominator) && !denominator.IsZero:
                    AddPending(term.Operands[0], CheckedMultiply(coefficient, denominator.Reciprocal(), budget), pending, budget);
                    continue;
                default:
                    AddAtom(term, coefficient, atoms, budget);
                    continue;
            }
        }

        return atoms.Values.Where(static atom => !atom.Coefficient.IsZero).ToImmutableArray();
    }

    private static void AddPending(ValueTerm term, BigRational coefficient, SortedDictionary<int, (ValueTerm Term, BigRational Coefficient)> pending, ResourceBudget budget)
    {
        budget.CheckCoefficient(coefficient);
        if (pending.TryGetValue(term.Id, out var existing))
        {
            coefficient += existing.Coefficient;
            budget.CheckCoefficient(coefficient);
        }

        if (coefficient.IsZero)
        {
            pending.Remove(term.Id);
            return;
        }

        pending[term.Id] = (term, coefficient);
        if (pending.Count > AnalysisLimits.SemanticNodes)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.SemanticNodes));
        }
    }

    private static void AddAtom(ValueTerm term, BigRational coefficient, SortedDictionary<string, (ValueTerm Term, BigRational Coefficient)> atoms, ResourceBudget budget)
    {
        budget.CheckCoefficient(coefficient);
        if (atoms.TryGetValue(term.Canonical, out var existing))
        {
            coefficient += existing.Coefficient;
            budget.CheckCoefficient(coefficient);
        }

        if (coefficient.IsZero)
        {
            atoms.Remove(term.Canonical);
            return;
        }

        atoms[term.Canonical] = (term, coefficient);
        if (atoms.Count > AnalysisLimits.SemanticNodes)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.SemanticNodes));
        }
    }

    private static bool TryGetRationalFactor(ValueTerm multiplication, IDictionary<int, RationalScalarEvaluation> cache, ResourceBudget budget, out ValueTerm target, out BigRational factor)
    {
        ValueTerm left = multiplication.Operands[0];
        ValueTerm right = multiplication.Operands[1];
        bool rightFirst = RationalScalarPriority(right) > RationalScalarPriority(left);
        ValueTerm first = rightFirst ? right : left;
        ValueTerm second = rightFirst ? left : right;
        if (TryGetRationalScalar(first, cache, budget, out factor))
        {
            target = ReferenceEquals(first, left) ? right : left;
            return true;
        }

        if (TryGetRationalScalar(second, cache, budget, out factor))
        {
            target = ReferenceEquals(second, left) ? right : left;
            return true;
        }

        target = null!;
        factor = default;
        return false;
    }

    private static int RationalScalarPriority(ValueTerm term)
    {
        return term.Kind switch
        {
            ValueKind.Constant => 4,
            ValueKind.Negate or ValueKind.Power or ValueKind.Function => 3,
            ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide => 2,
            ValueKind.SymbolicConstant => 1,
            _ => 0
        };
    }

    private static bool TryGetRationalScalar(ValueTerm term, IDictionary<int, RationalScalarEvaluation> cache, ResourceBudget budget, out BigRational value)
    {
        if (!cache.TryGetValue(term.Id, out RationalScalarEvaluation evaluation))
        {
            var reachable = new List<ValueTerm>();
            var pending = new Stack<ValueTerm>();
            var seen = new HashSet<int>();
            pending.Push(term);
            while (pending.Count != 0)
            {
                budget.Charge();
                ValueTerm current = pending.Pop();
                if (cache.ContainsKey(current.Id) || !seen.Add(current.Id))
                {
                    continue;
                }

                reachable.Add(current);
                if (reachable.Count > AnalysisLimits.SemanticNodes)
                {
                    throw new BudgetExceededException(nameof(AnalysisLimits.SemanticNodes));
                }

                foreach (ValueTerm operand in current.Operands)
                {
                    pending.Push(operand);
                }
            }

            reachable.Sort(static (left, right) => left.Id.CompareTo(right.Id));
            foreach (ValueTerm current in reachable)
            {
                budget.Charge();
                cache[current.Id] = EvaluateRationalScalar(current, cache, budget);
            }

            evaluation = cache[term.Id];
        }

        value = evaluation.Value;
        return evaluation.IsRational;
    }

    private static RationalScalarEvaluation EvaluateRationalScalar(ValueTerm term, IDictionary<int, RationalScalarEvaluation> cache, ResourceBudget budget)
    {
        return term.Kind switch
        {
            ValueKind.Constant => EvaluateRationalConstant(term, budget),
            ValueKind.Negate => EvaluateRationalNegation(term, cache, budget),
            ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide => EvaluateRationalBinary(term, cache, budget),
            ValueKind.Power => EvaluateRationalPower(term, cache, budget),
            ValueKind.Function => EvaluateRationalFunction(term, cache, budget),
            _ => default
        };
    }

    private static RationalScalarEvaluation EvaluateRationalConstant(ValueTerm term, ResourceBudget budget)
    {
        budget.CheckCoefficient(term.Constant);
        return new RationalScalarEvaluation(true, term.Constant);
    }

    private static RationalScalarEvaluation EvaluateRationalNegation(ValueTerm term, IDictionary<int, RationalScalarEvaluation> cache, ResourceBudget budget)
    {
        if (!TryCachedOperand(term, 0, cache, out BigRational operand))
        {
            return default;
        }

        BigRational result = -operand;
        budget.CheckCoefficient(result);
        return new RationalScalarEvaluation(true, result);
    }

    private static RationalScalarEvaluation EvaluateRationalBinary(ValueTerm term, IDictionary<int, RationalScalarEvaluation> cache, ResourceBudget budget)
    {
        if (!TryCachedOperand(term, 0, cache, out BigRational left) || !TryCachedOperand(term, 1, cache, out BigRational right))
        {
            return default;
        }

        BigRational? result = term.Kind switch
        {
            ValueKind.Add => left + right,
            ValueKind.Subtract => left - right,
            ValueKind.Multiply => left * right,
            ValueKind.Divide when !right.IsZero => left / right,
            _ => null
        };
        if (result is not { } rational)
        {
            return default;
        }

        budget.CheckCoefficient(rational);
        return new RationalScalarEvaluation(true, rational);
    }

    private static RationalScalarEvaluation EvaluateRationalPower(ValueTerm term, IDictionary<int, RationalScalarEvaluation> cache, ResourceBudget budget)
    {
        if (!TryCachedOperand(term, 0, cache, out BigRational basis) || !TryCachedOperand(term, 1, cache, out BigRational exponent) || !exponent.IsInteger || exponent.Numerator < int.MinValue || exponent.Numerator > int.MaxValue || basis.IsZero && exponent.Sign <= 0)
        {
            return default;
        }

        int integerExponent = (int)exponent.Numerator;
        PreflightPower(basis, integerExponent);
        BigRational power = basis.Pow(integerExponent);
        budget.CheckCoefficient(power);
        return new RationalScalarEvaluation(true, power);
    }

    private static RationalScalarEvaluation EvaluateRationalFunction(ValueTerm term, IDictionary<int, RationalScalarEvaluation> cache, ResourceBudget budget)
    {
        if (term.Operands.Length != 1 || !TryCachedOperand(term, 0, cache, out BigRational argument))
        {
            return default;
        }

        switch (term.Name)
        {
            case "abs":
                return new RationalScalarEvaluation(true, argument.Abs());
            case "sqrt" when BigRational.TrySquareRoot(argument, out BigRational squareRoot):
                budget.CheckCoefficient(squareRoot);
                return new RationalScalarEvaluation(true, squareRoot);
            case "exp" when argument.IsZero:
                return new RationalScalarEvaluation(true, BigRational.One);
            default:
                return default;
        }
    }

    private static void PreflightPower(BigRational basis, int exponent)
    {
        if (basis.IsZero || basis.Abs().IsOne || exponent == 0)
        {
            return;
        }

        long magnitude = Math.Abs((long)exponent);
        long numeratorBits = basis.Numerator.GetBitLength();
        long denominatorBits = basis.Denominator.GetBitLength();
        long guaranteedBits = Math.Max(checked((numeratorBits - 1) * magnitude + 1), checked((denominatorBits - 1) * magnitude + 1));
        if (guaranteedBits > AnalysisLimits.CoefficientBits)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.CoefficientBits));
        }
    }

    private static bool TryCachedOperand(ValueTerm term, int index, IDictionary<int, RationalScalarEvaluation> cache, out BigRational operand)
    {
        if (index < term.Operands.Length && cache.TryGetValue(term.Operands[index].Id, out RationalScalarEvaluation child) && child.IsRational)
        {
            operand = child.Value;
            return true;
        }

        operand = default;
        return false;
    }

    private static BigRational CheckedMultiply(BigRational left, BigRational right, ResourceBudget budget)
    {
        BigRational product = left * right;
        budget.CheckCoefficient(product);
        return product;
    }

    private static bool TryGetRationalAffine(ValueTerm term, string variable, ResourceBudget budget, out BigRational slope, out BigRational intercept)
    {
        if (!RationalFunctionExtractor.TryExtract(term, variable, budget, out RationalExtraction extraction) || extraction.Function.Denominator.Degree != 0 || extraction.Function.Numerator.Degree > 1)
        {
            slope = default;
            intercept = default;
            return false;
        }

        BigRational denominator = extraction.Function.Denominator.ConstantCoefficient;
        slope = extraction.Function.Numerator[1] / denominator;
        intercept = extraction.Function.Numerator[0] / denominator;
        budget.CheckCoefficient(slope);
        budget.CheckCoefficient(intercept);
        return true;
    }

    private static bool TryParity(LinearDriftTrigPattern pattern, out FunctionParity parity)
    {
        if (!pattern.Trig.Phase.IsZero)
        {
            parity = FunctionParity.Neither;
            return true;
        }

        parity = pattern.Trig.Function == "sin" && pattern.Intercept.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
        return true;
    }

    private static bool TryZeros(LinearDriftTrigPattern pattern, out RealSet zeros)
    {
        if (pattern.DerivativeRegime is not (LinearDriftDerivativeRegime.Increasing or LinearDriftDerivativeRegime.Decreasing) || pattern.Trig.Function != "sin" || !pattern.Trig.Phase.IsZero || !pattern.Intercept.IsZero)
        {
            zeros = null!;
            return false;
        }

        // A globally strictly monotone function with f(0)=0 has exactly one
        // zero. Equality in |m| >= |ak| is safe: the derivative can vanish
        // only at isolated points and is nonzero inside every open interval.
        zeros = RealSets.Points([new RationalReal(BigRational.Zero)]);
        return true;
    }

    private static ExactReal YIntercept(LinearDriftTrigPattern pattern, ResourceBudget budget)
    {
        if (pattern.Trig.Phase.IsZero)
        {
            ExactReal trigValue = pattern.Trig.Function == "cos" ? pattern.Trig.Amplitude.Value : new RationalReal(BigRational.Zero);
            return Checked(ExactRealArithmetic.Add(trigValue, pattern.Intercept.Value), budget);
        }

        ExactReal primitive = new FunctionReal(pattern.Trig.Function, [new RationalReal(pattern.Trig.Phase)]);
        return Checked(ExactRealArithmetic.Add(ExactRealArithmetic.Multiply(pattern.Trig.Amplitude.Value, primitive), pattern.Intercept.Value), budget);
    }

    private static bool TryExtrema(LinearDriftTrigPattern pattern, ResourceBudget budget, bool minimum, out ImmutableArray<FeaturePoint> points)
    {
        if (pattern.DerivativeRegime == LinearDriftDerivativeRegime.Unknown)
        {
            points = default;
            return false;
        }

        if (pattern.DerivativeRegime != LinearDriftDerivativeRegime.Oscillatory)
        {
            points = [];
            return true;
        }

        ExactScalar derivativeAmplitude = pattern.Trig.Amplitude.Multiply(ExactScalar.FromRational(pattern.Trig.Frequency), budget);
        BigRational targetNumerator = pattern.Trig.Function == "sin" ? -pattern.Slope : pattern.Slope;
        ExactScalar target = ExactScalar.FromRational(targetNumerator).Multiply(derivativeAmplitude.Reciprocal(budget), budget);
        LinearDriftTrigAnalyzerCriticalAnglePair angles = CriticalAngles(pattern.Trig.Function, target, budget);
        bool principalIsMaximum = pattern.Trig.Amplitude.Sign > 0;
        ExactReal angle = minimum == principalIsMaximum ? angles.Reflected : angles.Principal;
        IntegerAffineFeaturePoint point;
        if (pattern.Trig.Amplitude.RationalValue is { } rationalAmplitude && target.RationalValue is { } rationalTarget)
        {
            BigRational targetSquared = rationalTarget * rationalTarget;
            budget.CheckCoefficient(targetSquared);
            BigRational radicand = BigRational.One - targetSquared;
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

        points = [point];
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

    private static LinearDriftTrigAnalyzerCriticalAnglePair CriticalAngles(string function, ExactScalar target, ResourceBudget budget)
    {
        budget.Charge();
        if (target.RationalValue is { } rational)
        {
            ExactReal principal = function == "sin" ? InverseCosine(rational) : InverseSine(rational);
            ExactReal reflected = function == "sin" ? SubtractFromTwoPi(principal) : SubtractFromPi(principal);
            return new LinearDriftTrigAnalyzerCriticalAnglePair(principal, reflected);
        }

        ExactReal inverse = new FunctionReal(function == "sin" ? "acos" : "asin", [target.Abs().Value]);
        ExactReal pi = new AffinePiReal(BigRational.One, BigRational.Zero);
        ExactReal twoPi = new AffinePiReal(new BigRational(2), BigRational.Zero);
        if (function == "sin")
        {
            return target.Sign < 0 ? new LinearDriftTrigAnalyzerCriticalAnglePair(OrderedAdd([ExactRealArithmetic.Negate(inverse), pi]), OrderedAdd([inverse, pi])) : new LinearDriftTrigAnalyzerCriticalAnglePair(inverse, OrderedAdd([twoPi, ExactRealArithmetic.Negate(inverse)]));
        }

        return target.Sign < 0 ? new LinearDriftTrigAnalyzerCriticalAnglePair(ExactRealArithmetic.Negate(inverse), OrderedAdd([inverse, pi])) : new LinearDriftTrigAnalyzerCriticalAnglePair(inverse, OrderedAdd([pi, ExactRealArithmetic.Negate(inverse)]));
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

    private static ImmutableArray<FeaturePoint> Inflections(LinearDriftTrigPattern pattern, ResourceBudget budget)
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

    private static ExactReal InverseCosine(BigRational value)
    {
        return value switch
        {
            _ when value == BigRational.MinusOne => new AffinePiReal(BigRational.One, BigRational.Zero),
            _ when value == new BigRational(-1, 2) => new AffinePiReal(new BigRational(2, 3), BigRational.Zero),
            _ when value.IsZero => new AffinePiReal(new BigRational(1, 2), BigRational.Zero),
            _ when value == new BigRational(1, 2) => new AffinePiReal(new BigRational(1, 3), BigRational.Zero),
            _ when value == BigRational.One => new RationalReal(BigRational.Zero),
            _ => new FunctionReal("acos", [new RationalReal(value)])
        };
    }

    private static ExactReal InverseSine(BigRational value)
    {
        return value switch
        {
            _ when value == BigRational.MinusOne => new AffinePiReal(new BigRational(-1, 2), BigRational.Zero),
            _ when value == new BigRational(-1, 2) => new AffinePiReal(new BigRational(-1, 6), BigRational.Zero),
            _ when value.IsZero => new RationalReal(BigRational.Zero),
            _ when value == new BigRational(1, 2) => new AffinePiReal(new BigRational(1, 6), BigRational.Zero),
            _ when value == BigRational.One => new AffinePiReal(new BigRational(1, 2), BigRational.Zero),
            _ => new FunctionReal("asin", [new RationalReal(value)])
        };
    }

    private static ExactReal SubtractFromPi(ExactReal value)
    {
        return value switch
        {
            RationalReal rational => new AffinePiReal(BigRational.One, -rational.Value),
            AffinePiReal affine => new AffinePiReal(BigRational.One - affine.PiCoefficient, -affine.Constant),
            _ => new FunctionReal("pi-minus", [value])
        };
    }

    private static ExactReal SubtractFromTwoPi(ExactReal value)
    {
        return value switch
        {
            RationalReal rational => new AffinePiReal(new BigRational(2), -rational.Value),
            AffinePiReal affine => new AffinePiReal(new BigRational(2) - affine.PiCoefficient, -affine.Constant),
            _ => ExactRealArithmetic.Subtract(new AffinePiReal(new BigRational(2), BigRational.Zero), value)
        };
    }

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

    private static bool TryMonotonicity(LinearDriftTrigPattern pattern, out ImmutableArray<MonotoneRegion> monotonicity)
    {
        switch (pattern.DerivativeRegime)
        {
            case LinearDriftDerivativeRegime.Increasing:
                monotonicity = [new MonotoneRegion(AllRealSet.Instance, Monotonicity.Increasing)];
                return true;
            case LinearDriftDerivativeRegime.Decreasing:
                monotonicity = [new MonotoneRegion(AllRealSet.Instance, Monotonicity.Decreasing)];
                return true;
            default:
                monotonicity = default;
                return false;
        }
    }
}
