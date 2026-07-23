namespace Graphing;

public static class GraphPipelineDiagnostics
{
    private static long s_requestedGeneration;
    private static long s_workerGeneration;
    private static long s_completedGeneration;
    private static long s_publishedGeneration;
    private static long s_committedGeneration;
    private static long s_requestCount;
    private static long s_workerStartCount;
    private static long s_workerCompletionCount;
    private static long s_commitCount;
    private static long s_commitMissCount;
    private static long s_settlementTimerCount;
    private static long s_settlementRequestCount;
    private static long s_renderCount;
    private static long s_rendererCreatedCount;
    private static long s_rendererDisposedCount;
    private static int s_workerActive;
    private static int s_rendererActiveCount;
    private static int s_completedStatus;
    private static int s_commitStatus;

    public static void RecordRequest(long generation)
    {
        Volatile.Write(ref s_requestedGeneration, generation);
        Interlocked.Increment(ref s_requestCount);
    }

    public static void RecordWorkerStart(long generation)
    {
        Volatile.Write(ref s_workerGeneration, generation);
        Volatile.Write(ref s_workerActive, 1);
        Interlocked.Increment(ref s_workerStartCount);
    }

    public static void RecordWorkerCompletion(long generation, GraphStatus status)
    {
        Volatile.Write(ref s_completedGeneration, generation);
        Volatile.Write(ref s_completedStatus, status.Value);
        Volatile.Write(ref s_workerActive, 0);
        Interlocked.Increment(ref s_workerCompletionCount);
    }

    public static void RecordPublication(long generation)
    {
        Volatile.Write(ref s_publishedGeneration, generation);
    }

    public static void RecordCommit(long generation, GraphStatus status)
    {
        Volatile.Write(ref s_committedGeneration, generation);
        Volatile.Write(ref s_commitStatus, status.Value);
        Interlocked.Increment(ref s_commitCount);
    }

    public static void RecordCommitMiss()
    {
        Interlocked.Increment(ref s_commitMissCount);
    }

    public static void RecordSettlementTimer()
    {
        Interlocked.Increment(ref s_settlementTimerCount);
    }

    public static void RecordSettlementRequest()
    {
        Interlocked.Increment(ref s_settlementRequestCount);
    }

    public static void RecordRender()
    {
        Interlocked.Increment(ref s_renderCount);
    }

    public static void RecordRendererCreated()
    {
        Interlocked.Increment(ref s_rendererActiveCount);
        Interlocked.Increment(ref s_rendererCreatedCount);
    }

    public static void RecordRendererDisposed()
    {
        Interlocked.Decrement(ref s_rendererActiveCount);
        Interlocked.Increment(ref s_rendererDisposedCount);
    }

    public static int[] Capture()
    {
        return
        [
            ToInt32(Volatile.Read(ref s_requestedGeneration)),
            ToInt32(Volatile.Read(ref s_workerGeneration)),
            ToInt32(Volatile.Read(ref s_completedGeneration)),
            ToInt32(Volatile.Read(ref s_publishedGeneration)),
            ToInt32(Volatile.Read(ref s_committedGeneration)),
            Volatile.Read(ref s_workerActive),
            Volatile.Read(ref s_completedStatus),
            Volatile.Read(ref s_commitStatus),
            ToInt32(Volatile.Read(ref s_requestCount)),
            ToInt32(Volatile.Read(ref s_workerStartCount)),
            ToInt32(Volatile.Read(ref s_workerCompletionCount)),
            ToInt32(Volatile.Read(ref s_commitCount)),
            ToInt32(Volatile.Read(ref s_commitMissCount)),
            ToInt32(Volatile.Read(ref s_settlementTimerCount)),
            ToInt32(Volatile.Read(ref s_settlementRequestCount)),
            ToInt32(Volatile.Read(ref s_renderCount)),
            Volatile.Read(ref s_rendererActiveCount),
            ToInt32(Volatile.Read(ref s_rendererCreatedCount)),
            ToInt32(Volatile.Read(ref s_rendererDisposedCount))
        ];
    }

    private static int ToInt32(long value)
    {
        return unchecked((int)value);
    }
}
