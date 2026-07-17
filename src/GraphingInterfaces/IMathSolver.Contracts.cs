using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    public interface IMathSolver
    {
        IParsingOptions ParsingOptions();
        IEvalOptions EvalOptions();
        IFormatOptions FormatOptions();
        IExpression? ParseInput(string input, out int errorCode, out int errorType);
        void HRErrorToErrorInfo(GraphStatus status, out int errorCode, out int errorType);
        IGraph CreateGrapher(IExpression expression);
        IGraph CreateGrapher();
        string Serialize(IExpression expression);
        GraphFunctionAnalysisData Analyze(IGraphAnalyzer analyzer);
    }
}
