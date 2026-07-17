using System;
using System.Runtime.InteropServices;
using HarfBuzzSharp;

namespace CSharpMath.Rendering.BackEnd;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate uint HarfBuzzGetGlyphAssemblyFunction(
    IntPtr font,
    uint glyph,
    Direction direction,
    uint startOffset,
    ref uint partsCount,
    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] OpenTypeMathGlyphPart[]? parts,
    out int italicsCorrection);
