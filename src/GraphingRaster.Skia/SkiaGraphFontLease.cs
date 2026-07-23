using SkiaSharp;

namespace GraphingRaster.Skia;

internal readonly struct SkiaGraphFontLease : IDisposable
{
    private readonly SkiaGraphFontCacheEntry? _owned;

    public SkiaGraphFontLease(SkiaGraphFontCacheEntry entry, bool owns)
    {
        Font = entry.Font;
        _owned = owns ? entry : null;
    }

    public SKFont Font { get; }

    public void Dispose() => _owned?.Dispose();
}
