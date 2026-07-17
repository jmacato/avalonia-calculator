using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal enum AstKind
{
    Number,
    Variable,
    Add,
    Subtract,
    Multiply,
    Divide,
    Power,
    Negate,
    Function
}
