using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ExactCoefficientEvidence
{
    public const string Rule = "ordered-exact-coefficient-theorem";
    public static bool TryCreate(ExactCoefficientPattern pattern, ResourceBudget budget, out ImmutableArray<ExactOrderWitness> witnesses)
    {
        if (!TryCollectScalars(pattern, budget, out ImmutableArray<ExactScalar> scalars))
        {
            witnesses = default;
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<ExactOrderWitness>(scalars.Length);
        foreach (ExactScalar scalar in scalars)
        {
            budget.Charge();
            if (!ExactScalarOrder.TryEnclose(scalar.Value, budget, out RationalEnclosure enclosure) || !EnclosureProvesSign(enclosure, scalar.Sign))
            {
                witnesses = default;
                return false;
            }

            builder.Add(new ExactOrderWitness(scalar.Canonical, enclosure.Lower, enclosure.Upper, scalar.Sign));
        }

        witnesses = builder.MoveToImmutable();
        return true;
    }

    public static bool Verify(ExactCoefficientPattern pattern, ImmutableArray<ExactOrderWitness> witnesses, ResourceBudget budget)
    {
        if (!TryCreate(pattern, budget, out ImmutableArray<ExactOrderWitness> expected) || expected.Length != witnesses.Length)
        {
            return false;
        }

        for (int i = 0; i < expected.Length; i++)
        {
            budget.Charge();
            if (expected[i] != witnesses[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCollectScalars(ExactCoefficientPattern pattern, ResourceBudget budget, out ImmutableArray<ExactScalar> scalars)
    {
        var values = pattern.BaseScalars.ToList();
        switch (pattern)
        {
            case ExactRationalPattern { Kind: ExactCoefficientPatternKind.QuadraticPolynomial } quadratic:
                if (!ExactCoefficientMath.TryDiscriminant(quadratic.Numerator, budget, out ExactScalar discriminant))
                {
                    scalars = default;
                    return false;
                }

                values.Add(discriminant);
                break;
            case ExactRationalPattern { Kind: ExactCoefficientPatternKind.Mobius } mobius:
                if (!ExactCoefficientMath.TryDeterminant(mobius.Numerator, mobius.Denominator, budget, out ExactScalar determinant))
                {
                    scalars = default;
                    return false;
                }

                values.Add(determinant);
                break;
            case ExactTrigPattern trig when !trig.Shift.IsZero:
                values.Add(trig.Shift.Negate().Multiply(trig.Amplitude.Reciprocal(budget), budget));
                break;
        }

        scalars = values.DistinctBy(static scalar => scalar.Canonical).OrderBy(static scalar => scalar.Canonical, StringComparer.Ordinal).ToImmutableArray();
        return true;
    }

    private static bool EnclosureProvesSign(RationalEnclosure enclosure, int sign)
    {
        return sign switch
        {
            < 0 => enclosure.Upper < BigRational.Zero,
            0 => enclosure.Lower.IsZero && enclosure.Upper.IsZero,
            > 0 => enclosure.Lower > BigRational.Zero
        };
    }
}
