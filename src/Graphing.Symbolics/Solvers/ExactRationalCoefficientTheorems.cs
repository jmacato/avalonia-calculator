using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ExactRationalCoefficientTheorems
{
    public static bool TryCompute(
        ExactRationalPattern pattern,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value)
    {
        budget.Charge();
        value = null!;
        if (!TryDomainData(pattern, budget, out RealSet domain, out ImmutableArray<ExactScalar> holes))
        {
            return false;
        }

        bool proved = feature switch
        {
            AnalysisFeatures.Domain => Assign(domain, out value),
            AnalysisFeatures.Range => TryRange(pattern, domain, holes, budget, out value),
            AnalysisFeatures.Parity => TryParity(pattern, holes, budget, out value),
            AnalysisFeatures.Zeros => TryZeros(pattern, domain, budget, out value),
            AnalysisFeatures.YIntercept => TryYIntercept(pattern, budget, out value),
            AnalysisFeatures.Minima => TryExtrema(pattern, holes, true, budget, out value),
            AnalysisFeatures.Maxima => TryExtrema(pattern, holes, false, budget, out value),
            AnalysisFeatures.InflectionPoints =>
                Assign(ImmutableArray<FeaturePoint>.Empty, out value),
            AnalysisFeatures.VerticalAsymptotes =>
                TryVerticalAsymptotes(pattern, budget, out value),
            AnalysisFeatures.HorizontalAsymptotes =>
                TryHorizontalAsymptotes(pattern, budget, out value),
            AnalysisFeatures.ObliqueAsymptotes =>
                TryObliqueAsymptotes(pattern, out value),
            AnalysisFeatures.Monotonicity =>
                TryMonotonicity(pattern, domain, holes, budget, out value),
            AnalysisFeatures.Period => Assign(
                new Periodicity(PeriodicityKind.NotPeriodic, null),
                out value),
            _ => false
        };
        return proved;
    }

    private static bool TryDomainData(
        ExactRationalPattern pattern,
        ResourceBudget budget,
        out RealSet domain,
        out ImmutableArray<ExactScalar> holes)
    {
        var points = new List<ExactScalar>();
        foreach (ExactCoefficientPolynomial exclusion in pattern.DomainExclusions)
        {
            budget.Charge();
            if (exclusion.IsZero)
            {
                domain = EmptySet.Instance;
                holes = [];
                return true;
            }

            if (!ExactCoefficientMath.TrySolveRoots(exclusion, budget, out ImmutableArray<ExactScalar> roots))
            {
                domain = null!;
                holes = default;
                return false;
            }

            foreach (ExactScalar root in roots)
            {
                if (points.All(existing => !ExactCoefficientMath.SameValue(existing, root)))
                {
                    points.Add(root);
                }
            }
        }

        holes = points
            .OrderBy(static point => point.Canonical, StringComparer.Ordinal)
            .ToImmutableArray();
        domain = holes.IsEmpty
            ? AllRealSet.Instance
            : new DifferenceSet(
                AllRealSet.Instance,
                RealSets.Points(holes.Select(static point => point.Value)));
        return true;
    }

    private static bool TryRange(
        ExactRationalPattern pattern,
        RealSet domain,
        ImmutableArray<ExactScalar> holes,
        ResourceBudget budget,
        out object value)
    {
        if (domain is EmptySet)
        {
            return Assign(EmptySet.Instance, out value);
        }

        switch (pattern.Kind)
        {
            case ExactCoefficientPatternKind.AffinePolynomial:
                {
                    if (holes.IsEmpty)
                    {
                        return Assign(AllRealSet.Instance, out value);
                    }

                    var removed = new List<ExactReal>();
                    foreach (ExactScalar hole in holes)
                    {
                        if (!pattern.Numerator.TryEvaluate(hole, budget, out ExactScalar image))
                        {
                            value = null!;
                            return false;
                        }

                        removed.Add(image.Value);
                    }

                    return Assign(
                        new DifferenceSet(AllRealSet.Instance, RealSets.Points(removed)),
                        out value);
                }
            case ExactCoefficientPatternKind.QuadraticPolynomial:
                {
                    if (!holes.IsEmpty ||
                        !ExactCoefficientMath.TryVertex(
                            pattern.Numerator,
                            budget,
                            out _,
                            out ExactScalar y))
                    {
                        value = null!;
                        return false;
                    }

                    var range = pattern.Numerator[2].Sign > 0
                        ? new IntervalSet(
                            RealBound.Finite(y.Value),
                            true,
                            RealBound.PositiveInfinity,
                            false)
                        : new IntervalSet(
                            RealBound.NegativeInfinity,
                            false,
                            RealBound.Finite(y.Value),
                            true);
                    return Assign(range, out value);
                }
            case ExactCoefficientPatternKind.Mobius:
                return TryMobiusRange(pattern, holes, budget, out value);
            default:
                value = null!;
                return false;
        }
    }

    private static bool TryMobiusRange(
        ExactRationalPattern pattern,
        ImmutableArray<ExactScalar> holes,
        ResourceBudget budget,
        out object value)
    {
        if (!ExactCoefficientMath.TryDeterminant(
                pattern.Numerator,
                pattern.Denominator,
                budget,
                out ExactScalar determinant))
        {
            value = null!;
            return false;
        }

        ExactScalar horizontal = ExactCoefficientMath.Divide(
            pattern.Numerator[1],
            pattern.Denominator[1],
            budget);
        if (determinant.IsZero)
        {
            return Assign(RealSets.Points([horizontal.Value]), out value);
        }

        var removed = new List<ExactReal> { horizontal.Value };
        foreach (ExactScalar hole in holes)
        {
            if (!pattern.Denominator.TryEvaluate(hole, budget, out ExactScalar denominatorAtHole) ||
                denominatorAtHole.IsZero)
            {
                continue;
            }

            if (!pattern.Numerator.TryEvaluate(hole, budget, out ExactScalar numeratorAtHole))
            {
                value = null!;
                return false;
            }

            removed.Add(ExactCoefficientMath.Divide(
                numeratorAtHole,
                denominatorAtHole,
                budget).Value);
        }

        return Assign(
            new DifferenceSet(AllRealSet.Instance, RealSets.Points(removed)),
            out value);
    }

    private static bool TryParity(
        ExactRationalPattern pattern,
        ImmutableArray<ExactScalar> holes,
        ResourceBudget budget,
        out object value)
    {
        if (!TryIsSymmetric(holes, budget, out bool symmetric))
        {
            value = null!;
            return false;
        }

        if (!symmetric)
        {
            return Assign(FunctionParity.Neither, out value);
        }

        FunctionParity parity;
        if (pattern.Kind == ExactCoefficientPatternKind.AffinePolynomial)
        {
            parity = pattern.Numerator[0].IsZero
                ? FunctionParity.Odd
                : FunctionParity.Neither;
            return Assign(parity, out value);
        }

        if (pattern.Kind == ExactCoefficientPatternKind.QuadraticPolynomial)
        {
            parity = pattern.Numerator[1].IsZero
                ? FunctionParity.Even
                : FunctionParity.Neither;
            return Assign(parity, out value);
        }

        return TryMobiusParity(pattern, budget, out value);
    }

    private static bool TryMobiusParity(
        ExactRationalPattern pattern,
        ResourceBudget budget,
        out object value)
    {
        // Cross multiplication gives exact coefficient identities without
        // sampling: N(-x)D(x) = +/- N(x)D(-x).
        ExactCoefficientPolynomial reflectedNumerator = Reflect(pattern.Numerator, budget);
        ExactCoefficientPolynomial reflectedDenominator = Reflect(pattern.Denominator, budget);
        if (!ExactCoefficientPolynomial.TryMultiply(
                reflectedNumerator,
                pattern.Denominator,
                budget,
                out ExactCoefficientPolynomial left) ||
            !ExactCoefficientPolynomial.TryMultiply(
                pattern.Numerator,
                reflectedDenominator,
                budget,
                out ExactCoefficientPolynomial right) ||
            !ExactCoefficientPolynomial.TrySubtract(left, right, budget, out ExactCoefficientPolynomial even) ||
            !ExactCoefficientPolynomial.TryAdd(left, right, budget, out ExactCoefficientPolynomial odd))
        {
            value = null!;
            return false;
        }

        FunctionParity parity = even.IsZero
            ? odd.IsZero ? FunctionParity.Both : FunctionParity.Even
            : odd.IsZero ? FunctionParity.Odd : FunctionParity.Neither;
        return Assign(parity, out value);
    }

    private static bool TryZeros(
        ExactRationalPattern pattern,
        RealSet domain,
        ResourceBudget budget,
        out object value)
    {
        if (domain is EmptySet)
        {
            return Assign(EmptySet.Instance, out value);
        }

        if (pattern.Numerator.IsZero)
        {
            return Assign(domain, out value);
        }

        if (pattern.Kind == ExactCoefficientPatternKind.Mobius &&
            ExactCoefficientMath.TryDeterminant(
                pattern.Numerator,
                pattern.Denominator,
                budget,
                out ExactScalar determinant) &&
            determinant.IsZero)
        {
            // N(x)=kD(x) follows from the exact zero determinant.  On the
            // retained domain D(x)!=0, a nonzero k can therefore never have
            // a zero.  This avoids trying to compare the syntactically
            // different but equal roots -b/a and -d/c.
            ExactScalar constant = ExactCoefficientMath.Divide(
                pattern.Numerator[1],
                pattern.Denominator[1],
                budget);
            return Assign(constant.IsZero ? domain : EmptySet.Instance, out value);
        }

        if (!ExactCoefficientMath.TrySolveRoots(
                pattern.Numerator,
                budget,
                out ImmutableArray<ExactScalar> roots))
        {
            value = null!;
            return false;
        }

        var defined = new List<ExactReal>();
        foreach (ExactScalar root in roots)
        {
            if (!IsDefinedAt(pattern, root, budget, out bool isDefined))
            {
                value = null!;
                return false;
            }

            if (isDefined)
            {
                defined.Add(root.Value);
            }
        }

        return Assign(RealSets.Points(defined), out value);
    }

    private static bool TryYIntercept(
        ExactRationalPattern pattern,
        ResourceBudget budget,
        out object value)
    {
        ExactScalar zero = ExactScalar.Zero;
        if (!IsDefinedAt(pattern, zero, budget, out bool defined))
        {
            value = null!;
            return false;
        }

        if (!defined)
        {
            return Assign(OptionalValue<ExactReal>.None, out value);
        }

        if (!pattern.Numerator.TryEvaluate(zero, budget, out ExactScalar numerator) ||
            !pattern.Denominator.TryEvaluate(zero, budget, out ExactScalar denominator))
        {
            value = null!;
            return false;
        }

        return Assign(
            OptionalValue<ExactReal>.Some(
                ExactCoefficientMath.Divide(numerator, denominator, budget).Value),
            out value);
    }

    private static bool TryExtrema(
        ExactRationalPattern pattern,
        ImmutableArray<ExactScalar> holes,
        bool minimum,
        ResourceBudget budget,
        out object value)
    {
        if (pattern.Kind is ExactCoefficientPatternKind.AffinePolynomial or
            ExactCoefficientPatternKind.Mobius)
        {
            return Assign(ImmutableArray<FeaturePoint>.Empty, out value);
        }

        if (!holes.IsEmpty ||
            !ExactCoefficientMath.TryVertex(
                pattern.Numerator,
                budget,
                out ExactScalar x,
                out ExactScalar y))
        {
            value = null!;
            return false;
        }

        bool isMinimum = pattern.Numerator[2].Sign > 0;
        ImmutableArray<FeaturePoint> points = minimum == isMinimum
            ? [new ConstantYFeaturePoint(new SingletonReal(x.Value), y.Value)]
            : [];
        return Assign(points, out value);
    }

    private static bool TryVerticalAsymptotes(
        ExactRationalPattern pattern,
        ResourceBudget budget,
        out object value)
    {
        if (pattern.Denominator.IsConstant ||
            !ExactCoefficientMath.TrySolveRoots(
                pattern.Denominator,
                budget,
                out ImmutableArray<ExactScalar> roots))
        {
            return pattern.Denominator.IsConstant
                ? Assign(ImmutableArray<Asymptote>.Empty, out value)
                : Fail(out value);
        }

        var asymptotes = ImmutableArray.CreateBuilder<Asymptote>();
        foreach (ExactScalar root in roots)
        {
            if (!pattern.Numerator.TryEvaluate(root, budget, out ExactScalar numerator))
            {
                return Fail(out value);
            }

            if (!numerator.IsZero)
            {
                asymptotes.Add(new Asymptote(
                    AsymptoteOrientation.Vertical,
                    new SingletonReal(root.Value),
                    null,
                    null));
            }
        }

        return Assign(asymptotes.ToImmutable(), out value);
    }

    private static bool TryHorizontalAsymptotes(
        ExactRationalPattern pattern,
        ResourceBudget budget,
        out object value)
    {
        if (pattern.Kind != ExactCoefficientPatternKind.Mobius)
        {
            return Assign(ImmutableArray<Asymptote>.Empty, out value);
        }

        ExactScalar horizontal = ExactCoefficientMath.Divide(
            pattern.Numerator[1],
            pattern.Denominator[1],
            budget);
        return Assign(
            ImmutableArray.Create(new Asymptote(
                AsymptoteOrientation.Horizontal,
                new SingletonReal(horizontal.Value),
                null,
                horizontal.Value)),
            out value);
    }

    private static bool TryObliqueAsymptotes(
        ExactRationalPattern pattern,
        out object value)
    {
        if (pattern.Kind != ExactCoefficientPatternKind.AffinePolynomial)
        {
            return Assign(ImmutableArray<Asymptote>.Empty, out value);
        }

        return Assign(
            ImmutableArray.Create(new Asymptote(
                AsymptoteOrientation.Oblique,
                new SingletonReal(ExactScalar.Zero.Value),
                pattern.Numerator[1].Value,
                pattern.Numerator[0].Value)),
            out value);
    }

    private static bool TryMonotonicity(
        ExactRationalPattern pattern,
        RealSet domain,
        ImmutableArray<ExactScalar> holes,
        ResourceBudget budget,
        out object value)
    {
        if (domain is EmptySet)
        {
            return Assign(ImmutableArray<MonotoneRegion>.Empty, out value);
        }

        if (pattern.Kind == ExactCoefficientPatternKind.AffinePolynomial)
        {
            if (!TryDomainComponents(holes, out ImmutableArray<RealSet> components))
            {
                return Fail(out value);
            }

            Monotonicity direction = pattern.Numerator[1].Sign > 0
                ? Monotonicity.Increasing
                : Monotonicity.Decreasing;
            return Assign(
                components.Select(component => new MonotoneRegion(component, direction)).ToImmutableArray(),
                out value);
        }

        if (pattern.Kind == ExactCoefficientPatternKind.QuadraticPolynomial)
        {
            if (!holes.IsEmpty ||
                !ExactCoefficientMath.TryVertex(pattern.Numerator, budget, out ExactScalar x, out _))
            {
                return Fail(out value);
            }

            Monotonicity left = pattern.Numerator[2].Sign > 0
                ? Monotonicity.Decreasing
                : Monotonicity.Increasing;
            Monotonicity right = left == Monotonicity.Decreasing
                ? Monotonicity.Increasing
                : Monotonicity.Decreasing;
            return Assign(
                ImmutableArray.Create(
                    new MonotoneRegion(new IntervalSet(
                        RealBound.NegativeInfinity,
                        false,
                        RealBound.Finite(x.Value),
                        false), left),
                    new MonotoneRegion(new IntervalSet(
                        RealBound.Finite(x.Value),
                        false,
                        RealBound.PositiveInfinity,
                        false), right)),
                out value);
        }

        if (!ExactCoefficientMath.TryDeterminant(
                pattern.Numerator,
                pattern.Denominator,
                budget,
                out ExactScalar determinant) ||
            !TryDomainComponents(holes, out ImmutableArray<RealSet> mobiusComponents))
        {
            return Fail(out value);
        }

        Monotonicity mobiusDirection = determinant.Sign switch
        {
            > 0 => Monotonicity.Increasing,
            < 0 => Monotonicity.Decreasing,
            _ => Monotonicity.Constant
        };
        return Assign(
            mobiusComponents.Select(component =>
                new MonotoneRegion(component, mobiusDirection)).ToImmutableArray(),
            out value);
    }

    private static bool TryDomainComponents(
        ImmutableArray<ExactScalar> holes,
        out ImmutableArray<RealSet> components)
    {
        if (holes.IsEmpty)
        {
            components = [AllRealSet.Instance];
            return true;
        }

        if (holes.Length != 1)
        {
            components = default;
            return false;
        }

        ExactReal point = holes[0].Value;
        components =
        [
            new IntervalSet(
                RealBound.NegativeInfinity,
                false,
                RealBound.Finite(point),
                false),
            new IntervalSet(
                RealBound.Finite(point),
                false,
                RealBound.PositiveInfinity,
                false)
        ];
        return true;
    }

    private static bool TryIsSymmetric(
        ImmutableArray<ExactScalar> holes,
        ResourceBudget budget,
        out bool symmetric)
    {
        foreach (ExactScalar hole in holes)
        {
            bool matched = false;
            bool comparisonUnknown = false;
            ExactScalar opposite = hole.Negate();
            foreach (ExactScalar candidate in holes)
            {
                budget.Charge();
                if (ExactCoefficientMath.SameValue(candidate, opposite))
                {
                    matched = true;
                    break;
                }

                // Equal exact values need not have identical construction
                // syntax.  Prove candidate = -hole by proving their sum is
                // zero; conversely, a proved nonzero sum rules this candidate
                // out.  Failure to establish either direction is Unknown,
                // never evidence that the domain is asymmetric.
                if (!ExactCoefficientMath.TryAdd(candidate, hole, budget, out ExactScalar sum))
                {
                    comparisonUnknown = true;
                    continue;
                }

                if (sum.IsZero)
                {
                    matched = true;
                    break;
                }
            }

            if (matched)
            {
                continue;
            }

            if (comparisonUnknown)
            {
                symmetric = false;
                return false;
            }

            symmetric = false;
            return true;
        }

        symmetric = true;
        return true;
    }

    private static bool IsDefinedAt(
        ExactRationalPattern pattern,
        ExactScalar point,
        ResourceBudget budget,
        out bool defined)
    {
        foreach (ExactCoefficientPolynomial exclusion in pattern.DomainExclusions)
        {
            if (!exclusion.TryEvaluate(point, budget, out ExactScalar value))
            {
                defined = false;
                return false;
            }

            if (value.IsZero)
            {
                defined = false;
                return true;
            }
        }

        defined = true;
        return true;
    }

    private static ExactCoefficientPolynomial Reflect(
        ExactCoefficientPolynomial polynomial,
        ResourceBudget budget)
    {
        var coefficients = ImmutableArray.CreateBuilder<ExactScalar>(polynomial.Coefficients.Length);
        for (int degree = 0; degree <= polynomial.Degree; degree++)
        {
            budget.Charge();
            coefficients.Add((degree & 1) == 0
                ? polynomial[degree]
                : polynomial[degree].Negate());
        }

        ExactCoefficientPolynomial result = ExactCoefficientPolynomial.Constant(ExactScalar.Zero);
        for (int degree = 0; degree < coefficients.Count; degree++)
        {
            ExactCoefficientPolynomial monomial = ExactCoefficientPolynomial.Constant(coefficients[degree]);
            for (int power = 0; power < degree; power++)
            {
                if (!ExactCoefficientPolynomial.TryMultiply(
                        monomial,
                        ExactCoefficientPolynomial.Variable,
                        budget,
                        out monomial))
                {
                    throw new InvalidOperationException();
                }
            }

            if (!ExactCoefficientPolynomial.TryAdd(result, monomial, budget, out result))
            {
                throw new InvalidOperationException();
            }
        }

        return result;
    }

    private static bool Assign<T>(T assigned, out object value)
    {
        value = assigned!;
        return true;
    }

    private static bool Fail(out object value)
    {
        value = null!;
        return false;
    }
}
