using System.Runtime.InteropServices;
using System.Text;

namespace CalcManagerManaged.Interop;

/// <summary>
/// Wrapper for the Calculator Engine C API
/// </summary>
public class CalcEngineWrapper : IDisposable
{
    // Opaque handle types
    private IntPtr _calcManager;
    private bool _disposed;

    // Callback delegates need to be kept alive to prevent garbage collection
    private CalcDisplayCallback _displayCallback;
    private CalcHistoryCallback _historyCallback;
    private ResourceProviderCallback _resourceCallback;
    private GCHandle _contextHandle;

    // Resource provider
    private ICalcResourceProvider _resourceProvider;

    // Add new callback delegates
    private NativeMethods.CalcSetIsInErrorCallback _isInErrorCallback;
    private NativeMethods.CalcSetExpressionDisplayCallback _expressionDisplayCallback;
    private NativeMethods.CalcSetParenthesisNumberCallback _parenthesisCallback;
    private NativeMethods.CalcOnNoRightParenAddedCallback _noRightParenCallback;
    private NativeMethods.CalcMaxDigitsReachedCallback _maxDigitsCallback;
    private NativeMethods.CalcBinaryOperatorReceivedCallback _binaryOpCallback;
    private NativeMethods.CalcSetMemorizedNumbersCallback _memorizedNumbersCallback;
    private NativeMethods.CalcMemoryItemChangedCallback _memoryItemChangedCallback;
    private NativeMethods.CalcInputChangedCallback _inputChangedCallback;

    // Add new events
    public event Action<bool> IsInErrorChanged;
    public event Action MaxDigitsReached;
    public event Action BinaryOperatorReceived;
    public event Action<uint> ParenthesisNumberChanged;
    public event Action NoRightParenAdded;
    public event Action<string[]> MemorizedNumbersChanged;
    public event Action<uint> MemoryItemChanged;
    public event Action InputChanged;

    public event Action<List<(string Text, int Type)>> ExpressionDisplayChanged;

    public CalcEngineWrapper(ICalcResourceProvider resourceProvider)
    {
        _resourceProvider = resourceProvider ?? new DefaultCalcResourceProvider();

        // Create delegates for callbacks
        _displayCallback = OnDisplayChanged;
        _historyCallback = OnHistoryItemAdded;
        _resourceCallback = OnGetResource;

        // Initialize new callbacks
        _isInErrorCallback = OnIsInErrorChanged;
        _expressionDisplayCallback = OnExpressionDisplayChanged;
        _parenthesisCallback = OnParenthesisNumberChanged;
        _noRightParenCallback = OnNoRightParenAdded;
        _maxDigitsCallback = OnMaxDigitsReached;
        _binaryOpCallback = OnBinaryOperatorReceived;
        _memorizedNumbersCallback = OnMemorizedNumbersChanged;
        _memoryItemChangedCallback = OnMemoryItemChanged;
        _inputChangedCallback = OnInputChanged;

        // Create context handle
        _contextHandle = GCHandle.Alloc(this);
        IntPtr context = GCHandle.ToIntPtr(_contextHandle);

        // Create calculator manager with all callbacks
        _calcManager = NativeMethods.Calc_CreateManager(
            _displayCallback,
            _historyCallback,
            _resourceCallback,
            _isInErrorCallback,
            _expressionDisplayCallback,
            _parenthesisCallback,
            _noRightParenCallback,
            _maxDigitsCallback,
            _binaryOpCallback,
            _memorizedNumbersCallback,
            _memoryItemChangedCallback,
            _inputChangedCallback,
            context);

        if (_calcManager == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create calculator manager");
        }
    }

    // Implement callback handlers
    private void OnIsInErrorChanged(bool isError, IntPtr context)
    {
        IsInErrorChanged?.Invoke(isError);
    }

    // Implement the callback handler with UTF-8 strings
    private void OnExpressionDisplayChanged(NativeMethods.ExpressionToken[] tokens, int tokenCount, IntPtr context)
    {
        if (tokens == null || tokenCount <= 0 || ExpressionDisplayChanged == null)
            return;

        // Console.WriteLine($"Expression tokens ({tokenCount}):");
        var tokenList = new List<(string Text, int Type)>(tokenCount);

        for (int i = 0; i < tokenCount; i++)
        {
            string text = tokens[i].Text != IntPtr.Zero ? Marshal.PtrToStringAnsi(tokens[i].Text) : "";
            int type = tokens[i].Type;

            // Console.WriteLine($"  Token {i}: '{text}' (Type: {type})");
            tokenList.Add((text, type));
        }

        ExpressionDisplayChanged?.Invoke(tokenList);
    }

    private void OnParenthesisNumberChanged(uint count, IntPtr context)
    {
        ParenthesisNumberChanged?.Invoke(count);
    }

    private void OnNoRightParenAdded(IntPtr context)
    {
        NoRightParenAdded?.Invoke();
    }

    private void OnMaxDigitsReached(IntPtr context)
    {
        MaxDigitsReached?.Invoke();
    }

    private void OnBinaryOperatorReceived(IntPtr context)
    {
        BinaryOperatorReceived?.Invoke();
    }

    private void OnMemorizedNumbersChanged(IntPtr numbers, int count, IntPtr context)
    {
        if (count <= 0 || MemorizedNumbersChanged == null)
            return;

        try
        {
            string[] result = new string[count];
            for (int i = 0; i < count; i++)
            {
                IntPtr strPtr = Marshal.ReadIntPtr(numbers, i * IntPtr.Size);
                if (strPtr != IntPtr.Zero)
                {
                    // Use UTF8 explicitly for string conversion
                    int length = 0;
                    while (Marshal.ReadByte(strPtr, length) != 0)
                    {
                        length++;
                    }

                    if (length > 0)
                    {
                        byte[] buffer = new byte[length];
                        Marshal.Copy(strPtr, buffer, 0, length);
                        result[i] = Encoding.UTF8.GetString(buffer);
                    }
                    else
                    {
                        result[i] = string.Empty;
                    }

                    // Free the string allocated by C++
                    NativeMethods.Calc_FreeString(strPtr);
                }
                else
                {
                    result[i] = string.Empty;
                }
            }

            MemorizedNumbersChanged?.Invoke(result);
        }
        catch (Exception ex)
        {
            // Add logging if available
            Console.WriteLine($"Error in OnMemorizedNumbersChanged: {ex.Message}");
        }
    }

    private void OnMemoryItemChanged(uint indexOfMemory, IntPtr context)
    {
        MemoryItemChanged?.Invoke(indexOfMemory);
    }

    private void OnInputChanged(IntPtr context)
    {
        InputChanged?.Invoke();
    }

    // Event handlers
    public event Action<string, bool> DisplayChanged;
    public event Action<uint> HistoryItemAdded;

    /// <summary>
    /// Finalizer
    /// </summary>
    ~CalcEngineWrapper()
    {
        Dispose(false);
    }

    /// <summary>
    /// Dispose method
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose implementation
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (_calcManager != IntPtr.Zero)
            {
                NativeMethods.Calc_DestroyManager(_calcManager);
                _calcManager = IntPtr.Zero;
            }

            if (_contextHandle.IsAllocated)
            {
                _contextHandle.Free();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Display callback handler
    /// </summary>
    private void OnDisplayChanged(string displayString, bool isError, IntPtr context)
    {
        DisplayChanged?.Invoke(displayString, isError);
    }

    /// <summary>
    /// History callback handler
    /// </summary>
    private void OnHistoryItemAdded(uint addedItemIndex, IntPtr context)
    {
        HistoryItemAdded?.Invoke(addedItemIndex);
    }

    /// <summary>
    /// Resource provider callback handler
    /// </summary>
    private IntPtr OnGetResource(string resourceId, IntPtr context)
    {
        try
        {
            // Get the appropriate instance from the context
            GCHandle handle = GCHandle.FromIntPtr(context);
            var instance = (CalcEngineWrapper)handle.Target;

            // Get the string from the resource provider
            string resourceValue = instance._resourceProvider.GetString(resourceId);
            if (resourceValue == null)
            {
                return IntPtr.Zero;
            }

            // Convert the string to UTF-8 encoding
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(resourceValue + '\0'); // Include null terminator

            // Allocate unmanaged memory for the string
            IntPtr nativeStr = Marshal.AllocHGlobal(utf8Bytes.Length);

            // Copy the UTF-8 bytes to the unmanaged memory
            Marshal.Copy(utf8Bytes, 0, nativeStr, utf8Bytes.Length);

            return nativeStr;
        }
        catch
        {
            // Return null on any error
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Handles error codes from native methods and throws appropriate exceptions
    /// </summary>
    private void HandleError(CalcError errorCode, string operation)
    {
        if (errorCode == CalcError.Success)
            return;
            
        string message;
        
        switch (errorCode)
        {
            case CalcError.DivideByZero:
                message = "Cannot divide by zero";
                break;
            case CalcError.Domain:
                message = "Invalid input";
                break;
            case CalcError.Undefined:
                message = "Result is undefined";
                break;
            case CalcError.Overflow:
                message = "Overflow";
                break;
            case CalcError.InvalidParam:
                message = "Invalid parameter";
                break;
            case CalcError.OutOfMemory:
                message = "Out of memory";
                break;
            case CalcError.PositiveInfinity:
                message = "Result is positive infinity";
                break;
            case CalcError.NegativeInfinity:
                message = "Result is negative infinity";
                break;
            case CalcError.InvalidRange:
                message = "Value is outside the valid calculation range";
                break;
            case CalcError.NoResult:
                message = "No result could be computed";
                break;
            default:
                message = $"Error: {errorCode}";
                break;
        }
        
        throw new InvalidOperationException($"{operation}: {message}");
    }

    /// <summary>
    /// Send a command to the calculator
    /// </summary>
    public void SendCommand(int commandId)
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_SendCommand(_calcManager, commandId);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to send command");
        }
    }

    /// <summary>
    /// Set calculator mode
    /// </summary>
    public void SetMode(CalcMode mode)
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_SetMode(_calcManager, mode);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to set mode");
        }
    }

    /// <summary>
    /// Reset calculator state
    /// </summary>
    public void Reset(bool clearMemory = true)
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_Reset(_calcManager, clearMemory);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to reset");
        }
    }

    /// <summary>
    /// Set radix type (number base)
    /// </summary>
    public void SetRadix(CalcRadixType radixType)
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_SetRadix(_calcManager, radixType);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to set radix");
        }
    }

    /// <summary>
    /// Set precision for calculations
    /// </summary>
    public void SetPrecision(int precision)
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_SetPrecision(_calcManager, precision);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to set precision");
        }
    }

    /// <summary>
    /// Get current display string
    /// </summary>
    public string GetDisplayString()
    {
        CheckDisposed();

        IntPtr stringPtr = NativeMethods.Calc_GetDisplayString(_calcManager);
        if (stringPtr == IntPtr.Zero)
        {
            // If the C API returns null, it's likely an error state or empty display
            // Since the display could legitimately be empty, return empty string instead of throwing
            return string.Empty;
        }

        try
        {
            // Use UTF-8 string decoding consistently with other string handling in this class
            int length = 0;
            while (Marshal.ReadByte(stringPtr, length) != 0)
            {
                length++;
            }

            if (length > 0)
            {
                byte[] buffer = new byte[length];
                Marshal.Copy(stringPtr, buffer, 0, length);
                return Encoding.UTF8.GetString(buffer);
            }
            else
            {
                return string.Empty;
            }
        }
        finally
        {
            NativeMethods.Calc_FreeString(stringPtr);
        }
    }

    /// <summary>
    /// Check if calculator is in error state
    /// </summary>
    public bool IsInError()
    {
        CheckDisposed();

        bool isError = false;
        CalcError result = NativeMethods.Calc_IsInError(_calcManager, ref isError);

        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to check error state");
        }

        return isError;
    }

    /// <summary>
    /// Check if calculator input is empty
    /// </summary>
    public bool IsInputEmpty()
    {
        CheckDisposed();

        bool isEmpty = false;
        CalcError result = NativeMethods.Calc_IsInputEmpty(_calcManager, ref isEmpty);

        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to check input empty");
        }

        return isEmpty;
    }

    /// <summary>
    /// Get history items
    /// </summary>
    public CalcHistoryItem[] GetHistoryItems(CalcMode mode = CalcMode.Standard)
    {
        CheckDisposed();

        IntPtr historyItemsHandle = NativeMethods.Calc_GetHistoryItems(_calcManager, mode);
        if (historyItemsHandle == IntPtr.Zero)
        {
            return Array.Empty<CalcHistoryItem>();
        }

        try
        {
            uint count = 0;
            CalcError errorCode = NativeMethods.Calc_GetHistoryItemCount(historyItemsHandle, ref count);

            if (errorCode != CalcError.Success)
            {
                HandleError(errorCode, "Failed to get history item count");
            }

            CalcHistoryItem[] items = new CalcHistoryItem[count];

            for (uint i = 0; i < count; i++)
            {
                IntPtr historyItemHandle = NativeMethods.Calc_GetHistoryItem(historyItemsHandle, i);
                if (historyItemHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Failed to get history item at index {i}");
                }

                try
                {
                    string expression = null;
                    string result = null;

                    IntPtr expressionPtr = NativeMethods.Calc_GetHistoryItemExpression(historyItemHandle);
                    if (expressionPtr == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Failed to get history item expression");
                    }

                    try
                    {
                        expression = Marshal.PtrToStringAnsi(expressionPtr);
                    }
                    finally
                    {
                        NativeMethods.Calc_FreeString(expressionPtr);
                    }

                    IntPtr resultPtr = NativeMethods.Calc_GetHistoryItemResult(historyItemHandle);
                    if (resultPtr == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Failed to get history item result");
                    }

                    try
                    {
                        result = Marshal.PtrToStringAnsi(resultPtr);
                    }
                    finally
                    {
                        NativeMethods.Calc_FreeString(resultPtr);
                    }

                    items[i] = new CalcHistoryItem
                    {
                        Expression = expression,
                        Result = result
                    };
                }
                finally
                {
                    // No need to release individual history items - they are owned by the history items handle
                }
            }

            return items;
        }
        finally
        {
            NativeMethods.Calc_ReleaseHistoryItems(historyItemsHandle);
        }
    }

    /// <summary>
    /// Clear history
    /// </summary>
    public void ClearHistory()
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_ClearHistory(_calcManager);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to clear history");
        }
    }

    /// <summary>
    /// Check if object is disposed
    /// </summary>
    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }


    /// <summary>
    /// Memorize the current number
    /// </summary>
    public void MemorizeNumber()
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_MemorizeNumber(_calcManager);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to memorize number");
        }
    }

    /// <summary>
    /// Load number from memory
    /// </summary>
    public void MemorizedNumberLoad(uint memoryIndex)
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_MemorizedNumberLoad(_calcManager, memoryIndex);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to load from memory");
        }
    }

    /// <summary>
    /// Add current value to memory
    /// </summary>
    public void MemorizedNumberAdd(uint memoryIndex)
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_MemorizedNumberAdd(_calcManager, memoryIndex);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to add to memory");
        }
    }

    /// <summary>
    /// Subtract current value from memory
    /// </summary>
    public void MemorizedNumberSubtract(uint memoryIndex)
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_MemorizedNumberSubtract(_calcManager, memoryIndex);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to subtract from memory");
        }
    }

    /// <summary>
    /// Clear specific memory slot
    /// </summary>
    public void MemorizedNumberClear(uint memoryIndex)
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_MemorizedNumberClear(_calcManager, memoryIndex);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to clear memory slot");
        }
    }

    /// <summary>
    /// Clear all memory slots
    /// </summary>
    public void MemorizedNumberClearAll()
    {
        CheckDisposed();

        CalcError result = NativeMethods.Calc_MemorizedNumberClearAll(_calcManager);
        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to clear all memory");
        }
    }

    /// <summary>
    /// Get all memorized numbers as strings
    /// </summary>
    public string[] GetMemorizedNumbers()
    {
        CheckDisposed();

        uint count = 0;
        IntPtr[] bufferPtrs = new IntPtr[100]; // Assume maximum 100 memory slots

        CalcError result = NativeMethods.Calc_GetMemorizedNumbers(
            _calcManager,
            ref count,
            bufferPtrs,
            (uint)bufferPtrs.Length);

        if (result != CalcError.Success)
        {
            HandleError(result, "Failed to get memorized numbers");
        }

        string[] memorizedNumbers = new string[count];
        for (int i = 0; i < count; i++)
        {
            if (bufferPtrs[i] != IntPtr.Zero)
            {
                // Use UTF8 explicitly
                int length = 0;
                while (Marshal.ReadByte(bufferPtrs[i], length) != 0)
                {
                    length++;
                }

                byte[] buffer = new byte[length];
                Marshal.Copy(bufferPtrs[i], buffer, 0, length);
                memorizedNumbers[i] = Encoding.UTF8.GetString(buffer);

                NativeMethods.Calc_FreeString(bufferPtrs[i]);
            }
            else
            {
                memorizedNumbers[i] = string.Empty;
            }
        }

        return memorizedNumbers;
    }
    
    /// <summary>
    /// Represents a command from the expression history
    /// </summary>
    public class ExpressionCommand
    {
        /// <summary>
        /// Command type (0=Unary, 1=Binary, 2=Operand, 3=Parenthesis)
        /// </summary>
        public int CommandType { get; }
        
        /// <summary>
        /// The token text representation
        /// </summary>
        public string Token { get; }
        
        internal ExpressionCommand(int commandType, string token)
        {
            CommandType = commandType;
            Token = token;
        }
    }
    
    /// <summary>
    /// Gets a snapshot of the current display commands
    /// </summary>
    public List<ExpressionCommand> GetDisplayCommandsSnapshot()
    {
        CheckDisposed();
        
        IntPtr snapshotHandle = NativeMethods.Calc_GetDisplayCommandsSnapshot(_calcManager);
        if (snapshotHandle == IntPtr.Zero)
        {
            return new List<ExpressionCommand>();
        }
        
        try
        {
            uint count = 0;
            CalcError result = NativeMethods.Calc_GetCommandSnapshotSize(snapshotHandle, ref count);
            
            if (result != CalcError.Success)
            {
                HandleError(result, "Failed to get command snapshot size");
            }
            
            var commands = new List<ExpressionCommand>((int)count);
            
            for (uint i = 0; i < count; i++)
            {
                var command = new NativeMethods.ExpressionCommand();
                result = NativeMethods.Calc_GetCommandFromSnapshot(snapshotHandle, i, ref command);
                
                if (result != CalcError.Success)
                {
                    HandleError(result, "Failed to get command from snapshot");
                }
                
                string token = string.Empty;
                if (command.Token != IntPtr.Zero)
                {
                    // Get the string from the pointer
                    token = Marshal.PtrToStringAnsi(command.Token);
                    // Free the string allocated by C++
                    NativeMethods.Calc_FreeString(command.Token);
                }
                
                commands.Add(new ExpressionCommand(command.CommandType, token));
            }
            
            return commands;
        }
        finally
        {
            NativeMethods.Calc_ReleaseCommandSnapshot(snapshotHandle);
        }
    }
}
