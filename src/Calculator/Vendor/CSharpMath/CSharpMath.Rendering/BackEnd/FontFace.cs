using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Media;
using HarfBuzzSharp;
using AvaloniaGlyphInfo = Avalonia.Media.TextFormatting.GlyphInfo;
using HarfBuzzFont = HarfBuzzSharp.Font;

namespace CSharpMath.Rendering.BackEnd;

internal sealed class FontFace : IDisposable
{
    private readonly ConcurrentDictionary<ushort, Rect> _inkBounds = new();
    private readonly Face _harfBuzzFace;
    private readonly HarfBuzzFont _harfBuzzFont;
    private int _disposed;
    private FontFace(GlyphTypeface typeface)
    {
        Typeface = typeface;
        DesignEmHeight = typeface.Metrics.DesignEmHeight;
        if (!typeface.PlatformTypeface.TryGetStream(out Stream? stream))
        {
            throw new InvalidDataException($"The font '{typeface.FamilyName}' does not expose its font data.");
        }

        using (stream)
        using (Blob blob = Blob.FromStream(stream))
        {
            _harfBuzzFace = new Face(blob, 0);
        }

        _harfBuzzFont = new HarfBuzzFont(_harfBuzzFace);
        _harfBuzzFont.SetScale(_harfBuzzFace.UnitsPerEm, _harfBuzzFace.UnitsPerEm);
        HasMathData = HarfBuzzMath.HasData(_harfBuzzFace.Handle);
    }

    // HarfBuzz fonts carry mutable native scale/cache state. A face therefore
    // belongs to one MathPainter instead of being shared by every math view in
    // the process; sharing allowed a retiring view to corrupt a newly measured
    // view during rapid equation changes.
    internal static FontFace Get(GlyphTypeface typeface) => new(typeface);
    internal GlyphTypeface Typeface { get; }
    internal float DesignEmHeight { get; }
    internal bool HasMathData { get; }

    internal ushort FindGlyph(int codepoint)
        => Typeface.CharacterToGlyphMap.TryGetGlyph(codepoint, out ushort glyphId)
            ? glyphId
            : (ushort)0;

    internal float GetAdvance(ushort glyphId)
    {
        if (!Typeface.TryGetHorizontalGlyphAdvance(glyphId, out ushort advance))
        {
            throw new InvalidDataException($"The font '{Typeface.FamilyName}' has no horizontal advance for glyph {glyphId}.");
        }

        return advance;
    }

    internal Rect GetInkBounds(ushort glyphId)
        => _inkBounds.GetOrAdd(glyphId, CreateInkBounds);

    private Rect CreateInkBounds(ushort glyphId)
    {
        var glyphInfo = new AvaloniaGlyphInfo(glyphId, 0, GetAdvance(glyphId));
        using var glyphRun = new GlyphRun(Typeface, DesignEmHeight, ReadOnlyMemory<char>.Empty, new[] { glyphInfo }, new Point(0, 0));
        return glyphRun.InkBounds;
    }

    internal int GetConstant(OpenTypeMathConstant constant) =>
        WithHarfBuzzFont(handle => HarfBuzzMath.GetConstant(handle, constant));

    internal int GetItalicCorrection(ushort glyphId) =>
        WithHarfBuzzFont(handle => HarfBuzzMath.GetItalicCorrection(handle, glyphId));

    internal int GetTopAccentAttachment(ushort glyphId) =>
        WithHarfBuzzFont(handle => HarfBuzzMath.GetTopAccentAttachment(handle, glyphId));

    internal int GetMinConnectorOverlap(Direction direction) =>
        WithHarfBuzzFont(handle => HarfBuzzMath.GetMinConnectorOverlap(handle, direction));

    internal OpenTypeMathGlyphVariant[] GetVariants(ushort glyphId, Direction direction) =>
        WithHarfBuzzFont(handle => HarfBuzzMath.GetVariants(handle, glyphId, direction));

    internal OpenTypeMathGlyphPart[] GetAssembly(ushort glyphId, Direction direction) =>
        WithHarfBuzzFont(handle => HarfBuzzMath.GetAssembly(handle, glyphId, direction));

    private TResult WithHarfBuzzFont<TResult>(Func<IntPtr, TResult> action)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        return action(_harfBuzzFont.Handle);
    }

    ~FontFace() => Dispose();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _harfBuzzFont.Dispose();
        _harfBuzzFace.Dispose();
        GC.SuppressFinalize(this);
    }
}
