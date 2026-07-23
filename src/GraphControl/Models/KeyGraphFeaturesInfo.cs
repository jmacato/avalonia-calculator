using Graphing;

namespace GraphControl;

public sealed class KeyGraphFeaturesInfo
{
    public KeyGraphFeaturesInfo(GraphFunctionAnalysisData data)
    {
        Data = data;
    }

    public KeyGraphFeaturesInfo(AnalysisErrorType analysisError)
    {
        Data = GraphFunctionAnalysisData.Empty;
        AnalysisError = analysisError;
    }

    public GraphFunctionAnalysisData Data { get; }
    public GraphFunctionAnalysisMathDocuments Documents =>
        Data.Documents ?? GraphFunctionAnalysisMathDocuments.Empty;
    public AnalysisErrorType AnalysisError { get; }
    public string Domain => Data.Domain;
    public string Range => Data.Range;
    public IReadOnlyList<string> Minima => Data.Minima;
    public IReadOnlyList<string> Maxima => Data.Maxima;

    public bool HasDisplayablePeriod =>
        Data.PeriodicityDirection == (int)Graphing.Analyzer.FunctionPeriodicityType.Periodic &&
        !string.IsNullOrEmpty(Data.PeriodicityExpression);
}
