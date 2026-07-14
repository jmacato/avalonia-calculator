using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void CalcSetParenthesisNumberCallback(uint count, IntPtr context);
