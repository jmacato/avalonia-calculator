using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record EmptySet : RealSet
{
    public static EmptySet Instance { get; } = new();

    private EmptySet()
    {
    }

    public override string Canonical => "empty";
    public override bool IsEmpty => true;
}
