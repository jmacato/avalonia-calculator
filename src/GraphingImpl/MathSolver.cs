using GraphingImpl;

namespace Graphing;

/// <summary>Managed replacement for the native static solver constructor.</summary>
public static class MathSolver
{
    private static IMathSolverFactory s_factory = new ManagedMathSolverFactory();

    public static IMathSolver CreateMathSolver()
    {
        return Volatile.Read(ref s_factory).CreateMathSolver();
    }

    public static IMathSolver CreateMathSolver(IMathSolverFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory.CreateMathSolver();
    }

    public static void SetFactory(IMathSolverFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Volatile.Write(ref s_factory, factory);
    }
}
