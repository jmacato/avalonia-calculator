using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    public interface IGraph
    {
        IReadOnlyList<IEquation>? TryInitialize(IExpression? graphingExpression = null);
        GraphStatus GetInitializationError();
        IGraphingOptions GetOptions();
        IReadOnlyList<IVariable> GetVariables();
        void SetArgValue(string variableName, double value);
        IGraphRenderer GetRenderer();
        bool TryResetSelection();
        IGraphAnalyzer GetAnalyzer();
    }
}
