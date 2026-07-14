using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void CalcSetExpressionDisplayCallback(
    [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
    ExpressionToken[] tokens,
    int tokenCount,
    IntPtr context);
