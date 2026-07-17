using System;
using System.Runtime.InteropServices;
using HarfBuzzSharp;

namespace CSharpMath.Rendering.BackEnd;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate uint HarfBuzzGetGlyphVariantsFunction(
    IntPtr font,
    uint glyph,
    Direction direction,
    uint startOffset,
    ref uint variantsCount,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] OpenTypeMathGlyphVariant[]? variants);
