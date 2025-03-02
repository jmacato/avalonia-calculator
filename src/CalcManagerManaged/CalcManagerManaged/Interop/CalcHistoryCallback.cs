using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

/// <summary>
/// Delegate for history callback
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void CalcHistoryCallback(uint addedItemIndex, IntPtr context);