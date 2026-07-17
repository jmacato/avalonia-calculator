using System;
using System.Runtime.InteropServices;

namespace CSharpMath.Rendering.BackEnd;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int HarfBuzzGetGlyphValueFunction(IntPtr font, uint glyph);
