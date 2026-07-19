using System.Runtime.InteropServices;

namespace CalculatorApp;

internal static class DispatcherExceptionRecoveryGate
{
    internal const int RecoveryWindowMilliseconds = 2_000;
    internal const int MaximumRecoveriesPerWindow = 3;

    private const int MaximumInnerExceptionDepth = 16;
    private static long s_windowState;

    internal static bool TryRecover(Exception exception, out int occurrence) =>
        TryRecover(exception, Environment.TickCount, out occurrence);

    internal static bool TryRecover(
        Exception exception,
        int currentTick,
        out int occurrence)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (ContainsImmediatelyFatalException(exception, 0))
        {
            occurrence = 0;
            return false;
        }

        while (true)
        {
            long observedState = Interlocked.Read(ref s_windowState);
            int windowStartedAt = unchecked((int)(observedState >> 32));
            uint recoveryCount = unchecked((uint)observedState);
            uint elapsed = unchecked((uint)(currentTick - windowStartedAt));

            if (observedState == 0 || elapsed >= RecoveryWindowMilliseconds)
            {
                long resetState = PackState(currentTick, 1);
                if (Interlocked.CompareExchange(
                        ref s_windowState,
                        resetState,
                        observedState) == observedState)
                {
                    occurrence = 1;
                    return true;
                }

                continue;
            }

            occurrence = checked((int)recoveryCount + 1);
            if (recoveryCount >= MaximumRecoveriesPerWindow)
            {
                return false;
            }

            long incrementedState = PackState(
                windowStartedAt,
                recoveryCount + 1);
            if (Interlocked.CompareExchange(
                    ref s_windowState,
                    incrementedState,
                    observedState) == observedState)
            {
                return true;
            }
        }
    }

    internal static void ResetForTests() =>
        Interlocked.Exchange(ref s_windowState, 0);

    private static bool ContainsImmediatelyFatalException(
        Exception exception,
        int depth)
    {
        if (exception is OutOfMemoryException or
            StackOverflowException or
            AccessViolationException or
            BadImageFormatException or
            SEHException)
        {
            return true;
        }

        if (depth >= MaximumInnerExceptionDepth)
        {
            return false;
        }

        if (exception is AggregateException aggregateException)
        {
            foreach (Exception innerException in aggregateException.InnerExceptions)
            {
                if (ContainsImmediatelyFatalException(innerException, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        return exception.InnerException is { } inner &&
            ContainsImmediatelyFatalException(inner, depth + 1);
    }

    private static long PackState(int windowStartedAt, uint recoveryCount) =>
        unchecked((long)(((ulong)(uint)windowStartedAt << 32) | recoveryCount));
}
