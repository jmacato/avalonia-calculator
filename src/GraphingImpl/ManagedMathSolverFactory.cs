using Graphing;

namespace GraphingImpl;

public sealed class ManagedMathSolverFactory : IMathSolverFactory
{
    public IMathSolver CreateMathSolver()
    {
        return new ManagedMathSolver();
    }
}
