using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Graphing;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingImpl;

internal sealed record ManagedGraphAnalyzerAnalysisResult(long Revision, GraphFunctionAnalysisData Data);
