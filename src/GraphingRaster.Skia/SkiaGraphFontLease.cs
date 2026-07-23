using SkiaSharp;

namespace GraphingRaster.Skia;

internal readonly struct SkiaGraphFontLease(SkiaGraphFontCacheEntry entry, bool owns) : IDisposable
{
    private readonly SkiaGraphFontCacheEntry? _owned = owns ? entry : null;

    public SKFont Font { get; } = entry.Font;

    public void Dispose()
    {
        _owned?.Dispose();
    }
}
