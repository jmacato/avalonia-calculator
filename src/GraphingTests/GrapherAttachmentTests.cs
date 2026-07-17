using Avalonia.Headless.XUnit;
using Avalonia.Media;
using GraphControl;

namespace GraphingTests;

public sealed class GrapherAttachmentTests
{
    [AvaloniaFact]
    public void DetachedStyledPropertyUpdatesDoNotStartGraphPreparation()
    {
        using var grapher = new Grapher();

        grapher.ForceProportionalAxes = false;
        grapher.AxesColor = Colors.Red;
        grapher.GraphBackground = Colors.Black;
        grapher.GridLinesColor = Colors.Gray;
        grapher.LineWidth = 3;

        Assert.True(grapher.IsGraphPreparationDeferred);
    }
}
