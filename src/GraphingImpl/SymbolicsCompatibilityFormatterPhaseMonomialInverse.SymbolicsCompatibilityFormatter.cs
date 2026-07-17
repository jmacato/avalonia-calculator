using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Graphing.Symbolics;

namespace GraphingImpl;

internal readonly record struct SymbolicsCompatibilityFormatterPhaseMonomialInverse(BigRational Coefficient, BigRational Exponent, BigRational Constant);
