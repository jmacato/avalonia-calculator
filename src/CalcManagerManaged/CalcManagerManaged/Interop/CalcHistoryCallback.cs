using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

/// <summary>
/// Delegate for history callback
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void CalcHistoryCallback(uint addedItemIndex, IntPtr context);
