using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

internal readonly record struct AffineMinMaxWindowsCompatibilityMatrixTestsLineSpec(BigRational Slope, BigRational Intercept);
