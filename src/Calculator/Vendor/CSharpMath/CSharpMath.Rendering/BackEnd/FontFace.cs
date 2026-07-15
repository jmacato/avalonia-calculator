using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using HarfBuzzSharp;
using AvaloniaGlyphInfo = Avalonia.Media.TextFormatting.GlyphInfo;
using HarfBuzzFont = HarfBuzzSharp.Font;

namespace CSharpMath.Rendering.BackEnd;

internal sealed class FontFace
{
    private static readonly ConditionalWeakTable<GlyphTypeface, FontFace> s_cache = new();
    private readonly ConcurrentDictionary<ushort, Rect> _inkBounds = new();
    private readonly Face _harfBuzzFace;
    private readonly HarfBuzzFont _harfBuzzFont;

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

    internal static FontFace Get(GlyphTypeface typeface) =>
        s_cache.GetValue(typeface, static value => new FontFace(value));

    internal GlyphTypeface Typeface { get; }

    internal float DesignEmHeight { get; }

    internal bool HasMathData { get; }

    internal ushort FindGlyph(int codepoint) =>
        Typeface.CharacterToGlyphMap.TryGetGlyph(codepoint, out ushort glyphId) ? glyphId : (ushort)0;

    internal float GetAdvance(ushort glyphId)
    {
        if (!Typeface.TryGetHorizontalGlyphAdvance(glyphId, out ushort advance))
        {
            throw new InvalidDataException(
                $"The font '{Typeface.FamilyName}' has no horizontal advance for glyph {glyphId}.");
        }

        return advance;
    }

    internal Rect GetInkBounds(ushort glyphId) =>
        _inkBounds.GetOrAdd(glyphId, CreateInkBounds);

    private Rect CreateInkBounds(ushort glyphId)
    {
        var glyphInfo = new AvaloniaGlyphInfo(glyphId, 0, GetAdvance(glyphId));
        using var glyphRun = new GlyphRun(
            Typeface,
            DesignEmHeight,
            ReadOnlyMemory<char>.Empty,
            new[] { glyphInfo },
            new Point(0, 0));
        return glyphRun.InkBounds;
    }

    internal int GetConstant(OpenTypeMathConstant constant) =>
        HarfBuzzMath.GetConstant(_harfBuzzFont.Handle, constant);

    internal int GetItalicCorrection(ushort glyphId) =>
        HarfBuzzMath.GetItalicCorrection(_harfBuzzFont.Handle, glyphId);

    internal int GetTopAccentAttachment(ushort glyphId) =>
        HarfBuzzMath.GetTopAccentAttachment(_harfBuzzFont.Handle, glyphId);

    internal int GetMinConnectorOverlap(Direction direction) =>
        HarfBuzzMath.GetMinConnectorOverlap(_harfBuzzFont.Handle, direction);

    internal OpenTypeMathGlyphVariant[] GetVariants(ushort glyphId, Direction direction) =>
        HarfBuzzMath.GetVariants(_harfBuzzFont.Handle, glyphId, direction);

    internal OpenTypeMathGlyphPart[] GetAssembly(ushort glyphId, Direction direction) =>
        HarfBuzzMath.GetAssembly(_harfBuzzFont.Handle, glyphId, direction);
}

internal static unsafe partial class HarfBuzzMath
{
    private const string LibraryName = "libHarfBuzzSharp";

    internal static bool HasData(IntPtr face) => hb_ot_math_has_data(face) != 0;

    internal static int GetConstant(IntPtr font, OpenTypeMathConstant constant) =>
        hb_ot_math_get_constant(font, constant);

    internal static int GetItalicCorrection(IntPtr font, uint glyph) =>
        hb_ot_math_get_glyph_italics_correction(font, glyph);

    internal static int GetTopAccentAttachment(IntPtr font, uint glyph) =>
        hb_ot_math_get_glyph_top_accent_attachment(font, glyph);

    internal static int GetMinConnectorOverlap(IntPtr font, Direction direction) =>
        hb_ot_math_get_min_connector_overlap(font, direction);

    internal static OpenTypeMathGlyphVariant[] GetVariants(
        IntPtr font,
        uint glyph,
        Direction direction)
    {
        uint count = 0;
        uint total = hb_ot_math_get_glyph_variants(font, glyph, direction, 0, &count, null);
        if (total == 0)
        {
            return [];
        }

        var variants = new OpenTypeMathGlyphVariant[checked((int)total)];
        count = total;
        fixed (OpenTypeMathGlyphVariant* variantsPointer = variants)
        {
            hb_ot_math_get_glyph_variants(font, glyph, direction, 0, &count, variantsPointer);
        }

        if (count != total)
        {
            Array.Resize(ref variants, checked((int)count));
        }

        return variants;
    }

    internal static OpenTypeMathGlyphPart[] GetAssembly(
        IntPtr font,
        uint glyph,
        Direction direction)
    {
        uint count = 0;
        int italicCorrection;
        uint total = hb_ot_math_get_glyph_assembly(
            font,
            glyph,
            direction,
            0,
            &count,
            null,
            &italicCorrection);
        if (total == 0)
        {
            return [];
        }

        var parts = new OpenTypeMathGlyphPart[checked((int)total)];
        count = total;
        fixed (OpenTypeMathGlyphPart* partsPointer = parts)
        {
            hb_ot_math_get_glyph_assembly(
                font,
                glyph,
                direction,
                0,
                &count,
                partsPointer,
                &italicCorrection);
        }

        if (count != total)
        {
            Array.Resize(ref parts, checked((int)count));
        }

        return parts;
    }

    [LibraryImport(LibraryName)]
    private static partial int hb_ot_math_has_data(IntPtr face);

    [LibraryImport(LibraryName)]
    private static partial int hb_ot_math_get_constant(IntPtr font, OpenTypeMathConstant constant);

    [LibraryImport(LibraryName)]
    private static partial int hb_ot_math_get_glyph_italics_correction(IntPtr font, uint glyph);

    [LibraryImport(LibraryName)]
    private static partial int hb_ot_math_get_glyph_top_accent_attachment(IntPtr font, uint glyph);

    [LibraryImport(LibraryName)]
    private static partial int hb_ot_math_get_min_connector_overlap(IntPtr font, Direction direction);

    [LibraryImport(LibraryName)]
    private static partial uint hb_ot_math_get_glyph_variants(
        IntPtr font,
        uint glyph,
        Direction direction,
        uint startOffset,
        uint* variantsCount,
        OpenTypeMathGlyphVariant* variants);

    [LibraryImport(LibraryName)]
    private static partial uint hb_ot_math_get_glyph_assembly(
        IntPtr font,
        uint glyph,
        Direction direction,
        uint startOffset,
        uint* partsCount,
        OpenTypeMathGlyphPart* parts,
        int* italicsCorrection);
}
