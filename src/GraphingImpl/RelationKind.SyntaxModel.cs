using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal enum RelationKind
{
    None,
    Equal,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual
}
