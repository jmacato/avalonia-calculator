using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void CalcBinaryOperatorReceivedCallback(IntPtr context);
