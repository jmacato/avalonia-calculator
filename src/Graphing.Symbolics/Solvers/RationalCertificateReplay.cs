using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Checker-owned replay for legacy rational-function certificates. It rebuilds
/// sign charts, exact substitutions, derivative classifications, and limiting
/// asymptotes without calling the producing rational feature analyzer.
/// </summary>
internal static class RationalCertificateReplay
{
    public static bool TryCreateContext(SemanticExpression expression, string variable, ResourceBudget budget, out RationalAnalysisContext context)
    {
        budget.Charge();
        if (!RationalFunctionExtractor.TryExtract(expression.Value, variable, budget, out RationalExtraction extraction) || !PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, variable, budget, out PolynomialFormula domainFormula))
        {
            context = null!;
            return false;
        }

        context = new RationalAnalysisContext(expression, variable, extraction, domainFormula, CellDecomposer.Decompose(domainFormula, budget));
        return true;
    }

    public static bool TryCompute(RationalAnalysisContext context, AnalysisFeatures feature, ResourceBudget budget, out object value, out ImmutableArray<RootIsolationCertificate> roots)
    {
        budget.Charge();
        switch (feature)
        {
            case AnalysisFeatures.Zeros:
                value = ComputeZeros(context, budget, out roots);
                return true;
            case AnalysisFeatures.YIntercept:
                roots = [];
                value = ComputeYIntercept(context, budget);
                return true;
            case AnalysisFeatures.Parity:
                value = ComputeParity(context, budget, out roots);
                return true;
            case AnalysisFeatures.Minima:
                ComputeExtrema(context, budget, out ImmutableArray<FeaturePoint> minima, out _, out roots);
                value = minima;
                return true;
            case AnalysisFeatures.Maxima:
                ComputeExtrema(context, budget, out _, out ImmutableArray<FeaturePoint> maxima, out roots);
                value = maxima;
                return true;
            case AnalysisFeatures.InflectionPoints:
                value = ComputeInflections(context, budget, out roots);
                return true;
            case AnalysisFeatures.Monotonicity:
                value = ComputeMonotonicity(context, budget, out roots);
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
                value = ComputeVerticalAsymptotes(context, budget, out roots);
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
                roots = [];
                value = ComputeHorizontalAsymptotes(context);
                return true;
            case AnalysisFeatures.ObliqueAsymptotes:
                roots = [];
                value = ComputeObliqueAsymptotes(context, budget);
                return true;
            case AnalysisFeatures.Period:
                roots = [];
                value = ComputePeriod(context);
                return true;
            default:
                roots = [];
                value = null!;
                return false;
        }
    }

    internal static RealSet ComputeZeros(RationalAnalysisContext context, ResourceBudget budget, out ImmutableArray<RootIsolationCertificate> roots)
    {
        if (context.Function.Numerator.IsZero)
        {
            roots = [context.DomainCells.RootIsolation];
            return context.Domain;
        }

        PolynomialAtom zero = new(context.Function.Numerator.PrimitivePositive(budget), Comparison.Equal);
        PolynomialFormula formula = new PolynomialJunction(true, [context.DomainFormula, zero]);
        CellDecompositionCertificate cells = CellDecomposer.Decompose(formula, budget);
        roots = [cells.RootIsolation];
        return cells.Result;
    }

    internal static OptionalValue<ExactReal> ComputeYIntercept(RationalAnalysisContext context, ResourceBudget budget)
    {
        if (!EvaluateFormulaAt(context.DomainFormula, BigRational.Zero, budget))
        {
            return OptionalValue<ExactReal>.None;
        }

        return OptionalValue<ExactReal>.Some(new RationalReal(context.Function.Evaluate(BigRational.Zero, budget)));
    }

    internal static FunctionParity ComputeParity(RationalAnalysisContext context, ResourceBudget budget, out ImmutableArray<RootIsolationCertificate> roots)
    {
        PolynomialFormula reflectedDomain = SubstituteNegative(context.DomainFormula, budget);
        PolynomialFormula asymmetric = new PolynomialJunction(false, [new PolynomialJunction(true, [context.DomainFormula, new PolynomialNot(reflectedDomain)]), new PolynomialJunction(true, [new PolynomialNot(context.DomainFormula), reflectedDomain])]);
        CellDecompositionCertificate symmetryCells = CellDecomposer.Decompose(asymmetric, budget);
        if (!symmetryCells.Result.IsEmpty)
        {
            roots = [symmetryCells.RootIsolation];
            return FunctionParity.Neither;
        }

        RationalFunction function = context.Function;
        UnivariatePolynomial negativeNumerator = function.Numerator.SubstituteNegativeVariable(budget);
        UnivariatePolynomial negativeDenominator = function.Denominator.SubstituteNegativeVariable(budget);
        UnivariatePolynomial left = negativeNumerator.Multiply(function.Denominator, budget);
        UnivariatePolynomial right = function.Numerator.Multiply(negativeDenominator, budget);
        bool even = left.Subtract(right, budget).IsZero;
        bool odd = left.Add(right, budget).IsZero;
        roots = [symmetryCells.RootIsolation];
        return (even, odd) switch
        {
            (true, true) => FunctionParity.Both,
            (true, false) => FunctionParity.Even,
            (false, true) => FunctionParity.Odd,
            _ => FunctionParity.Neither
        };
    }

    internal static void ComputeExtrema(RationalAnalysisContext context, ResourceBudget budget, out ImmutableArray<FeaturePoint> minima, out ImmutableArray<FeaturePoint> maxima, out ImmutableArray<RootIsolationCertificate> roots)
    {
        RationalFunction derivative = context.Function.Derivative(budget);
        if (derivative.Numerator.IsZero)
        {
            minima = [];
            maxima = [];
            roots = [];
            return;
        }

        SignChart chart = BuildSignChart(context, [derivative.Numerator, derivative.Denominator], budget);
        var minimumBuilder = ImmutableArray.CreateBuilder<FeaturePoint>();
        var maximumBuilder = ImmutableArray.CreateBuilder<FeaturePoint>();
        for (int point = 0; point < chart.Roots.Roots.Length; point++)
        {
            if (!chart.PointDomain[point] || RationalSignAtPoint(chart, derivative, point) != 0)
            {
                continue;
            }

            bool leftDomain = chart.GapDomain[point];
            bool rightDomain = chart.GapDomain[point + 1];
            if (!leftDomain && !rightDomain)
            {
                continue;
            }

            int leftSign = leftDomain ? RationalSignInGap(chart, derivative, point) : 0;
            int rightSign = rightDomain ? RationalSignInGap(chart, derivative, point + 1) : 0;
            ExactReal x = chart.Roots.Roots[point];
            var featurePoint = new ConstantYFeaturePoint(new SingletonReal(x), EvaluateExact(context.Function, x, budget));
            RationalCertificateReplayExtremumKind kind = ClassifyExtremum(leftDomain, rightDomain, leftSign, rightSign);
            if (kind == RationalCertificateReplayExtremumKind.Minimum)
            {
                minimumBuilder.Add(featurePoint);
            }

            if (kind == RationalCertificateReplayExtremumKind.Maximum)
            {
                maximumBuilder.Add(featurePoint);
            }
        }

        minima = minimumBuilder.ToImmutable();
        maxima = maximumBuilder.ToImmutable();
        roots = [chart.Roots];
    }

    internal static ImmutableArray<FeaturePoint> ComputeInflections(RationalAnalysisContext context, ResourceBudget budget, out ImmutableArray<RootIsolationCertificate> roots)
    {
        RationalFunction second = context.Function.Derivative(budget).Derivative(budget);
        if (second.Numerator.IsZero)
        {
            roots = [];
            return [];
        }

        SignChart chart = BuildSignChart(context, [second.Numerator, second.Denominator], budget);
        var result = ImmutableArray.CreateBuilder<FeaturePoint>();
        for (int point = 0; point < chart.Roots.Roots.Length; point++)
        {
            if (!chart.PointDomain[point] || !chart.GapDomain[point] || !chart.GapDomain[point + 1] || RationalSignAtPoint(chart, second, point) != 0)
            {
                continue;
            }

            int left = RationalSignInGap(chart, second, point);
            int right = RationalSignInGap(chart, second, point + 1);
            if (left == 0 || right == 0 || left == right)
            {
                continue;
            }

            ExactReal x = chart.Roots.Roots[point];
            result.Add(new ConstantYFeaturePoint(new SingletonReal(x), EvaluateExact(context.Function, x, budget)));
        }

        roots = [chart.Roots];
        return result.ToImmutable();
    }

    internal static ImmutableArray<MonotoneRegion> ComputeMonotonicity(RationalAnalysisContext context, ResourceBudget budget, out ImmutableArray<RootIsolationCertificate> roots)
    {
        RationalFunction derivative = context.Function.Derivative(budget);
        if (derivative.Numerator.IsZero)
        {
            roots = [context.DomainCells.RootIsolation];
            return OneDimensionalComponents(context.Domain).Select(static component => new MonotoneRegion(component, Graphing.Symbolics.Monotonicity.Constant)).ToImmutableArray();
        }

        SignChart chart = BuildSignChart(context, [derivative.Numerator, derivative.Denominator], budget);
        var result = ImmutableArray.CreateBuilder<MonotoneRegion>();
        int rootCount = chart.Roots.Roots.Length;
        int gap = 0;
        while (gap <= rootCount)
        {
            budget.Charge();
            if (!chart.GapDomain[gap])
            {
                gap++;
                continue;
            }

            int sign = RationalSignInGap(chart, derivative, gap);
            if (sign == 0)
            {
                gap++;
                continue;
            }

            int startGap = gap;
            int endGap = gap;
            while (endGap < rootCount && chart.PointDomain[endGap] && chart.GapDomain[endGap + 1] && RationalSignInGap(chart, derivative, endGap + 1) == sign)
            {
                budget.Charge();
                endGap++;
            }

            RealBound lower = startGap == 0 ? RealBound.NegativeInfinity : RealBound.Finite(chart.Roots.Roots[startGap - 1]);
            RealBound upper = endGap == rootCount ? RealBound.PositiveInfinity : RealBound.Finite(chart.Roots.Roots[endGap]);
            RealSet region = startGap == 0 && endGap == rootCount ? AllRealSet.Instance : new IntervalSet(lower, false, upper, false);
            result.Add(new MonotoneRegion(region, sign > 0 ? Graphing.Symbolics.Monotonicity.Increasing : Graphing.Symbolics.Monotonicity.Decreasing));
            gap = endGap + 1;
        }

        roots = [chart.Roots];
        return result.ToImmutable();
    }

    internal static ImmutableArray<Asymptote> ComputeVerticalAsymptotes(RationalAnalysisContext context, ResourceBudget budget, out ImmutableArray<RootIsolationCertificate> roots)
    {
        if (context.Function.Denominator.Degree <= 0)
        {
            roots = [];
            return [];
        }

        RootIsolationCertificate isolation = SturmRootIsolator.Isolate(context.Function.Denominator, budget);
        roots = [isolation];
        return isolation.Roots.Select(static root => new Asymptote(AsymptoteOrientation.Vertical, new SingletonReal(root), null, null)).ToImmutableArray();
    }

    internal static ImmutableArray<Asymptote> ComputeHorizontalAsymptotes(RationalAnalysisContext context)
    {
        RationalFunction function = context.Function;
        if (!HasUnboundedComponent(context.Domain) || function.Numerator.Degree > function.Denominator.Degree)
        {
            return [];
        }

        bool constant = function.Numerator.Degree <= 0 && function.Denominator.Degree <= 0;
        if (!constant && function.Denominator.Degree <= 0)
        {
            return [];
        }

        BigRational value = function.Numerator.Degree == function.Denominator.Degree ? function.Numerator.LeadingCoefficient / function.Denominator.LeadingCoefficient : BigRational.Zero;
        return [new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(new RationalReal(value)), null, new RationalReal(value))];
    }

    internal static ImmutableArray<Asymptote> ComputeObliqueAsymptotes(RationalAnalysisContext context, ResourceBudget budget)
    {
        RationalFunction function = context.Function;
        if (!HasUnboundedComponent(context.Domain) || function.Numerator.Degree - function.Denominator.Degree != 1)
        {
            return [];
        }

        (UnivariatePolynomial quotient, _) = function.Numerator.Divide(function.Denominator, budget);
        ExactReal slope = new RationalReal(quotient[1]);
        ExactReal intercept = new RationalReal(quotient[0]);
        return [new Asymptote(AsymptoteOrientation.Oblique, new SingletonReal(intercept), slope, intercept)];
    }

    internal static Periodicity ComputePeriod(RationalAnalysisContext context) => context.Function.Numerator.Degree <= 0 && context.Function.Denominator.Degree <= 0 && context.Domain is AllRealSet or EmptySet ? new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null) : new Periodicity(PeriodicityKind.NotPeriodic, null);
    private static SignChart BuildSignChart(RationalAnalysisContext context, ImmutableArray<UnivariatePolynomial> extras, ResourceBudget budget)
    {
        ImmutableArray<UnivariatePolynomial> domainAtoms = PolynomialFormulaConverter.Atoms(context.DomainFormula);
        ImmutableArray<UnivariatePolynomial> polynomials = domainAtoms.Concat(extras.Where(static polynomial => !polynomial.IsZero)).DistinctBy(static polynomial => polynomial.Canonical).OrderBy(static polynomial => polynomial.Canonical, StringComparer.Ordinal).ToImmutableArray();
        UnivariatePolynomial combined = UnivariatePolynomial.One;
        foreach (UnivariatePolynomial polynomial in polynomials)
        {
            combined = combined.Multiply(polynomial, budget);
        }

        combined = combined.SquareFreePart(budget).PrimitivePositive(budget);
        RootIsolationCertificate roots = SturmRootIsolator.Isolate(combined, budget);
        var gapSamples = ImmutableArray.CreateBuilder<BigRational>(roots.Roots.Length + 1);
        var gapSigns = ImmutableArray.CreateBuilder<ImmutableArray<int>>(roots.Roots.Length + 1);
        var pointSigns = ImmutableArray.CreateBuilder<ImmutableArray<int>>(roots.Roots.Length);
        var gapDomain = ImmutableArray.CreateBuilder<bool>(roots.Roots.Length + 1);
        var pointDomain = ImmutableArray.CreateBuilder<bool>(roots.Roots.Length);
        for (int gap = 0; gap <= roots.Roots.Length; gap++)
        {
            BigRational sample = SampleGap(roots.Roots, gap);
            ImmutableArray<int> signs = polynomials.Select(polynomial => polynomial.Evaluate(sample, budget).Sign).ToImmutableArray();
            gapSamples.Add(sample);
            gapSigns.Add(signs);
            gapDomain.Add(EvaluateFormula(context.DomainFormula, polynomials, signs));
            if (gap == roots.Roots.Length)
            {
                continue;
            }

            ExactReal root = roots.Roots[gap];
            ImmutableArray<int> atPoint = polynomials.Select(polynomial => SignAt(polynomial, root, budget)).ToImmutableArray();
            pointSigns.Add(atPoint);
            pointDomain.Add(EvaluateFormula(context.DomainFormula, polynomials, atPoint));
        }

        return new SignChart(polynomials, roots, gapSamples.MoveToImmutable(), gapSigns.MoveToImmutable(), pointSigns.MoveToImmutable(), gapDomain.MoveToImmutable(), pointDomain.MoveToImmutable());
    }

    private static int RationalSignInGap(SignChart chart, RationalFunction function, int gap) => chart.SignInGap(function.Numerator, gap) * chart.SignInGap(function.Denominator, gap);
    private static int RationalSignAtPoint(SignChart chart, RationalFunction function, int point) => chart.SignAtPoint(function.Numerator, point) * chart.SignAtPoint(function.Denominator, point);
    private static bool EvaluateFormulaAt(PolynomialFormula formula, BigRational value, ResourceBudget budget)
    {
        ImmutableArray<UnivariatePolynomial> atoms = PolynomialFormulaConverter.Atoms(formula);
        var signs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnivariatePolynomial atom in atoms)
        {
            signs.Add(atom.Canonical, atom.Evaluate(value, budget).Sign);
        }

        return PolynomialFormulaConverter.Evaluate(formula, signs);
    }

    private static bool EvaluateFormula(PolynomialFormula formula, ImmutableArray<UnivariatePolynomial> polynomials, ImmutableArray<int> signs)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < polynomials.Length; index++)
        {
            map.Add(polynomials[index].Canonical, signs[index]);
        }

        return PolynomialFormulaConverter.Evaluate(formula, map);
    }

    private static PolynomialFormula SubstituteNegative(PolynomialFormula formula, ResourceBudget budget) => formula switch
    {
        PolynomialBoolean boolean => boolean,
        PolynomialAtom atom => NormalizeReflectedAtom(atom, budget),
        PolynomialNot not => new PolynomialNot(SubstituteNegative(not.Operand, budget)),
        PolynomialJunction junction => new PolynomialJunction(junction.IsConjunction, junction.Operands.Select(operand => SubstituteNegative(operand, budget)).ToImmutableArray()),
        _ => throw new ArgumentOutOfRangeException(nameof(formula))
    };
    private static PolynomialAtom NormalizeReflectedAtom(PolynomialAtom atom, ResourceBudget budget)
    {
        UnivariatePolynomial reflected = atom.Polynomial.SubstituteNegativeVariable(budget);
        Comparison comparison = reflected.LeadingCoefficient.Sign < 0 ? Reverse(atom.Comparison) : atom.Comparison;
        return new PolynomialAtom(reflected.PrimitivePositive(budget), comparison);
    }

    private static Comparison Reverse(Comparison comparison) => comparison switch
    {
        Comparison.Less => Comparison.Greater,
        Comparison.LessOrEqual => Comparison.GreaterOrEqual,
        Comparison.Greater => Comparison.Less,
        Comparison.GreaterOrEqual => Comparison.LessOrEqual,
        _ => comparison
    };
    private static bool DomainIsExactlyReducedDenominator(RationalAnalysisContext context, ResourceBudget budget)
    {
        PolynomialAtom nonzero = new(context.Function.Denominator.PrimitivePositive(budget), Comparison.NotEqual);
        CellDecompositionCertificate expected = CellDecomposer.Decompose(nonzero, budget);
        return string.Equals(expected.Result.Canonical, context.Domain.Canonical, StringComparison.Ordinal);
    }

    private static IEnumerable<RealSet> OneDimensionalComponents(RealSet set) => set switch
    {
        AllRealSet => [AllRealSet.Instance],
        IntervalSet interval => [interval],
        UnionSet union => union.Operands.SelectMany(OneDimensionalComponents),
        _ => []
    };
    private static bool HasUnboundedComponent(RealSet set) => set switch
    {
        AllRealSet => true,
        IntervalSet interval => interval.Lower.Kind == BoundKind.NegativeInfinity || interval.Upper.Kind == BoundKind.PositiveInfinity,
        UnionSet union => union.Operands.Any(HasUnboundedComponent),
        _ => false
    };
    private static ExactReal EvaluateExact(RationalFunction function, ExactReal value, ResourceBudget budget) => value switch
    {
        RationalReal rational => new RationalReal(function.Evaluate(rational.Value, budget)),
        AlgebraicReal algebraic => new AlgebraicImageReal(function, algebraic),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    private static BigRational SampleGap(ImmutableArray<ExactReal> roots, int gap)
    {
        if (roots.IsEmpty)
        {
            return BigRational.Zero;
        }

        if (gap == 0)
        {
            return Lower(roots[0]) - BigRational.One;
        }

        if (gap == roots.Length)
        {
            return Upper(roots[^1]) + BigRational.One;
        }

        return (Upper(roots[gap - 1]) + Lower(roots[gap])) / 2;
    }

    private static BigRational Lower(ExactReal root) => root switch
    {
        RationalReal rational => rational.Value,
        AlgebraicReal algebraic => algebraic.IsolatingInterval.Lower,
        _ => throw new ArgumentOutOfRangeException(nameof(root))
    };
    private static BigRational Upper(ExactReal root) => root switch
    {
        RationalReal rational => rational.Value,
        AlgebraicReal algebraic => algebraic.IsolatingInterval.Upper,
        _ => throw new ArgumentOutOfRangeException(nameof(root))
    };
    private static int SignAt(UnivariatePolynomial polynomial, ExactReal root, ResourceBudget budget) => root switch
    {
        RationalReal rational => polynomial.Evaluate(rational.Value, budget).Sign,
        AlgebraicReal algebraic => SturmRootIsolator.SignAtIsolatedRoot(algebraic.Polynomial, polynomial, algebraic.IsolatingInterval, budget),
        _ => throw new ArgumentOutOfRangeException(nameof(root))
    };
    private static RationalCertificateReplayExtremumKind ClassifyExtremum(bool leftDomain, bool rightDomain, int leftSign, int rightSign)
    {
        if (leftDomain && rightDomain)
        {
            if (leftSign < 0 && rightSign > 0)
            {
                return RationalCertificateReplayExtremumKind.Minimum;
            }

            if (leftSign > 0 && rightSign < 0)
            {
                return RationalCertificateReplayExtremumKind.Maximum;
            }

            return RationalCertificateReplayExtremumKind.None;
        }

        if (rightDomain)
        {
            return rightSign switch
            {
                > 0 => RationalCertificateReplayExtremumKind.Minimum,
                < 0 => RationalCertificateReplayExtremumKind.Maximum,
                _ => RationalCertificateReplayExtremumKind.None
            };
        }

        if (leftDomain)
        {
            return leftSign switch
            {
                < 0 => RationalCertificateReplayExtremumKind.Minimum,
                > 0 => RationalCertificateReplayExtremumKind.Maximum,
                _ => RationalCertificateReplayExtremumKind.None
            };
        }

        return RationalCertificateReplayExtremumKind.None;
    }
}
