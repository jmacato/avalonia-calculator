using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingTests;

internal sealed record MonotoneTrigonometricPhaseAnalysisTestsPublicCase(string Formula, string Domain, string Zeros, string Minima, string Maxima, FunctionParityType Parity);
