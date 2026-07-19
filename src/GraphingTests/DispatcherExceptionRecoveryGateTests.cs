using CalculatorApp;

namespace GraphingTests;

public sealed class DispatcherExceptionRecoveryGateTests
{
    [Fact]
    public void RecoveryPolicyIsBoundedAndRejectsRuntimeCorruption()
    {
        DispatcherExceptionRecoveryGate.ResetForTests();

        for (int occurrence = 1;
             occurrence <= DispatcherExceptionRecoveryGate.MaximumRecoveriesPerWindow;
             occurrence++)
        {
            Assert.True(DispatcherExceptionRecoveryGate.TryRecover(
                new InvalidCastException(),
                1_000,
                out int actualOccurrence));
            Assert.Equal(occurrence, actualOccurrence);
        }

        Assert.False(DispatcherExceptionRecoveryGate.TryRecover(
            new InvalidCastException(),
            1_001,
            out int rejectedOccurrence));
        Assert.Equal(
            DispatcherExceptionRecoveryGate.MaximumRecoveriesPerWindow + 1,
            rejectedOccurrence);

        DispatcherExceptionRecoveryGate.ResetForTests();

        Assert.True(DispatcherExceptionRecoveryGate.TryRecover(
            new InvalidOperationException(),
            int.MaxValue - 500,
            out int firstOccurrence));
        Assert.True(DispatcherExceptionRecoveryGate.TryRecover(
            new InvalidOperationException(),
            int.MinValue + 1_600,
            out int resetOccurrence));

        Assert.Equal(1, firstOccurrence);
        Assert.Equal(1, resetOccurrence);

        DispatcherExceptionRecoveryGate.ResetForTests();

        Exception[] fatalExceptions =
        [
            new BadImageFormatException(),
            new AggregateException(
                new InvalidOperationException(),
                new BadImageFormatException()),
        ];

        foreach (Exception exception in fatalExceptions)
        {
            Assert.False(DispatcherExceptionRecoveryGate.TryRecover(
                exception,
                1_000,
                out int occurrence));
            Assert.Equal(0, occurrence);
        }
    }
}
