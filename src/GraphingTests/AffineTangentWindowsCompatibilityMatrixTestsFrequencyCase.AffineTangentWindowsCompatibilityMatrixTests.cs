using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

internal sealed record AffineTangentWindowsCompatibilityMatrixTestsFrequencyCase(string Id, string Argument, string ExpectedPeriod, bool IsNegative, bool IsPi);
