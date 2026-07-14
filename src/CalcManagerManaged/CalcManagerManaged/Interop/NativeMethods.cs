using System.Reflection;
using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

namespace CalcManagerManaged.Interop;

/// <summary>
/// Native methods
/// </summary>
internal static class NativeMethods
{
    private const string DllName =
        "libCalcManager.dylib";

    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, ResolveNativeLibrary);
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != DllName)
        {
            return IntPtr.Zero;
        }

        string libraryPath = Path.Combine(AppContext.BaseDirectory, DllName);
        return NativeLibrary.TryLoad(libraryPath, out IntPtr handle) ? handle : IntPtr.Zero;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_CreateManager")]
    internal static extern IntPtr CalcCreateManager(
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

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_DestroyManager")]
    internal static extern void CalcDestroyManager(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_SendCommand")]
    internal static extern CalcError CalcSendCommand(IntPtr handle, int commandId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_SetMode")]
    internal static extern CalcError CalcSetMode(IntPtr handle, CalcMode mode);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_Reset")]
    internal static extern CalcError CalcReset(IntPtr handle, [MarshalAs(UnmanagedType.Bool)] bool clearMemory);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_SetRadix")]
    internal static extern CalcError CalcSetRadix(IntPtr handle, CalcRadixType radixType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_SetPrecision")]
    internal static extern CalcError CalcSetPrecision(IntPtr handle, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "Calc_GetDisplayString")]
    internal static extern IntPtr CalcGetDisplayString(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_IsInError")]
    internal static extern CalcError CalcIsInError(
        IntPtr handle, [MarshalAs(UnmanagedType.Bool)] ref bool isError);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_IsInputEmpty")]
    internal static extern CalcError CalcIsInputEmpty(
        IntPtr handle, [MarshalAs(UnmanagedType.Bool)] ref bool isEmpty);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_FreeString")]
    internal static extern void CalcFreeString(IntPtr stringPtr);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_GetHistoryItems")]
    internal static extern IntPtr CalcGetHistoryItems(IntPtr handle, CalcMode mode);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_GetHistoryItemCount")]
    internal static extern CalcError CalcGetHistoryItemCount(IntPtr historyItems, ref uint count);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_GetHistoryItem")]
    internal static extern IntPtr CalcGetHistoryItem(IntPtr historyItems, uint index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "Calc_GetHistoryItemExpression")]
    internal static extern IntPtr CalcGetHistoryItemExpression(IntPtr historyItem);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "Calc_GetHistoryItemResult")]
    internal static extern IntPtr CalcGetHistoryItemResult(IntPtr historyItem);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_ReleaseHistoryItems")]
    internal static extern void CalcReleaseHistoryItems(IntPtr historyItems);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_ClearHistory")]
    internal static extern CalcError CalcClearHistory(IntPtr handle);


    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_MemorizeNumber")]
    internal static extern CalcError CalcMemorizeNumber(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_MemorizedNumberLoad")]
    internal static extern CalcError CalcMemorizedNumberLoad(IntPtr handle, uint memoryIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_MemorizedNumberAdd")]
    internal static extern CalcError CalcMemorizedNumberAdd(IntPtr handle, uint memoryIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_MemorizedNumberSubtract")]
    internal static extern CalcError CalcMemorizedNumberSubtract(IntPtr handle, uint memoryIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_MemorizedNumberClear")]
    internal static extern CalcError CalcMemorizedNumberClear(IntPtr handle, uint memoryIndex);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_MemorizedNumberClearAll")]
    internal static extern CalcError CalcMemorizedNumberClearAll(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_GetMemorizedNumbers")]
    internal static extern CalcError CalcGetMemorizedNumbers(
        IntPtr handle,
        ref uint count,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
        IntPtr[] buffer,
        uint bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_GetDisplayCommandsSnapshot")]
    internal static extern IntPtr CalcGetDisplayCommandsSnapshot(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_GetCommandSnapshotSize")]
    internal static extern CalcError CalcGetCommandSnapshotSize(IntPtr snapshot, ref uint count);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_GetCommandFromSnapshot")]
    internal static extern CalcError CalcGetCommandFromSnapshot(
        IntPtr snapshot,
        uint index,
        ref NativeExpressionCommand command);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_ReleaseCommandSnapshot")]
    internal static extern void CalcReleaseCommandSnapshot(IntPtr snapshot);

    #region Rational Number Functions

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_CreateRationalFromInt32")]
    internal static extern IntPtr CalcCreateRationalFromInt32(int value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "Calc_CreateRationalFromString", BestFitMapping = false, ThrowOnUnmappableChar = true)]
    internal static extern IntPtr CalcCreateRationalFromString([MarshalAs(UnmanagedType.LPStr)] string valueStr, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_DestroyRational")]
    internal static extern void CalcDestroyRational(IntPtr rational);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "Calc_RationalToString")]
    internal static extern IntPtr CalcRationalToString(IntPtr rational, uint radix, CalcNumberFormat format, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalToInt32")]
    internal static extern CalcError CalcRationalToInt32(IntPtr rational, uint radix, int precision, ref int value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalToUInt64")]
    internal static extern CalcError CalcRationalToUInt64(IntPtr rational, uint radix, int precision, ref ulong value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalAdd")]
    internal static extern IntPtr CalcRationalAdd(IntPtr a, IntPtr b, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalSubtract")]
    internal static extern IntPtr CalcRationalSubtract(IntPtr a, IntPtr b, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalMultiply")]
    internal static extern IntPtr CalcRationalMultiply(IntPtr a, IntPtr b, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalDivide")]
    internal static extern IntPtr CalcRationalDivide(IntPtr a, IntPtr b, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalMod")]
    internal static extern IntPtr CalcRationalMod(IntPtr a, IntPtr b);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalNegate")]
    internal static extern IntPtr CalcRationalNegate(IntPtr rational);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalEquals")]
    internal static extern CalcError CalcRationalEquals(IntPtr a, IntPtr b, int precision, [MarshalAs(UnmanagedType.Bool)] ref bool result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalCompare")]
    internal static extern CalcError CalcRationalCompare(IntPtr a, IntPtr b, int precision, ref int result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalSin")]
    internal static extern IntPtr CalcRationalSin(IntPtr rational, CalcAngleType angleType, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalCos")]
    internal static extern IntPtr CalcRationalCos(IntPtr rational, CalcAngleType angleType, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalTan")]
    internal static extern IntPtr CalcRationalTan(IntPtr rational, CalcAngleType angleType, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalSqrt")]
    internal static extern IntPtr CalcRationalSqrt(IntPtr rational, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalPow")]
    internal static extern IntPtr CalcRationalPow(IntPtr baseValue, IntPtr exponent, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalRoot")]
    internal static extern IntPtr CalcRationalRoot(IntPtr value, IntPtr root, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalFact")]
    internal static extern IntPtr CalcRationalFact(IntPtr rational, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalLn")]
    internal static extern IntPtr CalcRationalLn(IntPtr rational, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalLog10")]
    internal static extern IntPtr CalcRationalLog10(IntPtr rational, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_RationalExp")]
    internal static extern IntPtr CalcRationalExp(IntPtr rational, uint radix, int precision);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Calc_GetDecimalSeparator")]
    internal static extern char CalcGetDecimalSeparator(IntPtr handle);

    #endregion
}
