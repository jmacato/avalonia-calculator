using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CalculatorApp.ViewModel;

/// <summary>
/// Supplies explicit managed stops for diagnostic browser AOT builds. Mono's
/// WebAssembly LLVM backend does not emit soft-debug sequence points, but its
/// debugger agent can still unwind a managed AOT stack from Debugger.Break().
/// Calls to this type are removed from every normal build by Conditional.
/// </summary>
internal static class ThreadedManagedDebugging
{
    private static int s_lastCheckpoint;

    public static int LastCheckpoint => Volatile.Read(ref s_lastCheckpoint);

    [Conditional("CALCULATOR_THREADED_MANAGED_DEBUG")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Checkpoint(int checkpoint)
    {
        Volatile.Write(ref s_lastCheckpoint, checkpoint);
        Debugger.Break();
    }
}
