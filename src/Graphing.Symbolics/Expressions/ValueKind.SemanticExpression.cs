using System.Collections.Immutable;
using System.Globalization;

namespace Graphing.Symbolics;

internal enum ValueKind
{
    Constant,
    Variable,
    SymbolicConstant,
    Add,
    Subtract,
    Multiply,
    Divide,
    Power,
    Negate,
    Function
}
