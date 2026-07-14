using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

/// <summary>
/// Delegate for resource provider callback
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
internal delegate IntPtr ResourceProviderCallback(
    [MarshalAs(UnmanagedType.LPStr)] string resourceId,
    IntPtr context);
