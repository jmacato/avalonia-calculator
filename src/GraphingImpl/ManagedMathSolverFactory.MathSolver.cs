using Graphing;
using Graphing.Analyzer;

namespace GraphingImpl;

public sealed class ManagedMathSolverFactory : IMathSolverFactory
{
    public IMathSolver CreateMathSolver() => new ManagedMathSolver();
}
