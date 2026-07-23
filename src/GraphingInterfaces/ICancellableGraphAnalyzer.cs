namespace Graphing.Analyzer;

/// <summary>
/// Managed extension for callers that need to abandon an in-flight analysis.
/// The native-compatible <see cref="IGraphAnalyzer"/> contract remains unchanged.
/// </summary>
public interface ICancellableGraphAnalyzer : IGraphAnalyzer
{
    GraphStatus PerformFunctionAnalysis(
        uint analysisType,
        CancellationToken cancellationToken);
}
