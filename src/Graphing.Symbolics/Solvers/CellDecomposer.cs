using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal enum CellKind
{
    OpenInterval,
    Point
}

internal sealed record CellWitness(
    CellKind Kind,
    int Index,
    BigRational? Sample,
    ImmutableArray<int> AtomSigns,
    bool Included)
{
    public string Canonical =>
        $"cell[{(int)Kind},{Index},{Sample?.ToString() ?? "point"},{string.Join(',', AtomSigns)},{(Included ? 1 : 0)}]";
}

internal sealed record CellDecompositionCertificate(
    PolynomialFormula Formula,
    ImmutableArray<UnivariatePolynomial> Atoms,
    UnivariatePolynomial CombinedPolynomial,
    RootIsolationCertificate RootIsolation,
    ImmutableArray<CellWitness> Cells,
    RealSet Result);

internal static class CellDecomposer
{
    public static CellDecompositionCertificate Decompose(
        PolynomialFormula formula,
        ResourceBudget budget)
    {
        ImmutableArray<UnivariatePolynomial> atoms = PolynomialFormulaConverter.Atoms(formula);
        UnivariatePolynomial combined = UnivariatePolynomial.One;
        foreach (UnivariatePolynomial atom in atoms)
        {
            combined = combined.Multiply(atom, budget);
        }

        combined = combined.SquareFreePart(budget).PrimitivePositive(budget);
        RootIsolationCertificate roots = SturmRootIsolator.Isolate(combined, budget);
        ImmutableArray<CellWitness> cells = BuildWitnesses(formula, atoms, roots.Roots, budget);
        RealSet result = BuildSet(roots.Roots, cells);
        return new CellDecompositionCertificate(formula, atoms, combined, roots, cells, result);
    }

    public static bool Verify(
        CellDecompositionCertificate certificate,
        ResourceBudget budget)
    {
        ImmutableArray<UnivariatePolynomial> expectedAtoms =
            PolynomialFormulaConverter.Atoms(certificate.Formula);
        if (!SamePolynomials(expectedAtoms, certificate.Atoms))
        {
            return false;
        }

        UnivariatePolynomial combined = UnivariatePolynomial.One;
        foreach (UnivariatePolynomial atom in expectedAtoms)
        {
            combined = combined.Multiply(atom, budget);
        }

        combined = combined.SquareFreePart(budget).PrimitivePositive(budget);
        if (!combined.Equals(certificate.CombinedPolynomial) ||
            !SturmRootIsolator.Verify(certificate.RootIsolation, budget) ||
            !certificate.RootIsolation.Polynomial.Equals(combined))
        {
            return false;
        }

        ImmutableArray<CellWitness> expected = BuildWitnesses(
            certificate.Formula,
            expectedAtoms,
            certificate.RootIsolation.Roots,
            budget);
        if (expected.Length != certificate.Cells.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    expected[index].Canonical,
                    certificate.Cells[index].Canonical,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        RealSet result = BuildSet(certificate.RootIsolation.Roots, expected);
        return string.Equals(result.Canonical, certificate.Result.Canonical, StringComparison.Ordinal);
    }

    private static ImmutableArray<CellWitness> BuildWitnesses(
        PolynomialFormula formula,
        ImmutableArray<UnivariatePolynomial> atoms,
        ImmutableArray<ExactReal> roots,
        ResourceBudget budget)
    {
        var witnesses = ImmutableArray.CreateBuilder<CellWitness>((roots.Length * 2) + 1);
        for (int gap = 0; gap <= roots.Length; gap++)
        {
            BigRational sample = SampleGap(roots, gap);
            ImmutableArray<int> intervalSigns = atoms
                .Select(polynomial => polynomial.Evaluate(sample, budget).Sign)
                .ToImmutableArray();
            bool intervalIncluded = Evaluate(formula, atoms, intervalSigns);
            witnesses.Add(new CellWitness(
                CellKind.OpenInterval,
                gap,
                sample,
                intervalSigns,
                intervalIncluded));
            budget.AddCells(1);

            if (gap == roots.Length)
            {
                continue;
            }

            ImmutableArray<int> pointSigns = atoms
                .Select(polynomial => SignAt(polynomial, roots[gap], budget))
                .ToImmutableArray();
            bool pointIncluded = Evaluate(formula, atoms, pointSigns);
            witnesses.Add(new CellWitness(
                CellKind.Point,
                gap,
                null,
                pointSigns,
                pointIncluded));
            budget.AddCells(1);
        }

        return witnesses.MoveToImmutable();
    }

    private static bool Evaluate(
        PolynomialFormula formula,
        ImmutableArray<UnivariatePolynomial> atoms,
        ImmutableArray<int> signs)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < atoms.Length; index++)
        {
            map.Add(atoms[index].Canonical, signs[index]);
        }

        return PolynomialFormulaConverter.Evaluate(formula, map);
    }

    private static int SignAt(
        UnivariatePolynomial polynomial,
        ExactReal root,
        ResourceBudget budget) => root switch
    {
        RationalReal rational => polynomial.Evaluate(rational.Value, budget).Sign,
        AlgebraicReal algebraic => SturmRootIsolator.SignAtIsolatedRoot(
            algebraic.Polynomial,
            polynomial,
            algebraic.IsolatingInterval,
            budget),
        _ => throw new ArgumentOutOfRangeException(nameof(root))
    };

    private static BigRational SampleGap(ImmutableArray<ExactReal> roots, int gap)
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

        BigRational lower = UpperBound(roots[gap - 1]);
        BigRational upper = LowerBound(roots[gap]);
        if (lower >= upper)
        {
            throw new InvalidOperationException("Root isolating intervals were not pairwise disjoint.");
        }

        return (lower + upper) / 2;
    }

    private static RealSet BuildSet(
        ImmutableArray<ExactReal> roots,
        ImmutableArray<CellWitness> witnesses)
    {
        if (witnesses.All(static cell => cell.Included))
        {
            return AllRealSet.Instance;
        }

        if (witnesses.All(static cell => !cell.Included))
        {
            return EmptySet.Instance;
        }

        var components = new List<RealSet>();
        int index = 0;
        while (index < witnesses.Length)
        {
            if (!witnesses[index].Included)
            {
                index++;
                continue;
            }

            int start = index;
            while (index + 1 < witnesses.Length && witnesses[index + 1].Included)
            {
                index++;
            }

            int end = index;
            if (start == end && witnesses[start].Kind == CellKind.Point)
            {
                components.Add(new PointSet([roots[witnesses[start].Index]]));
                index++;
                continue;
            }

            (RealBound lower, bool includesLower) = LowerForRun(roots, witnesses[start]);
            (RealBound upper, bool includesUpper) = UpperForRun(roots, witnesses[end]);
            components.Add(new IntervalSet(lower, includesLower, upper, includesUpper));
            index++;
        }

        return RealSets.Union(components);
    }

    private static (RealBound Bound, bool Included) LowerForRun(
        ImmutableArray<ExactReal> roots,
        CellWitness cell) => cell.Kind switch
    {
        CellKind.OpenInterval when cell.Index == 0 => (RealBound.NegativeInfinity, false),
        CellKind.OpenInterval => (RealBound.Finite(roots[cell.Index - 1]), false),
        CellKind.Point => (RealBound.Finite(roots[cell.Index]), true),
        _ => throw new ArgumentOutOfRangeException(nameof(cell))
    };

    private static (RealBound Bound, bool Included) UpperForRun(
        ImmutableArray<ExactReal> roots,
        CellWitness cell) => cell.Kind switch
    {
        CellKind.OpenInterval when cell.Index == roots.Length => (RealBound.PositiveInfinity, false),
        CellKind.OpenInterval => (RealBound.Finite(roots[cell.Index]), false),
        CellKind.Point => (RealBound.Finite(roots[cell.Index]), true),
        _ => throw new ArgumentOutOfRangeException(nameof(cell))
    };

    private static BigRational LowerBound(ExactReal root) => root switch
    {
        RationalReal rational => rational.Value,
        AlgebraicReal algebraic => algebraic.IsolatingInterval.Lower,
        _ => throw new ArgumentOutOfRangeException(nameof(root))
    };

    private static BigRational UpperBound(ExactReal root) => root switch
    {
        RationalReal rational => rational.Value,
        AlgebraicReal algebraic => algebraic.IsolatingInterval.Upper,
        _ => throw new ArgumentOutOfRangeException(nameof(root))
    };

    private static bool SamePolynomials(
        ImmutableArray<UnivariatePolynomial> left,
        ImmutableArray<UnivariatePolynomial> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
