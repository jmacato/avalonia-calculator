using Graphing.Analyzer;
using Graphing.Renderer;

namespace Graphing
{
    public interface IMathSolverFactory
    {
        IMathSolver CreateMathSolver();
    }
}
