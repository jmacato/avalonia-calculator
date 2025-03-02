using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

/// <summary>
/// Delegate for display callback
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public delegate void CalcDisplayCallback(
    [MarshalAs(UnmanagedType.LPStr)] string displayString,
    [MarshalAs(UnmanagedType.Bool)] bool isError,
    IntPtr context);