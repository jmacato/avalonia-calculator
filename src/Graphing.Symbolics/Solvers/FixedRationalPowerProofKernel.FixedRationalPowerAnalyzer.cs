using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class FixedRationalPowerProofKernel
{
    public static CellDecompositionCertificate BuildSignChart(FixedRationalPowerContext context, ResourceBudget budget) => CellDecomposer.Decompose(BuildSignChartFormula(context, budget), budget);
    public static bool VerifySignChart(FixedRationalPowerContext context, CellDecompositionCertificate chart, ResourceBudget budget)
    {
        PolynomialFormula expected = BuildSignChartFormula(context, budget);
        return string.Equals(expected.Canonical, chart.Formula.Canonical, StringComparison.Ordinal) && CellDecomposer.Verify(chart, budget);
    }

    public static ImmutableArray<CellDecompositionCertificate> BuildParityCells(FixedRationalPowerContext context, ResourceBudget budget)
    {
        PolynomialFormula reflectedDomain = ReflectFormula(context.DomainFormula, budget);
        PolynomialFormula asymmetric = new PolynomialJunction(false, [new PolynomialJunction(true, [context.DomainFormula, new PolynomialNot(reflectedDomain)]), new PolynomialJunction(true, [new PolynomialNot(context.DomainFormula), reflectedDomain])]);
        RationalFunction reflected = new(context.Basis.Numerator.SubstituteNegativeVariable(budget), context.Basis.Denominator.SubstituteNegativeVariable(budget));
        RationalFunction difference = context.Basis.Subtract(reflected, budget);
        PolynomialFormula unequal = difference.Numerator.IsZero ? new PolynomialBoolean(false) : new PolynomialAtom(difference.Numerator.PrimitivePositive(budget), Comparison.NotEqual);
        PolynomialFormula equalityViolation = new PolynomialJunction(true, [context.DomainFormula, unequal]);
        return [CellDecomposer.Decompose(asymmetric, budget), CellDecomposer.Decompose(equalityViolation, budget)];
    }

    public static bool TryComputeFeature(FixedRationalPowerContext context, CellDecompositionCertificate chart, AnalysisFeatures feature, ImmutableArray<CellDecompositionCertificate> auxiliaries, ResourceBudget budget, out object value)
    {
        switch (feature)
        {
            case AnalysisFeatures.Parity when auxiliaries.Length == 2:
                value = ComputeParity(context, chart, auxiliaries);
                return true;
            case AnalysisFeatures.Zeros:
                value = ComputeZeros(context, chart);
                return true;
            case AnalysisFeatures.YIntercept:
                value = ComputeYIntercept(context, budget);
                return true;
            case AnalysisFeatures.Minima:
                ComputeExtrema(context, chart, budget, out ImmutableArray<FeaturePoint> minima, out _);
                value = minima;
                return true;
            case AnalysisFeatures.Maxima:
                ComputeExtrema(context, chart, budget, out _, out ImmutableArray<FeaturePoint> maxima);
                value = maxima;
                return true;
            case AnalysisFeatures.InflectionPoints:
                value = ComputeInflections(context, chart, budget);
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
                value = ComputeVerticalAsymptotes(context, chart);
                return true;
            case AnalysisFeatures.HorizontalAsymptotes:
                value = ComputeHorizontalAsymptotes(context, chart, budget);
                return true;
            case AnalysisFeatures.ObliqueAsymptotes:
                value = ComputeObliqueAsymptotes(context, chart, budget);
                return true;
            case AnalysisFeatures.Monotonicity:
                value = ComputeMonotonicity(context, chart, budget);
                return true;
            case AnalysisFeatures.Period:
                value = ComputePeriod(context, chart, budget);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    public static bool TryBuildRangeProof(FixedRationalPowerContext context, CellDecompositionCertificate chart, ResourceBudget budget, out RealSet range, out ImmutableArray<BigRational> boundaries, out ImmutableArray<RationalPowerRangeFiberWitness> fibers)
    {
        if (!TryDeriveRangeBoundaries(context, chart, budget, out boundaries))
        {
            range = null!;
            fibers = [];
            return false;
        }

        fibers = BuildRangeFibers(context, boundaries, budget);
        range = BuildRange(boundaries, fibers);
        return true;
    }

    public static bool VerifyRangeProof(FixedRationalPowerContext context, CellDecompositionCertificate chart, ImmutableArray<BigRational> boundaries, ImmutableArray<RationalPowerRangeFiberWitness> fibers, ResourceBudget budget, out RealSet range)
    {
        if (!TryDeriveRangeBoundaries(context, chart, budget, out ImmutableArray<BigRational> expectedBoundaries) || !expectedBoundaries.SequenceEqual(boundaries) || fibers.Length != checked((boundaries.Length * 2) + 1))
        {
            range = null!;
            return false;
        }

        int witness = 0;
        for (int gap = 0; gap <= boundaries.Length; gap++)
        {
            budget.Charge();
            if (!VerifyRangeFiber(context, fibers[witness++], RationalRangeCellKind.OpenInterval, gap, RangeGapSample(boundaries, gap), budget))
            {
                range = null!;
                return false;
            }

            if (gap < boundaries.Length && !VerifyRangeFiber(context, fibers[witness++], RationalRangeCellKind.Boundary, gap, boundaries[gap], budget))
            {
                range = null!;
                return false;
            }
        }

        range = BuildRange(boundaries, fibers);
        return true;
    }

    private static FunctionParity ComputeParity(FixedRationalPowerContext context, CellDecompositionCertificate chart, ImmutableArray<CellDecompositionCertificate> auxiliaries)
    {
        if (!auxiliaries[0].Result.IsEmpty || !auxiliaries[1].Result.IsEmpty)
        {
            return FunctionParity.Neither;
        }

        bool identicallyZero = chart.Result.IsEmpty || context.Exponent.Sign > 0 && chart.Cells.Where(static cell => cell.Included).All(cell => BasisSign(context, chart, cell) == 0);
        return identicallyZero ? FunctionParity.Both : FunctionParity.Even;
    }

    private static RealSet ComputeZeros(FixedRationalPowerContext context, CellDecompositionCertificate chart)
    {
        if (context.Exponent.Sign < 0)
        {
            return EmptySet.Instance;
        }

        return BuildSubset(chart, cell => cell.Included && BasisSign(context, chart, cell) == 0);
    }

    private static OptionalValue<ExactReal> ComputeYIntercept(FixedRationalPowerContext context, ResourceBudget budget)
    {
        if (!EvaluateFormulaAt(context.DomainFormula, BigRational.Zero, budget))
        {
            return OptionalValue<ExactReal>.None;
        }

        BigRational basis = context.Basis.Evaluate(BigRational.Zero, budget);
        budget.CheckCoefficient(basis);
        return OptionalValue<ExactReal>.Some(FixedRationalPowerValue.Compose(new RationalReal(basis), context.Exponent, budget));
    }

    private static void ComputeExtrema(FixedRationalPowerContext context, CellDecompositionCertificate chart, ResourceBudget budget, out ImmutableArray<FeaturePoint> minima, out ImmutableArray<FeaturePoint> maxima)
    {
        var minimumBuilder = ImmutableArray.CreateBuilder<FeaturePoint>();
        var maximumBuilder = ImmutableArray.CreateBuilder<FeaturePoint>();
        FixedRationalPowerProofKernelDifferentialData differential = Differential(context, budget);
        for (int point = 0; point < chart.RootIsolation.Roots.Length; point++)
        {
            budget.Charge();
            if (!PointIncluded(chart, point))
            {
                continue;
            }

            bool leftDomain = GapIncluded(chart, point);
            bool rightDomain = GapIncluded(chart, point + 1);
            if (!leftDomain && !rightDomain)
            {
                continue;
            }

            int left = leftDomain ? FirstDerivativeSign(context, chart, differential, GapCell(chart, point)) : 0;
            int right = rightDomain ? FirstDerivativeSign(context, chart, differential, GapCell(chart, point + 1)) : 0;
            FixedRationalPowerProofKernelExtremumKind kind = ClassifyExtremum(leftDomain, rightDomain, left, right);
            if (kind == FixedRationalPowerProofKernelExtremumKind.None)
            {
                continue;
            }

            ExactReal x = chart.RootIsolation.Roots[point];
            var featurePoint = new ConstantYFeaturePoint(new SingletonReal(x), ComposeAt(context, chart, point, x, budget));
            if (kind == FixedRationalPowerProofKernelExtremumKind.Minimum)
            {
                minimumBuilder.Add(featurePoint);
            }
            else
            {
                maximumBuilder.Add(featurePoint);
            }
        }

        minima = minimumBuilder.ToImmutable();
        maxima = maximumBuilder.ToImmutable();
    }

    private static ImmutableArray<FeaturePoint> ComputeInflections(FixedRationalPowerContext context, CellDecompositionCertificate chart, ResourceBudget budget)
    {
        var result = ImmutableArray.CreateBuilder<FeaturePoint>();
        FixedRationalPowerProofKernelDifferentialData differential = Differential(context, budget);
        for (int point = 0; point < chart.RootIsolation.Roots.Length; point++)
        {
            budget.Charge();
            if (!PointIncluded(chart, point) || !GapIncluded(chart, point) || !GapIncluded(chart, point + 1))
            {
                continue;
            }

            int left = CurvatureSign(context, chart, differential, GapCell(chart, point));
            int right = CurvatureSign(context, chart, differential, GapCell(chart, point + 1));
            if (left == 0 || right == 0 || left == right)
            {
                continue;
            }

            ExactReal x = chart.RootIsolation.Roots[point];
            result.Add(new ConstantYFeaturePoint(new SingletonReal(x), ComposeAt(context, chart, point, x, budget)));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<MonotoneRegion> ComputeMonotonicity(FixedRationalPowerContext context, CellDecompositionCertificate chart, ResourceBudget budget)
    {
        var result = ImmutableArray.CreateBuilder<MonotoneRegion>();
        FixedRationalPowerProofKernelDifferentialData differential = Differential(context, budget);
        int gap = 0;
        while (gap <= chart.RootIsolation.Roots.Length)
        {
            budget.Charge();
            if (!GapIncluded(chart, gap))
            {
                gap++;
                continue;
            }

            int sign = FirstDerivativeSign(context, chart, differential, GapCell(chart, gap));
            int start = gap;
            int end = gap;
            while (end < chart.RootIsolation.Roots.Length && PointIncluded(chart, end) && GapIncluded(chart, end + 1) && FirstDerivativeSign(context, chart, differential, GapCell(chart, end + 1)) == sign)
            {
                budget.Charge();
                end++;
            }

            RealSet region = start == 0 && end == chart.RootIsolation.Roots.Length ? AllRealSet.Instance : new IntervalSet(start == 0 ? RealBound.NegativeInfinity : RealBound.Finite(chart.RootIsolation.Roots[start - 1]), false, end == chart.RootIsolation.Roots.Length ? RealBound.PositiveInfinity : RealBound.Finite(chart.RootIsolation.Roots[end]), false);
            result.Add(new MonotoneRegion(region, sign switch
            {
                > 0 => Monotonicity.Increasing,
                < 0 => Monotonicity.Decreasing,
                _ => Monotonicity.Constant
            }));
            gap = end + 1;
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<Asymptote> ComputeVerticalAsymptotes(FixedRationalPowerContext context, CellDecompositionCertificate chart)
    {
        var result = ImmutableArray.CreateBuilder<Asymptote>();
        for (int point = 0; point < chart.RootIsolation.Roots.Length; point++)
        {
            if (!GapIncluded(chart, point) && !GapIncluded(chart, point + 1))
            {
                continue;
            }

            CellWitness pointCell = PointCell(chart, point);
            bool basisPole = PolynomialSign(chart, context.Basis.Denominator, pointCell) == 0 && PolynomialSign(chart, context.Basis.Numerator, pointCell) != 0;
            bool basisZero = PolynomialSign(chart, context.Basis.Numerator, pointCell) == 0 && PolynomialSign(chart, context.Basis.Denominator, pointCell) != 0;
            if (context.Exponent.Sign > 0 ? !basisPole : !basisZero)
            {
                continue;
            }

            result.Add(new Asymptote(AsymptoteOrientation.Vertical, new SingletonReal(chart.RootIsolation.Roots[point]), null, null));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<Asymptote> ComputeHorizontalAsymptotes(FixedRationalPowerContext context, CellDecompositionCertificate chart, ResourceBudget budget)
    {
        var coordinates = new SortedDictionary<string, ExactReal>(StringComparer.Ordinal);
        if (GapIncluded(chart, 0) && TryTailLimit(context, budget, out ExactReal? left))
        {
            coordinates[ExactRealCanonical.Format(left)] = left;
        }

        int rightGap = chart.RootIsolation.Roots.Length;
        if (GapIncluded(chart, rightGap) && TryTailLimit(context, budget, out ExactReal? right))
        {
            coordinates[ExactRealCanonical.Format(right)] = right;
        }

        return coordinates.Values.Select(static coordinate => new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(coordinate), null, coordinate)).ToImmutableArray();
    }

    private static ImmutableArray<Asymptote> ComputeObliqueAsymptotes(FixedRationalPowerContext context, CellDecompositionCertificate chart, ResourceBudget budget)
    {
        int degreeDifference = context.Basis.Numerator.Degree - context.Basis.Denominator.Degree;
        if (degreeDifference <= 0 || context.Exponent.Numerator != ExactInteger.One || context.Exponent.Denominator != degreeDifference)
        {
            return [];
        }

        BigRational leading = context.Basis.Numerator.LeadingCoefficient / context.Basis.Denominator.LeadingCoefficient;
        BigRational next = AsymptoticNextCoefficient(context.Basis);
        budget.CheckCoefficient(leading);
        budget.CheckCoefficient(next);
        ExactReal magnitude = FixedRationalPowerValue.Compose(new RationalReal(leading.Abs()), context.Exponent, budget);
        BigRational interceptFactor = next / (new BigRational(degreeDifference) * leading);
        budget.CheckCoefficient(interceptFactor);
        var result = ImmutableArray.CreateBuilder<Asymptote>(2);
        if (GapIncluded(chart, 0))
        {
            ExactReal slope = ExactRealArithmetic.Negate(magnitude);
            ExactReal intercept = ExactRealArithmetic.Scale(magnitude, -interceptFactor);
            result.Add(Oblique(slope, intercept));
        }

        if (GapIncluded(chart, chart.RootIsolation.Roots.Length))
        {
            ExactReal intercept = ExactRealArithmetic.Scale(magnitude, interceptFactor);
            result.Add(Oblique(magnitude, intercept));
        }

        return result.ToImmutable();
    }

    private static Periodicity ComputePeriod(FixedRationalPowerContext context, CellDecompositionCertificate chart, ResourceBudget budget)
    {
        if (chart.Result.IsEmpty)
        {
            return new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null);
        }

        RationalFunction derivative = context.Basis.Derivative(budget);
        return chart.Result is AllRealSet && derivative.Numerator.IsZero ? new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null) : new Periodicity(PeriodicityKind.NotPeriodic, null);
    }

    private static bool TryDeriveRangeBoundaries(FixedRationalPowerContext context, CellDecompositionCertificate chart, ResourceBudget budget, out ImmutableArray<BigRational> boundaries)
    {
        if (chart.Result.IsEmpty)
        {
            boundaries = [];
            return true;
        }

        var result = new SortedSet<BigRational>
        {
            BigRational.Zero
        };
        FixedRationalPowerProofKernelDifferentialData differential = Differential(context, budget);
        for (int point = 0; point < chart.RootIsolation.Roots.Length; point++)
        {
            budget.Charge();
            if (!TouchesDomain(chart, point))
            {
                continue;
            }

            CellWitness pointCell = PointCell(chart, point);
            if (PolynomialSign(chart, context.Basis.Denominator, pointCell) == 0)
            {
                continue;
            }

            int basisSign = BasisSign(context, chart, pointCell);
            if (basisSign == 0 && context.Exponent.Sign < 0)
            {
                continue;
            }

            bool critical = PolynomialSign(chart, differential.First.Numerator, pointCell) == 0;
            bool boundary = IsDomainBoundary(chart, point);
            if (!critical && !boundary)
            {
                continue;
            }

            if (!TryRationalCoordinate(chart, point, budget, out BigRational x))
            {
                boundaries = [];
                return false;
            }

            BigRational basis = context.Basis.Evaluate(x, budget);
            budget.CheckCoefficient(basis);
            if (!FixedRationalPowerValue.TryComposeRational(basis, context.Exponent, budget, out BigRational composed))
            {
                boundaries = [];
                return false;
            }

            result.Add(composed);
        }

        if (GapIncluded(chart, 0) && !TryAddTailRangeBoundary(context, result, budget) || GapIncluded(chart, chart.RootIsolation.Roots.Length) && !TryAddTailRangeBoundary(context, result, budget))
        {
            boundaries = [];
            return false;
        }

        boundaries = result.ToImmutableArray();
        return true;
    }

    private static bool TryAddTailRangeBoundary(FixedRationalPowerContext context, System.Collections.Generic.SortedSet<Graphing.Symbolics.BigRational> result, ResourceBudget budget)
    {
        int degreeDifference = context.Basis.Numerator.Degree - context.Basis.Denominator.Degree;
        if (degreeDifference < 0)
        {
            return true;
        }

        if (degreeDifference > 0)
        {
            return true;
        }

        BigRational limit = context.Basis.Numerator.LeadingCoefficient / context.Basis.Denominator.LeadingCoefficient;
        budget.CheckCoefficient(limit);
        if (!FixedRationalPowerValue.TryComposeRational(limit, context.Exponent, budget, out BigRational composed))
        {
            return false;
        }

        result.Add(composed);
        return true;
    }

    private static ImmutableArray<RationalPowerRangeFiberWitness> BuildRangeFibers(FixedRationalPowerContext context, ImmutableArray<BigRational> boundaries, ResourceBudget budget)
    {
        var result = ImmutableArray.CreateBuilder<RationalPowerRangeFiberWitness>(checked((boundaries.Length * 2) + 1));
        for (int gap = 0; gap <= boundaries.Length; gap++)
        {
            budget.Charge();
            result.Add(CreateRangeFiber(context, RationalRangeCellKind.OpenInterval, gap, RangeGapSample(boundaries, gap), budget));
            if (gap < boundaries.Length)
            {
                result.Add(CreateRangeFiber(context, RationalRangeCellKind.Boundary, gap, boundaries[gap], budget));
            }
        }

        return result.MoveToImmutable();
    }

    private static RationalPowerRangeFiberWitness CreateRangeFiber(FixedRationalPowerContext context, RationalRangeCellKind kind, int index, BigRational sample, ResourceBudget budget)
    {
        PolynomialFormula formula = RangeFiberFormula(context, sample, budget);
        CellDecompositionCertificate fiber = CellDecomposer.Decompose(formula, budget);
        return new RationalPowerRangeFiberWitness(kind, index, sample, fiber, !fiber.Result.IsEmpty);
    }

    private static bool VerifyRangeFiber(FixedRationalPowerContext context, RationalPowerRangeFiberWitness witness, RationalRangeCellKind kind, int index, BigRational sample, ResourceBudget budget)
    {
        if (witness.Kind != kind || witness.Index != index || witness.Sample != sample)
        {
            return false;
        }

        PolynomialFormula expected = RangeFiberFormula(context, sample, budget);
        return string.Equals(expected.Canonical, witness.Fiber.Formula.Canonical, StringComparison.Ordinal) && CellDecomposer.Verify(witness.Fiber, budget) && witness.HasPreimage == !witness.Fiber.Result.IsEmpty;
    }

    private static Graphing.Symbolics.PolynomialJunction RangeFiberFormula(FixedRationalPowerContext context, BigRational y, ResourceBudget budget)
    {
        if (y.Sign < 0 || y.IsZero && context.Exponent.Sign < 0)
        {
            return new PolynomialJunction(true, [context.DomainFormula, new PolynomialBoolean(false)]);
        }

        int numerator = checked((int)ExactInteger.Abs(context.Exponent.Numerator));
        int denominator = checked((int)context.Exponent.Denominator);
        BigRational yPower = y.Pow(denominator);
        budget.CheckCoefficient(yPower);
        UnivariatePolynomial equation = context.Exponent.Sign > 0 ? context.Basis.Numerator.Pow(numerator, budget).Subtract(context.Basis.Denominator.Pow(numerator, budget).Multiply(yPower, budget), budget) : context.Basis.Numerator.Pow(numerator, budget).Multiply(yPower, budget).Subtract(context.Basis.Denominator.Pow(numerator, budget), budget);
        PolynomialFormula equality = equation.IsZero ? new PolynomialBoolean(true) : new PolynomialAtom(equation.PrimitivePositive(budget), Comparison.Equal);
        return new PolynomialJunction(true, [context.DomainFormula, equality]);
    }

    private static RealSet BuildRange(ImmutableArray<BigRational> boundaries, ImmutableArray<RationalPowerRangeFiberWitness> fibers)
    {
        if (fibers.All(static fiber => fiber.HasPreimage))
        {
            return AllRealSet.Instance;
        }

        if (fibers.All(static fiber => !fiber.HasPreimage))
        {
            return EmptySet.Instance;
        }

        var components = new List<RealSet>();
        int position = 0;
        while (position < fibers.Length)
        {
            if (!fibers[position].HasPreimage)
            {
                position++;
                continue;
            }

            int start = position;
            while (position + 1 < fibers.Length && fibers[position + 1].HasPreimage)
            {
                position++;
            }

            int end = position;
            RationalPowerRangeFiberWitness first = fibers[start];
            RationalPowerRangeFiberWitness last = fibers[end];
            if (start == end && first.Kind == RationalRangeCellKind.Boundary)
            {
                components.Add(RealSets.Points([new RationalReal(first.Sample)]));
                position++;
                continue;
            }

            RealBound lower = first.Kind == RationalRangeCellKind.OpenInterval && first.Index == 0 ? RealBound.NegativeInfinity : RealBound.Finite(new RationalReal(first.Kind == RationalRangeCellKind.Boundary ? first.Sample : boundaries[first.Index - 1]));
            RealBound upper = last.Kind == RationalRangeCellKind.OpenInterval && last.Index == boundaries.Length ? RealBound.PositiveInfinity : RealBound.Finite(new RationalReal(last.Kind == RationalRangeCellKind.Boundary ? last.Sample : boundaries[last.Index]));
            components.Add(new IntervalSet(lower, first.Kind == RationalRangeCellKind.Boundary, upper, last.Kind == RationalRangeCellKind.Boundary));
            position++;
        }

        return components.All(static component => component is PointSet) ? RealSets.Points(components.Cast<PointSet>().SelectMany(static points => points.Points)) : RealSets.Union(components);
    }

    private static Graphing.Symbolics.PolynomialJunction BuildSignChartFormula(FixedRationalPowerContext context, ResourceBudget budget)
    {
        ImmutableArray<UnivariatePolynomial> domainAtoms = PolynomialFormulaConverter.Atoms(context.DomainFormula);
        var known = domainAtoms.Select(static polynomial => polynomial.Canonical).ToHashSet(StringComparer.Ordinal);
        var operands = ImmutableArray.CreateBuilder<PolynomialFormula>();
        operands.Add(context.DomainFormula);
        foreach (UnivariatePolynomial polynomial in RequiredPolynomials(context, budget))
        {
            budget.Charge();
            if (polynomial.IsConstant || !known.Add(polynomial.Canonical))
            {
                continue;
            }

            operands.Add(new PolynomialJunction(false, [new PolynomialAtom(polynomial, Comparison.Equal), new PolynomialAtom(polynomial, Comparison.NotEqual)]));
        }

        return new PolynomialJunction(true, operands.ToImmutable());
    }

    private static ImmutableArray<UnivariatePolynomial> RequiredPolynomials(FixedRationalPowerContext context, ResourceBudget budget)
    {
        FixedRationalPowerProofKernelDifferentialData differential = Differential(context, budget);
        return PolynomialFormulaConverter.Atoms(context.DomainFormula).Concat([context.Basis.Numerator, context.Basis.Denominator, differential.First.Numerator, differential.First.Denominator, differential.Curvature.Numerator, differential.Curvature.Denominator]).Where(static polynomial => !polynomial.IsZero).DistinctBy(static polynomial => polynomial.Canonical).OrderBy(static polynomial => polynomial.Canonical, StringComparer.Ordinal).ToImmutableArray();
    }

    private static FixedRationalPowerProofKernelDifferentialData Differential(FixedRationalPowerContext context, ResourceBudget budget)
    {
        RationalFunction first = context.Basis.Derivative(budget);
        RationalFunction second = first.Derivative(budget);
        RationalFunction curvature = context.Basis.Multiply(second, budget).Multiply(RationalFunction.Constant(new BigRational(context.Exponent.Denominator), budget), budget).Add(first.Multiply(first, budget).Multiply(RationalFunction.Constant(new BigRational(context.Exponent.Numerator - context.Exponent.Denominator), budget), budget), budget);
        return new FixedRationalPowerProofKernelDifferentialData(first, curvature);
    }

    private static int FirstDerivativeSign(FixedRationalPowerContext context, CellDecompositionCertificate chart, FixedRationalPowerProofKernelDifferentialData differential, CellWitness cell) => context.Exponent.Sign * RationalSign(chart, differential.First, cell);
    private static int CurvatureSign(FixedRationalPowerContext context, CellDecompositionCertificate chart, FixedRationalPowerProofKernelDifferentialData differential, CellWitness cell) => context.Exponent.Sign * RationalSign(chart, differential.Curvature, cell);
    private static int BasisSign(FixedRationalPowerContext context, CellDecompositionCertificate chart, CellWitness cell) => RationalSign(chart, context.Basis, cell);
    private static int RationalSign(CellDecompositionCertificate chart, RationalFunction function, CellWitness cell) => PolynomialSign(chart, function.Numerator, cell) * PolynomialSign(chart, function.Denominator, cell);
    private static int PolynomialSign(CellDecompositionCertificate chart, UnivariatePolynomial polynomial, CellWitness cell)
    {
        if (polynomial.IsZero)
        {
            return 0;
        }

        if (polynomial.IsConstant)
        {
            return polynomial.ConstantCoefficient.Sign;
        }

        string canonical = polynomial.Canonical;
        for (int index = 0; index < chart.Atoms.Length; index++)
        {
            if (string.Equals(chart.Atoms[index].Canonical, canonical, StringComparison.Ordinal))
            {
                return cell.AtomSigns[index];
            }
        }

        throw new InvalidOperationException("A required rational-power sign polynomial is absent.");
    }

    private static ExactReal ComposeAt(FixedRationalPowerContext context, CellDecompositionCertificate chart, int point, ExactReal x, ResourceBudget budget)
    {
        CellWitness pointCell = PointCell(chart, point);
        if (BasisSign(context, chart, pointCell) == 0)
        {
            return new RationalReal(BigRational.Zero);
        }

        ExactReal basis = x switch
        {
            RationalReal rational => RationalBasisAt(context, rational.Value, budget),
            AlgebraicReal algebraic => new AlgebraicImageReal(context.Basis, algebraic),
            _ => throw new ArgumentOutOfRangeException(nameof(x))
        };
        return FixedRationalPowerValue.Compose(basis, context.Exponent, budget);
    }

    private static RationalReal RationalBasisAt(FixedRationalPowerContext context, BigRational x, ResourceBudget budget)
    {
        BigRational value = context.Basis.Evaluate(x, budget);
        budget.CheckCoefficient(value);
        return new RationalReal(value);
    }

    private static bool TryTailLimit(FixedRationalPowerContext context, ResourceBudget budget, out ExactReal coordinate)
    {
        int degreeDifference = context.Basis.Numerator.Degree - context.Basis.Denominator.Degree;
        if (degreeDifference < 0)
        {
            if (context.Exponent.Sign > 0)
            {
                coordinate = new RationalReal(BigRational.Zero);
                return true;
            }

            coordinate = null!;
            return false;
        }

        if (degreeDifference > 0)
        {
            if (context.Exponent.Sign < 0)
            {
                coordinate = new RationalReal(BigRational.Zero);
                return true;
            }

            coordinate = null!;
            return false;
        }

        BigRational limit = context.Basis.Numerator.LeadingCoefficient / context.Basis.Denominator.LeadingCoefficient;
        budget.CheckCoefficient(limit);
        coordinate = FixedRationalPowerValue.Compose(new RationalReal(limit), context.Exponent, budget);
        return true;
    }

    private static BigRational AsymptoticNextCoefficient(RationalFunction function)
    {
        int numeratorDegree = function.Numerator.Degree;
        int denominatorDegree = function.Denominator.Degree;
        BigRational numeratorLeading = function.Numerator[numeratorDegree];
        BigRational denominatorLeading = function.Denominator[denominatorDegree];
        BigRational numeratorNext = function.Numerator[numeratorDegree - 1];
        BigRational denominatorNext = function.Denominator[denominatorDegree - 1];
        return ((numeratorNext * denominatorLeading) - (numeratorLeading * denominatorNext)) / (denominatorLeading * denominatorLeading);
    }

    private static Asymptote Oblique(ExactReal slope, ExactReal intercept) => new(AsymptoteOrientation.Oblique, new SingletonReal(intercept), slope, intercept);
    private static bool TryRationalCoordinate(CellDecompositionCertificate chart, int point, ResourceBudget budget, out BigRational value)
    {
        ExactReal root = chart.RootIsolation.Roots[point];
        if (root is RationalReal rational)
        {
            value = rational.Value;
            return true;
        }

        CellWitness pointCell = PointCell(chart, point);
        foreach (UnivariatePolynomial polynomial in chart.Atoms.Where(static polynomial => polynomial.Degree == 1))
        {
            budget.Charge();
            if (PolynomialSign(chart, polynomial, pointCell) != 0)
            {
                continue;
            }

            value = -polynomial[0] / polynomial[1];
            budget.CheckCoefficient(value);
            return polynomial.Evaluate(value, budget).IsZero;
        }

        value = default;
        return false;
    }

    private static bool EvaluateFormulaAt(PolynomialFormula formula, BigRational value, ResourceBudget budget)
    {
        var signs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnivariatePolynomial polynomial in PolynomialFormulaConverter.Atoms(formula))
        {
            budget.Charge();
            signs.Add(polynomial.Canonical, polynomial.Evaluate(value, budget).Sign);
        }

        return PolynomialFormulaConverter.Evaluate(formula, signs);
    }

    private static PolynomialFormula ReflectFormula(PolynomialFormula formula, ResourceBudget budget) => formula switch
    {
        PolynomialBoolean boolean => boolean,
        PolynomialAtom atom => ReflectAtom(atom, budget),
        PolynomialNot not => new PolynomialNot(ReflectFormula(not.Operand, budget)),
        PolynomialJunction junction => new PolynomialJunction(junction.IsConjunction, junction.Operands.Select(operand => ReflectFormula(operand, budget)).ToImmutableArray()),
        _ => throw new ArgumentOutOfRangeException(nameof(formula))
    };
    private static PolynomialAtom ReflectAtom(PolynomialAtom atom, ResourceBudget budget)
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
    private static RealSet BuildSubset(CellDecompositionCertificate chart, Func<CellWitness, bool> include)
    {
        bool[] included = chart.Cells.Select(include).ToArray();
        if (included.All(static value => value))
        {
            return AllRealSet.Instance;
        }

        if (included.All(static value => !value))
        {
            return EmptySet.Instance;
        }

        ImmutableArray<ExactReal> roots = chart.RootIsolation.Roots;
        var components = new List<RealSet>();
        int position = 0;
        while (position < included.Length)
        {
            if (!included[position])
            {
                position++;
                continue;
            }

            int start = position;
            while (position + 1 < included.Length && included[position + 1])
            {
                position++;
            }

            int end = position;
            if (start == end && (start & 1) != 0)
            {
                components.Add(RealSets.Points([roots[start / 2]]));
                position++;
                continue;
            }

            RealBound lower = start == 0 ? RealBound.NegativeInfinity : RealBound.Finite(roots[(start - 1) / 2]);
            RealBound upper = end == included.Length - 1 ? RealBound.PositiveInfinity : RealBound.Finite(roots[end / 2]);
            components.Add(new IntervalSet(lower, (start & 1) != 0, upper, (end & 1) != 0));
            position++;
        }

        return components.All(static component => component is PointSet) ? RealSets.Points(components.Cast<PointSet>().SelectMany(static points => points.Points)) : RealSets.Union(components);
    }

    private static bool GapIncluded(CellDecompositionCertificate chart, int gap) => GapCell(chart, gap).Included;
    private static bool PointIncluded(CellDecompositionCertificate chart, int point) => PointCell(chart, point).Included;
    private static CellWitness GapCell(CellDecompositionCertificate chart, int gap) => chart.Cells[gap * 2];
    private static CellWitness PointCell(CellDecompositionCertificate chart, int point) => chart.Cells[(point * 2) + 1];
    private static bool TouchesDomain(CellDecompositionCertificate chart, int point) => PointIncluded(chart, point) || GapIncluded(chart, point) || GapIncluded(chart, point + 1);
    private static bool IsDomainBoundary(CellDecompositionCertificate chart, int point)
    {
        bool left = GapIncluded(chart, point);
        bool at = PointIncluded(chart, point);
        bool right = GapIncluded(chart, point + 1);
        return left != at || at != right;
    }

    private static FixedRationalPowerProofKernelExtremumKind ClassifyExtremum(bool leftDomain, bool rightDomain, int leftSign, int rightSign)
    {
        if (leftDomain && rightDomain)
        {
            if (leftSign < 0 && rightSign > 0)
            {
                return FixedRationalPowerProofKernelExtremumKind.Minimum;
            }

            if (leftSign > 0 && rightSign < 0)
            {
                return FixedRationalPowerProofKernelExtremumKind.Maximum;
            }

            return FixedRationalPowerProofKernelExtremumKind.None;
        }

        if (rightDomain)
        {
            return rightSign switch
            {
                > 0 => FixedRationalPowerProofKernelExtremumKind.Minimum,
                < 0 => FixedRationalPowerProofKernelExtremumKind.Maximum,
                _ => FixedRationalPowerProofKernelExtremumKind.None
            };
        }

        return leftSign switch
        {
            < 0 => FixedRationalPowerProofKernelExtremumKind.Minimum,
            > 0 => FixedRationalPowerProofKernelExtremumKind.Maximum,
            _ => FixedRationalPowerProofKernelExtremumKind.None
        };
    }

    private static BigRational RangeGapSample(ImmutableArray<BigRational> boundaries, int gap)
    {
        if (boundaries.IsEmpty)
        {
            return BigRational.Zero;
        }

        if (gap == 0)
        {
            return boundaries[0] - BigRational.One;
        }

        if (gap == boundaries.Length)
        {
            return boundaries[^1] + BigRational.One;
        }

        return (boundaries[gap - 1] + boundaries[gap]) / 2;
    }
}
