using Graphing;
using Graphing.Renderer;
using SkiaSharp;

namespace GraphingRaster.Skia;

public sealed class PngGraphBitmap : IBitmap
{
    private readonly ReadOnlyMemory<byte> _data;
    public PngGraphBitmap(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("PNG data cannot be empty.", nameof(data));
        }

        _data = data.ToArray();
    }

    public ReadOnlyMemory<byte> GetData() => _data;
}
