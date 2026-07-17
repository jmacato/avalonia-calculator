using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    namespace Analyzer
    {
        public interface IGraphAnalyzer
        {
            bool CanFunctionAnalysisBePerformed(out bool variableIsNotX);
            GraphStatus PerformFunctionAnalysis(uint analysisType);
            GraphStatus GetAnalysisTypeCaption(AnalysisType type, out string caption);
            GraphStatus GetMessage(GraphAnalyzerMessage message, out string text);
        }
    }
}
