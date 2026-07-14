using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void CalcOnNoRightParenAddedCallback(IntPtr context);
