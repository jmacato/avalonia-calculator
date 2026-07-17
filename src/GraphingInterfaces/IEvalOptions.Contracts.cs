using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    public interface IEvalOptions
    {
        EvalTrigUnitMode GetTrigUnitMode();
        void SetTrigUnitMode(EvalTrigUnitMode value);
    }
}
