using System;
using System.Runtime.InteropServices;

namespace CSharpMath.Rendering.BackEnd;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int HarfBuzzHasDataFunction(IntPtr face);
