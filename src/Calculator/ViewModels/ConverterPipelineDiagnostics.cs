namespace CalculatorApp.ViewModel;

/// <summary>
/// Allocation-free cross-thread state for diagnosing converter startup in the
/// browser. The stage is written last so a reader never observes a new stage
/// paired with the preceding stage's timestamp or thread id.
/// </summary>
public static class ConverterPipelineDiagnostics
{
    private static int s_stage;
    private static int s_stageTick;
    private static int s_stageThreadId;
    private static int s_uiThreadId;
    private static int s_requestCount;
    private static int s_completionCount;
    private static int s_failureCount;
    private static int s_failureKind;
    private static int s_pageCount;
    private static int s_rootPageId;
    private static int s_lastNavigatedPageId;
    private static int s_lastNavigatedMode;

    public static void Record(int stage)
    {
        Volatile.Write(ref s_stageThreadId, Environment.CurrentManagedThreadId);
        Volatile.Write(ref s_stageTick, Environment.TickCount);
        Volatile.Write(ref s_stage, stage);
    }

    public static void RecordUiThread(int threadId) =>
        Volatile.Write(ref s_uiThreadId, threadId);

    public static void RecordRequest()
    {
        Interlocked.Increment(ref s_requestCount);
        Record(1);
    }

    public static void RecordCompletion()
    {
        Interlocked.Increment(ref s_completionCount);
        Record(16);
    }

    public static void RecordFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Volatile.Write(ref s_failureKind, GetFailureKind(exception));
        Interlocked.Increment(ref s_failureCount);
        Record(-1);
    }

    public static int[] Capture() =>
    [
        Volatile.Read(ref s_stage),
        Volatile.Read(ref s_stageTick),
        Volatile.Read(ref s_stageThreadId),
        Volatile.Read(ref s_uiThreadId),
        Volatile.Read(ref s_requestCount),
        Volatile.Read(ref s_completionCount),
        Volatile.Read(ref s_failureCount),
        Volatile.Read(ref s_failureKind)
    ];

    public static int RecordPageCreated() => Interlocked.Increment(ref s_pageCount);

    public static void RecordRootPage(int pageId) =>
        Volatile.Write(ref s_rootPageId, pageId);

    public static void RecordNavigation(int pageId, int mode)
    {
        Volatile.Write(ref s_lastNavigatedPageId, pageId);
        Volatile.Write(ref s_lastNavigatedMode, mode);
    }

    public static int[] CapturePageState() =>
    [
        Volatile.Read(ref s_pageCount),
        Volatile.Read(ref s_rootPageId),
        Volatile.Read(ref s_lastNavigatedPageId),
        Volatile.Read(ref s_lastNavigatedMode)
    ];

    internal static bool CanReport(Exception exception) =>
        exception is not OutOfMemoryException and
        not AccessViolationException and
        not StackOverflowException;

    private static int GetFailureKind(Exception exception) => exception switch
    {
        ArgumentException => 1,
        FormatException => 2,
        InvalidDataException => 3,
        ObjectDisposedException => 7,
        InvalidOperationException => 4,
        KeyNotFoundException => 5,
        NotSupportedException => 6,
        OperationCanceledException => 8,
        OverflowException => 9,
        System.Resources.MissingManifestResourceException => 10,
        _ => 255
    };
}
