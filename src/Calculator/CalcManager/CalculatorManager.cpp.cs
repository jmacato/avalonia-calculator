// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CalcEngine;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;
using wchar_t = char;
using wstring_view = string;
using WString = string;
using size_t = ulong;
using OpCode = uint;
using System.Collections.Generic;

namespace CalculationManager;

internal sealed partial class CalculatorManager : ICalcDisplay
//
// #ifndef _MSC_VER
// #define __pragma(x)
// #endif
{
    const size_t MAX_HISTORY_ITEMS = 20;
    public CalculatorManager(ICalcDisplay displayCallback, IResourceProvider resourceProvider)
    {
        m_displayCallback = displayCallback;
        m_resourceProvider = (resourceProvider);
        m_inHistoryItemLoadMode = (false);
        m_persistedPrimaryValue = null;
        m_isExponentialFormat = (false);
        m_currentDegreeMode = (Command.NULL);
        m_pStdHistory = (new CalculatorHistory(MAX_HISTORY_ITEMS));
        m_pSciHistory = (new CalculatorHistory(MAX_HISTORY_ITEMS));
        SetStandardMode();
    }

    /// <summary>
    /// Call the callback function using passed in IDisplayHelper.
    /// Used to set the primary display value on ViewModel
    /// </summary>
    /// <param name = "text">WString representing text to be displayed</param>
    public void SetPrimaryDisplay(WString displayString, bool isError)
    {
        if (!m_inHistoryItemLoadMode)
        {
            m_displayCallback.SetPrimaryDisplay(displayString, isError);
        }
    }

    public void SetIsInError(bool isError)
    {
        m_displayCallback.SetIsInError(isError);
    }

    public void DisplayPasteError()
    {
        m_currentCalculatorEngine.DisplayError(CalcErr.CALC_E_DOMAIN /*code for "Invalid input" error*/);
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
    /// <param name = "expressionString">WString representing expression to be displayed</param>
    public void SetExpressionDisplay(List<(WString, int)> tokens, List<IExpressionCommand> commands)
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
    /// <param name = "memorizedNumber">vector containing WString values of memorized numbers</param>
    public void SetMemorizedNumbers(List<WString> memorizedNumbers)
    {
        m_displayCallback.SetMemorizedNumbers(memorizedNumbers);
    }

    /// <summary>
    /// Callback from the engine
    /// </summary>
    /// <param name = "parenthesisCount">string containing the parenthesis count</param>
    public void SetParenthesisNumber(uint parenthesisCount)
    {
        m_displayCallback.SetParenthesisNumber(parenthesisCount);
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
            m_scientificCalculatorEngine.ProcessCommand(CCommand.IDC_DEG);
            m_scientificCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
            if (m_isExponentialFormat)
            {
                m_isExponentialFormat = false;
                m_scientificCalculatorEngine.ProcessCommand(CCommand.IDC_FE);
            }
        }

        if (m_programmerCalculatorEngine != null)
        {
            m_programmerCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
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
            m_standardCalculatorEngine = new CCalcEngine(false /* Respect Order of Operations */, false /* Set to Integer Mode */, m_resourceProvider, this, m_pStdHistory);
            m_standardCalculatorEngine.InitialOneTimeOnlySetup(m_resourceProvider);
        }

        m_currentCalculatorEngine = m_standardCalculatorEngine;
        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_DEC);
        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
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
            m_scientificCalculatorEngine = new CCalcEngine(true /* Respect Order of Operations */, false /* Set to Integer Mode */, m_resourceProvider, this, m_pSciHistory);
            m_scientificCalculatorEngine.InitialOneTimeOnlySetup(m_resourceProvider);
        }

        m_currentCalculatorEngine = m_scientificCalculatorEngine;
        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_DEC);
        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
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
            m_programmerCalculatorEngine = new CCalcEngine(true /* Respect Order of Operations */, true /* Set to Integer Mode */, m_resourceProvider, this, null);
            m_programmerCalculatorEngine.InitialOneTimeOnlySetup(m_resourceProvider);
        }

        m_currentCalculatorEngine = m_programmerCalculatorEngine;
        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_DEC);
        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
        m_currentCalculatorEngine.ChangePrecision((int)(CalculatorPrecision.ProgrammerModePrecision));
    }

    /// <summary>
    /// Send command to the Calc Engine
    /// Cast Command Enum to OpCode.
    /// Handle special commands such as mode change and combination of two commands.
    /// </summary>
    /// <param name = "command">Enum Command</command>
    public void SendCommand(Command command)
    {
        // When the expression line is cleared, we save the current state, which includes,
        // primary display, memory, and degree mode
        if (command == Command.CLEAR || command == Command.EQU || command == Command.ModeBasic || command == Command.ModeScientific || command == Command.ModeProgrammer)
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

            InputChanged();
            return;
        }

        if (command == Command.DEG || command == Command.RAD || command == Command.GRAD)
        {
            m_currentDegreeMode = command;
        }

        _ = HandleSpecialCommandGroup1(command) || HandleSpecialCommandGroup2(command);
        InputChanged();
    }

    private bool HandleSpecialCommandGroup1(global::CalculationManager.Command command)
    {
        switch (command)
        {
            case Command.ASIN:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.SIN));
                break;
            case Command.ACOS:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.COS));
                break;
            case Command.ATAN:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.TAN));
                break;
            case Command.POWE:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.LN));
                break;
            case Command.ASINH:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.SINH));
                break;
            case Command.ACOSH:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.COSH));
                break;
            case Command.ATANH:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.TANH));
                break;
            case Command.ASEC:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.SEC));
                break;
            default:
                return false;
        }

        return true;
    }

    private bool HandleSpecialCommandGroup2(global::CalculationManager.Command command)
    {
        switch (command)
        {
            case Command.ACSC:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.CSC));
                break;
            case Command.ACOT:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.COT));
                break;
            case Command.ASECH:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.SECH));
                break;
            case Command.ACSCH:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.CSCH));
                break;
            case Command.ACOTH:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.INV));
                m_currentCalculatorEngine.ProcessCommand((OpCode)(Command.COTH));
                break;
            case Command.FE:
                m_isExponentialFormat = !m_isExponentialFormat;
                goto default;
            default:
                m_currentCalculatorEngine.ProcessCommand((OpCode)(command));
                break;
        }

        return true;
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

        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_RECALL);
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

        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_STORE);
        var memoryObjectPtr = m_currentCalculatorEngine.PersistedMemObject();
        if (memoryObjectPtr != null)
        {
            m_memorizedNumbers.Insert(0, memoryObjectPtr);
        }

        if (m_memorizedNumbers.Count > m_maximumMemorySize)
        {
            //TODO: check
            m_memorizedNumbers.RemoveRange((int)(m_maximumMemorySize - 1), (int)(m_memorizedNumbers.Count - m_maximumMemorySize));
        }

        this.SetMemorizedNumbersString();
    }

    /// <summary>
    /// Recall the memorized number.
    /// The memorized number gets loaded to the primary display
    /// </summary>
    /// <param name = "indexOfMemory">Index of the target memory</param>
    public void MemorizedNumberLoad(int indexOfMemory)
    {
        if (m_currentCalculatorEngine.FInErrorState())
        {
            return;
        }

        this.MemorizedNumberSelect(indexOfMemory);
        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_RECALL);
        InputChanged();
    }

    /// <summary>
    /// Do the addition to the selected memory
    /// It adds primary display value to the selected memory
    /// Notify the client with new the new memorize value vector
    /// </summary>
    /// <param name = "indexOfMemory">Index of the target memory</param>
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
            m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_MPLUS);
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
    /// <param name = "indexOfMemory">Index of the target memory</param>
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
            m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_MMINUS);
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
        m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_MCLEAR);
        this.SetMemorizedNumbersString();
    }

    /// <summary>
    /// Helper function that selects a memory from the vector and set it to CCalcEngine
    /// Saved RatPakRAT number needs to be copied and passed in, as CCalcEngine destroyed the passed in RatPakRAT
    /// </summary>
    /// <param name = "indexOfMemory">Index of the target memory</param>
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
    /// When memory is modified, destroy the old RatPakRAT and put the new RatPakRAT in vector
    /// </summary>
    /// <param name = "indexOfMemory">Index of the target memory</param>
    public void MemorizedNumberChanged(int indexOfMemory)
    {
        if (m_currentCalculatorEngine.FInErrorState())
        {
            return;
        }

        var memoryObject = m_currentCalculatorEngine.PersistedMemObject();
        if (memoryObject != null)
        {
            m_memorizedNumbers[(int)indexOfMemory] = memoryObject;
        }
    }

    public List<HISTORYITEM> GetHistoryItems()
    {
        return m_pHistory.GetHistory();
    }

    public List<HISTORYITEM> GetHistoryItems(CalculatorMode mode)
    {
        return (mode == CalculatorMode.Standard) ? m_pStdHistory.GetHistory() : m_pSciHistory.GetHistory();
    }

    public void SetHistoryItems(IEnumerable<HISTORYITEM> historyItems)
    {
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
                m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_HEX);
                break;
            case RadixType.Decimal:
                m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_DEC);
                break;
            case RadixType.Octal:
                m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_OCT);
                break;
            case RadixType.Binary:
                m_currentCalculatorEngine.ProcessCommand(CCommand.IDC_BIN);
                break;
            default:
                break;
        }

        SetMemorizedNumbersString();
    }

    public void SetMemorizedNumbersString()
    {
        List<WString> resultVector = new List<WString>();
        foreach (var memoryItem in m_memorizedNumbers)
        {
            var radix = m_currentCalculatorEngine.GetCurrentRadix();
            WString stringValue = m_currentCalculatorEngine.GetStringForDisplay(memoryItem, radix);
            if (stringValue.Length != 0)
            {
                resultVector.Add(m_currentCalculatorEngine.GroupDigitsPerRadix(stringValue, radix));
            }
        }

        m_displayCallback.SetMemorizedNumbers(resultVector);
    }

    public Command GetCurrentDegreeMode()
    {
        if (m_currentDegreeMode == Command.NULL)
        {
            m_currentDegreeMode = Command.DEG;
        }

        return m_currentDegreeMode;
    }

    public WString GetResultForRadix(uint32_t radix, int32_t precision, bool groupDigitsPerRadix)
    {
        return m_currentCalculatorEngine is not null ? m_currentCalculatorEngine.GetCurrentResultForRadix(radix, precision, groupDigitsPerRadix) : "";
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
        return m_currentCalculatorEngine is not null ? m_currentCalculatorEngine.DecimalSeparator() : m_resourceProvider.GetCEngineString("sDecimal")[0];
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

    public List<IExpressionCommand> GetDisplayCommandsSnapshot()
    {
        return m_currentCalculatorEngine.GetHistoryCollectorCommandsSnapshot();
    }
}
