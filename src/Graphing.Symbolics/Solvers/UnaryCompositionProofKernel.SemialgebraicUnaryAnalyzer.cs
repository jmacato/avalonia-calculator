using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class UnaryCompositionProofKernel
{
    public static UnarySignChartCertificate BuildChart(SemialgebraicUnaryContext context, ResourceBudget budget)
    {
        ImmutableArray<UnivariatePolynomial> polynomials = RequiredPolynomials(context, budget);
        UnivariatePolynomial combined = Combined(polynomials, budget);
        RootIsolationCertificate roots = SturmRootIsolator.Isolate(combined, budget);
        BuildSigns(context.DomainFormula, polynomials, roots.Roots, budget, out ImmutableArray<BigRational> gapSamples, out ImmutableArray<ImmutableArray<int>> gapSigns, out ImmutableArray<ImmutableArray<int>> pointSigns, out ImmutableArray<bool> gapDomain, out ImmutableArray<bool> pointDomain);
        return new UnarySignChartCertificate(polynomials, combined, roots, gapSamples, gapSigns, pointSigns, gapDomain, pointDomain);
    }

    public static bool VerifyChart(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ResourceBudget budget)
    {
        ImmutableArray<UnivariatePolynomial> expectedPolynomials = RequiredPolynomials(context, budget);
        if (!SamePolynomials(expectedPolynomials, chart.Polynomials))
        {
            return false;
        }

        UnivariatePolynomial expectedCombined = Combined(expectedPolynomials, budget);
        if (!expectedCombined.Equals(chart.CombinedPolynomial) || !chart.RootIsolation.Polynomial.Equals(expectedCombined) || !SturmRootIsolator.Verify(chart.RootIsolation, budget))
        {
            return false;
        }

        BuildSigns(context.DomainFormula, expectedPolynomials, chart.RootIsolation.Roots, budget, out ImmutableArray<BigRational> gapSamples, out ImmutableArray<ImmutableArray<int>> gapSigns, out ImmutableArray<ImmutableArray<int>> pointSigns, out ImmutableArray<bool> gapDomain, out ImmutableArray<bool> pointDomain);
        return gapSamples.SequenceEqual(chart.GapSamples) && NestedEqual(gapSigns, chart.GapSigns) && NestedEqual(pointSigns, chart.PointSigns) && gapDomain.SequenceEqual(chart.GapDomain) && pointDomain.SequenceEqual(chart.PointDomain);
    }

    public static ImmutableArray<CellDecompositionCertificate> BuildParityCells(SemialgebraicUnaryContext context, ResourceBudget budget)
    {
        PolynomialFormula reflectedDomain = ReflectFormula(context.DomainFormula, budget);
        PolynomialFormula asymmetric = new PolynomialJunction(false, [new PolynomialJunction(true, [context.DomainFormula, new PolynomialNot(reflectedDomain)]), new PolynomialJunction(true, [new PolynomialNot(context.DomainFormula), reflectedDomain])]);
        RationalFunction reflected = new(context.Inner.Numerator.SubstituteNegativeVariable(budget), context.Inner.Denominator.SubstituteNegativeVariable(budget));
        RationalFunction difference = context.Kind == SemialgebraicUnaryKind.AbsoluteValue ? context.Inner.Multiply(context.Inner, budget).Subtract(reflected.Multiply(reflected, budget), budget) : context.Inner.Subtract(reflected, budget);
        PolynomialFormula unequal = difference.Numerator.IsZero ? new PolynomialBoolean(false) : new PolynomialAtom(difference.Numerator.PrimitivePositive(budget), Comparison.NotEqual);
        PolynomialFormula equalityViolation = new PolynomialJunction(true, [context.DomainFormula, unequal]);
        return [CellDecomposer.Decompose(asymmetric, budget), CellDecomposer.Decompose(equalityViolation, budget)];
    }

    public static bool TryComputeFeature(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, AnalysisFeatures feature, ImmutableArray<CellDecompositionCertificate> auxiliaries, ResourceBudget budget, out object value)
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
            case AnalysisFeatures.Monotonicity:
                value = ComputeMonotonicity(context, chart, budget);
                return true;
            case AnalysisFeatures.VerticalAsymptotes:
                value = ComputeVerticalAsymptotes(context, chart);
                return true;
            case AnalysisFeatures.HorizontalAsymptotes when TryComputeHorizontalAsymptotes(context, chart, out ImmutableArray<Asymptote> horizontal):
                value = horizontal;
                return true;
            case AnalysisFeatures.ObliqueAsymptotes when TryComputeObliqueAsymptotes(context, chart, budget, out ImmutableArray<Asymptote> oblique):
                value = oblique;
                return true;
            case AnalysisFeatures.Period:
                value = ComputePeriod(context, chart, budget);
                return true;
            default:
                value = null!;
                return false;
        }
    }

    public static bool TryBuildRangeProof(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ResourceBudget budget, out RealSet range, out ImmutableArray<BigRational> boundaries, out ImmutableArray<UnaryRangeFiberWitness> fibers)
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

    public static bool VerifyRangeProof(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ImmutableArray<BigRational> boundaries, ImmutableArray<UnaryRangeFiberWitness> fibers, ResourceBudget budget, out RealSet range)
    {
        if (!TryDeriveRangeBoundaries(context, chart, budget, out ImmutableArray<BigRational> expectedBoundaries) || !expectedBoundaries.SequenceEqual(boundaries) || fibers.Length != checked((boundaries.Length * 2) + 1))
        {
            range = null!;
            return false;
        }

        int witness = 0;
        for (int gap = 0; gap <= boundaries.Length; gap++)
        {
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

    private static FunctionParity ComputeParity(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ImmutableArray<CellDecompositionCertificate> auxiliaries)
    {
        if (!auxiliaries[0].Result.IsEmpty || !auxiliaries[1].Result.IsEmpty)
        {
            return FunctionParity.Neither;
        }

        bool identicallyZero = chart.GapDomain.Select((included, gap) => !included || InnerSignInGap(context, chart, gap) == 0).All(static cell => cell) && chart.PointDomain.Select((included, point) => !included || InnerSignAtPoint(context, chart, point) == 0).All(static cell => cell);
        return identicallyZero ? FunctionParity.Both : FunctionParity.Even;
    }

    private static RealSet ComputeZeros(SemialgebraicUnaryContext context, UnarySignChartCertificate chart)
    {
        bool[] gaps = new bool[chart.GapDomain.Length];
        bool[] points = new bool[chart.PointDomain.Length];
        for (int gap = 0; gap < gaps.Length; gap++)
        {
            gaps[gap] = chart.GapDomain[gap] && InnerSignInGap(context, chart, gap) == 0;
        }

        for (int point = 0; point < points.Length; point++)
        {
            points[point] = chart.PointDomain[point] && InnerSignAtPoint(context, chart, point) == 0;
        }

        return BuildSet(chart.RootIsolation.Roots, gaps, points);
    }

    private static OptionalValue<ExactReal> ComputeYIntercept(SemialgebraicUnaryContext context, ResourceBudget budget)
    {
        if (!EvaluateFormulaAt(context.DomainFormula, BigRational.Zero, budget))
        {
            return OptionalValue<ExactReal>.None;
        }

        BigRational inner = context.Inner.Evaluate(BigRational.Zero, budget);
        return OptionalValue<ExactReal>.Some(ComposeRational(context.Kind, inner));
    }

    private static void ComputeExtrema(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ResourceBudget budget, out ImmutableArray<FeaturePoint> minima, out ImmutableArray<FeaturePoint> maxima)
    {
        var minimumBuilder = ImmutableArray.CreateBuilder<FeaturePoint>();
        var maximumBuilder = ImmutableArray.CreateBuilder<FeaturePoint>();
        UnaryCompositionProofKernelDifferentialData differential = Differential(context, budget);
        for (int point = 0; point < chart.RootIsolation.Roots.Length; point++)
        {
            if (!chart.PointDomain[point])
            {
                continue;
            }

            bool leftDomain = chart.GapDomain[point];
            bool rightDomain = chart.GapDomain[point + 1];
            if (!leftDomain && !rightDomain)
            {
                continue;
            }

            int left = leftDomain ? FirstSignInGap(context, chart, differential, point) : 0;
            int right = rightDomain ? FirstSignInGap(context, chart, differential, point + 1) : 0;
            UnaryCompositionProofKernelExtremumKind kind = ClassifyExtremum(leftDomain, rightDomain, left, right);
            if (kind == UnaryCompositionProofKernelExtremumKind.None)
            {
                continue;
            }

            ExactReal x = chart.RootIsolation.Roots[point];
            var featurePoint = new ConstantYFeaturePoint(new SingletonReal(x), ComposeAt(context, chart, point, x, budget));
            if (kind == UnaryCompositionProofKernelExtremumKind.Minimum)
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

    private static ImmutableArray<FeaturePoint> ComputeInflections(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ResourceBudget budget)
    {
        var result = ImmutableArray.CreateBuilder<FeaturePoint>();
        UnaryCompositionProofKernelDifferentialData differential = Differential(context, budget);
        for (int point = 0; point < chart.RootIsolation.Roots.Length; point++)
        {
            if (!chart.PointDomain[point] || !chart.GapDomain[point] || !chart.GapDomain[point + 1])
            {
                continue;
            }

            int left = CurvatureSignInGap(context, chart, differential, point);
            int right = CurvatureSignInGap(context, chart, differential, point + 1);
            if (left == 0 || right == 0 || left == right)
            {
                continue;
            }

            ExactReal x = chart.RootIsolation.Roots[point];
            result.Add(new ConstantYFeaturePoint(new SingletonReal(x), ComposeAt(context, chart, point, x, budget)));
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<MonotoneRegion> ComputeMonotonicity(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ResourceBudget budget)
    {
        var result = ImmutableArray.CreateBuilder<MonotoneRegion>();
        UnaryCompositionProofKernelDifferentialData differential = Differential(context, budget);
        int gap = 0;
        while (gap < chart.GapDomain.Length)
        {
            if (!chart.GapDomain[gap])
            {
                gap++;
                continue;
            }

            int sign = FirstSignInGap(context, chart, differential, gap);
            int start = gap;
            int end = gap;
            while (end + 1 < chart.GapDomain.Length && chart.PointDomain[end] && chart.GapDomain[end + 1] && FirstSignInGap(context, chart, differential, end + 1) == sign)
            {
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

    private static Periodicity ComputePeriod(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ResourceBudget budget)
    {
        if (context.DomainCells.Result.IsEmpty)
        {
            return new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null);
        }

        UnaryCompositionProofKernelDifferentialData differential = Differential(context, budget);
        bool constant = chart.GapDomain.Select((included, gap) => !included || FirstSignInGap(context, chart, differential, gap) == 0).All(static value => value);
        return context.DomainCells.Result is AllRealSet && constant ? new Periodicity(PeriodicityKind.PeriodicWithoutFundamentalPeriod, null) : new Periodicity(PeriodicityKind.NotPeriodic, null);
    }

    private static ImmutableArray<Asymptote> ComputeVerticalAsymptotes(SemialgebraicUnaryContext context, UnarySignChartCertificate chart)
    {
        var result = ImmutableArray.CreateBuilder<Asymptote>();
        for (int point = 0; point < chart.PointDomain.Length; point++)
        {
            if (PolynomialSignAtPoint(chart, context.Inner.Denominator, point) != 0 || PolynomialSignAtPoint(chart, context.Inner.Numerator, point) == 0 || (!chart.GapDomain[point] && !chart.GapDomain[point + 1]))
            {
                continue;
            }

            ExactReal coordinate = chart.RootIsolation.Roots[point];
            result.Add(new Asymptote(AsymptoteOrientation.Vertical, new SingletonReal(coordinate), null, null));
        }

        return result.ToImmutable();
    }

    private static bool TryComputeHorizontalAsymptotes(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, out ImmutableArray<Asymptote> asymptotes)
    {
        if (!chart.GapDomain[0] && !chart.GapDomain[^1])
        {
            asymptotes = [];
            return true;
        }

        if (context.Inner.Numerator.Degree > context.Inner.Denominator.Degree)
        {
            asymptotes = [];
            return true;
        }

        BigRational limit = context.Inner.Numerator.Degree == context.Inner.Denominator.Degree ? context.Inner.Numerator.LeadingCoefficient / context.Inner.Denominator.LeadingCoefficient : BigRational.Zero;
        if (context.Kind == SemialgebraicUnaryKind.PrincipalSquareRoot && limit.Sign < 0)
        {
            asymptotes = [];
            return false;
        }

        ExactReal coordinate = ComposeRational(context.Kind, limit);
        asymptotes = [new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(coordinate), null, coordinate)];
        return true;
    }

    private static bool TryComputeObliqueAsymptotes(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ResourceBudget budget, out ImmutableArray<Asymptote> asymptotes)
    {
        bool leftTail = chart.GapDomain[0];
        bool rightTail = chart.GapDomain[^1];
        if (!leftTail && !rightTail)
        {
            asymptotes = [];
            return true;
        }

        int degreeDifference = context.Inner.Numerator.Degree - context.Inner.Denominator.Degree;
        if (context.Kind == SemialgebraicUnaryKind.AbsoluteValue)
        {
            if (degreeDifference != 1)
            {
                asymptotes = [];
                return true;
            }

            (UnivariatePolynomial quotient, _) = context.Inner.Numerator.Divide(context.Inner.Denominator, budget);
            BigRational slope = quotient[1];
            BigRational intercept = quotient[0];
            BigRational orientation = slope.Sign > 0 ? BigRational.One : BigRational.MinusOne;
            var absolute = ImmutableArray.CreateBuilder<Asymptote>(2);
            if (leftTail)
            {
                absolute.Add(Oblique(new RationalReal(-slope.Abs()), new RationalReal(-orientation * intercept)));
            }

            if (rightTail)
            {
                absolute.Add(Oblique(new RationalReal(slope.Abs()), new RationalReal(orientation * intercept)));
            }

            asymptotes = absolute.ToImmutable();
            return true;
        }

        if (degreeDifference != 2)
        {
            asymptotes = [];
            return true;
        }

        (UnivariatePolynomial quadratic, _) = context.Inner.Numerator.Divide(context.Inner.Denominator, budget);
        BigRational leading = quadratic[2];
        if (leading.Sign <= 0)
        {
            asymptotes = [];
            return false;
        }

        ExactReal root = ComposeRational(SemialgebraicUnaryKind.PrincipalSquareRoot, leading);
        ExactReal interceptRoot = ExactRealArithmetic.Divide(new RationalReal(quadratic[1]), ExactRealArithmetic.Scale(root, new BigRational(2)));
        var squareRoot = ImmutableArray.CreateBuilder<Asymptote>(2);
        if (leftTail)
        {
            squareRoot.Add(Oblique(ExactRealArithmetic.Negate(root), ExactRealArithmetic.Negate(interceptRoot)));
        }

        if (rightTail)
        {
            squareRoot.Add(Oblique(root, interceptRoot));
        }

        asymptotes = squareRoot.ToImmutable();
        return true;
    }

    private static Asymptote Oblique(ExactReal slope, ExactReal intercept) => new(AsymptoteOrientation.Oblique, new SingletonReal(intercept), slope, intercept);
    private static bool TryDeriveRangeBoundaries(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, ResourceBudget budget, out ImmutableArray<BigRational> boundaries)
    {
        if (context.DomainCells.Result.IsEmpty)
        {
            boundaries = [];
            return true;
        }

        var result = new SortedSet<BigRational>
        {
            BigRational.Zero
        };
        if (context.Inner.Numerator.Degree <= 0 && context.Inner.Denominator.Degree <= 0)
        {
            BigRational constant = context.Inner.Numerator.ConstantCoefficient / context.Inner.Denominator.ConstantCoefficient;
            if (!TryComposeRationalBoundary(context.Kind, constant, out BigRational composed))
            {
                boundaries = [];
                return false;
            }

            result.Add(composed);
        }

        for (int point = 0; point < chart.RootIsolation.Roots.Length; point++)
        {
            if (!TouchesDomain(chart, point) || PolynomialSignAtPoint(chart, context.Inner.Denominator, point) == 0)
            {
                continue;
            }

            if (!TryRationalCoordinate(context, chart, point, budget, out BigRational x))
            {
                boundaries = [];
                return false;
            }

            BigRational inner = context.Inner.Evaluate(x, budget);
            if (!TryComposeRationalBoundary(context.Kind, inner, out BigRational composed))
            {
                boundaries = [];
                return false;
            }

            result.Add(composed);
        }

        bool unbounded = chart.GapDomain[0] || chart.GapDomain[^1];
        if (unbounded && context.Inner.Numerator.Degree <= context.Inner.Denominator.Degree)
        {
            BigRational limit = context.Inner.Numerator.Degree == context.Inner.Denominator.Degree ? context.Inner.Numerator.LeadingCoefficient / context.Inner.Denominator.LeadingCoefficient : BigRational.Zero;
            if (!TryComposeRationalBoundary(context.Kind, limit, out BigRational composed))
            {
                boundaries = [];
                return false;
            }

            result.Add(composed);
        }

        boundaries = result.ToImmutableArray();
        return true;
    }

    private static ImmutableArray<UnaryRangeFiberWitness> BuildRangeFibers(SemialgebraicUnaryContext context, ImmutableArray<BigRational> boundaries, ResourceBudget budget)
    {
        var result = ImmutableArray.CreateBuilder<UnaryRangeFiberWitness>(checked((boundaries.Length * 2) + 1));
        for (int gap = 0; gap <= boundaries.Length; gap++)
        {
            result.Add(CreateRangeFiber(context, RationalRangeCellKind.OpenInterval, gap, RangeGapSample(boundaries, gap), budget));
            if (gap < boundaries.Length)
            {
                result.Add(CreateRangeFiber(context, RationalRangeCellKind.Boundary, gap, boundaries[gap], budget));
            }
        }

        return result.MoveToImmutable();
    }

    private static UnaryRangeFiberWitness CreateRangeFiber(SemialgebraicUnaryContext context, RationalRangeCellKind kind, int index, BigRational sample, ResourceBudget budget)
    {
        PolynomialFormula formula = RangeFiberFormula(context, sample, budget);
        CellDecompositionCertificate fiber = CellDecomposer.Decompose(formula, budget);
        return new UnaryRangeFiberWitness(kind, index, sample, fiber, !fiber.Result.IsEmpty);
    }

    private static bool VerifyRangeFiber(SemialgebraicUnaryContext context, UnaryRangeFiberWitness witness, RationalRangeCellKind kind, int index, BigRational sample, ResourceBudget budget)
    {
        if (witness.Kind != kind || witness.Index != index || witness.Sample != sample)
        {
            return false;
        }

        PolynomialFormula expected = RangeFiberFormula(context, sample, budget);
        return string.Equals(expected.Canonical, witness.Fiber.Formula.Canonical, StringComparison.Ordinal) && CellDecomposer.Verify(witness.Fiber, budget) && witness.HasPreimage == !witness.Fiber.Result.IsEmpty;
    }

    private static Graphing.Symbolics.PolynomialJunction RangeFiberFormula(SemialgebraicUnaryContext context, BigRational y, ResourceBudget budget)
    {
        if (y.Sign < 0)
        {
            return new PolynomialJunction(true, [context.DomainFormula, new PolynomialBoolean(false)]);
        }

        UnivariatePolynomial positive = context.Inner.Numerator.Subtract(context.Inner.Denominator.Multiply(context.Kind == SemialgebraicUnaryKind.PrincipalSquareRoot ? y * y : y, budget), budget);
        PolynomialFormula equation = Equality(positive, budget);
        if (context.Kind == SemialgebraicUnaryKind.AbsoluteValue)
        {
            UnivariatePolynomial negative = context.Inner.Numerator.Add(context.Inner.Denominator.Multiply(y, budget), budget);
            equation = new PolynomialJunction(false, [equation, Equality(negative, budget)]);
        }

        return new PolynomialJunction(true, [context.DomainFormula, equation]);
    }

    private static PolynomialFormula Equality(UnivariatePolynomial polynomial, ResourceBudget budget) => polynomial.IsZero ? new PolynomialBoolean(true) : new PolynomialAtom(polynomial.PrimitivePositive(budget), Comparison.Equal);
    private static RealSet BuildRange(ImmutableArray<BigRational> boundaries, ImmutableArray<UnaryRangeFiberWitness> fibers)
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
            UnaryRangeFiberWitness first = fibers[start];
            UnaryRangeFiberWitness last = fibers[end];
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

    private static ImmutableArray<UnivariatePolynomial> RequiredPolynomials(SemialgebraicUnaryContext context, ResourceBudget budget)
    {
        UnaryCompositionProofKernelDifferentialData differential = Differential(context, budget);
        return PolynomialFormulaConverter.Atoms(context.DomainFormula).Concat([context.Inner.Numerator, context.Inner.Denominator, differential.First.Numerator, differential.First.Denominator, differential.Curvature.Numerator, differential.Curvature.Denominator]).Where(static polynomial => !polynomial.IsZero).DistinctBy(static polynomial => polynomial.Canonical).OrderBy(static polynomial => polynomial.Canonical, StringComparer.Ordinal).ToImmutableArray();
    }

    private static UnaryCompositionProofKernelDifferentialData Differential(SemialgebraicUnaryContext context, ResourceBudget budget)
    {
        RationalFunction first = context.Inner.Derivative(budget);
        RationalFunction second = first.Derivative(budget);
        RationalFunction curvature = context.Kind == SemialgebraicUnaryKind.AbsoluteValue ? second : context.Inner.Multiply(second, budget).Multiply(RationalFunction.Constant(new BigRational(2), budget), budget).Subtract(first.Multiply(first, budget), budget);
        return new UnaryCompositionProofKernelDifferentialData(first, curvature);
    }

    private static int FirstSignInGap(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, UnaryCompositionProofKernelDifferentialData differential, int gap)
    {
        int first = RationalSignInGap(chart, differential.First, gap);
        return context.Kind == SemialgebraicUnaryKind.AbsoluteValue ? InnerSignInGap(context, chart, gap) * first : first;
    }

    private static int CurvatureSignInGap(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, UnaryCompositionProofKernelDifferentialData differential, int gap)
    {
        int curvature = RationalSignInGap(chart, differential.Curvature, gap);
        return context.Kind == SemialgebraicUnaryKind.AbsoluteValue ? InnerSignInGap(context, chart, gap) * curvature : curvature;
    }

    private static int InnerSignInGap(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, int gap) => RationalSignInGap(chart, context.Inner, gap);
    private static int InnerSignAtPoint(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, int point) => RationalSignAtPoint(chart, context.Inner, point);
    private static int RationalSignInGap(UnarySignChartCertificate chart, RationalFunction function, int gap) => PolynomialSignInGap(chart, function.Numerator, gap) * PolynomialSignInGap(chart, function.Denominator, gap);
    private static int RationalSignAtPoint(UnarySignChartCertificate chart, RationalFunction function, int point) => PolynomialSignAtPoint(chart, function.Numerator, point) * PolynomialSignAtPoint(chart, function.Denominator, point);
    private static int PolynomialSignInGap(UnarySignChartCertificate chart, UnivariatePolynomial polynomial, int gap)
    {
        if (polynomial.IsZero)
        {
            return 0;
        }

        int index = IndexOf(chart.Polynomials, polynomial);
        return chart.GapSigns[gap][index];
    }

    private static int PolynomialSignAtPoint(UnarySignChartCertificate chart, UnivariatePolynomial polynomial, int point)
    {
        if (polynomial.IsZero)
        {
            return 0;
        }

        int index = IndexOf(chart.Polynomials, polynomial);
        return chart.PointSigns[point][index];
    }

    private static int IndexOf(ImmutableArray<UnivariatePolynomial> polynomials, UnivariatePolynomial polynomial)
    {
        string canonical = polynomial.Canonical;
        for (int index = 0; index < polynomials.Length; index++)
        {
            if (string.Equals(polynomials[index].Canonical, canonical, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException("A required sign polynomial is absent from the chart.");
    }

    private static ExactReal ComposeAt(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, int point, ExactReal x, ResourceBudget budget)
    {
        ExactReal inner = x switch
        {
            RationalReal rational => new RationalReal(context.Inner.Evaluate(rational.Value, budget)),
            AlgebraicReal algebraic => new AlgebraicImageReal(context.Inner, algebraic),
            _ => throw new ArgumentOutOfRangeException(nameof(x))
        };
        if (context.Kind == SemialgebraicUnaryKind.AbsoluteValue)
        {
            return InnerSignAtPoint(context, chart, point) < 0 ? ExactRealArithmetic.Negate(inner) : inner;
        }

        return inner is RationalReal rationalInner ? ComposeRational(context.Kind, rationalInner.Value) : new FunctionReal("sqrt", [inner]);
    }

    private static ExactReal ComposeRational(SemialgebraicUnaryKind kind, BigRational value)
    {
        if (kind == SemialgebraicUnaryKind.AbsoluteValue)
        {
            return new RationalReal(value.Abs());
        }

        return BigRational.TrySquareRoot(value, out BigRational root) ? new RationalReal(root) : new FunctionReal("sqrt", [new RationalReal(value)]);
    }

    private static bool TryComposeRationalBoundary(SemialgebraicUnaryKind kind, BigRational value, out BigRational composed)
    {
        if (kind == SemialgebraicUnaryKind.AbsoluteValue)
        {
            composed = value.Abs();
            return true;
        }

        if (value.Sign >= 0 && BigRational.TrySquareRoot(value, out composed))
        {
            return true;
        }

        composed = default;
        return false;
    }

    private static bool TryRationalCoordinate(SemialgebraicUnaryContext context, UnarySignChartCertificate chart, int point, ResourceBudget budget, out BigRational value)
    {
        ExactReal root = chart.RootIsolation.Roots[point];
        if (root is RationalReal rational)
        {
            value = rational.Value;
            return true;
        }

        foreach (UnivariatePolynomial polynomial in chart.Polynomials.Where(static polynomial => polynomial.Degree == 1))
        {
            if (PolynomialSignAtPoint(chart, polynomial, point) == 0)
            {
                value = -polynomial[0] / polynomial[1];
                return polynomial.Evaluate(value, budget).IsZero;
            }
        }

        value = default;
        return false;
    }

    private static bool TouchesDomain(UnarySignChartCertificate chart, int point) => chart.PointDomain[point] || chart.GapDomain[point] || chart.GapDomain[point + 1];
    private static UnaryCompositionProofKernelExtremumKind ClassifyExtremum(bool leftDomain, bool rightDomain, int leftSign, int rightSign)
    {
        if (leftDomain && rightDomain)
        {
            if (leftSign < 0 && rightSign > 0)
            {
                return UnaryCompositionProofKernelExtremumKind.Minimum;
            }

            if (leftSign > 0 && rightSign < 0)
            {
                return UnaryCompositionProofKernelExtremumKind.Maximum;
            }

            return UnaryCompositionProofKernelExtremumKind.None;
        }

        if (rightDomain)
        {
            return rightSign switch
            {
                > 0 => UnaryCompositionProofKernelExtremumKind.Minimum,
                < 0 => UnaryCompositionProofKernelExtremumKind.Maximum,
                _ => UnaryCompositionProofKernelExtremumKind.None
            };
        }

        return leftSign switch
        {
            < 0 => UnaryCompositionProofKernelExtremumKind.Minimum,
            > 0 => UnaryCompositionProofKernelExtremumKind.Maximum,
            _ => UnaryCompositionProofKernelExtremumKind.None
        };
    }

    private static RealSet BuildSet(ImmutableArray<ExactReal> roots, bool[] gaps, bool[] points)
    {
        var included = new bool[checked((roots.Length * 2) + 1)];
        for (int gap = 0; gap <= roots.Length; gap++)
        {
            included[gap * 2] = gaps[gap];
            if (gap < roots.Length)
            {
                included[(gap * 2) + 1] = points[gap];
            }
        }

        if (included.All(static value => value))
        {
            return AllRealSet.Instance;
        }

        if (included.All(static value => !value))
        {
            return EmptySet.Instance;
        }

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

    private static UnivariatePolynomial Combined(ImmutableArray<UnivariatePolynomial> polynomials, ResourceBudget budget)
    {
        UnivariatePolynomial combined = UnivariatePolynomial.One;
        foreach (UnivariatePolynomial polynomial in polynomials.Where(static polynomial => !polynomial.IsConstant))
        {
            combined = combined.Multiply(polynomial, budget);
        }

        return combined.SquareFreePart(budget).PrimitivePositive(budget);
    }

    private static void BuildSigns(PolynomialFormula domain, ImmutableArray<UnivariatePolynomial> polynomials, ImmutableArray<ExactReal> roots, ResourceBudget budget, out ImmutableArray<BigRational> gapSamples, out ImmutableArray<ImmutableArray<int>> gapSigns, out ImmutableArray<ImmutableArray<int>> pointSigns, out ImmutableArray<bool> gapDomain, out ImmutableArray<bool> pointDomain)
    {
        var samples = ImmutableArray.CreateBuilder<BigRational>(roots.Length + 1);
        var gaps = ImmutableArray.CreateBuilder<ImmutableArray<int>>(roots.Length + 1);
        var points = ImmutableArray.CreateBuilder<ImmutableArray<int>>(roots.Length);
        var gapMembership = ImmutableArray.CreateBuilder<bool>(roots.Length + 1);
        var pointMembership = ImmutableArray.CreateBuilder<bool>(roots.Length);
        for (int gap = 0; gap <= roots.Length; gap++)
        {
            BigRational sample = RootGapSample(roots, gap);
            ImmutableArray<int> signs = polynomials.Select(polynomial => polynomial.Evaluate(sample, budget).Sign).ToImmutableArray();
            samples.Add(sample);
            gaps.Add(signs);
            gapMembership.Add(EvaluateFormula(domain, polynomials, signs));
            budget.AddCells(1);
            if (gap == roots.Length)
            {
                continue;
            }

            ImmutableArray<int> atPoint = polynomials.Select(polynomial => SignAt(polynomial, roots[gap], budget)).ToImmutableArray();
            points.Add(atPoint);
            pointMembership.Add(EvaluateFormula(domain, polynomials, atPoint));
            budget.AddCells(1);
        }

        gapSamples = samples.MoveToImmutable();
        gapSigns = gaps.MoveToImmutable();
        pointSigns = points.MoveToImmutable();
        gapDomain = gapMembership.MoveToImmutable();
        pointDomain = pointMembership.MoveToImmutable();
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

    private static bool EvaluateFormulaAt(PolynomialFormula formula, BigRational value, ResourceBudget budget)
    {
        var signs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnivariatePolynomial polynomial in PolynomialFormulaConverter.Atoms(formula))
        {
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
    private static bool SamePolynomials(ImmutableArray<UnivariatePolynomial> left, ImmutableArray<UnivariatePolynomial> right) => left.Length == right.Length && left.Zip(right).All(static pair => pair.First.Equals(pair.Second));
    private static bool NestedEqual(ImmutableArray<ImmutableArray<int>> left, ImmutableArray<ImmutableArray<int>> right) => left.Length == right.Length && left.Zip(right).All(static pair => pair.First.SequenceEqual(pair.Second));
    private static int SignAt(UnivariatePolynomial polynomial, ExactReal root, ResourceBudget budget) => root switch
    {
        RationalReal rational => polynomial.Evaluate(rational.Value, budget).Sign,
        AlgebraicReal algebraic => SturmRootIsolator.SignAtIsolatedRoot(algebraic.Polynomial, polynomial, algebraic.IsolatingInterval, budget),
        _ => throw new ArgumentOutOfRangeException(nameof(root))
    };
    private static BigRational RootGapSample(ImmutableArray<ExactReal> roots, int gap)
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
}
