namespace Graphing;

/// <summary>
/// Carries the inputs and output for a closest-point query across the graph
/// renderer boundary without expanding the managed ABI into many arguments.
/// </summary>
public record struct ClosePointRequest(double ScreenPointX, double ScreenPointY, double Precision)
{
    public ClosestPointData Result { get; set; } = ClosestPointData.Unavailable;
}
