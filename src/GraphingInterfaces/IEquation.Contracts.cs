using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    public interface IEquation
    {
        IEquationOptions GetGraphEquationOptions();
        uint GetGraphEquationID();
        bool TrySelectEquation();
        bool IsEquationSelected();
    }
}
