using System;
using System.Runtime.InteropServices;
using HarfBuzzSharp;

namespace CSharpMath.Rendering.BackEnd;

internal static class HarfBuzzMath
{
    private const string LibraryName = "libHarfBuzzSharp";
    private static readonly IntPtr s_libraryHandle = NativeLibrary.Load(
        LibraryName,
        typeof(Face).Assembly,
        DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory);
    private static readonly HarfBuzzHasDataFunction s_hasData = LoadFunction<HarfBuzzHasDataFunction>("hb_ot_math_has_data");
    private static readonly HarfBuzzGetConstantFunction s_getConstant = LoadFunction<HarfBuzzGetConstantFunction>("hb_ot_math_get_constant");
    private static readonly HarfBuzzGetGlyphValueFunction s_getItalicCorrection =
        LoadFunction<HarfBuzzGetGlyphValueFunction>("hb_ot_math_get_glyph_italics_correction");
    private static readonly HarfBuzzGetGlyphValueFunction s_getTopAccentAttachment =
        LoadFunction<HarfBuzzGetGlyphValueFunction>("hb_ot_math_get_glyph_top_accent_attachment");
    private static readonly HarfBuzzGetMinConnectorOverlapFunction s_getMinConnectorOverlap =
        LoadFunction<HarfBuzzGetMinConnectorOverlapFunction>("hb_ot_math_get_min_connector_overlap");
    private static readonly HarfBuzzGetGlyphVariantsFunction s_getGlyphVariants =
        LoadFunction<HarfBuzzGetGlyphVariantsFunction>("hb_ot_math_get_glyph_variants");
    private static readonly HarfBuzzGetGlyphAssemblyFunction s_getGlyphAssembly =
        LoadFunction<HarfBuzzGetGlyphAssemblyFunction>("hb_ot_math_get_glyph_assembly");

    internal static bool HasData(IntPtr face) => s_hasData(face) != 0;

    internal static int GetConstant(IntPtr font, OpenTypeMathConstant constant) =>
        s_getConstant(font, constant);

    internal static int GetItalicCorrection(IntPtr font, uint glyph) =>
        s_getItalicCorrection(font, glyph);

    internal static int GetTopAccentAttachment(IntPtr font, uint glyph) =>
        s_getTopAccentAttachment(font, glyph);

    internal static int GetMinConnectorOverlap(IntPtr font, Direction direction) =>
        s_getMinConnectorOverlap(font, direction);

    internal static OpenTypeMathGlyphVariant[] GetVariants(IntPtr font, uint glyph, Direction direction)
    {
        uint count = 0;
        uint total = s_getGlyphVariants(font, glyph, direction, 0, ref count, null);
        if (total == 0)
            return [];

        var variants = new OpenTypeMathGlyphVariant[checked((int)total)];
        count = total;
        uint available = s_getGlyphVariants(font, glyph, direction, 0, ref count, variants);
        ValidateNativeCount(available, count, variants.Length);
        if (count != total)
            Array.Resize(ref variants, checked((int)count));
        return variants;
    }

    internal static OpenTypeMathGlyphPart[] GetAssembly(IntPtr font, uint glyph, Direction direction)
    {
        uint count = 0;
        uint total = s_getGlyphAssembly(font, glyph, direction, 0, ref count, null, out _);
        if (total == 0)
            return [];

        var parts = new OpenTypeMathGlyphPart[checked((int)total)];
        count = total;
        uint available = s_getGlyphAssembly(font, glyph, direction, 0, ref count, parts, out _);
        ValidateNativeCount(available, count, parts.Length);
        if (count != total)
            Array.Resize(ref parts, checked((int)count));
        return parts;
    }

    private static void ValidateNativeCount(uint available, uint written, int capacity)
    {
        if (available < written || written > capacity)
            throw new InvalidOperationException("HarfBuzz returned an invalid OpenType MATH result count.");
    }

    private static TFunction LoadFunction<TFunction>(string exportName)
        where TFunction : Delegate =>
        Marshal.GetDelegateForFunctionPointer<TFunction>(NativeLibrary.GetExport(s_libraryHandle, exportName));

}
