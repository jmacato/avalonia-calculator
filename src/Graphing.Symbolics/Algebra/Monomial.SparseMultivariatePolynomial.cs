using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed class Monomial : IEquatable<Monomial>, IComparable<Monomial>
{
    public Monomial(IEnumerable<int> exponents)
    {
        Exponents = exponents.ToImmutableArray();
        if (Exponents.Any(static exponent => exponent < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(exponents));
        }
    }

    public ImmutableArray<int> Exponents { get; }

    public int TotalDegree
    {
        get
        {
            int degree = 0;
            foreach (int exponent in Exponents)
            {
                degree = checked(degree + exponent);
            }

            return degree;
        }
    }

    public Monomial Multiply(Monomial other)
    {
        if (Exponents.Length != other.Exponents.Length)
        {
            throw new ArgumentException("Monomials must use the same variable count.", nameof(other));
        }

        return new Monomial(Exponents.Zip(other.Exponents, checked((left, right) => left + right)));
    }

    public Monomial Differentiate(int variable)
    {
        int[] exponents = Exponents.ToArray();
        exponents[variable]--;
        return new Monomial(exponents);
    }

    public int CompareTo(Monomial? other)
    {
        if (other is null)
        {
            return 1;
        }

        int degree = TotalDegree.CompareTo(other.TotalDegree);
        if (degree != 0)
        {
            return degree;
        }

        int length = Exponents.Length.CompareTo(other.Exponents.Length);
        if (length != 0)
        {
            return length;
        }

        for (int index = 0; index < Exponents.Length; index++)
        {
            int comparison = Exponents[index].CompareTo(other.Exponents[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    public bool Equals(Monomial? other) => CompareTo(other) == 0;
    public override bool Equals(object? obj) => Equals(obj as Monomial);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (int exponent in Exponents)
        {
            hash.Add(exponent);
        }

        return hash.ToHashCode();
    }
}
