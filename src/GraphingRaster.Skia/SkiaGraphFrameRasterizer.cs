using Graphing;
using Graphing.Renderer;
using SkiaSharp;

namespace GraphingRaster.Skia;

public static class SkiaGraphFrameRasterizer
{
    public static ReadOnlyMemory<byte> EncodePng(GraphFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        using var target = new SkiaGraphDrawingTarget();
        target.BeginFrame(frame);
        foreach (GraphFrameCommand command in frame.Commands)
        {
            target.Draw(command);
        }

        target.EndFrame();
        return target.GetPngData();
    }
}
