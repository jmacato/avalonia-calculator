using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Exact image projection for a univariate rational function over an exact
/// semialgebraic domain.  The image-membership truth value can change only at
/// a finite domain-boundary value, a critical value, or a finite value at
/// infinity.  Those values partition the y-axis; every boundary and every
/// open gap is then decided by an exact existential fiber decomposition.
/// </summary>
internal static class RationalRangeProjection
{
    private const string ProjectionRule = "rational-range-fiber-projection";

    public static bool TryProject(
        RationalAnalysisContext context,
        ResourceBudget budget,
        out RealSet range,
        out RationalRangeProofCertificate certificate)
    {
        budget.Charge();
        UnivariatePolynomial partitionPolynomial = CreatePartitionPolynomial(context, budget);
        RootIsolationCertificate partitionRoots = SturmRootIsolator.Isolate(
            partitionPolynomial,
            budget);
        if (!TryDeriveBoundaries(
                context,
                partitionRoots.Roots,
                budget,
                out ImmutableArray<BigRational> boundaries))
        {
            range = null!;
            certificate = null!;
            return false;
        }

        ImmutableArray<RationalRangeFiberWitness> fibers = BuildFibers(
            context,
            boundaries,
            budget);
        range = BuildRange(boundaries, fibers);
        certificate = new RationalRangeProofCertificate(
            context.Expression.Value.Canonical,
            ClaimCanonical.For(range),
            context.Function.Numerator,
            context.Function.Denominator,
            context.DomainFormula,
            partitionPolynomial,
            partitionRoots,
            boundaries,
            fibers,
            ProjectionRule);
        return true;
    }

    /// <summary>
    /// Production replay.  This reconstructs the partition theorem premises
    /// and validates every supplied fiber certificate; it never invokes the
    /// solver entry point or RationalFeatureAnalyzer.TryComputeRange.
    /// </summary>
    public static bool Verify(
        AnalysisRequest request,
        SemanticExpression expression,
        RationalRangeProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != AnalysisFeatures.Range ||
            !request.Features.HasFlag(AnalysisFeatures.Range) ||
            certificate.Rule != ProjectionRule ||
            !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) ||
            !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) ||
            !RationalCertificateReplay.TryCreateContext(
                expression,
                request.Variable,
                budget,
                out RationalAnalysisContext context) ||
            !context.Function.Numerator.Equals(certificate.Numerator) ||
            !context.Function.Denominator.Equals(certificate.Denominator) ||
            !string.Equals(
                context.DomainFormula.Canonical,
                certificate.DomainFormula.Canonical,
                StringComparison.Ordinal))
        {
            return false;
        }

        UnivariatePolynomial expectedPartition = CreatePartitionPolynomial(context, budget);
        if (!expectedPartition.Equals(certificate.PartitionPolynomial) ||
            !certificate.PartitionRoots.Polynomial.Equals(expectedPartition) ||
            !SturmRootIsolator.Verify(certificate.PartitionRoots, budget) ||
            !TryDeriveBoundaries(
                context,
                certificate.PartitionRoots.Roots,
                budget,
                out ImmutableArray<BigRational> expectedBoundaries) ||
            !expectedBoundaries.SequenceEqual(certificate.Boundaries))
        {
            return false;
        }

        int expectedFiberCount = checked(certificate.Boundaries.Length * 2 + 1);
        if (certificate.Fibers.Length != expectedFiberCount)
        {
            return false;
        }

        int witness = 0;
        for (int gap = 0; gap <= certificate.Boundaries.Length; gap++)
        {
            BigRational gapSample = GapSample(certificate.Boundaries, gap);
            if (!VerifyFiber(
                    context,
                    certificate.Fibers[witness++],
                    RationalRangeCellKind.OpenInterval,
                    gap,
                    gapSample,
                    budget))
            {
                return false;
            }

            if (gap == certificate.Boundaries.Length)
            {
                continue;
            }

            BigRational boundary = certificate.Boundaries[gap];
            if (!VerifyFiber(
                    context,
                    certificate.Fibers[witness++],
                    RationalRangeCellKind.Boundary,
                    gap,
                    boundary,
                    budget))
            {
                return false;
            }
        }

        RealSet replayed = BuildRange(certificate.Boundaries, certificate.Fibers);
        return string.Equals(ClaimCanonical.For(replayed), claim, StringComparison.Ordinal);
    }

    public static bool VerifyLegacyPolynomialProof(
        RationalAnalysisContext context,
        RationalFunctionProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        if (!TryComputeLegacyPolynomialRange(
                context,
                budget,
                out RealSet range,
                out ImmutableArray<RootIsolationCertificate> expectedRoots) ||
            expectedRoots.Length != certificate.RootIsolations.Length)
        {
            return false;
        }

        for (int index = 0; index < expectedRoots.Length; index++)
        {
            RootIsolationCertificate expected = expectedRoots[index];
            RootIsolationCertificate actual = certificate.RootIsolations[index];
            if (!expected.Polynomial.Equals(actual.Polynomial) ||
                !expected.Roots.Select(ExactRealCanonical.Format)
                    .SequenceEqual(actual.Roots.Select(ExactRealCanonical.Format), StringComparer.Ordinal))
            {
                return false;
            }
        }

        return string.Equals(ClaimCanonical.For(range), claim, StringComparison.Ordinal);
    }

    private static UnivariatePolynomial CreatePartitionPolynomial(
        RationalAnalysisContext context,
        ResourceBudget budget)
    {
        RationalFunction derivative = context.Function.Derivative(budget);
        IEnumerable<UnivariatePolynomial> sources = PolynomialFormulaConverter
            .Atoms(context.DomainFormula)
            .Concat([derivative.Numerator, context.Function.Denominator]);
        UnivariatePolynomial combined = UnivariatePolynomial.One;
        foreach (UnivariatePolynomial polynomial in sources
                     .Where(static polynomial => !polynomial.IsZero && !polynomial.IsConstant)
                     .DistinctBy(static polynomial => polynomial.Canonical)
                     .OrderBy(static polynomial => polynomial.Canonical, StringComparer.Ordinal))
        {
            combined = combined.Multiply(polynomial, budget);
        }

        return combined.SquareFreePart(budget).PrimitivePositive(budget);
    }

    private static bool TryComputeLegacyPolynomialRange(
        RationalAnalysisContext context,
        ResourceBudget budget,
        out RealSet range,
        out ImmutableArray<RootIsolationCertificate> roots)
    {
        RationalFunction function = context.Function;
        roots = [];
        if (function.Numerator.IsZero)
        {
            range = RealSets.Points([new RationalReal(BigRational.Zero)]);
            return !context.Domain.IsEmpty;
        }

        if (function.Denominator.Degree == 0 && context.Domain is AllRealSet)
        {
            UnivariatePolynomial polynomial = function.Numerator.Multiply(
                function.Denominator.ConstantCoefficient.Reciprocal(),
                budget);
            if (polynomial.Degree == 0)
            {
                range = RealSets.Points([new RationalReal(polynomial.ConstantCoefficient)]);
                return true;
            }

            if ((polynomial.Degree & 1) != 0)
            {
                range = AllRealSet.Instance;
                return true;
            }

            RootIsolationCertificate critical = SturmRootIsolator.Isolate(
                polynomial.Derivative(budget),
                budget);
            roots = [critical];
            if (polynomial.Degree == 2)
            {
                BigRational x = -polynomial[1] / (new BigRational(2) * polynomial[2]);
                ExactReal y = new RationalReal(polynomial.Evaluate(x, budget));
                bool minimum = polynomial.LeadingCoefficient.Sign > 0;
                range = OneSidedRange(y, minimum);
                return true;
            }

            if (TryReplayEvenQuarticRange(
                    polynomial,
                    budget,
                    out BigRational quarticEndpoint))
            {
                range = OneSidedRange(
                    new RationalReal(quarticEndpoint),
                    polynomial.LeadingCoefficient.Sign > 0);
                return true;
            }

            if (critical.Roots.Length == 1)
            {
                ExactReal x = critical.Roots[0];
                ExactReal y = x switch
                {
                    RationalReal rational => new RationalReal(function.Evaluate(rational.Value, budget)),
                    AlgebraicReal algebraic => new AlgebraicImageReal(function, algebraic),
                    _ => throw new InvalidOperationException("Unsupported exact real root representation.")
                };
                bool minimum = polynomial.LeadingCoefficient.Sign > 0;
                range = OneSidedRange(y, minimum);
                return true;
            }
        }

        if (function.Numerator.Degree == 1 &&
            function.Denominator.Degree == 1 &&
            DomainIsReducedDenominator(context, budget))
        {
            BigRational excluded = function.Numerator.LeadingCoefficient /
                                   function.Denominator.LeadingCoefficient;
            range = new DifferenceSet(
                AllRealSet.Instance,
                RealSets.Points([new RationalReal(excluded)]));
            return true;
        }

        range = null!;
        return false;
    }

    // Independent certificate replay for the u=x^2 reduction used by the
    // production range theorem. Keep this derivation local to the checker;
    // no solver-selected branch or claimed endpoint is trusted.
    private static bool TryReplayEvenQuarticRange(
        UnivariatePolynomial polynomial,
        ResourceBudget budget,
        out BigRational endpoint)
    {
        budget.Charge();
        if (polynomial.Degree != 4 ||
            !polynomial[3].IsZero ||
            !polynomial[1].IsZero)
        {
            endpoint = default;
            return false;
        }

        BigRational leading = polynomial[4];
        BigRational quadratic = polynomial[2];
        BigRational constant = polynomial[0];
        bool vertexLiesOnNonnegativeRay =
            leading.Sign != quadratic.Sign && !quadratic.IsZero;
        if (!vertexLiesOnNonnegativeRay)
        {
            endpoint = constant;
            return true;
        }

        BigRational square = quadratic * quadratic;
        BigRational denominator = new BigRational(4) * leading;
        budget.CheckCoefficient(square);
        budget.CheckCoefficient(denominator);
        endpoint = constant - square / denominator;
        budget.CheckCoefficient(endpoint);
        return true;
    }

    private static IntervalSet OneSidedRange(ExactReal endpoint, bool minimum)
    {
        return new IntervalSet(
            minimum ? RealBound.Finite(endpoint) : RealBound.NegativeInfinity,
            minimum,
            minimum ? RealBound.PositiveInfinity : RealBound.Finite(endpoint),
            !minimum);
    }

    private static bool DomainIsReducedDenominator(
        RationalAnalysisContext context,
        ResourceBudget budget)
    {
        PolynomialAtom nonzero = new(
            context.Function.Denominator.PrimitivePositive(budget),
            Comparison.NotEqual);
        CellDecompositionCertificate expected = CellDecomposer.Decompose(nonzero, budget);
        return string.Equals(
            expected.Result.Canonical,
            context.Domain.Canonical,
            StringComparison.Ordinal);
    }

    private static bool TryDeriveBoundaries(
        RationalAnalysisContext context,
        ImmutableArray<ExactReal> partitionRoots,
        ResourceBudget budget,
        out ImmutableArray<BigRational> boundaries)
    {
        var result = new SortedSet<BigRational>();
        if (context.Domain.IsEmpty)
        {
            boundaries = [];
            return true;
        }

        RationalFunction function = context.Function;
        if (function.Numerator.Degree <= 0 && function.Denominator.Degree <= 0)
        {
            result.Add(function.Numerator.ConstantCoefficient /
                       function.Denominator.ConstantCoefficient);
        }

        for (int index = 0; index < partitionRoots.Length; index++)
        {
            ExactReal root = partitionRoots[index];
            if (!TouchesDomain(context.DomainFormula, partitionRoots, index, budget))
            {
                continue;
            }

            int denominatorSign = SignAt(function.Denominator, root, budget);
            if (denominatorSign == 0)
            {
                continue;
            }

            if (!TryRationalCoordinate(context, root, budget, out BigRational coordinate))
            {
                // A finite non-rational critical/boundary image requires an
                // ordered algebraic-image representation.  Until that proof
                // object is available, incompleteness is explicit.
                boundaries = [];
                return false;
            }

            result.Add(function.Evaluate(coordinate, budget));
        }

        bool unboundedDomain = DomainInGap(
                                   context.DomainFormula,
                                   partitionRoots,
                                   0,
                                   budget) ||
                               DomainInGap(
                                   context.DomainFormula,
                                   partitionRoots,
                                   partitionRoots.Length,
                                   budget);
        if (unboundedDomain && function.Numerator.Degree <= function.Denominator.Degree)
        {
            BigRational finiteLimit = function.Numerator.Degree == function.Denominator.Degree
                ? function.Numerator.LeadingCoefficient / function.Denominator.LeadingCoefficient
                : BigRational.Zero;
            result.Add(finiteLimit);
        }

        boundaries = result.ToImmutableArray();
        return true;
    }

    private static bool TryRationalCoordinate(
        RationalAnalysisContext context,
        ExactReal root,
        ResourceBudget budget,
        out BigRational value)
    {
        if (root is RationalReal rational)
        {
            value = rational.Value;
            return true;
        }

        RationalFunction derivative = context.Function.Derivative(budget);
        IEnumerable<UnivariatePolynomial> sources = PolynomialFormulaConverter
            .Atoms(context.DomainFormula)
            .Concat([derivative.Numerator, context.Function.Denominator]);
        foreach (UnivariatePolynomial source in sources.Where(static source => source.Degree == 1))
        {
            if (SignAt(source, root, budget) != 0)
            {
                continue;
            }

            BigRational candidate = -source[0] / source[1];
            if (source.Evaluate(candidate, budget).IsZero)
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static ImmutableArray<RationalRangeFiberWitness> BuildFibers(
        RationalAnalysisContext context,
        ImmutableArray<BigRational> boundaries,
        ResourceBudget budget)
    {
        var result = ImmutableArray.CreateBuilder<RationalRangeFiberWitness>(
            checked(boundaries.Length * 2 + 1));
        for (int gap = 0; gap <= boundaries.Length; gap++)
        {
            result.Add(CreateFiberWitness(
                context,
                RationalRangeCellKind.OpenInterval,
                gap,
                GapSample(boundaries, gap),
                budget));
            if (gap < boundaries.Length)
            {
                result.Add(CreateFiberWitness(
                    context,
                    RationalRangeCellKind.Boundary,
                    gap,
                    boundaries[gap],
                    budget));
            }
        }

        return result.MoveToImmutable();
    }

    private static RationalRangeFiberWitness CreateFiberWitness(
        RationalAnalysisContext context,
        RationalRangeCellKind kind,
        int index,
        BigRational sample,
        ResourceBudget budget)
    {
        PolynomialFormula formula = FiberFormula(context, sample, budget);
        CellDecompositionCertificate fiber = CellDecomposer.Decompose(formula, budget);
        return new RationalRangeFiberWitness(
            kind,
            index,
            sample,
            fiber,
            !fiber.Result.IsEmpty);
    }

    private static bool VerifyFiber(
        RationalAnalysisContext context,
        RationalRangeFiberWitness witness,
        RationalRangeCellKind kind,
        int index,
        BigRational sample,
        ResourceBudget budget)
    {
        if (witness.Kind != kind ||
            witness.Index != index ||
            witness.Sample != sample)
        {
            return false;
        }

        PolynomialFormula expected = FiberFormula(context, sample, budget);
        if (!string.Equals(
                expected.Canonical,
                witness.Fiber.Formula.Canonical,
                StringComparison.Ordinal) ||
            !CellDecomposer.Verify(witness.Fiber, budget))
        {
            return false;
        }

        return witness.HasPreimage == !witness.Fiber.Result.IsEmpty;
    }

    private static PolynomialJunction FiberFormula(
        RationalAnalysisContext context,
        BigRational value,
        ResourceBudget budget)
    {
        UnivariatePolynomial fiber = context.Function.Numerator.Subtract(
            context.Function.Denominator.Multiply(value, budget),
            budget);
        PolynomialFormula equation = fiber.IsZero
            ? new PolynomialBoolean(true)
            : new PolynomialAtom(fiber.PrimitivePositive(budget), Comparison.Equal);
        return new PolynomialJunction(true, [context.DomainFormula, equation]);
    }

    private static RealSet BuildRange(
        ImmutableArray<BigRational> boundaries,
        ImmutableArray<RationalRangeFiberWitness> fibers)
    {
        if (fibers.All(static fiber => fiber.HasPreimage))
        {
            return AllRealSet.Instance;
        }

        if (fibers.All(static fiber => !fiber.HasPreimage))
        {
            return EmptySet.Instance;
        }

        bool allGapsIncluded = fibers
            .Where(static fiber => fiber.Kind == RationalRangeCellKind.OpenInterval)
            .All(static fiber => fiber.HasPreimage);
        if (allGapsIncluded)
        {
            ExactReal[] removed = fibers
                .Where(static fiber =>
                    fiber.Kind == RationalRangeCellKind.Boundary && !fiber.HasPreimage)
                .Select(static fiber => (ExactReal)new RationalReal(fiber.Sample))
                .ToArray();
            if (removed.Length > 0)
            {
                return new DifferenceSet(AllRealSet.Instance, RealSets.Points(removed));
            }
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
            RationalRangeFiberWitness first = fibers[start];
            RationalRangeFiberWitness last = fibers[end];
            if (start == end && first.Kind == RationalRangeCellKind.Boundary)
            {
                components.Add(RealSets.Points([new RationalReal(first.Sample)]));
                position++;
                continue;
            }

            RealBound lower = first.Kind == RationalRangeCellKind.OpenInterval && first.Index == 0
                ? RealBound.NegativeInfinity
                : RealBound.Finite(new RationalReal(
                    first.Kind == RationalRangeCellKind.Boundary
                        ? first.Sample
                        : boundaries[first.Index - 1]));
            RealBound upper = last.Kind == RationalRangeCellKind.OpenInterval &&
                              last.Index == boundaries.Length
                ? RealBound.PositiveInfinity
                : RealBound.Finite(new RationalReal(
                    last.Kind == RationalRangeCellKind.Boundary
                        ? last.Sample
                        : boundaries[last.Index]));
            components.Add(new IntervalSet(
                lower,
                first.Kind == RationalRangeCellKind.Boundary,
                upper,
                last.Kind == RationalRangeCellKind.Boundary));
            position++;
        }

        return RealSets.Union(components);
    }

    private static bool TouchesDomain(
        PolynomialFormula domain,
        ImmutableArray<ExactReal> roots,
        int rootIndex,
        ResourceBudget budget)
    {
        return DomainAtRoot(domain, roots[rootIndex], budget) ||
               DomainInGap(domain, roots, rootIndex, budget) ||
               DomainInGap(domain, roots, rootIndex + 1, budget);
    }

    private static bool DomainAtRoot(
        PolynomialFormula domain,
        ExactReal root,
        ResourceBudget budget)
    {
        ImmutableArray<UnivariatePolynomial> atoms = PolynomialFormulaConverter.Atoms(domain);
        var signs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnivariatePolynomial atom in atoms)
        {
            signs.Add(atom.Canonical, SignAt(atom, root, budget));
        }

        return PolynomialFormulaConverter.Evaluate(domain, signs);
    }

    private static bool DomainInGap(
        PolynomialFormula domain,
        ImmutableArray<ExactReal> roots,
        int gap,
        ResourceBudget budget)
    {
        BigRational sample = RootGapSample(roots, gap);
        ImmutableArray<UnivariatePolynomial> atoms = PolynomialFormulaConverter.Atoms(domain);
        var signs = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnivariatePolynomial atom in atoms)
        {
            signs.Add(atom.Canonical, atom.Evaluate(sample, budget).Sign);
        }

        return PolynomialFormulaConverter.Evaluate(domain, signs);
    }

    private static int SignAt(
        UnivariatePolynomial polynomial,
        ExactReal root,
        ResourceBudget budget)
    {
        return root switch
        {
            RationalReal rational => polynomial.Evaluate(rational.Value, budget).Sign,
            AlgebraicReal algebraic => SturmRootIsolator.SignAtIsolatedRoot(
                algebraic.Polynomial,
                polynomial,
                algebraic.IsolatingInterval,
                budget),
            _ => throw new ArgumentOutOfRangeException(nameof(root))
        };
    }

    private static BigRational GapSample(ImmutableArray<BigRational> boundaries, int gap)
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

    private static BigRational RootGapSample(ImmutableArray<ExactReal> roots, int gap)
    {
        if (roots.IsEmpty)
        {
            return BigRational.Zero;
        }

        if (gap == 0)
        {
            return LowerBound(roots[0]) - BigRational.One;
        }

        if (gap == roots.Length)
        {
            return UpperBound(roots[^1]) + BigRational.One;
        }

        return (UpperBound(roots[gap - 1]) + LowerBound(roots[gap])) / 2;
    }

    private static BigRational LowerBound(ExactReal root)
    {
        return root switch
        {
            RationalReal rational => rational.Value,
            AlgebraicReal algebraic => algebraic.IsolatingInterval.Lower,
            _ => throw new ArgumentOutOfRangeException(nameof(root))
        };
    }

    private static BigRational UpperBound(ExactReal root)
    {
        return root switch
        {
            RationalReal rational => rational.Value,
            AlgebraicReal algebraic => algebraic.IsolatingInterval.Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(root))
        };
    }
}
