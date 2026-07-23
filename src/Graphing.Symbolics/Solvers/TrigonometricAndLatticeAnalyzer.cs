using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class TrigonometricAndLatticeAnalyzer
{
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (AffineReciprocalTrigZeroAnalyzer.TryAnalyze(request, expression, feature, budget, out outcome))
        {
            return true;
        }

        if (!TryCompute(request, expression, feature, budget, out object? value, out TheoremRule theorem, out ImmutableArray<string> parameters))
        {
            outcome = null!;
            return false;
        }

        if (value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new TheoremProofCertificate(feature, expression.Value.Canonical, ClaimCanonical.ForObject(value), theorem, parameters);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    private static bool TryCompute(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out object value, out TheoremRule theorem, out ImmutableArray<string> parameters)
    {
        budget.Charge();
        if (DegenerateDomainAnalyzer.TryCompute(expression, request.Variable, feature, budget, out value, out parameters))
        {
            theorem = TheoremRule.SemialgebraicCellDecomposition;
            return true;
        }

        if (ExactConstantAnalyzer.TryCompute(expression, request.Variable, request.AngleUnit, feature, budget, out value, out parameters))
        {
            theorem = TheoremRule.ConstantFunction;
            return true;
        }

        if (PowerDomainSolver.TryCompute(expression, request.Variable, request.AngleUnit, feature, budget, out object powerValue, out ImmutableArray<string> powerParameters))
        {
            value = powerValue;
            theorem = TheoremRule.VariablePowerDomain;
            parameters = powerParameters;
            return true;
        }

        if (feature == AnalysisFeatures.Domain && MixedTrigonometricSolver.TryDomain(expression, request.Variable, request.AngleUnit, budget, out RealSet mixedDomain))
        {
            value = mixedDomain;
            theorem = TheoremRule.TrigonometricPolynomial;
            parameters = ["mixed-domain", expression.DefinedWhen.Canonical, request.AngleUnit.ToString()];
            return true;
        }

        if (feature == AnalysisFeatures.Zeros && MixedTrigonometricSolver.TryZeros(expression, request.Variable, request.AngleUnit, budget, out RealSet mixedZeros))
        {
            value = mixedZeros;
            theorem = TheoremRule.TrigonometricPolynomial;
            parameters = ["mixed-zeros", expression.Value.Canonical, request.AngleUnit.ToString()];
            return true;
        }

        if (feature == AnalysisFeatures.YIntercept && MixedTrigonometricSolver.TryYIntercept(expression, request.Variable, request.AngleUnit, budget, out OptionalValue<ExactReal> mixedIntercept))
        {
            value = mixedIntercept;
            theorem = TheoremRule.TrigonometricPolynomial;
            parameters = ["mixed-intercept", expression.DefinedWhen.Canonical, request.AngleUnit.ToString()];
            return true;
        }

        if (TryComputeAffinePrimitive(expression, request.Variable, request.AngleUnit, feature, budget, out value, out theorem, out parameters))
        {
            return true;
        }

        if (AffineReciprocalTrigonometricAnalyzer.TryCompute(expression, request.Variable, request.AngleUnit, feature, budget, out value, out parameters))
        {
            theorem = TheoremRule.AffineReciprocalTrigonometric;
            return true;
        }

        if (TryGetAffineTrig(expression.Value, request.Variable, budget, out AffineTrigPattern? pattern))
        {
            if (DomainMatches(expression, request.Variable, request.AngleUnit, AffineDomain(pattern, request.AngleUnit), budget))
            {
                theorem = pattern.Function switch
                {
                    "sin" => TheoremRule.AffineSine,
                    "cos" => TheoremRule.AffineCosine,
                    "tan" => TheoremRule.AffineTangent,
                    _ => throw new InvalidOperationException("The affine trigonometric pattern was not canonical.")
                };
                parameters = [pattern.Canonical, request.AngleUnit.ToString(), expression.DefinedWhen.Canonical];
                return TryComputeAffine(pattern, request.AngleUnit, feature, out value);
            }
        }

        if (LinearDriftTrigAnalyzer.TryCompute(expression, request.Variable, request.AngleUnit, feature, budget, out value, out parameters))
        {
            theorem = TheoremRule.LinearDriftTrigonometric;
            return true;
        }

        if (DomainMatches(expression, request.Variable, request.AngleUnit, AllRealSet.Instance, budget) && TrigonometricPolynomialAnalyzer.TryCompute(expression.Value, request.Variable, request.AngleUnit, feature, budget, out value, out parameters))
        {
            theorem = TheoremRule.TrigonometricPolynomial;
            return true;
        }

        value = null!;
        theorem = default;
        parameters = [];
        return false;
    }

    internal static bool DomainMatches(SemanticExpression expression, string variable, AngleUnit angleUnit, RealSet expected, ResourceBudget budget)
    {
        bool solved = DomainSolver.TrySolve(expression, variable, budget, out RealSet actual, out _);
        if (!solved)
        {
            solved = MixedTrigonometricSolver.TryDomain(expression, variable, angleUnit, budget, out actual);
        }

        return solved && string.Equals(actual.Canonical, expected.Canonical, StringComparison.Ordinal);
    }

    private static bool TryComputeAffinePrimitive(SemanticExpression expression, string variable, AngleUnit angleUnit, AnalysisFeatures feature, ResourceBudget budget, out object value, out TheoremRule theorem, out ImmutableArray<string> parameters)
    {
        if (!TryGetAffinePrimitive(expression.Value, variable, budget, out AffinePrimitivePattern? pattern))
        {
            value = null!;
            theorem = default;
            parameters = [];
            return false;
        }

        RealSet domain = PrimitiveDomain(pattern, budget);
        if (!PrimitiveDomainMatches(expression, pattern, variable, angleUnit, domain, budget))
        {
            value = null!;
            theorem = default;
            parameters = [];
            return false;
        }

        theorem = pattern.Function switch
        {
            "asin" or "acos" or "atan" => TheoremRule.InversePrimitive,
            "root" => TheoremRule.OddRootPrimitive,
            _ => TheoremRule.ElementaryPrimitive
        };
        parameters = [pattern.Canonical, angleUnit.ToString(), expression.DefinedWhen.Canonical];
        return TryComputePrimitiveFeature(pattern, angleUnit, feature, domain, budget, out value);
    }

    internal static bool TryGetAffinePrimitive(ValueTerm root, string variable, ResourceBudget budget, out AffinePrimitivePattern pattern)
    {
        ImmutableArray<(ValueTerm Term, BigRational Coefficient)> terms = CollectPrimitiveLinearCombination(root, budget);
        ValueTerm? core = null;
        BigRational outerScale = BigRational.Zero;
        BigRational outerShift = BigRational.Zero;
        foreach ((ValueTerm term, BigRational coefficient) in terms)
        {
            budget.Charge();
            budget.CheckCoefficient(coefficient);
            if (TryRationalScalar(term, budget, out BigRational constant))
            {
                outerShift = Checked(outerShift + coefficient * constant, budget);
                continue;
            }

            if (core is null)
            {
                core = term;
            }
            else if (!string.Equals(core.Canonical, term.Canonical, StringComparison.Ordinal))
            {
                pattern = null!;
                return false;
            }

            outerScale = Checked(outerScale + coefficient, budget);
        }

        if (core is null || outerScale.IsZero || !TryDescribePrimitiveCore(core, variable, budget, out string function, out BigRational innerSlope, out BigRational innerIntercept, out int rootDegree))
        {
            pattern = null!;
            return false;
        }

        pattern = new AffinePrimitivePattern(function, innerSlope, innerIntercept, outerScale, outerShift, rootDegree, core);
        return true;
    }

    private static ImmutableArray<(ValueTerm Term, BigRational Coefficient)> CollectPrimitiveLinearCombination(ValueTerm root, ResourceBudget budget)
    {
        var descendingIds = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var pending = new SortedDictionary<int, (ValueTerm Term, BigRational Coefficient)>(descendingIds);
        var atoms = new SortedDictionary<string, (ValueTerm Term, BigRational Coefficient)>(StringComparer.Ordinal);
        AddPrimitivePending(root, BigRational.One, pending, budget);
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
                    AddPrimitivePending(term.Operands[0], coefficient, pending, budget);
                    AddPrimitivePending(term.Operands[1], coefficient, pending, budget);
                    continue;
                case ValueKind.Subtract:
                    AddPrimitivePending(term.Operands[0], coefficient, pending, budget);
                    AddPrimitivePending(term.Operands[1], -coefficient, pending, budget);
                    continue;
                case ValueKind.Negate:
                    AddPrimitivePending(term.Operands[0], -coefficient, pending, budget);
                    continue;
                case ValueKind.Multiply when TryRationalScalar(term.Operands[0], budget, out BigRational left):
                    AddPrimitivePending(term.Operands[1], Checked(coefficient * left, budget), pending, budget);
                    continue;
                case ValueKind.Multiply when TryRationalScalar(term.Operands[1], budget, out BigRational right):
                    AddPrimitivePending(term.Operands[0], Checked(coefficient * right, budget), pending, budget);
                    continue;
                case ValueKind.Divide when TryRationalScalar(term.Operands[1], budget, out BigRational denominator) && !denominator.IsZero:
                    AddPrimitivePending(term.Operands[0], Checked(coefficient / denominator, budget), pending, budget);
                    continue;
                default:
                    AddPrimitiveAtom(term, coefficient, atoms, budget);
                    continue;
            }
        }

        return atoms.Values.Where(static atom => !atom.Coefficient.IsZero).ToImmutableArray();
    }

    private static void AddPrimitivePending(ValueTerm term, BigRational coefficient, SortedDictionary<int, (ValueTerm Term, BigRational Coefficient)> pending, ResourceBudget budget)
    {
        budget.CheckCoefficient(coefficient);
        if (pending.TryGetValue(term.Id, out var existing))
        {
            coefficient = Checked(coefficient + existing.Coefficient, budget);
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

    private static void AddPrimitiveAtom(ValueTerm term, BigRational coefficient, SortedDictionary<string, (ValueTerm Term, BigRational Coefficient)> atoms, ResourceBudget budget)
    {
        budget.CheckCoefficient(coefficient);
        if (atoms.TryGetValue(term.Canonical, out var existing))
        {
            coefficient = Checked(coefficient + existing.Coefficient, budget);
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

    private static bool TryDescribePrimitiveCore(ValueTerm core, string variable, ResourceBudget budget, out string function, out BigRational innerSlope, out BigRational innerIntercept, out int rootDegree)
    {
        function = string.Empty;
        innerSlope = default;
        innerIntercept = default;
        rootDegree = 0;
        if (core.Kind != ValueKind.Function)
        {
            return false;
        }

        function = core.Name switch
        {
            "arcsin" => "asin",
            "arccos" => "acos",
            "arctan" => "atan",
            var name => name
        };
        ValueTerm argument;
        if (function == "root")
        {
            if (core.Operands.Length != 2 || core.Operands[1].Kind != ValueKind.Constant)
            {
                return false;
            }

            BigRational degree = core.Operands[1].Constant;
            budget.CheckCoefficient(degree);
            if (!degree.IsInteger || degree.Numerator <= 1 || degree.Numerator.IsEven)
            {
                return false;
            }

            if (degree.Numerator > AnalysisLimits.UnivariateDegree)
            {
                throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
            }

            rootDegree = (int)degree.Numerator;
            argument = core.Operands[0];
        }
        else
        {
            if (function is not ("asin" or "acos" or "atan" or "exp" or "sinh" or "cosh" or "tanh" or "log" or "ln") || core.Operands.Length != 1)
            {
                return false;
            }

            argument = core.Operands[0];
        }

        if (!TryAffineArgument(argument, variable, budget, out innerSlope, out innerIntercept))
        {
            return false;
        }

        budget.CheckCoefficient(innerSlope);
        budget.CheckCoefficient(innerIntercept);
        return true;
    }

    private static bool TryRationalScalar(ValueTerm term, ResourceBudget budget, out BigRational value)
    {
        if (ExactScalar.TryCreate(term, budget, out ExactScalar scalar) && scalar.RationalValue is { } rational)
        {
            budget.CheckCoefficient(rational);
            value = rational;
            return true;
        }

        value = default;
        return false;
    }

    private static bool PrimitiveDomainMatches(SemanticExpression expression, AffinePrimitivePattern pattern, string variable, AngleUnit angleUnit, RealSet expected, ResourceBudget budget)
    {
        if (DomainMatches(expression, variable, angleUnit, expected, budget))
        {
            return true;
        }

        // tanh and spelling aliases currently enter the semantic graph as an
        // opaque primitive predicate. Accept only the exact, unguarded source
        // predicate for the recognized primitive. Any retained hole produces
        // a conjunction and is therefore rejected here.
        return pattern.Core.Name is "tanh" or "arcsin" or "arccos" or "arctan" && expression.DefinedWhen is PredicateFormula { Kind: ExactPredicate.FunctionIsDefined, Terms.Length: 1 } predicate && string.Equals(predicate.Terms[0].Canonical, pattern.Core.Canonical, StringComparison.Ordinal);
    }

    private static RealSet PrimitiveDomain(AffinePrimitivePattern pattern, ResourceBudget budget)
    {
        if (pattern.Function is "asin" or "acos")
        {
            BigRational atMinusOne = SolveInnerRational(pattern, BigRational.MinusOne, budget);
            BigRational atOne = SolveInnerRational(pattern, BigRational.One, budget);
            BigRational lower = pattern.InnerSlope.Sign > 0 ? atMinusOne : atOne;
            BigRational upper = pattern.InnerSlope.Sign > 0 ? atOne : atMinusOne;
            return new IntervalSet(RealBound.Finite(new RationalReal(lower)), true, RealBound.Finite(new RationalReal(upper)), true);
        }

        if (pattern.Function is "log" or "ln")
        {
            BigRational boundary = SolveInnerRational(pattern, BigRational.Zero, budget);
            ExactReal boundaryReal = new RationalReal(boundary);
            return pattern.InnerSlope.Sign > 0 ? new IntervalSet(RealBound.Finite(boundaryReal), false, RealBound.PositiveInfinity, false) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(boundaryReal), false);
        }

        return AllRealSet.Instance;
    }

    private static bool TryComputePrimitiveFeature(AffinePrimitivePattern pattern, AngleUnit angleUnit, AnalysisFeatures feature, RealSet domain, ResourceBudget budget, out object value)
    {
        budget.Charge();
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = domain;
                return true;
            case AnalysisFeatures.Range:
                value = PrimitiveRange(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.Parity:
                value = PrimitiveParity(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.Zeros:
                return TryPrimitiveZeros(pattern, angleUnit, budget, out value);
            case AnalysisFeatures.YIntercept:
                value = PrimitiveYIntercept(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.Minima:
                value = PrimitiveExtrema(pattern, angleUnit, minimum: true, budget);
                return true;
            case AnalysisFeatures.Maxima:
                value = PrimitiveExtrema(pattern, angleUnit, minimum: false, budget);
                return true;
            case AnalysisFeatures.InflectionPoints:
                value = PrimitiveInflections(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
                value = PrimitiveVerticalAsymptotes(pattern, budget);
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
                value = PrimitiveHorizontalAsymptotes(pattern, angleUnit, budget);
                return true;
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.Monotonicity:
                value = PrimitiveMonotonicity(pattern, domain, budget);
                return true;
            case AnalysisFeatures.Period:
                value = new Periodicity(PeriodicityKind.NotPeriodic, null);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static RealSet PrimitiveRange(AffinePrimitivePattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        ExactReal zero = new RationalReal(BigRational.Zero);
        ExactReal one = new RationalReal(BigRational.One);
        ExactReal minusOne = new RationalReal(BigRational.MinusOne);
        ExactReal minusHalfTurn = Angle(angleUnit, new BigRational(-1, 2));
        ExactReal halfTurn = Angle(angleUnit, new BigRational(1, 2));
        ExactReal fullHalfTurn = Angle(angleUnit, BigRational.One);
        return pattern.Function switch
        {
            "asin" => TransformInterval(pattern, minusHalfTurn, true, halfTurn, true, budget),
            "acos" => TransformInterval(pattern, zero, true, fullHalfTurn, true, budget),
            "atan" => TransformInterval(pattern, minusHalfTurn, false, halfTurn, false, budget),
            "exp" => pattern.OuterScale.Sign > 0 ? new IntervalSet(RealBound.Finite(TransformOutput(pattern, zero, budget)), false, RealBound.PositiveInfinity, false) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(TransformOutput(pattern, zero, budget)), false),
            "cosh" => pattern.OuterScale.Sign > 0 ? new IntervalSet(RealBound.Finite(TransformOutput(pattern, one, budget)), true, RealBound.PositiveInfinity, false) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(TransformOutput(pattern, one, budget)), true),
            "tanh" => TransformInterval(pattern, minusOne, false, one, false, budget),
            _ => AllRealSet.Instance
        };
    }

    private static IntervalSet TransformInterval(AffinePrimitivePattern pattern, ExactReal lower, bool includesLower, ExactReal upper, bool includesUpper, ResourceBudget budget)
    {
        ExactReal transformedLower = TransformOutput(pattern, lower, budget);
        ExactReal transformedUpper = TransformOutput(pattern, upper, budget);
        return pattern.OuterScale.Sign > 0 ? new IntervalSet(RealBound.Finite(transformedLower), includesLower, RealBound.Finite(transformedUpper), includesUpper) : new IntervalSet(RealBound.Finite(transformedUpper), includesUpper, RealBound.Finite(transformedLower), includesLower);
    }

    private static FunctionParity PrimitiveParity(AffinePrimitivePattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        if (!pattern.InnerIntercept.IsZero)
        {
            return FunctionParity.Neither;
        }

        if (pattern.Function == "cosh")
        {
            return FunctionParity.Even;
        }

        if (pattern.Function == "acos")
        {
            ExactReal center = TransformOutput(pattern, Angle(angleUnit, new BigRational(1, 2)), budget);
            return IsExactZero(center) ? FunctionParity.Odd : FunctionParity.Neither;
        }

        bool oddPrimitive = pattern.Function is "asin" or "atan" or "sinh" or "tanh" or "root";
        return oddPrimitive && pattern.OuterShift.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
    }

    private static bool TryPrimitiveZeros(AffinePrimitivePattern pattern, AngleUnit angleUnit, ResourceBudget budget, out object value)
    {
        BigRational target = Checked(-pattern.OuterShift / pattern.OuterScale, budget);
        switch (pattern.Function)
        {
            case "asin" when target.IsZero:
            case "atan" when target.IsZero:
                value = PointAtInnerValue(pattern, new RationalReal(BigRational.Zero), budget);
                return true;
            case "acos" when target.IsZero:
                value = PointAtInnerValue(pattern, new RationalReal(BigRational.One), budget);
                return true;
            case "asin":
            case "acos":
            case "atan":
                // A general rational inverse-trig output requires a certified
                // comparison with the unit-dependent principal range and an
                // exact special-angle table. Leave that obligation unknown.
                value = null!;
                return false;
            case "exp":
                if (target.Sign <= 0)
                {
                    value = EmptySet.Instance;
                    return true;
                }

                value = PointAtInnerValue(pattern, NaturalLogOfPositiveRational(target), budget);
                return true;
            case "sinh":
                value = PointAtInnerValue(pattern, InverseSinh(target, budget), budget);
                return true;
            case "cosh":
                if (target < BigRational.One)
                {
                    value = EmptySet.Instance;
                    return true;
                }

                if (target == BigRational.One)
                {
                    value = PointAtInnerValue(pattern, new RationalReal(BigRational.Zero), budget);
                    return true;
                }

                ExactReal acosh = InverseCosh(target, budget);
                value = RealSets.Points([SolveInnerExact(pattern, ExactRealArithmetic.Negate(acosh), budget), SolveInnerExact(pattern, acosh, budget)]);
                return true;
            case "tanh":
                if (target <= BigRational.MinusOne || target >= BigRational.One)
                {
                    value = EmptySet.Instance;
                    return true;
                }

                value = PointAtInnerValue(pattern, InverseTanh(target, budget), budget);
                return true;
            case "log":
            case "ln":
                value = PointAtInnerValue(pattern, ExponentialInverse(pattern.Function, target, budget), budget);
                return true;
            case "root":
                BigRational radicand = target.Pow(pattern.RootDegree);
                budget.CheckCoefficient(radicand);
                value = PointAtInnerValue(pattern, new RationalReal(radicand), budget);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static OptionalValue<ExactReal> PrimitiveYIntercept(AffinePrimitivePattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        if (!PrimitiveDefinedAt(pattern, pattern.InnerIntercept))
        {
            return OptionalValue<ExactReal>.None;
        }

        ExactReal primitive = PrimitiveAtRational(pattern, pattern.InnerIntercept, angleUnit, budget);
        return OptionalValue<ExactReal>.Some(TransformOutput(pattern, primitive, budget));
    }

    private static bool PrimitiveDefinedAt(AffinePrimitivePattern pattern, BigRational argument)
    {
        return pattern.Function switch
        {
            "asin" or "acos" => argument >= BigRational.MinusOne && argument <= BigRational.One,
            "log" or "ln" => argument.Sign > 0,
            _ => true
        };
    }

    private static ImmutableArray<FeaturePoint> PrimitiveExtrema(AffinePrimitivePattern pattern, AngleUnit angleUnit, bool minimum, ResourceBudget budget)
    {
        if (pattern.Function is "asin" or "acos")
        {
            bool useBaseMinimum = pattern.OuterScale.Sign > 0 == minimum;
            BigRational innerValue = (pattern.Function, useBaseMinimum) switch
            {
                ("asin", true) => BigRational.MinusOne,
                ("asin", false) => BigRational.One,
                ("acos", true) => BigRational.One,
                _ => BigRational.MinusOne
            };
            ExactReal x = new RationalReal(SolveInnerRational(pattern, innerValue, budget));
            ExactReal y = TransformOutput(pattern, PrimitiveAtRational(pattern, innerValue, angleUnit, budget), budget);
            return [new ConstantYFeaturePoint(new SingletonReal(x), y)];
        }

        if (pattern.Function == "cosh" && minimum == pattern.OuterScale.Sign > 0)
        {
            ExactReal x = new RationalReal(SolveInnerRational(pattern, BigRational.Zero, budget));
            ExactReal y = TransformOutput(pattern, new RationalReal(BigRational.One), budget);
            return [new ConstantYFeaturePoint(new SingletonReal(x), y)];
        }

        return [];
    }

    private static ImmutableArray<FeaturePoint> PrimitiveInflections(AffinePrimitivePattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        if (pattern.Function is not ("asin" or "acos" or "atan" or "sinh" or "tanh" or "root"))
        {
            return [];
        }

        ExactReal x = new RationalReal(SolveInnerRational(pattern, BigRational.Zero, budget));
        ExactReal y = TransformOutput(pattern, PrimitiveAtRational(pattern, BigRational.Zero, angleUnit, budget), budget);
        return [new ConstantYFeaturePoint(new SingletonReal(x), y)];
    }

    private static ImmutableArray<Asymptote> PrimitiveVerticalAsymptotes(AffinePrimitivePattern pattern, ResourceBudget budget)
    {
        if (pattern.Function is not ("log" or "ln"))
        {
            return [];
        }

        ExactReal x = new RationalReal(SolveInnerRational(pattern, BigRational.Zero, budget));
        return [new Asymptote(AsymptoteOrientation.Vertical, new SingletonReal(x), null, null)];
    }

    private static ImmutableArray<Asymptote> PrimitiveHorizontalAsymptotes(AffinePrimitivePattern pattern, AngleUnit angleUnit, ResourceBudget budget)
    {
        ExactReal positive;
        ExactReal negative;
        switch (pattern.Function)
        {
            case "exp":
                return [HorizontalAsymptote(TransformOutput(pattern, new RationalReal(BigRational.Zero), budget))];
            case "atan":
                positive = Angle(angleUnit, new BigRational(1, 2));
                negative = Angle(angleUnit, new BigRational(-1, 2));
                break;
            case "tanh":
                positive = new RationalReal(BigRational.One);
                negative = new RationalReal(BigRational.MinusOne);
                break;
            default:
                return [];
        }

        return [HorizontalAsymptote(TransformOutput(pattern, positive, budget)), HorizontalAsymptote(TransformOutput(pattern, negative, budget))];
    }

    private static Asymptote HorizontalAsymptote(ExactReal y)
    {
        return new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(y), null, y);
    }

    private static ImmutableArray<MonotoneRegion> PrimitiveMonotonicity(AffinePrimitivePattern pattern, RealSet domain, ResourceBudget budget)
    {
        if (pattern.Function == "cosh")
        {
            ExactReal center = new RationalReal(SolveInnerRational(pattern, BigRational.Zero, budget));
            RealSet left = new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(center), false);
            RealSet right = new IntervalSet(RealBound.Finite(center), false, RealBound.PositiveInfinity, false);
            return pattern.OuterScale.Sign > 0 ? [new MonotoneRegion(left, Monotonicity.Decreasing), new MonotoneRegion(right, Monotonicity.Increasing)] : [new MonotoneRegion(left, Monotonicity.Increasing), new MonotoneRegion(right, Monotonicity.Decreasing)];
        }

        int primitiveDirection = pattern.Function == "acos" ? -1 : 1;
        int direction = primitiveDirection * pattern.InnerSlope.Sign * pattern.OuterScale.Sign;
        return [new MonotoneRegion(domain, direction > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing)];
    }

    private static ExactReal PrimitiveAtRational(AffinePrimitivePattern pattern, BigRational argument, AngleUnit angleUnit, ResourceBudget budget)
    {
        budget.CheckCoefficient(argument);
        if (pattern.Function is "asin" or "acos" or "atan")
        {
            return ExactInverseTrigonometry.PrincipalAngle(pattern.Function, ExactScalar.FromRational(argument), angleUnit, normalizeOddNegative: false);
        }

        ExactReal zero = new RationalReal(BigRational.Zero);
        ExactReal one = new RationalReal(BigRational.One);
        return pattern.Function switch
        {
            "exp" when argument.IsZero => one,
            "exp" when argument == BigRational.One => new NamedReal("e"),
            "sinh" or "tanh" when argument.IsZero => zero,
            "cosh" when argument.IsZero => one,
            "log" or "ln" when argument == BigRational.One => zero,
            "log" when argument == new BigRational(10) => one,
            "root" when argument.IsZero => zero,
            "root" when argument == BigRational.One => one,
            "root" when argument == BigRational.MinusOne => new RationalReal(BigRational.MinusOne),
            "root" => new FunctionReal("root", [new RationalReal(argument), new RationalReal(new BigRational(pattern.RootDegree))]),
            _ => new FunctionReal(pattern.Function, [new RationalReal(argument)])
        };
    }

    private static RealSet PointAtInnerValue(AffinePrimitivePattern pattern, ExactReal innerValue, ResourceBudget budget)
    {
        return RealSets.Points([SolveInnerExact(pattern, innerValue, budget)]);
    }

    private static ExactReal SolveInnerExact(AffinePrimitivePattern pattern, ExactReal innerValue, ResourceBudget budget)
    {
        budget.CheckCoefficient(pattern.InnerIntercept);
        budget.CheckCoefficient(pattern.InnerSlope);
        return Checked(ExactRealArithmetic.Scale(ExactRealArithmetic.AddRational(innerValue, -pattern.InnerIntercept), pattern.InnerSlope.Reciprocal()), budget);
    }

    private static BigRational SolveInnerRational(AffinePrimitivePattern pattern, BigRational innerValue, ResourceBudget budget)
    {
        return Checked((innerValue - pattern.InnerIntercept) / pattern.InnerSlope, budget);
    }

    private static ExactReal TransformOutput(AffinePrimitivePattern pattern, ExactReal primitiveValue, ResourceBudget budget)
    {
        budget.CheckCoefficient(pattern.OuterScale);
        budget.CheckCoefficient(pattern.OuterShift);
        return Checked(ExactRealArithmetic.AddRational(ExactRealArithmetic.Scale(primitiveValue, pattern.OuterScale), pattern.OuterShift), budget);
    }

    private static ExactReal NaturalLogOfPositiveRational(BigRational value)
    {
        return value == BigRational.One
            ? new RationalReal(BigRational.Zero)
            : new FunctionReal("ln", [new RationalReal(value)]);
    }

    private static ExactReal InverseSinh(BigRational value, ResourceBudget budget)
    {
        BigRational radicand = Checked(value * value + BigRational.One, budget);
        ExactReal argument = ExactRealArithmetic.AddRational(SquareRoot(radicand), value);
        return NaturalLog(argument);
    }

    private static ExactReal InverseCosh(BigRational value, ResourceBudget budget)
    {
        BigRational radicand = Checked(value * value - BigRational.One, budget);
        ExactReal argument = ExactRealArithmetic.AddRational(SquareRoot(radicand), value);
        return NaturalLog(argument);
    }

    private static ExactReal InverseTanh(BigRational value, ResourceBudget budget)
    {
        BigRational ratio = Checked((BigRational.One + value) / (BigRational.One - value), budget);
        return ExactRealArithmetic.Scale(NaturalLogOfPositiveRational(ratio), new BigRational(1, 2));
    }

    private static ExactReal ExponentialInverse(string logarithm, BigRational exponent, ResourceBudget budget)
    {
        budget.CheckCoefficient(exponent);
        if (exponent.IsZero)
        {
            return new RationalReal(BigRational.One);
        }

        if (logarithm == "ln")
        {
            return exponent == BigRational.One ? new NamedReal("e") : new FunctionReal("exp", [new RationalReal(exponent)]);
        }

        if (exponent.IsInteger && exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue)
        {
            if (ExactInteger.Abs(exponent.Numerator) > AnalysisLimits.CoefficientBits)
            {
                throw new BudgetExceededException(nameof(AnalysisLimits.CoefficientBits));
            }

            BigRational power = new BigRational(10).Pow((int)exponent.Numerator);
            budget.CheckCoefficient(power);
            return new RationalReal(power);
        }

        return new FunctionReal("power", [new RationalReal(new BigRational(10)), new RationalReal(exponent)]);
    }

    private static ExactReal SquareRoot(BigRational value)
    {
        return BigRational.TrySquareRoot(value, out BigRational root)
            ? new RationalReal(root)
            : new FunctionReal("sqrt", [new RationalReal(value)]);
    }

    private static ExactReal NaturalLog(ExactReal value)
    {
        return value is RationalReal { Value.IsOne: true }
            ? new RationalReal(BigRational.Zero)
            : new FunctionReal("ln", [value]);
    }

    private static bool IsExactZero(ExactReal value)
    {
        return value switch
        {
            RationalReal rational => rational.Value.IsZero,
            AffinePiReal affine => affine.PiCoefficient.IsZero && affine.Constant.IsZero,
            _ => false
        };
    }

    private static BigRational Checked(BigRational value, ResourceBudget budget)
    {
        budget.CheckCoefficient(value);
        return value;
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

    private static bool TryComputeAffine(AffineTrigPattern pattern, AngleUnit angleUnit, AnalysisFeatures feature, out object value)
    {
        if (feature == AnalysisFeatures.Zeros && !pattern.Shift.IsZero && pattern.Amplitude.RationalValue is null)
        {
            value = null!;
            return false;
        }

        value = feature switch
        {
            AnalysisFeatures.Domain => AffineDomain(pattern, angleUnit),
            AnalysisFeatures.Range => AffineRange(pattern),
            AnalysisFeatures.Parity => AffineParity(pattern, angleUnit),
            AnalysisFeatures.Zeros => AffineZeros(pattern, angleUnit),
            AnalysisFeatures.YIntercept => AffineYIntercept(pattern, angleUnit),
            AnalysisFeatures.Minima => AffineExtrema(pattern, angleUnit, minimum: true),
            AnalysisFeatures.Maxima => AffineExtrema(pattern, angleUnit, minimum: false),
            AnalysisFeatures.InflectionPoints => AffineInflections(pattern, angleUnit),
            AnalysisFeatures.VerticalAsymptotes => AffineVerticalAsymptotes(pattern, angleUnit),
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => AffineMonotonicity(pattern, angleUnit),
            AnalysisFeatures.Period => AffinePeriod(pattern, angleUnit),
            _ => null!
        };
        return value is not null;
    }

    internal static RealSet AffineDomain(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Function != "tan")
        {
            return AllRealSet.Instance;
        }

        ExactReal lower = SolveAngle(pattern, Angle(angleUnit, new BigRational(-1, 2)));
        ExactReal upper = SolveAngle(pattern, Angle(angleUnit, new BigRational(1, 2)));
        ExactReal period = ScaleAngle(Angle(angleUnit, BigRational.One), pattern.Frequency.Reciprocal());
        return new PeriodicIntervalSet(period, "m", IntegerConstraint.All("m"), [new PeriodicInterval(lower, false, upper, false)]);
    }

    private static RealSet AffineRange(AffineTrigPattern pattern)
    {
        if (pattern.Function == "tan" && !pattern.Amplitude.IsZero)
        {
            return AllRealSet.Instance;
        }

        ExactScalar magnitude = pattern.Amplitude.Abs();
        if (magnitude.IsZero)
        {
            return RealSets.Points([new RationalReal(pattern.Shift)]);
        }

        return new IntervalSet(RealBound.Finite(ExactRealArithmetic.AddRational(magnitude.Negate().Value, pattern.Shift)), true, RealBound.Finite(ExactRealArithmetic.AddRational(magnitude.Value, pattern.Shift)), true);
    }

    private static FunctionParity AffineParity(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        BigRational? phaseFraction = PhaseInPi(pattern.Phase, angleUnit);
        bool integer = phaseFraction is { IsInteger: true };
        bool halfInteger = phaseFraction is { } fraction && (fraction - new BigRational(1, 2)).IsInteger;
        if (pattern.Function == "tan" && !integer && !halfInteger)
        {
            return FunctionParity.Neither;
        }

        if (pattern.Amplitude.IsZero)
        {
            return pattern.Shift.IsZero ? FunctionParity.Both : FunctionParity.Even;
        }

        return pattern.Function switch
        {
            "sin" when halfInteger => FunctionParity.Even,
            "sin" when integer && pattern.Shift.IsZero => FunctionParity.Odd,
            "cos" when integer => FunctionParity.Even,
            "cos" when halfInteger && pattern.Shift.IsZero => FunctionParity.Odd,
            "tan" when pattern.Shift.IsZero => FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
    }

    internal static RealSet AffineZeros(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Amplitude.IsZero)
        {
            return pattern.Shift.IsZero ? AffineDomain(pattern, angleUnit) : EmptySet.Instance;
        }

        if (!pattern.Shift.IsZero)
        {
            return SolveShiftedTrigZeros(pattern, angleUnit);
        }

        BigRational offsetFraction = pattern.Function == "cos" ? new BigRational(1, 2) : BigRational.Zero;
        ExactReal offset = SolveAngle(pattern, Angle(angleUnit, offsetFraction));
        BigRational periodFraction = pattern.Function is "sin" or "tan" ? BigRational.One : BigRational.One;
        ExactReal period = ScaleAngle(Angle(angleUnit, periodFraction), pattern.Frequency.Reciprocal());
        return new PeriodicPointSet(offset, period, "m", IntegerConstraint.All("m"));
    }

    private static OptionalValue<ExactReal> AffineYIntercept(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Phase.IsZero)
        {
            ExactReal origin = pattern.Function == "cos" ? ExactRealArithmetic.AddRational(pattern.Amplitude.Value, pattern.Shift) : new RationalReal(pattern.Shift);
            return OptionalValue<ExactReal>.Some(origin);
        }

        BigRational? phaseFraction = PhaseInPi(pattern.Phase, angleUnit);
        if (phaseFraction is { } exactFraction && TryPrimitiveQuarterTurn(pattern.Function, exactFraction, out bool defined, out BigRational exactPrimitive))
        {
            if (!defined)
            {
                return OptionalValue<ExactReal>.None;
            }

            ExactReal exactScaled = ExactRealArithmetic.Scale(pattern.Amplitude.Value, exactPrimitive);
            return OptionalValue<ExactReal>.Some(pattern.Shift.IsZero ? exactScaled : ExactRealArithmetic.AddRational(exactScaled, pattern.Shift));
        }

        ExactScalar amplitude = pattern.Amplitude;
        BigRational phase = pattern.Phase;
        if (pattern.Function == "tan" && phase.Sign < 0)
        {
            // tan(-b) = -tan(b). Keep the exact sign outside the primitive so
            // every amplitude spelling reaches the same canonical value.
            amplitude = amplitude.Negate();
            phase = phase.Abs();
        }

        ExactReal primitive = new FunctionReal(pattern.Function, [ExactAngleArithmetic.ToRadians(new RationalReal(phase), angleUnit)]);
        ExactReal scaled;
        scaled = ExactRealArithmetic.Multiply(amplitude.Value, primitive);
        ExactReal shifted = pattern.Shift.IsZero ? scaled : ExactRealArithmetic.AddRational(scaled, pattern.Shift);
        return OptionalValue<ExactReal>.Some(shifted);
    }

    private static BigRational? PhaseInPi(BigRational phase, AngleUnit angleUnit)
    {
        return angleUnit switch
        {
            AngleUnit.Radians => phase.IsZero ? BigRational.Zero : null,
            AngleUnit.Degrees => phase / new BigRational(180),
            AngleUnit.Grads => phase / new BigRational(200),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnit))
        };
    }

    private static bool TryPrimitiveQuarterTurn(string function, BigRational phaseInPi, out bool defined, out BigRational value)
    {
        BigRational quarterTurns = phaseInPi * new BigRational(2);
        if (!quarterTurns.IsInteger)
        {
            defined = false;
            value = default;
            return false;
        }

        int residue = (int)(quarterTurns.Numerator % 4);
        if (residue < 0)
        {
            residue += 4;
        }

        if (function == "tan" && residue is 1 or 3)
        {
            defined = false;
            value = default;
            return true;
        }

        defined = true;
        value = function switch
        {
            "sin" => residue switch
            {
                1 => BigRational.One,
                3 => BigRational.MinusOne,
                _ => BigRational.Zero
            },
            "cos" => residue switch
            {
                0 => BigRational.One,
                2 => BigRational.MinusOne,
                _ => BigRational.Zero
            },
            "tan" => BigRational.Zero,
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        };
        return true;
    }

    private static ImmutableArray<FeaturePoint> AffineExtrema(AffineTrigPattern pattern, AngleUnit angleUnit, bool minimum)
    {
        if (pattern.Function == "tan" || pattern.Amplitude.IsZero)
        {
            return [];
        }

        bool positiveAmplitude = pattern.Amplitude.Sign > 0;
        BigRational angleFraction;
        if (pattern.Function == "sin")
        {
            bool positivePeak = minimum != positiveAmplitude;
            angleFraction = positivePeak ? new BigRational(1, 2) : new BigRational(3, 2);
        }
        else
        {
            bool positivePeak = minimum != positiveAmplitude;
            angleFraction = positivePeak ? BigRational.Zero : BigRational.One;
        }

        ExactReal offset = SolveAngle(pattern, Angle(angleUnit, angleFraction));
        ExactReal period = ScaleAngle(Angle(angleUnit, new BigRational(2)), pattern.Frequency.Reciprocal());
        ExactScalar magnitude = pattern.Amplitude.Abs();
        ExactReal y = ExactRealArithmetic.AddRational(minimum ? magnitude.Negate().Value : magnitude.Value, pattern.Shift);
        return [new ConstantYFeaturePoint(new PeriodicReal(offset, period, "m", IntegerConstraint.All("m")), y)];
    }

    private static ImmutableArray<FeaturePoint> AffineInflections(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Amplitude.IsZero)
        {
            return [];
        }

        BigRational fraction = pattern.Function == "cos" ? new BigRational(1, 2) : BigRational.Zero;
        ExactReal offset = SolveAngle(pattern, Angle(angleUnit, fraction));
        ExactReal period = ScaleAngle(Angle(angleUnit, BigRational.One), pattern.Frequency.Reciprocal());
        return [new ConstantYFeaturePoint(new PeriodicReal(offset, period, "m", IntegerConstraint.All("m")), new RationalReal(pattern.Shift))];
    }

    private static ImmutableArray<Asymptote> AffineVerticalAsymptotes(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Function != "tan" || pattern.Amplitude.IsZero)
        {
            return [];
        }

        ExactReal offset = SolveAngle(pattern, Angle(angleUnit, new BigRational(1, 2)));
        ExactReal period = ScaleAngle(Angle(angleUnit, BigRational.One), pattern.Frequency.Reciprocal());
        return [new Asymptote(AsymptoteOrientation.Vertical, new PeriodicReal(offset, period, "m", IntegerConstraint.All("m")), null, null)];
    }

    private static ImmutableArray<MonotoneRegion> AffineMonotonicity(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Amplitude.IsZero)
        {
            return [new MonotoneRegion(AffineDomain(pattern, angleUnit), Monotonicity.Constant)];
        }

        if (pattern.Function == "tan")
        {
            ExactReal tangentPeriod = ScaleAngle(Angle(angleUnit, BigRational.One), pattern.Frequency.Reciprocal());
            return [new MonotoneRegion(PeriodicInterval(pattern, angleUnit, tangentPeriod, new BigRational(1, 2), new BigRational(3, 2)), pattern.Amplitude.Sign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing)];
        }

        ExactReal period = ScaleAngle(Angle(angleUnit, new BigRational(2)), pattern.Frequency.Reciprocal());
        BigRational increasingStart = pattern.Function == "sin" ? new BigRational(3, 2) : BigRational.One;
        BigRational increasingEnd = pattern.Function == "sin" ? new BigRational(5, 2) : new BigRational(2);
        BigRational decreasingStart = pattern.Function == "sin" ? new BigRational(1, 2) : BigRational.Zero;
        BigRational decreasingEnd = pattern.Function == "sin" ? new BigRational(3, 2) : BigRational.One;
        RealSet increasing = PeriodicInterval(pattern, angleUnit, period, increasingStart, increasingEnd);
        RealSet decreasing = PeriodicInterval(pattern, angleUnit, period, decreasingStart, decreasingEnd);
        bool positiveAmplitude = pattern.Amplitude.Sign > 0;
        return [new MonotoneRegion(decreasing, positiveAmplitude ? Monotonicity.Decreasing : Monotonicity.Increasing), new MonotoneRegion(increasing, positiveAmplitude ? Monotonicity.Increasing : Monotonicity.Decreasing)];
    }

    private static Periodicity AffinePeriod(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Amplitude.IsZero)
        {
            return new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null);
        }

        BigRational fraction = pattern.Function == "tan" ? BigRational.One : new BigRational(2);
        ExactReal period = ScaleAngle(Angle(angleUnit, fraction), pattern.Frequency.Reciprocal());
        return new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, period);
    }

    private static RealSet SolveShiftedTrigZeros(AffineTrigPattern pattern, AngleUnit angleUnit)
    {
        if (pattern.Amplitude.RationalValue is not { } rationalAmplitude)
        {
            throw new InvalidOperationException("Shifted affine trigonometric zeros require a rational amplitude.");
        }

        BigRational target = -pattern.Shift / rationalAmplitude;
        if (pattern.Function != "tan" && (target < BigRational.MinusOne || target > BigRational.One))
        {
            return EmptySet.Instance;
        }

        string inverse = pattern.Function switch
        {
            "sin" => "asin",
            "cos" => "acos",
            "tan" => "atan",
            _ => throw new ArgumentOutOfRangeException(nameof(pattern))
        };
        ExactReal principal = ExactInverseTrigonometry.PrincipalAngle(inverse, ExactScalar.FromRational(target), angleUnit, normalizeOddNegative: false);
        ExactReal offset = TransformInverseAngle(pattern, principal);
        ExactReal period = ScaleAngle(Angle(angleUnit, pattern.Function is "sin" or "cos" ? new BigRational(2) : BigRational.One), pattern.Frequency.Reciprocal());
        RealSet first = new PeriodicPointSet(offset, period, "m", IntegerConstraint.All("m"));
        if (pattern.Function == "tan" || target.Abs().IsOne)
        {
            return first;
        }

        ExactReal reflectedAngle = pattern.Function == "sin" ? ExactRealArithmetic.Subtract(Angle(angleUnit, BigRational.One), principal) : ExactRealArithmetic.Negate(principal);
        ExactReal reflected = TransformInverseAngle(pattern, reflectedAngle);
        RealSet second = new PeriodicPointSet(reflected, period, "m", IntegerConstraint.All("m"));
        return RealSets.Union(first, second);
    }

    private static PeriodicIntervalSet PeriodicInterval(AffineTrigPattern pattern, AngleUnit angleUnit, ExactReal period, BigRational lowerFraction, BigRational upperFraction)
    {
        return new PeriodicIntervalSet(period, "m", IntegerConstraint.All("m"),
        [
            new PeriodicInterval(SolveAngle(pattern, Angle(angleUnit, lowerFraction)), false,
                SolveAngle(pattern, Angle(angleUnit, upperFraction)), false)
        ]);
    }

    private static ExactReal SolveAngle(AffineTrigPattern pattern, ExactReal angle)
    {
        return AddRational(ScaleAngle(angle, pattern.Frequency.Reciprocal()), -pattern.Phase / pattern.Frequency);
    }

    private static ExactReal TransformInverseAngle(AffineTrigPattern pattern, ExactReal angle)
    {
        return SolveAngle(pattern, angle);
    }

    private static ExactReal Angle(AngleUnit unit, BigRational piFraction)
    {
        return ExactAngleArithmetic.PiFraction(unit, piFraction);
    }

    private static ExactReal ScaleAngle(ExactReal value, BigRational scale)
    {
        return value switch
        {
            RationalReal rational => new RationalReal(rational.Value * scale),
            AffinePiReal affine => new AffinePiReal(affine.PiCoefficient * scale, affine.Constant * scale),
            _ => new FunctionReal("scale", [value, new RationalReal(scale)])
        };
    }

    private static ExactReal AddRational(ExactReal value, BigRational addend)
    {
        return value switch
        {
            RationalReal rational => new RationalReal(rational.Value + addend),
            AffinePiReal affine => affine with { Constant = affine.Constant + addend },
            _ => new FunctionReal("add", [value, new RationalReal(addend)])
        };
    }

    internal static bool TryGetAffineTrig(ValueTerm term, string variable, ResourceBudget budget, out AffineTrigPattern pattern)
    {
        BigRational shift = BigRational.Zero;
        ValueTerm core = term;
        if (term.Kind is ValueKind.Add or ValueKind.Subtract)
        {
            if (TryConstant(term.Operands[1], out BigRational right))
            {
                core = term.Operands[0];
                shift = term.Kind == ValueKind.Add ? right : -right;
            }
            else if (term.Kind == ValueKind.Add && TryConstant(term.Operands[0], out BigRational left))
            {
                core = term.Operands[1];
                shift = left;
            }
        }

        if (!TryStripAmplitude(core, budget, out ValueTerm trig, out ExactScalar amplitude))
        {
            pattern = null!;
            return false;
        }

        if (trig.Kind != ValueKind.Function || trig.Name is not ("sin" or "cos" or "tan") || trig.Operands.Length != 1 || !RationalFunctionExtractor.TryExtract(trig.Operands[0], variable, budget, out RationalExtraction argument) || argument.Function.Denominator.Degree != 0 || argument.Function.Numerator.Degree > 1)
        {
            pattern = null!;
            return false;
        }

        BigRational denominator = argument.Function.Denominator.ConstantCoefficient;
        BigRational frequency = argument.Function.Numerator[1] / denominator;
        BigRational phase = argument.Function.Numerator[0] / denominator;
        if (frequency.IsZero)
        {
            pattern = null!;
            return false;
        }

        string function = trig.Name;
        if (frequency.Sign < 0)
        {
            frequency = -frequency;
            phase = -phase;
            if (function is "sin" or "tan")
            {
                amplitude = amplitude.Negate();
            }
        }

        pattern = new AffineTrigPattern(function, amplitude, frequency, phase, shift);
        return true;
    }

    private static bool TryStripAmplitude(ValueTerm core, ResourceBudget budget, out ValueTerm trig, out ExactScalar amplitude)
    {
        amplitude = ExactScalar.One;
        trig = core;
        StripNegations(ref trig, ref amplitude);
        bool stripped;
        do
        {
            stripped = false;
            if (trig.Kind == ValueKind.Multiply && ExactScalar.TryCreate(trig.Operands[0], budget, out ExactScalar left))
            {
                amplitude = amplitude.Multiply(left, budget);
                trig = trig.Operands[1];
                stripped = true;
            }
            else if (trig.Kind == ValueKind.Multiply && ExactScalar.TryCreate(trig.Operands[1], budget, out ExactScalar right))
            {
                amplitude = amplitude.Multiply(right, budget);
                trig = trig.Operands[0];
                stripped = true;
            }
            else if (trig.Kind == ValueKind.Divide && ExactScalar.TryCreate(trig.Operands[1], budget, out ExactScalar denominator) && !denominator.IsZero)
            {
                amplitude = amplitude.Multiply(denominator.Reciprocal(budget), budget);
                trig = trig.Operands[0];
                stripped = true;
            }

            stripped |= StripNegations(ref trig, ref amplitude);
        }
        while (stripped);
        // A cancelled symbolic coefficient (for example (pi-pi)*tan(x))
        // still carries the source function's partial domain. Until the
        // constant-on-periodic-domain theorem is represented explicitly, do
        // not misclassify it as an everywhere-defined constant.
        return !amplitude.IsZero;
    }

    private static bool StripNegations(ref ValueTerm term, ref ExactScalar amplitude)
    {
        bool stripped = false;
        while (term.Kind == ValueKind.Negate)
        {
            amplitude = amplitude.Negate();
            term = term.Operands[0];
            stripped = true;
        }

        return stripped;
    }

    private static bool TryConstant(ValueTerm term, out BigRational value)
    {
        if (term.Kind == ValueKind.Constant)
        {
            value = term.Constant;
            return true;
        }

        value = default;
        return false;
    }
}
