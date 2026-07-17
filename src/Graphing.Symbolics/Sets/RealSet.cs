using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal abstract record RealSet
{
    public abstract string Canonical { get; }
    public virtual bool IsEmpty => false;
}
