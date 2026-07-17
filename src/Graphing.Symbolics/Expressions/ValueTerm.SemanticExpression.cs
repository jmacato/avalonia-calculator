using System.Collections.Immutable;
using System.Globalization;

namespace Graphing.Symbolics;

internal sealed class ValueTerm : IEquatable<ValueTerm>
{
    internal ValueTerm(int id, ValueKind kind, BigRational constant, string name, ImmutableArray<ValueTerm> operands, string canonical)
    {
        Id = id;
        Kind = kind;
        Constant = constant;
        Name = name;
        Operands = operands;
        Canonical = canonical;
    }

    public int Id { get; }
    public ValueKind Kind { get; }
    public BigRational Constant { get; }
    public string Name { get; }
    public ImmutableArray<ValueTerm> Operands { get; }
    public string Canonical { get; }

    public bool Equals(ValueTerm? other) => ReferenceEquals(this, other);
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
    public override int GetHashCode() => Id;
}
