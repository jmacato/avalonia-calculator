using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void CalcSetIsInErrorCallback([MarshalAs(UnmanagedType.Bool)] bool isError, IntPtr context);
