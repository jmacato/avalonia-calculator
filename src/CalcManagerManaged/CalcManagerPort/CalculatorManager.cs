// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;
using CalculationManager;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Text;

namespace CalculationManager;

public class CalculatorManager : ICalcDisplay
{
    //     friend  CalcEngineTests;
    const uint m_maximumMemorySize = 100;

    ICalcDisplay m_displayCallback;

    CCalcEngine? m_scientificCalculatorEngine;

    CCalcEngine? m_standardCalculatorEngine;

    CCalcEngine? m_programmerCalculatorEngine;

    IResourceProvider m_resourceProvider;

    bool m_inHistoryItemLoadMode;

    Rational? m_persistedPrimaryValue;

    bool m_isExponentialFormat;

    Command m_currentDegreeMode;

    CalculatorHistory m_pStdHistory;

    CalculatorHistory m_pSciHistory;

    CalculatorHistory m_pHistory;

    CCalcEngine m_currentCalculatorEngine;

    List<Rational> m_memorizedNumbers = [];

    public static int MaxHistorySize()
    {
        return (int)MAX_HISTORY_ITEMS;
    }

    const ulong MAX_HISTORY_ITEMS = 20;

    public CalculatorManager(ICalcDisplay displayCallback, IResourceProvider resourceProvider)

    {
        m_displayCallback = displayCallback;
        m_resourceProvider = (resourceProvider);
        m_inHistoryItemLoadMode = (false);
        m_persistedPrimaryValue = null;
        m_isExponentialFormat = (false);
        m_currentDegreeMode = (Command.None);
        m_pStdHistory = (new CalculatorHistory(MAX_HISTORY_ITEMS));
        m_pSciHistory = (new CalculatorHistory(MAX_HISTORY_ITEMS));

        SetStandardMode();
    }

    /// <summary>
    /// Call the callback function using passed in IDisplayHelper.
    /// Used to set the primary display value on ViewModel
    /// </summary>
    /// <param name="text">wstring representing text to be displayed</param>
    public void SetPrimaryDisplay(wstring pszText, bool isError)
    {
        if (!m_inHistoryItemLoadMode)
        {
            m_displayCallback.SetPrimaryDisplay(pszText, isError);
        }
    }

    public void SetIsInError(bool isInError)
    {
        m_displayCallback.SetIsInError(isInError);
    }

    public void DisplayPasteError()
    {
        m_currentCalculatorEngine.DisplayError(CalcErr.Domain /*code for "Invalid input" error*/);
    }

    public void MaxDigitsReached()
    {
        m_displayCallback.MaxDigitsReached();
    }

    public void BinaryOperatorReceived()
    {
        m_displayCallback.BinaryOperatorReceived();
    }

    public void MemoryItemChanged(uint indexOfMemory)
    {
        m_displayCallback.MemoryItemChanged(indexOfMemory);
    }

    public void InputChanged()
    {
        m_displayCallback.InputChanged();
    }

    /// <summary>
    /// Call the callback function using passed in IDisplayHelper.
    /// Used to set the expression display value on ViewModel
    /// </summary>
    /// <param name="expressionString">wstring representing expression to be displayed</param>
    public void SetExpressionDisplay(IList<(wstring, int)> tokens,
        IList<IExpressionCommand> commands)
    {
        if (!m_inHistoryItemLoadMode)
        {
            m_displayCallback.SetExpressionDisplay(tokens, commands);
        }
    }

    /// <summary>
    /// Callback from the CalculatorControl
    /// Passed in string representations of memorized numbers get passed to the client
    /// </summary>
    /// <param name="memorizedNumber">vector containing wstring values of memorized numbers</param>
    public void SetMemorizedNumbers(IList<wstring> memorizedNumbers)
    {
        m_displayCallback.SetMemorizedNumbers(memorizedNumbers);
    }

    /// <summary>
    /// Callback from the engine
    /// </summary>
    /// <param name="parenthesisCount">string containing the parenthesis count</param>
    public void SetParenthesisNumber(uint count)
    {
        m_displayCallback.SetParenthesisNumber(count);
    }

    /// <summary>
    /// Callback from the engine
    /// </summary>
    public void OnNoRightParenAdded()
    {
        m_displayCallback.OnNoRightParenAdded();
    }

    /// <summary>
    /// Reset
    /// Set the mode to the standard calculator
    /// Set the degree mode as regular degree (as oppose to Rad or Grad)
    /// Clear all the entries and memories
    /// Clear Memory if clearMemory parameter is true.(Default value is true)
    /// </summary>
    public void Reset(bool clearMemory = true)
    {
        SetStandardMode();

        if (m_scientificCalculatorEngine != null)
        {
            m_scientificCalculatorEngine.ProcessCommand(CCommand.IdcDeg);
            m_scientificCalculatorEngine.ProcessCommand(CCommand.IdcClear);

            if (m_isExponentialFormat)
            {
                m_isExponentialFormat = false;
                m_scientificCalculatorEngine.ProcessCommand(CCommand.IdcFe);
            }
        }

        if (m_programmerCalculatorEngine != null)
        {
            m_programmerCalculatorEngine.ProcessCommand(CCommand.IdcClear);
        }

        if (clearMemory)
        {
            this.MemorizedNumberClearAll();
        }
    }

    /// <summary>
    /// Change the current calculator engine to standard calculator engine.
    /// </summary>
    [MemberNotNull(nameof(m_currentCalculatorEngine), nameof(m_pHistory))]
    public void SetStandardMode()
    {
        if (m_standardCalculatorEngine is null)
        {
            m_standardCalculatorEngine =
                new CCalcEngine(false /* Respect Order of Operations */, false /* Set to Integer Mode */,
                    m_resourceProvider, this, m_pStdHistory);

            m_standardCalculatorEngine.InitialOneTimeOnlySetup(m_resourceProvider);
        }

        m_currentCalculatorEngine = m_standardCalculatorEngine;
        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcDec);
        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcClear);
        m_currentCalculatorEngine.ChangePrecision((int)(CalculatorPrecision.StandardModePrecision));
        UpdateMaxIntDigits();
        m_pHistory = m_pStdHistory;
    }

    /// <summary>
    /// Change the current calculator engine to scientific calculator engine.
    /// </summary>
    [MemberNotNull(nameof(m_currentCalculatorEngine), nameof(m_pHistory))]
    public void SetScientificMode()
    {
        if (m_scientificCalculatorEngine is null)
        {
            m_scientificCalculatorEngine =
                new CCalcEngine(true /* Respect Order of Operations */, false /* Set to Integer Mode */,
                    m_resourceProvider, this, m_pSciHistory);

            m_scientificCalculatorEngine.InitialOneTimeOnlySetup(m_resourceProvider);
        }

        m_currentCalculatorEngine = m_scientificCalculatorEngine;
        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcDec);
        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcClear);
        m_currentCalculatorEngine.ChangePrecision((int)(CalculatorPrecision.ScientificModePrecision));
        m_pHistory = m_pSciHistory;
    }

    /// <summary>
    /// Change the current calculator engine to scientific calculator engine.
    /// </summary>
    [MemberNotNull(nameof(m_currentCalculatorEngine))]
    public void SetProgrammerMode()
    {
        if (m_programmerCalculatorEngine is null)
        {
            m_programmerCalculatorEngine =
                new CCalcEngine(true /* Respect Order of Operations */, true /* Set to Integer Mode */,
                    m_resourceProvider, this, null!);

            m_programmerCalculatorEngine.InitialOneTimeOnlySetup(m_resourceProvider);
        }

        m_currentCalculatorEngine = m_programmerCalculatorEngine;
        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcDec);
        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcClear);
        m_currentCalculatorEngine.ChangePrecision((int)(CalculatorPrecision.ProgrammerModePrecision));
    }

    /// <summary>
    /// Send command to the Calc Engine
    /// Cast Command Enum to OpCode.
    /// Handle special commands such as mode change and combination of two commands.
    /// </summary>
    /// <param name="command">Enum Command</command>
    public void SendCommand(Command command)
    {
        // When the expression line is cleared, we save the current state, which includes,
        // primary display, memory, and degree mode
        if (command == Command.Clear || command == Command.Equ || command == Command.ModeBasic ||
            command == Command.ModeScientific
            || command == Command.ModeProgrammer)
        {
            ProcessModeChangeCommand(command);

            InputChanged();
            return;
        }

        if (command == Command.Deg || command == Command.Rad || command == Command.Grad)
        {
            m_currentDegreeMode = command;
        }

        ProcessEngineCommand(command);

        InputChanged();
    }

    private void ProcessModeChangeCommand(Command command)
    {
        switch (command)
        {
            case Command.ModeBasic:
                this.SetStandardMode();
                break;
            case Command.ModeScientific:
                this.SetScientificMode();
                break;
            case Command.ModeProgrammer:
                this.SetProgrammerMode();
                break;
            default:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(command));
                break;
        }
    }

    private void ProcessEngineCommand(Command command)
    {
        switch (command)
        {
            case Command.Asin:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Sin));
                break;
            case Command.Acos:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Cos));
                break;
            case Command.Atan:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Tan));
                break;
            case Command.PowE:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.NumLN));
                break;
            case Command.Asinh:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Sinh));
                break;
            case Command.Acosh:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Cosh));
                break;
            case Command.Atanh:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Tanh));
                break;
            case Command.Asec:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Sec));
                break;
            case Command.Acsc:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Csc));
                break;
            case Command.Acot:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Cot));
                break;
            case Command.Asech:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Sech));
                break;
            case Command.Acsch:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Csch));
                break;
            case Command.Acoth:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Inv));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.Coth));
                break;
            case Command.NumFE:
                m_isExponentialFormat = !m_isExponentialFormat;
                goto default;
            default:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(command));
                break;
        }
    }

    /// <summary>
    /// Load the persisted value that is saved in memory of CalcEngine
    /// </summary>
    public void LoadPersistedPrimaryValue()
    {
        if (m_persistedPrimaryValue is not null)
        {
            m_currentCalculatorEngine.PersistedMemObject(m_persistedPrimaryValue);
        }

        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcRecall);
        InputChanged();
    }

    /// <summary>
    /// Memorize the current displayed value
    /// Notify the client with new the new memorize value vector
    /// </summary>
    public void MemorizeNumber()
    {
        if (m_currentCalculatorEngine.FInErrorState())
        {
            return;
        }

        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcStore);

        var memoryObjectPtr = m_currentCalculatorEngine.PersistedMemObject();
        if (memoryObjectPtr is not null)
        {
            m_memorizedNumbers.Insert(0, memoryObjectPtr);
        }

        if (m_memorizedNumbers.Count > m_maximumMemorySize)
        {
            //TODO: check
            m_memorizedNumbers.RemoveRange((int)(m_maximumMemorySize - 1),
                (int)(m_memorizedNumbers.Count - m_maximumMemorySize));
        }

        this.SetMemorizedNumbersString();
    }

    /// <summary>
    /// Recall the memorized number.
    /// The memorized number gets loaded to the primary display
    /// </summary>
    /// <param name="indexOfMemory">Index of the target memory</param>
    public void MemorizedNumberLoad(int indexOfMemory)
    {
        if (m_currentCalculatorEngine.FInErrorState())
        {
            return;
        }

        this.MemorizedNumberSelect(indexOfMemory);
        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcRecall);
        InputChanged();
    }

    /// <summary>
    /// Do the addition to the selected memory
    /// It adds primary display value to the selected memory
    /// Notify the client with new the new memorize value vector
    /// </summary>
    /// <param name="indexOfMemory">Index of the target memory</param>
    public void MemorizedNumberAdd(int indexOfMemory)
    {
        if (m_currentCalculatorEngine.FInErrorState())
        {
            return;
        }

        if (m_memorizedNumbers.Count == 0)
        {
            this.MemorizeNumber();
        }
        else
        {
            this.MemorizedNumberSelect(indexOfMemory);
            m_currentCalculatorEngine.ProcessCommand(CCommand.IdcMplus);

            this.MemorizedNumberChanged(indexOfMemory);

            this.SetMemorizedNumbersString();
        }

        m_displayCallback.MemoryItemChanged((uint)indexOfMemory);
    }

    public void MemorizedNumberClear(int indexOfMemory)
    {
        if (indexOfMemory < m_memorizedNumbers.Count)
        {
            m_memorizedNumbers.RemoveAt((int)indexOfMemory);
        }
    }

    /// <summary>
    /// Do the subtraction to the selected memory
    /// It adds primary display value to the selected memory
    /// Notify the client with new the new memorize value vector
    /// </summary>
    /// <param name="indexOfMemory">Index of the target memory</param>
    public void MemorizedNumberSubtract(int indexOfMemory)
    {
        if (m_currentCalculatorEngine.FInErrorState())
        {
            return;
        }

        // To add negative of the number on display to the memory -x = x - 2x
        if (m_memorizedNumbers.Count == 0)
        {
            this.MemorizeNumber();
            this.MemorizedNumberSubtract(0);
            this.MemorizedNumberSubtract(0);
        }
        else
        {
            this.MemorizedNumberSelect(indexOfMemory);
            m_currentCalculatorEngine.ProcessCommand(CCommand.IdcMminus);

            this.MemorizedNumberChanged(indexOfMemory);

            this.SetMemorizedNumbersString();
        }

        m_displayCallback.MemoryItemChanged((uint)indexOfMemory);
    }

    /// <summary>
    /// Clear all the memorized values
    /// Notify the client with new the new memorize value vector
    /// </summary>
    public void MemorizedNumberClearAll()
    {
        m_memorizedNumbers.Clear();

        m_currentCalculatorEngine.ProcessCommand(CCommand.IdcMclear);
        this.SetMemorizedNumbersString();
    }

    /// <summary>
    /// Helper function that selects a memory from the vector and set it to CCalcEngine
    /// Saved RAT number needs to be copied and passed in, as CCalcEngine destroyed the passed in RAT
    /// </summary>
    /// <param name="indexOfMemory">Index of the target memory</param>
    public void MemorizedNumberSelect(int indexOfMemory)
    {
        if (m_currentCalculatorEngine.FInErrorState())
        {
            return;
        }

        var memoryObject = m_memorizedNumbers[(int)indexOfMemory];
        m_currentCalculatorEngine.PersistedMemObject(memoryObject);
    }

    /// <summary>
    /// Helper function that needs to be executed when memory is modified
    /// When memory is modified, destroy the old RAT and put the new RAT in vector
    /// </summary>
    /// <param name="indexOfMemory">Index of the target memory</param>
    public void MemorizedNumberChanged(int indexOfMemory)
    {
        if (m_currentCalculatorEngine.FInErrorState())
        {
            return;
        }

        var memoryObject = m_currentCalculatorEngine.PersistedMemObject();
        if (memoryObject is not null)
        {
            m_memorizedNumbers[(int)indexOfMemory] = memoryObject;
        }
    }

    public IList<HISTORYITEM> GetHistoryItems()
    {
        return m_pHistory.History;
    }

    public IList<HISTORYITEM> GetHistoryItems(CalculatorMode mode)
    {
        return (mode == CalculatorMode.Standard) ? m_pStdHistory.History : m_pSciHistory.History;
    }

    public void SetHistoryItems(IList<HISTORYITEM> historyItems)
    {
        if (historyItems is null)
        {
            throw new ArgumentNullException(nameof(historyItems));
        }

        foreach (var historyItem in historyItems)
        {
            var index = m_pHistory.AddItem(historyItem);
            OnHistoryItemAdded(index);
        }
    }

    public HISTORYITEM GetHistoryItem(uint uIdx)
    {
        return m_pHistory.GetHistoryItem(uIdx);
    }

    public void OnHistoryItemAdded(uint addedItemIndex)
    {
        m_displayCallback.OnHistoryItemAdded(addedItemIndex);
    }

    public bool RemoveHistoryItem(int uIdx)
    {
        return m_pHistory.RemoveItem(uIdx);
    }

    public void ClearHistory()
    {
        m_pHistory.ClearHistory();
    }

    public void SetRadix(RadixType iRadixType)
    {
        switch (iRadixType)
        {
            case RadixType.Hex:
                m_currentCalculatorEngine.ProcessCommand(CCommand.IdcHex);
                break;
            case RadixType.Dec:
                m_currentCalculatorEngine.ProcessCommand(CCommand.IdcDec);
                break;
            case RadixType.Octal:
                m_currentCalculatorEngine.ProcessCommand(CCommand.IdcOct);
                break;
            case RadixType.Binary:
                m_currentCalculatorEngine.ProcessCommand(CCommand.IdcBin);
                break;
            default:
                break;
        }

        SetMemorizedNumbersString();
    }

    public void SetMemorizedNumbersString()
    {
        List<wstring> resultVector = new List<wstring>();
        foreach (var memoryItem in m_memorizedNumbers)
        {
            var radix = m_currentCalculatorEngine.CurrentRadix;
            wstring stringValue = m_currentCalculatorEngine.GetStringForDisplay(memoryItem, radix);

            if (stringValue.Length != 0)
            {
                resultVector.Add(m_currentCalculatorEngine.GroupDigitsPerRadix(stringValue, radix));
            }
        }

        m_displayCallback.SetMemorizedNumbers(resultVector);
    }

    public Command CurrentDegreeMode
    {
        get
        {
            if (m_currentDegreeMode == Command.None)
            {
                m_currentDegreeMode = Command.Deg;
            }

            return m_currentDegreeMode;
        }
    }

    public wstring_view GetUnaryOperatorDisplayName(int command, bool inverse, AngleType angleType)
    {
        return m_currentCalculatorEngine.OpCodeToUnaryString(command, inverse, angleType);
    }

    public wstring_view GetOperatorDisplayName(int command)
    {
        return m_currentCalculatorEngine.OpCodeToString(command);
    }

    public wstring GetResultForRadix(uint32_t radix, int32_t precision, bool groupDigitsPerRadix)
    {
        return m_currentCalculatorEngine is not null
            ? m_currentCalculatorEngine.GetCurrentResultForRadix(radix, precision, groupDigitsPerRadix)
            : "";
    }

    public void SetPrecision(int32_t precision)
    {
        m_currentCalculatorEngine?.ChangePrecision(precision);
    }

    public void UpdateMaxIntDigits()
    {
        m_currentCalculatorEngine.UpdateMaxIntDigits();
    }

    public wchar_t DecimalSeparator()
    {
        return m_currentCalculatorEngine is not null
            ? m_currentCalculatorEngine.DecimalSeparator()
            : m_resourceProvider.GetCEngineString("sDecimal")[0];
    }

    public bool IsEngineRecording()
    {
        return m_currentCalculatorEngine.FInRecordingState();
    }

    public bool IsInputEmpty()
    {
        return m_currentCalculatorEngine.IsInputEmpty();
    }

    public void SetInHistoryItemLoadMode(bool isHistoryItemLoadMode)
    {
        m_inHistoryItemLoadMode = isHistoryItemLoadMode;
    }

    public IList<IExpressionCommand> GetDisplayCommandsSnapshot()
    {
        return m_currentCalculatorEngine.GetHistoryCollectorCommandsSnapshot();
    }
}
