using System.Runtime.InteropServices;

namespace CalcManagerManaged.Interop;

/// <summary>
/// Native methods
/// </summary>
public static class NativeMethods
{
    private const string DllName =
        "libCalcManager.dylib";

    [StructLayout(LayoutKind.Sequential)]
    public struct ExpressionToken
    {
        public IntPtr Text;
        public int Type;
    }

    public struct ExpressionCommand
    {
        public int CommandType;
        public IntPtr Token;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CalcSetIsInErrorCallback([MarshalAs(UnmanagedType.Bool)] bool isError, IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CalcSetExpressionDisplayCallback(
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
        ExpressionToken[] tokens,
        int tokenCount,
        IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CalcSetParenthesisNumberCallback(uint count, IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CalcOnNoRightParenAddedCallback(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CalcMaxDigitsReachedCallback(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CalcBinaryOperatorReceivedCallback(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CalcSetMemorizedNumbersCallback(IntPtr numbers, int count, IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CalcMemoryItemChangedCallback(uint indexOfMemory, IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CalcInputChangedCallback(IntPtr context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_CreateManager(
        CalcDisplayCallback displayCallback,
        CalcHistoryCallback historyCallback,
        ResourceProviderCallback resourceCallback,
        CalcSetIsInErrorCallback isInErrorCallback,
        CalcSetExpressionDisplayCallback expressionDisplayCallback,
        CalcSetParenthesisNumberCallback parenthesisNumberCallback,
        CalcOnNoRightParenAddedCallback noRightParenCallback,
        CalcMaxDigitsReachedCallback maxDigitsCallback,
        CalcBinaryOperatorReceivedCallback binaryOpCallback,
        CalcSetMemorizedNumbersCallback memorizedNumbersCallback,
        CalcMemoryItemChangedCallback memoryItemCallback,
        CalcInputChangedCallback inputChangedCallback,
        IntPtr context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Calc_DestroyManager(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_SendCommand(IntPtr handle, int commandId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_SetMode(IntPtr handle, CalcMode mode);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_Reset(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] bool clearMemory);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_SetRadix(IntPtr handle, CalcRadixType radixType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_SetPrecision(IntPtr handle, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr Calc_GetDisplayString(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_IsInError(
        IntPtr handle, [MarshalAs(UnmanagedType.Bool)] ref bool isError);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_IsInputEmpty(
        IntPtr handle, [MarshalAs(UnmanagedType.Bool)] ref bool isEmpty);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Calc_FreeString(IntPtr stringPtr);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_GetHistoryItems(IntPtr handle, CalcMode mode);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_GetHistoryItemCount(IntPtr historyItems, ref uint count);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_GetHistoryItem(IntPtr historyItems, uint index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr Calc_GetHistoryItemExpression(IntPtr historyItem);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr Calc_GetHistoryItemResult(IntPtr historyItem);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Calc_ReleaseHistoryItems(IntPtr historyItems);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_ClearHistory(IntPtr handle);


    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_MemorizeNumber(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_MemorizedNumberLoad(IntPtr handle, uint memoryIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_MemorizedNumberAdd(IntPtr handle, uint memoryIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_MemorizedNumberSubtract(IntPtr handle, uint memoryIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_MemorizedNumberClear(IntPtr handle, uint memoryIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_MemorizedNumberClearAll(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_GetMemorizedNumbers(
        IntPtr handle,
        ref uint count,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
        IntPtr[] buffer,
        uint bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_GetDisplayCommandsSnapshot(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_GetCommandSnapshotSize(IntPtr snapshot, ref uint count);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_GetCommandFromSnapshot(
        IntPtr snapshot,
        uint index,
        ref ExpressionCommand command);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Calc_ReleaseCommandSnapshot(IntPtr snapshot);

    #region Rational Number Functions

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_CreateRationalFromInt32(int value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr Calc_CreateRationalFromString(string valueStr, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Calc_DestroyRational(IntPtr rational);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr Calc_RationalToString(IntPtr rational, uint radix, CalcNumberFormat format, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_RationalToInt32(IntPtr rational, uint radix, int precision, ref int value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_RationalToUInt64(IntPtr rational, uint radix, int precision, ref ulong value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalAdd(IntPtr a, IntPtr b, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalSubtract(IntPtr a, IntPtr b, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalMultiply(IntPtr a, IntPtr b, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalDivide(IntPtr a, IntPtr b, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalMod(IntPtr a, IntPtr b);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalNegate(IntPtr rational);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_RationalEquals(IntPtr a, IntPtr b, int precision, [MarshalAs(UnmanagedType.Bool)] ref bool result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern CalcError Calc_RationalCompare(IntPtr a, IntPtr b, int precision, ref int result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalSin(IntPtr rational, CalcAngleType angleType, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalCos(IntPtr rational, CalcAngleType angleType, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalTan(IntPtr rational, CalcAngleType angleType, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalSqrt(IntPtr rational, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalPow(IntPtr baseValue, IntPtr exponent, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalRoot(IntPtr value, IntPtr root, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalFact(IntPtr rational, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalLn(IntPtr rational, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalLog10(IntPtr rational, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Calc_RationalExp(IntPtr rational, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern char Calc_GetDecimalSeparator(IntPtr handle);

    #endregion
}
