using System;
using System.Runtime.InteropServices;
using HarfBuzzSharp;

namespace CSharpMath.Rendering.BackEnd;

internal static class HarfBuzzMath
{
    private const string LibraryName = "libHarfBuzzSharp";

    internal static bool HasData(IntPtr face) => HasDataNative(face) != 0;

    internal static int GetConstant(IntPtr font, OpenTypeMathConstant constant) =>
        GetConstantNative(font, constant);

    internal static int GetItalicCorrection(IntPtr font, uint glyph) =>
        GetItalicCorrectionNative(font, glyph);

    internal static int GetTopAccentAttachment(IntPtr font, uint glyph) =>
        GetTopAccentAttachmentNative(font, glyph);

    internal static int GetMinConnectorOverlap(IntPtr font, Direction direction) =>
        GetMinConnectorOverlapNative(font, direction);

    internal static OpenTypeMathGlyphVariant[] GetVariants(IntPtr font, uint glyph, Direction direction)
    {
        uint count = 0;
        uint total = GetGlyphVariantsNative(font, glyph, direction, 0, ref count, null);
        if (total == 0)
            return [];

        var variants = new OpenTypeMathGlyphVariant[checked((int)total)];
        count = total;
        uint available = GetGlyphVariantsNative(font, glyph, direction, 0, ref count, variants);
        ValidateNativeCount(available, count, variants.Length);
        if (count != total)
            Array.Resize(ref variants, checked((int)count));
        return variants;
    }

    internal static OpenTypeMathGlyphPart[] GetAssembly(IntPtr font, uint glyph, Direction direction)
    {
        uint count = 0;
        uint total = GetGlyphAssemblyNative(font, glyph, direction, 0, ref count, null, out _);
        if (total == 0)
            return [];

        var parts = new OpenTypeMathGlyphPart[checked((int)total)];
        count = total;
        uint available = GetGlyphAssemblyNative(font, glyph, direction, 0, ref count, parts, out _);
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

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "hb_ot_math_has_data", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int HasDataNative(IntPtr face);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "hb_ot_math_get_constant", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GetConstantNative(IntPtr font, OpenTypeMathConstant constant);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "hb_ot_math_get_glyph_italics_correction", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GetItalicCorrectionNative(IntPtr font, uint glyph);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "hb_ot_math_get_glyph_top_accent_attachment", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GetTopAccentAttachmentNative(IntPtr font, uint glyph);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "hb_ot_math_get_min_connector_overlap", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GetMinConnectorOverlapNative(IntPtr font, Direction direction);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "hb_ot_math_get_glyph_variants", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern uint GetGlyphVariantsNative(
        IntPtr font,
        uint glyph,
        Direction direction,
        uint startOffset,
        ref uint variantsCount,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] OpenTypeMathGlyphVariant[]? variants);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport(LibraryName, EntryPoint = "hb_ot_math_get_glyph_assembly", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern uint GetGlyphAssemblyNative(
        IntPtr font,
        uint glyph,
        Direction direction,
        uint startOffset,
        ref uint partsCount,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] OpenTypeMathGlyphPart[]? parts,
        out int italicsCorrection);

}
