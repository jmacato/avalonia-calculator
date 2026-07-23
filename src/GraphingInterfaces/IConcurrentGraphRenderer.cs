namespace Graphing.Renderer;

public interface IConcurrentGraphRenderer
{
    bool IsPrepareGraphPending { get; }
    GraphStatus RequestPrepareGraph();
    GraphStatus TryCommitPreparedGraph(out bool completed);
    bool TryGetPreparedDisplayRanges(out double xMinimum, out double xMaximum, out double yMinimum, out double yMaximum);
    void ReleasePreparedResources();
}
