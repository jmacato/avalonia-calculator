using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal enum ExactPredicate
{
    IsInteger,
    IsOddInteger,
    IsLocallyConstant,
    PowerIsContinuous,
    PowerIsDifferentiable,
    RootIsContinuous,
    RootIsDifferentiable,
    FunctionIsDefined,
    FunctionIsContinuous,
    FunctionIsDifferentiable
}
