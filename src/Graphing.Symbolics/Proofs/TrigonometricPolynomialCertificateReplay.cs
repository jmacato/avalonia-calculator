using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Rebuilds mixed-trigonometric and half-angle polynomial claims without
/// invoking either solver's feature dispatcher.
/// </summary>
internal static class TrigonometricPolynomialCertificateReplay
{
    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Parameters.Length != 3)
        {
            return false;
        }

        bool reconstructed = certificate.Parameters[0] switch
        {
            "mixed-domain" => TryReplayMixedDomain(
                request,
                expression,
                certificate,
                budget,
                out object expectedDomain) &&
                ClaimMatches(expectedDomain, claim),
            "mixed-zeros" => TryReplayMixedZeros(
                request,
                expression,
                certificate,
                budget,
                out object expectedZeros) &&
                ClaimMatches(expectedZeros, claim),
            "mixed-intercept" => TryReplayMixedIntercept(
                request,
                expression,
                certificate,
                budget,
                out object expectedIntercept) &&
                ClaimMatches(expectedIntercept, claim),
            "half-angle" => TryReplayHalfAngle(
                request,
                expression,
                certificate,
                budget,
                out object expected) &&
                ClaimMatches(expected, claim),
            _ => false
        };
        return reconstructed;
    }

    private static bool TryReplayHalfAngle(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        ResourceBudget budget,
        out object value)
    {
        if (!TrigonometricPolynomialAnalyzer.TryBuild(
                expression.Value,
                request.Variable,
                budget,
                out TrigonometricPolynomialModel model) ||
            !string.Equals(certificate.Parameters[1], model.Canonical, StringComparison.Ordinal) ||
            !string.Equals(
                certificate.Parameters[2],
                model.Fourier.FrequencyGcd.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal) ||
            !ExactFormulaVerifier.IsAlwaysTrue(expression.DefinedWhen, budget))
        {
            value = null!;
            return false;
        }

        return TryReconstructHalfAngle(
            model,
            request.AngleUnit,
            certificate.Feature,
            budget,
            out value);
    }

    private static bool TryReconstructHalfAngle(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        switch (feature)
        {
            case AnalysisFeatures.Domain:
                value = AllRealSet.Instance;
                return true;
            case AnalysisFeatures.Range when model.Fourier.IsConstant:
                value = RealSets.Points([ConstantValue(model, budget)]);
                return true;
            case AnalysisFeatures.Parity:
                value = BuildParity(model, budget);
                return true;
            case AnalysisFeatures.Zeros:
                value = BuildZeros(model, angleUnit, budget);
                return true;
            case AnalysisFeatures.YIntercept:
                value = OptionalValue<ExactReal>.Some(ConstantAtZero(model, budget));
                return true;
            case AnalysisFeatures.Minima:
                value = BuildExtrema(model, angleUnit, minimum: true, budget);
                return true;
            case AnalysisFeatures.Maxima:
                value = BuildExtrema(model, angleUnit, minimum: false, budget);
                return true;
            case AnalysisFeatures.InflectionPoints:
                value = BuildInflections(model, angleUnit, budget);
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.HorizontalAsymptotes when model.Fourier.IsConstant:
                ExactReal constant = ConstantValue(model, budget);
                value = ImmutableArray.Create(
                    new Asymptote(
                        AsymptoteOrientation.Horizontal,
                        new SingletonReal(constant),
                        null,
                        constant));
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
                value = ImmutableArray<Asymptote>.Empty;
                return true;
            case AnalysisFeatures.Monotonicity:
                value = BuildMonotonicity(model, angleUnit, budget);
                return true;
            case AnalysisFeatures.Period:
                value = BuildPeriod(model, angleUnit);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static RationalReal ConstantValue(
        TrigonometricPolynomialModel model,
        ResourceBudget budget)
    {
        return new RationalReal(model.HalfAngleFunction.Evaluate(BigRational.Zero, budget));
    }

    private static RationalReal ConstantAtZero(
        TrigonometricPolynomialModel model,
        ResourceBudget budget)
    {
        return new RationalReal(model.HalfAngleFunction.Evaluate(BigRational.Zero, budget));
    }

    private static FunctionParity BuildParity(
        TrigonometricPolynomialModel model,
        ResourceBudget budget)
    {
        RationalFunction function = model.HalfAngleFunction;
        UnivariatePolynomial negativeNumerator =
            function.Numerator.SubstituteNegativeVariable(budget);
        UnivariatePolynomial negativeDenominator =
            function.Denominator.SubstituteNegativeVariable(budget);
        UnivariatePolynomial reflected = negativeNumerator.Multiply(
            function.Denominator,
            budget);
        UnivariatePolynomial original = function.Numerator.Multiply(
            negativeDenominator,
            budget);
        bool even = reflected.Subtract(original, budget).IsZero;
        bool odd = reflected.Add(original, budget).IsZero;
        return (even, odd) switch
        {
            (true, true) => FunctionParity.Both,
            (true, false) => FunctionParity.Even,
            (false, true) => FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
    }

    private static RealSet BuildZeros(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        if (model.HalfAngleFunction.Numerator.IsZero)
        {
            return AllRealSet.Instance;
        }

        RootIsolationCertificate roots = SturmRootIsolator.Isolate(
            model.HalfAngleFunction.Numerator,
            budget);
        ExactReal period = FullTurn(angleUnit);
        var sets = new List<RealSet>();
        foreach (ExactReal root in roots.Roots)
        {
            sets.Add(new PeriodicPointSet(
                HalfAngleInverse(root, angleUnit),
                period,
                "m",
                IntegerConstraint.All("m")));
        }

        if (TryLimitAtInfinity(model.HalfAngleFunction, out BigRational limit) &&
            limit.IsZero)
        {
            sets.Add(new PeriodicPointSet(
                HalfTurn(angleUnit),
                period,
                "m",
                IntegerConstraint.All("m")));
        }

        return RealSets.Union(sets);
    }

    private static ImmutableArray<FeaturePoint> BuildExtrema(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit,
        bool minimum,
        ResourceBudget budget)
    {
        RationalFunction derivative = DifferentiateByAngle(
            model.HalfAngleFunction,
            budget);
        if (derivative.Numerator.IsZero)
        {
            return [];
        }

        CircularSignChart chart = CircularSignChart.Create(derivative, budget);
        var result = ImmutableArray.CreateBuilder<FeaturePoint>();
        for (int index = 0; index < chart.Roots.Roots.Length; index++)
        {
            int left = chart.GapSigns[index];
            int right = chart.GapSigns[index + 1];
            bool matches = minimum ? left < 0 && right > 0 : left > 0 && right < 0;
            if (!matches)
            {
                continue;
            }

            ExactReal parameter = chart.Roots.Roots[index];
            result.Add(new ConstantYFeaturePoint(
                new PeriodicReal(
                    HalfAngleInverse(parameter, angleUnit),
                    FullTurn(angleUnit),
                    "m",
                    IntegerConstraint.All("m")),
                EvaluateAt(model.HalfAngleFunction, parameter, budget)));
        }

        if (chart.InfinityIsRoot)
        {
            int left = chart.GapSigns[^1];
            int right = chart.GapSigns[0];
            bool matches = minimum ? left < 0 && right > 0 : left > 0 && right < 0;
            if (matches &&
                TryLimitAtInfinity(
                    model.HalfAngleFunction,
                    out BigRational functionLimit))
            {
                result.Add(new ConstantYFeaturePoint(
                    new PeriodicReal(
                        HalfTurn(angleUnit),
                        FullTurn(angleUnit),
                        "m",
                        IntegerConstraint.All("m")),
                    new RationalReal(functionLimit)));
            }
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<FeaturePoint> BuildInflections(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        RationalFunction second = DifferentiateByAngle(
            DifferentiateByAngle(model.HalfAngleFunction, budget),
            budget);
        if (second.Numerator.IsZero)
        {
            return [];
        }

        CircularSignChart chart = CircularSignChart.Create(second, budget);
        var result = ImmutableArray.CreateBuilder<FeaturePoint>();
        for (int index = 0; index < chart.Roots.Roots.Length; index++)
        {
            int left = chart.GapSigns[index];
            int right = chart.GapSigns[index + 1];
            if (left == right)
            {
                continue;
            }

            ExactReal parameter = chart.Roots.Roots[index];
            result.Add(new ConstantYFeaturePoint(
                new PeriodicReal(
                    HalfAngleInverse(parameter, angleUnit),
                    FullTurn(angleUnit),
                    "m",
                    IntegerConstraint.All("m")),
                EvaluateAt(model.HalfAngleFunction, parameter, budget)));
        }

        if (chart.InfinityIsRoot &&
            chart.GapSigns[^1] != chart.GapSigns[0] &&
            TryLimitAtInfinity(
                model.HalfAngleFunction,
                out BigRational functionLimit))
        {
            result.Add(new ConstantYFeaturePoint(
                new PeriodicReal(
                    HalfTurn(angleUnit),
                    FullTurn(angleUnit),
                    "m",
                    IntegerConstraint.All("m")),
                new RationalReal(functionLimit)));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<MonotoneRegion> BuildMonotonicity(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit,
        ResourceBudget budget)
    {
        RationalFunction derivative = DifferentiateByAngle(
            model.HalfAngleFunction,
            budget);
        if (derivative.Numerator.IsZero)
        {
            return
            [
                new MonotoneRegion(AllRealSet.Instance, Monotonicity.Constant)
            ];
        }

        CircularSignChart chart = CircularSignChart.Create(derivative, budget);
        if (chart.Roots.Roots.IsEmpty)
        {
            return [];
        }

        ExactReal period = FullTurn(angleUnit);
        var result = ImmutableArray.CreateBuilder<MonotoneRegion>();
        for (int root = 0; root < chart.Roots.Roots.Length - 1; root++)
        {
            AddMonotoneInterval(
                result,
                HalfAngleInverse(chart.Roots.Roots[root], angleUnit),
                HalfAngleInverse(chart.Roots.Roots[root + 1], angleUnit),
                period,
                chart.GapSigns[root + 1]);
        }

        ExactReal last = HalfAngleInverse(chart.Roots.Roots[^1], angleUnit);
        ExactReal first = HalfAngleInverse(chart.Roots.Roots[0], angleUnit);
        int positiveInfinity = chart.GapSigns[^1];
        int negativeInfinity = chart.GapSigns[0];
        if (!chart.InfinityIsRoot || positiveInfinity == negativeInfinity)
        {
            AddMonotoneInterval(
                result,
                last,
                ExactRealArithmetic.Add(first, period),
                period,
                positiveInfinity);
        }
        else
        {
            AddMonotoneInterval(
                result,
                last,
                HalfTurn(angleUnit),
                period,
                positiveInfinity);
            AddMonotoneInterval(
                result,
                ExactRealArithmetic.Negate(HalfTurn(angleUnit)),
                first,
                period,
                negativeInfinity);
        }

        return result.ToImmutable();
    }

    private static void AddMonotoneInterval(
        ImmutableArray<MonotoneRegion>.Builder result,
        ExactReal lower,
        ExactReal upper,
        ExactReal period,
        int sign)
    {
        if (sign == 0)
        {
            return;
        }

        result.Add(new MonotoneRegion(
            new PeriodicIntervalSet(
                period,
                "m",
                IntegerConstraint.All("m"),
                [new PeriodicInterval(lower, false, upper, false)]),
            sign > 0 ? Monotonicity.Increasing : Monotonicity.Decreasing));
    }

    private static Periodicity BuildPeriod(
        TrigonometricPolynomialModel model,
        AngleUnit angleUnit)
    {
        int frequency = model.Fourier.FrequencyGcd;
        if (frequency == 0)
        {
            return new Periodicity(
                PeriodicityKind.PeriodicWithoutFundamentalPeriod,
                null);
        }

        return new Periodicity(
            PeriodicityKind.PeriodicWithFundamentalPeriod,
            ExactRealArithmetic.Scale(
                FullTurn(angleUnit),
                new BigRational(1, frequency)));
    }

    private static RationalFunction DifferentiateByAngle(
        RationalFunction function,
        ResourceBudget budget)
    {
        RationalFunction derivativeInT = function.Derivative(budget);
        UnivariatePolynomial onePlusSquare = UnivariatePolynomial.Create(
            [BigRational.One, BigRational.Zero, BigRational.One],
            budget);
        return derivativeInT.Multiply(
            new RationalFunction(
                onePlusSquare,
                UnivariatePolynomial.Create([new BigRational(2)], budget)),
            budget);
    }

    private static ExactReal EvaluateAt(
        RationalFunction function,
        ExactReal parameter,
        ResourceBudget budget)
    {
        return parameter switch
        {
            RationalReal rational => new RationalReal(
                function.Evaluate(rational.Value, budget)),
            AlgebraicReal algebraic => new AlgebraicImageReal(function, algebraic),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter))
        };
    }

    private static bool TryLimitAtInfinity(
        RationalFunction function,
        out BigRational value)
    {
        int difference = function.Numerator.Degree - function.Denominator.Degree;
        if (difference < 0)
        {
            value = BigRational.Zero;
            return true;
        }

        if (difference == 0)
        {
            value = function.Numerator.LeadingCoefficient /
                    function.Denominator.LeadingCoefficient;
            return true;
        }

        value = default;
        return false;
    }

    private static ExactReal HalfAngleInverse(
        ExactReal parameter,
        AngleUnit angleUnit)
    {
        if (parameter is RationalReal rational)
        {
            if (rational.Value.IsZero)
            {
                return new RationalReal(BigRational.Zero);
            }

            if (rational.Value == BigRational.One)
            {
                return ExactRealArithmetic.Scale(
                    HalfTurn(angleUnit),
                    new BigRational(1, 2));
            }

            if (rational.Value == BigRational.MinusOne)
            {
                return ExactRealArithmetic.Negate(
                    ExactRealArithmetic.Scale(
                        HalfTurn(angleUnit),
                        new BigRational(1, 2)));
            }
        }

        return ExactAngleArithmetic.FromRadians(
            new FunctionReal("twice-atan", [parameter]),
            angleUnit);
    }

    private static ExactReal FullTurn(AngleUnit angleUnit)
    {
        return ExactAngleArithmetic.PiFraction(angleUnit, new BigRational(2));
    }

    private static ExactReal HalfTurn(AngleUnit angleUnit)
    {
        return ExactAngleArithmetic.PiFraction(angleUnit, BigRational.One);
    }

    private static bool TryReplayMixedDomain(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        ResourceBudget budget,
        out object value)
    {
        if (certificate.Feature != AnalysisFeatures.Domain ||
            !string.Equals(
                certificate.Parameters[1],
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal) ||
            !MatchesAngle(certificate.Parameters[2], request.AngleUnit) ||
            !TrySolveDefinedness(
                expression.DefinedWhen,
                request.Variable,
                request.AngleUnit,
                budget,
                out RealSet domain))
        {
            value = null!;
            return false;
        }

        value = domain;
        return true;
    }

    private static bool TryReplayMixedZeros(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        ResourceBudget budget,
        out object value)
    {
        if (certificate.Feature != AnalysisFeatures.Zeros ||
            !string.Equals(
                certificate.Parameters[1],
                expression.Value.Canonical,
                StringComparison.Ordinal) ||
            !MatchesAngle(certificate.Parameters[2], request.AngleUnit) ||
            !TrySolveAlgebraicRestrictions(
                expression.DefinedWhen,
                request.Variable,
                request.AngleUnit,
                budget,
                out RealSet domain))
        {
            value = null!;
            return false;
        }

        var factors = new List<ValueTerm>();
        FlattenProduct(expression.Value, factors);
        if (factors.Count < 2)
        {
            value = null!;
            return false;
        }

        var zeroSets = new List<RealSet>();
        foreach (ValueTerm factor in factors)
        {
            if (!TryFactorZeros(
                    factor,
                    request.Variable,
                    request.AngleUnit,
                    budget,
                    out RealSet zeros))
            {
                value = null!;
                return false;
            }

            zeroSets.Add(zeros);
        }

        RealSet candidates = RealSets.Union(zeroSets);
        if (!RealSetIntersectionSolver.TryIntersect(
                candidates,
                domain,
                budget,
                out RealSet result))
        {
            value = null!;
            return false;
        }

        value = result;
        return true;
    }

    private static bool TryReplayMixedIntercept(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        ResourceBudget budget,
        out object value)
    {
        if (certificate.Feature != AnalysisFeatures.YIntercept ||
            !string.Equals(
                certificate.Parameters[1],
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal) ||
            !MatchesAngle(certificate.Parameters[2], request.AngleUnit) ||
            !TrySolveAlgebraicRestrictions(
                expression.DefinedWhen,
                request.Variable,
                request.AngleUnit,
                budget,
                out RealSet domain) ||
            !RealSetIntersectionSolver.TryContainsRational(
                domain,
                BigRational.Zero,
                out bool containsZero) ||
            containsZero)
        {
            value = null!;
            return false;
        }

        value = OptionalValue<ExactReal>.None;
        return true;
    }

    private static bool TrySolveDefinedness(
        Formula formula,
        string variable,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out RealSet domain)
    {
        if (TryPeriodicAtom(formula, variable, angleUnit, budget, out domain))
        {
            return true;
        }

        if (formula is not JunctionFormula { IsConjunction: true } conjunction)
        {
            domain = null!;
            return false;
        }

        var algebraic = new List<Formula>();
        var periodic = new List<RealSet>();
        foreach (Formula operand in conjunction.Operands)
        {
            if (TryPeriodicAtom(operand, variable, angleUnit, budget, out RealSet set))
            {
                periodic.Add(set);
            }
            else
            {
                algebraic.Add(operand);
            }
        }

        if (periodic.Count == 0 ||
            !PolynomialFormulaConverter.TryConvert(
                Formula.And(algebraic),
                variable,
                budget,
                out PolynomialFormula polynomial))
        {
            domain = null!;
            return false;
        }

        RealSet result = CellDecomposer.Decompose(polynomial, budget).Result;
        foreach (RealSet periodicSet in periodic)
        {
            if (!RealSetIntersectionSolver.TryIntersect(
                    result,
                    periodicSet,
                    budget,
                    out result))
            {
                domain = null!;
                return false;
            }
        }

        domain = result;
        return true;
    }

    private static bool TrySolveAlgebraicRestrictions(
        Formula formula,
        string variable,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out RealSet domain)
    {
        if (formula is not JunctionFormula { IsConjunction: true } conjunction)
        {
            domain = null!;
            return false;
        }

        var algebraic = new List<Formula>();
        bool removedPeriodic = false;
        foreach (Formula operand in conjunction.Operands)
        {
            if (TryPeriodicAtom(operand, variable, angleUnit, budget, out _))
            {
                removedPeriodic = true;
            }
            else
            {
                algebraic.Add(operand);
            }
        }

        if (!removedPeriodic ||
            !PolynomialFormulaConverter.TryConvert(
                Formula.And(algebraic),
                variable,
                budget,
                out PolynomialFormula polynomial))
        {
            domain = null!;
            return false;
        }

        domain = CellDecomposer.Decompose(polynomial, budget).Result;
        return true;
    }

    private static bool TryPeriodicAtom(
        Formula formula,
        string variable,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out RealSet set)
    {
        if (formula is ComparisonFormula
            {
                Comparison: Comparison.NotEqual,
                Left: var primitive,
                Right: { Kind: ValueKind.Constant, Constant.IsZero: true }
            } &&
            TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(
                primitive,
                variable,
                budget,
                out AffineTrigPattern pattern) &&
            pattern.Function is "sin" or "cos" &&
            pattern.Shift.IsZero)
        {
            BigRational lower = pattern.Function == "sin"
                ? BigRational.MinusOne
                : new BigRational(-1, 2);
            BigRational upper = pattern.Function == "sin"
                ? BigRational.Zero
                : new BigRational(1, 2);
            ExactReal period = ExactRealArithmetic.Scale(
                ExactAngleArithmetic.PiFraction(angleUnit, BigRational.One),
                pattern.Frequency.Reciprocal());
            set = new PeriodicIntervalSet(
                period,
                "m",
                IntegerConstraint.All("m"),
                [
                    new PeriodicInterval(
                        SolveAffine(pattern, angleUnit, lower),
                        false,
                        SolveAffine(pattern, angleUnit, upper),
                        false)
                ]);
            return true;
        }

        set = null!;
        return false;
    }

    private static bool TryFactorZeros(
        ValueTerm factor,
        string variable,
        AngleUnit angleUnit,
        ResourceBudget budget,
        out RealSet zeros)
    {
        if (TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(
                factor,
                variable,
                budget,
                out AffineTrigPattern trig))
        {
            var exact = new ExactTrigPattern(
                trig.Function switch
                {
                    "sin" => ExactCoefficientPatternKind.AffineSine,
                    "cos" => ExactCoefficientPatternKind.AffineCosine,
                    "tan" => ExactCoefficientPatternKind.AffineTangent,
                    _ => throw new ArgumentOutOfRangeException(nameof(factor))
                },
                trig.Amplitude,
                ExactScalar.FromRational(trig.Frequency),
                ExactScalar.FromRational(trig.Phase),
                ExactScalar.FromRational(trig.Shift));
            if (ExactTrigCertificateReplay.TryCompute(
                    exact,
                    angleUnit,
                    AnalysisFeatures.Zeros,
                    budget,
                    out object result) &&
                result is RealSet zeroSet)
            {
                zeros = zeroSet;
                return true;
            }
        }

        if (factor.Kind == ValueKind.Function &&
            factor.Name is "log" or "ln" &&
            factor.Operands.Length == 1 &&
            RationalFunctionExtractor.TryExtract(
                factor.Operands[0],
                variable,
                budget,
                out RationalExtraction argument))
        {
            RationalFunction difference = argument.Function.Subtract(
                RationalFunction.Constant(BigRational.One, budget),
                budget);
            if (difference.Numerator.Degree == 1)
            {
                BigRational root =
                    -difference.Numerator[0] / difference.Numerator[1];
                zeros = RealSets.Points([new RationalReal(root)]);
                return true;
            }
        }

        zeros = null!;
        return false;
    }

    private static ExactReal SolveAffine(
        AffineTrigPattern pattern,
        AngleUnit angleUnit,
        BigRational fraction)
    {
        return ExactRealArithmetic.Scale(
            ExactRealArithmetic.AddRational(
                ExactAngleArithmetic.PiFraction(angleUnit, fraction),
                -pattern.Phase),
            pattern.Frequency.Reciprocal());
    }

    private static void FlattenProduct(ValueTerm term, ICollection<ValueTerm> factors)
    {
        if (term.Kind == ValueKind.Multiply)
        {
            FlattenProduct(term.Operands[0], factors);
            FlattenProduct(term.Operands[1], factors);
            return;
        }

        factors.Add(term);
    }

    private static bool MatchesAngle(string parameter, AngleUnit angleUnit)
    {
        return string.Equals(parameter, angleUnit.ToString(), StringComparison.Ordinal);
    }

    private static bool ClaimMatches(object value, string claim)
    {
        return string.Equals(
            ClaimCanonical.ForObject(value),
            claim,
            StringComparison.Ordinal);
    }
}
