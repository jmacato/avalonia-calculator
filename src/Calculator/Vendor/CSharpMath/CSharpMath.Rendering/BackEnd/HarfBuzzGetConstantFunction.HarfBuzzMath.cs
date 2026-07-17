using System;
using System.Runtime.InteropServices;
using HarfBuzzSharp;

namespace CSharpMath.Rendering.BackEnd;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int HarfBuzzGetConstantFunction(IntPtr font, OpenTypeMathConstant constant);
