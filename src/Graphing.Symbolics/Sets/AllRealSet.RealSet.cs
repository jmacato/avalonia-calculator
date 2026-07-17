using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record AllRealSet : RealSet
{
    public static AllRealSet Instance { get; } = new();

    private AllRealSet()
    {
    }

    public override string Canonical => "reals";
}
