// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//#include  "pch.h"
//#include  "StandardCalculatorViewModel.h"
//#include  "Common/CalculatorButtonCommandParameter.h"
//#include  "Common/LocalizationStringUtil.h"
//#include  "Common/LocalizationSettings.h"
//#include  "Common/CopyPasteManager.h"
//#include  "Common/TraceLogger.h"
using CalcEngine;
using CalculationManager;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.Automation;
using CalculatorApp.ViewModel.Snapshot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static CalcEngine.RatPak;
using RawTokenCollection = System.Collections.Generic.List<(string, int)>;

namespace CalculatorApp.ViewModel;

public sealed partial class StandardCalculatorViewModel
{
    const int StandardModePrecision = 16;
    const int ScientificModePrecision = 32;
    const int ProgrammerModePrecision = 64;
    //StringReference IsStandardPropertyName("IsStandard");
    //StringReference IsScientificPropertyName("IsScientific");
    //StringReference IsProgrammerPropertyName("IsProgrammer");
    //StringReference IsAlwaysOnTopPropertyName("IsAlwaysOnTop");
    //StringReference DisplayValuePropertyName("DisplayValue");
    //StringReference CalculationResultAutomationNamePropertyName("CalculationResultAutomationName");
    //StringReference IsBitFlipCheckedPropertyName("IsBitFlipChecked");
    const string CalcAlwaysOnTop = ("CalcAlwaysOnTop");
    const string CalcBackToFullView = ("CalcBackToFullView");
    static List<int> GetCommandsFromExpressionCommands(IEnumerable<IExpressionCommand> expressionCommands)
    {
        List<int> commands = new List<int>();
        foreach (var command in expressionCommands)
        {
            CommandType commandType = command.GetCommandType();
            if (commandType == CommandType.UnaryCommand)
            {
                IUnaryCommand spCommand = (IUnaryCommand)(command);
                List<int> unaryCommands = spCommand.GetCommands().ToList();
                foreach (int nUCode in unaryCommands)
                {
                    commands.Add(nUCode);
                }
            }

            if (commandType == CommandType.BinaryCommand)
            {
                IBinaryCommand spCommand = (IBinaryCommand)(command);
                commands.Add(spCommand.GetCommand());
            }

            if (commandType == CommandType.Parentheses)
            {
                IParenthesisCommand spCommand = (IParenthesisCommand)(command);
                commands.Add(spCommand.GetCommand());
            }

            if (commandType == CommandType.OperandCommand)
            {
                IOpndCommand spCommand = (IOpndCommand)(command);
                List<int> opndCommands = spCommand.GetCommands().ToList();
                bool fNeedIDCSign = spCommand.IsNegative();
                foreach (int nOCode in opndCommands)
                {
                    commands.Add(nOCode);
                    if (fNeedIDCSign && nOCode != CCommand.Idc0)
                    {
                        commands.Add((int)(CalculationManager.Command.CommandSIGN));
                        fNeedIDCSign = false;
                    }
                }
            }
        }

        return commands;
    }

    public StandardCalculatorViewModel()
    {
        ;
        m_DisplayValue = ("0");
        m_DecimalDisplayValue = ("0");
        m_HexDisplayValue = ("0");
        m_BinaryDisplayValue = ("0");
        m_OctalDisplayValue = ("0");
        m_BinaryDigits = (new ObservableCollection<bool>(Enumerable.Repeat(false, 64)));
        m_standardCalculatorManager = new CalculatorManager(m_calculatorDisplay, m_resourceProvider);
        m_ExpressionTokens = (new ObservableCollection<DisplayExpressionToken>());
        m_MemorizedNumbers = (new ObservableCollection<MemoryItemViewModel>());
        m_IsMemoryEmpty = (true);
        m_IsFToEChecked = (false);
        m_IsShiftProgrammerChecked = (false);
        m_valueBitLength = (BitLength.SixtyFourBits);
        m_isBitFlipChecked = (false);
        m_IsBinaryBitFlippingEnabled = (false);
        m_CurrentRadixType = (NumberBase.DecBase);
        m_CurrentAngleType = (CalculatorButtonId.Degree);
        m_Announcement = (null);
        m_OpenParenthesisCount = (0);
        m_feedbackForButtonPress = (null);
        m_isRtlLanguage = (false);
        m_localizedMaxDigitsReachedAutomationFormat = string.Empty;
        m_localizedButtonPressFeedbackAutomationFormat = string.Empty;
        m_localizedMemorySavedAutomationFormat = string.Empty;
        m_localizedMemoryItemChangedAutomationFormat = string.Empty;
        m_localizedMemoryItemClearedAutomationFormat = string.Empty;
        m_localizedMemoryCleared = string.Empty;
        m_localizedOpenParenthesisCountChangedAutomationFormat = string.Empty;
        m_localizedNoRightParenthesisAddedFormat = string.Empty;
        m_TokenPosition = (-1);
        m_isLastOperationHistoryLoad = (false);
        WeakReference calculatorViewModel = new WeakReference(this);
        var appResourceProvider = AppResourceProvider.Instance;
        m_calculatorDisplay.SetCallback(calculatorViewModel);
        m_expressionAutomationNameFormat = appResourceProvider.GetResourceString(CalculatorResourceKeys.CalculatorExpression);
        m_localizedCalculationResultAutomationFormat = appResourceProvider.GetResourceString(CalculatorResourceKeys.CalculatorResults);
        m_localizedCalculationResultDecimalAutomationFormat = appResourceProvider.GetResourceString(CalculatorResourceKeys.CalculatorResults_DecimalSeparator_Announced);
        m_localizedHexaDecimalAutomationFormat = appResourceProvider.GetResourceString(CalculatorResourceKeys.HexButton);
        m_localizedDecimalAutomationFormat = appResourceProvider.GetResourceString(CalculatorResourceKeys.DecButton);
        m_localizedOctalAutomationFormat = appResourceProvider.GetResourceString(CalculatorResourceKeys.OctButton);
        m_localizedBinaryAutomationFormat = appResourceProvider.GetResourceString(CalculatorResourceKeys.BinButton);
        // Initialize the Automation Name
        CalculationResultAutomationName = GetLocalizedStringFormat(m_localizedCalculationResultAutomationFormat, m_DisplayValue);
        CalculationExpressionAutomationName = GetLocalizedStringFormat(m_expressionAutomationNameFormat, "");
        // Initialize history view model
        m_HistoryVM = new HistoryViewModel(m_standardCalculatorManager);
        m_HistoryVM.SetCalculatorDisplay(m_calculatorDisplay);
        m_decimalSeparator = LocalizationSettings.Instance.DecimalSeparator;
        m_isRtlLanguage = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
        IsEditingEnabled = false;
        IsUnaryOperatorEnabled = true;
        IsBinaryOperatorEnabled = true;
        IsOperandEnabled = true;
        IsNegateEnabled = true;
        IsDecimalEnabled = true;
        AreProgrammerRadixOperatorsVisible = false;
    }

    String LocalizeDisplayValue(string displayValue)
    {
        string result = (displayValue);
        // Adds leading padding 0's to Programmer Mode's Binary Display
        if (IsProgrammer && CurrentRadixType == NumberBase.BinBase)
        {
            result = AddPadding(result);
        }

        LocalizationSettings.LocalizeDisplayValue(ref result);
        return (result);
    }

    String CalculateNarratorDisplayValue(string displayValue, string localizedDisplayValue)
    {
        string localizedValue = localizedDisplayValue;
        string automationFormat = m_localizedCalculationResultAutomationFormat;
        // The narrator doesn't read the decimalSeparator if it's the last character
        if (displayValue.Length > 0 && displayValue[^1] == m_decimalSeparator)
        {
            // remove the decimal separator, to avoid a long pause between words
            localizedValue = LocalizeDisplayValue(displayValue.Substring(0, displayValue.Length - 1));
            // Use a format which has a word in the decimal separator's place
            // "The Display is 10 point"
            automationFormat = m_localizedCalculationResultDecimalAutomationFormat;
        }

        // In Programmer modes using non-base10, we want the strings to be read as literal digits.
        if (IsProgrammer && CurrentRadixType != NumberBase.DecBase)
        {
            localizedValue = GetNarratorStringReadRawNumbers(localizedValue);
        }

        return GetLocalizedStringFormat(automationFormat, localizedValue);
    }

    static String GetNarratorStringReadRawNumbers(string localizedDisplayValue)
    {
        StringBuilder ws = new StringBuilder();
        LocalizationSettings locSettings = LocalizationSettings.Instance;
        // Insert a space after each digit in the string, to force Narrator to read them as separate numbers.
        foreach (var c in localizedDisplayValue)
        {
            ws.Append(c);
            if (locSettings.IsLocalizedHexDigit(c))
            {
                ws.Append(' ');
            }
        }

        return (ws.ToString());
    }

    void ICalcDisplay.SetPrimaryDisplay(string pszText, bool isError) =>
        SetPrimaryDisplay(pszText, isError);

    internal void SetPrimaryDisplay(string pszText, bool isError)
    {
        if (pszText is null)
        {
            System.ArgumentNullException.ThrowIfNull(pszText);
        }
        string primaryText = pszText;
        string localizedDisplayStringValue = LocalizeDisplayValue(primaryText);
        // Set this variable before the DisplayValue is modified, Otherwise the DisplayValue will
        // not match what the narrator is saying
        m_CalculationResultAutomationName = CalculateNarratorDisplayValue(primaryText, localizedDisplayStringValue);
        AreAlwaysOnTopResultsUpdated = false;
        if (DisplayValue != localizedDisplayStringValue)
        {
            DisplayValue = localizedDisplayStringValue;
            AreAlwaysOnTopResultsUpdated = true;
        }

        IsInError = isError;
        if (IsProgrammer)
        {
            UpdateProgrammerPanelDisplay();
        }
    }

    public void DisplayPasteError()
    {
        m_standardCalculatorManager.DisplayPasteError();
    }

    public void SetParenthesisNumber(uint count)
    {
        if (m_OpenParenthesisCount == count)
        {
            return;
        }

        OpenParenthesisCount = count;
        if (IsProgrammer || IsScientific)
        {
            SetOpenParenthesisCountNarratorAnnouncement();
        }
    }

    public void SetOpenParenthesisCountNarratorAnnouncement()
    {
        string localizedParenthesisCount = m_OpenParenthesisCount.ToString(CultureInfo.InvariantCulture);
        LocalizationSettings.LocalizeDisplayValue(ref localizedParenthesisCount);
        if (m_localizedOpenParenthesisCountChangedAutomationFormat == null)
        {
            m_localizedOpenParenthesisCountChangedAutomationFormat = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.OpenParenthesisCountAutomationFormat);
        }

        string announcement = LocalizationStringUtil.GetLocalizedString(m_localizedOpenParenthesisCountChangedAutomationFormat, (localizedParenthesisCount));
        Announcement = NarratorAnnouncement.GetOpenParenthesisCountChangedAnnouncement(announcement);
    }

    public void OnNoRightParenAdded()
    {
        SetNoParenAddedNarratorAnnouncement();
    }

    public void SetNoParenAddedNarratorAnnouncement()
    {
        if (m_localizedNoRightParenthesisAddedFormat == null)
        {
            m_localizedNoRightParenthesisAddedFormat = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.NoParenthesisAdded);
        }

        Announcement = NarratorAnnouncement.GetNoRightParenthesisAddedAnnouncement(m_localizedNoRightParenthesisAddedFormat);
    }

    void DisableButtons(CommandType selectedExpressionCommandType)
    {
        if (selectedExpressionCommandType == CommandType.OperandCommand)
        {
            IsBinaryOperatorEnabled = false;
            IsUnaryOperatorEnabled = false;
            IsOperandEnabled = true;
            IsNegateEnabled = true;
            IsDecimalEnabled = true;
        }

        if (selectedExpressionCommandType == CommandType.BinaryCommand)
        {
            IsBinaryOperatorEnabled = true;
            IsUnaryOperatorEnabled = false;
            IsOperandEnabled = false;
            IsNegateEnabled = false;
            IsDecimalEnabled = false;
        }

        if (selectedExpressionCommandType == CommandType.UnaryCommand)
        {
            IsBinaryOperatorEnabled = false;
            IsUnaryOperatorEnabled = true;
            IsOperandEnabled = false;
            IsNegateEnabled = true;
            IsDecimalEnabled = false;
        }
    }

    public void SetExpressionDisplay(IReadOnlyList<(string, int)> tokens, IReadOnlyList<IExpressionCommand> commands)
    {
        m_tokens = tokens.ToList();
        m_commands = commands.ToList();
        if (!IsEditingEnabled)
        {
            SetTokens(m_tokens);
        }

        CalculationExpressionAutomationName = GetCalculatorExpressionAutomationName();
        AreTokensUpdated = true;
    }

    public void SetHistoryExpressionDisplay(IReadOnlyList<(string, int)> tokens, IReadOnlyList<IExpressionCommand> commands)
    {
        m_tokens = (tokens).ToList();
        m_commands = (commands).ToList();
        ;
        IsEditingEnabled = false;
        // Setting the History Item Load Mode so that UI does not get updated with recalculation of every token
        m_standardCalculatorManager.SetInHistoryItemLoadMode(true);
        Recalculate(true);
        m_standardCalculatorManager.SetInHistoryItemLoadMode(false);
        m_isLastOperationHistoryLoad = true;
    }

    void SetTokens(List<(string, int)> tokens)
    {
        AreTokensUpdated = false;
        int nTokens = tokens.Count;
        if (nTokens == 0)
        {
            m_ExpressionTokens.Clear();
            return;
        }

        LocalizationSettings localizer = LocalizationSettings.Instance;
        string separator = " ";
        for (int i = 0; i < nTokens; ++i)
        {
            var currentToken = tokens[i];
            Common.TokenType type;
            bool isEditable = currentToken.Item2 != -1;
            //TODO: Check if this mutates currentToken at all.
            LocalizationSettings.LocalizeDisplayValue(ref currentToken.Item1);
            if (!isEditable)
            {
                type = currentToken.Item1 == separator ? TokenType.Separator : TokenType.Operator;
            }
            else
            {
                IExpressionCommand command = m_commands[currentToken.Item2];
                type = command.GetCommandType() == CommandType.OperandCommand ? TokenType.Operand : TokenType.Operator;
            }

            var currentTokenString = (currentToken.Item1);
            if (i < m_ExpressionTokens.Count)
            {
                var existingItem = m_ExpressionTokens[i];
                if (type == existingItem.Type && existingItem.Token.Equals(currentTokenString, StringComparison.Ordinal))
                {
                    existingItem.TokenPosition = i;
                    existingItem.IsTokenEditable = isEditable;
                    existingItem.CommandIndex = 0;
                }
                else
                {
                    var expressionToken = new DisplayExpressionToken(currentTokenString, i, isEditable, type);
                    m_ExpressionTokens.Insert(i, expressionToken);
                }
            }
            else
            {
                var expressionToken = new DisplayExpressionToken(currentTokenString, i, isEditable, type);
                m_ExpressionTokens.Add(expressionToken);
            }
        }

        while (m_ExpressionTokens.Count != nTokens)
        {
            m_ExpressionTokens.RemoveAt(m_ExpressionTokens.Count - 1); //.RemoveAtEnd();
        }
    }

    String GetCalculatorExpressionAutomationName()
    {
        string expression = "";
        foreach (var token in m_ExpressionTokens)
        {
            expression += LocalizationStringUtil.GetNarratorReadableToken(token.Token);
        }

        return GetLocalizedStringFormat(m_expressionAutomationNameFormat, expression);
    }

    public void SetMemorizedNumbers(IList<string> memorizedNumbers)
    {
        System.ArgumentNullException.ThrowIfNull(memorizedNumbers);
        LocalizationSettings localizer = LocalizationSettings.Instance;
        if (memorizedNumbers.Count == 0) // Memory has been cleared
        {
            MemorizedNumbers.Clear();
            IsMemoryEmpty = true;
        }
        // A new value is added to the memory
        else if (memorizedNumbers.Count > MemorizedNumbers.Count)
        {
            while (memorizedNumbers.Count > MemorizedNumbers.Count)
            {
                int newValuePosition = memorizedNumbers.Count - MemorizedNumbers.Count - 1;
                var stringValue = memorizedNumbers[newValuePosition];
                MemoryItemViewModel memorySlot = new MemoryItemViewModel(this);
                memorySlot.Position = 0;
                LocalizationSettings.LocalizeDisplayValue(ref stringValue);
                memorySlot.Value = (stringValue);
                MemorizedNumbers.Insert(0, memorySlot);
                IsMemoryEmpty = IsAlwaysOnTop;
                // Update the slot position for the rest of the slots
                for (int i = 1; i < MemorizedNumbers.Count; i++)
                {
                    MemorizedNumbers[i].Position++;
                }
            }
        }
        else if (memorizedNumbers.Count == MemorizedNumbers.Count) // Either M+ or M-
        {
            for (int i = 0; i < MemorizedNumbers.Count; i++)
            {
                var nestringValue = memorizedNumbers[i];
                LocalizationSettings.LocalizeDisplayValue(ref nestringValue);
                // If the value is different, update the value
                if (MemorizedNumbers[i].Value != (nestringValue))
                {
                    MemorizedNumbers[i].Value = (nestringValue);
                }
            }
        }
    }

    public void FtoEButtonToggled()
    {
        OnButtonPressed(CalculatorButtonId.FToE);
    }

    void HandleUpdatedOperandData(Command cmdenum)
    {
        DisplayExpressionToken displayExpressionToken = ExpressionTokens[m_TokenPosition];
        if (displayExpressionToken == null)
        {
            return;
        }

        if ((displayExpressionToken.Token == null) || (displayExpressionToken.Token.Length == 0))
        {
            displayExpressionToken.CommandIndex = 0;
        }

        if (!TryGetOperandEditCharacter(cmdenum, out char ch))
        {
            return;
        }

        int commandIndex = displayExpressionToken.CommandIndex;
        string updatedData;
        if (IsOperandTextCompletelySelected)
        {
            m_selectedExpressionLastData = "";
            updatedData = ch == 'x' ? string.Empty : ch.ToString();
            commandIndex = updatedData.Length;
            IsOperandTextCompletelySelected = false;
        }
        else if (ch == 'x')
        {
            if (commandIndex <= 0 || commandIndex > m_selectedExpressionLastData.Length)
            {
                return;
            }

            commandIndex--;
            updatedData = RemoveCharacter(m_selectedExpressionLastData, commandIndex);
        }
        else
        {
            if (m_selectedExpressionLastData.Length >= 50 ||
                commandIndex < 0 ||
                commandIndex > m_selectedExpressionLastData.Length)
            {
                return;
            }

            updatedData = InsertCharacter(m_selectedExpressionLastData, commandIndex, ch);
            commandIndex++;
        }

        UpdateOperand(m_TokenPosition, updatedData);
        displayExpressionToken.Token = updatedData;
        IsOperandUpdatedUsingViewModel = true;
        displayExpressionToken.CommandIndex = commandIndex;
    }

    private static bool TryGetOperandEditCharacter(Command command, out char character)
    {
        if (command >= Command.Command0 && command <= Command.Command9)
        {
            character = (char)('0' + ((int)command - (int)Command.Command0));
            return true;
        }

        character = command switch
        {
            Command.CommandPNT => '.',
            Command.CommandBACK => 'x',
            _ => '\0'
        };
        return character != '\0';
    }

    private static string RemoveCharacter(string source, int index) =>
        string.Create(source.Length - 1, (Source: source, Index: index), static (destination, state) =>
        {
            state.Source.AsSpan(0, state.Index).CopyTo(destination);
            state.Source.AsSpan(state.Index + 1).CopyTo(destination[state.Index..]);
        });

    private static string InsertCharacter(string source, int index, char character) =>
        string.Create(source.Length + 1, (Source: source, Index: index, Character: character), static (destination, state) =>
        {
            state.Source.AsSpan(0, state.Index).CopyTo(destination);
            destination[state.Index] = state.Character;
            state.Source.AsSpan(state.Index).CopyTo(destination[(state.Index + 1)..]);
        });

    static bool IsOperator(Command cmdenum)
    {
        if ((cmdenum >= Command.Command0 && cmdenum <= Command.Command9) || (cmdenum == Command.CommandPNT) || (cmdenum == Command.CommandBACK) || (cmdenum == Command.CommandEXP) || (cmdenum == Command.CommandFE) || (cmdenum == Command.ModeBasic) || (cmdenum == Command.ModeProgrammer) || (cmdenum == Command.ModeScientific) || (cmdenum == Command.CommandINV) || (cmdenum == Command.CommandCENTR) || (cmdenum == Command.CommandDEG) || (cmdenum == Command.CommandRAD) || (cmdenum == Command.CommandGRAD) || ((cmdenum >= Command.CommandBINEDITSTART) && (cmdenum <= Command.BinEditEnd)))
        {
            return false;
        }

        return true;
    }

    void OnButtonPressed(object? parameter)
    {
        m_feedbackForButtonPress = CalculatorButtonCommandParameter.GetAuditoryFeedbackFromCommandParameter(parameter);
        CalculatorButtonId numOpEnum = CalculatorButtonCommandParameter.GetOperationFromCommandParameter(parameter);
        Command cmdenum = ConvertToOperatorsEnum(numOpEnum);
        if (IsInError)
        {
            m_standardCalculatorManager.SendCommand(Command.CommandCLEAR);
            if (!IsRecoverableCommand((Command)(numOpEnum)))
            {
                return;
            }
        }

        if (IsEditingEnabled && IsBlockedWhileEditing(numOpEnum))
        {
            return;
        }

        if (IsModeCommand(numOpEnum))
        {
            IsEditingEnabled = false;
        }

        if (numOpEnum == CalculatorButtonId.Memory)
        {
            OnMemoryButtonPressed();
            return;
        }

        if (ClearsFToEState(numOpEnum) && IsFToEChecked)
        {
            // C/CE and mode switches reset exponential display state.
            IsFToEChecked = false;
        }

        if (IsAngleCommand(numOpEnum))
        {
            m_CurrentAngleType = numOpEnum;
        }

        IsOperatorCommand = !IsOperandEntryCommand(cmdenum);

        if (m_isLastOperationHistoryLoad && !IsAngleCommand(numOpEnum))
        {
            IsFToEEnabled = true;
            m_isLastOperationHistoryLoad = false;
        }

        TraceLogger.Instance.UpdateButtonUsage(numOpEnum, GetCalculatorMode());
        m_standardCalculatorManager.SendCommand(cmdenum);
    }

    private static bool IsModeCommand(CalculatorButtonId operation) =>
        operation is CalculatorButtonId.IsStandardMode
            or CalculatorButtonId.IsScientificMode
            or CalculatorButtonId.IsProgrammerMode;

    private static bool IsAngleCommand(CalculatorButtonId operation) =>
        operation is CalculatorButtonId.Degree
            or CalculatorButtonId.Radians
            or CalculatorButtonId.Grads;

    private static bool IsBlockedWhileEditing(CalculatorButtonId operation) =>
        !IsModeCommand(operation) && operation != CalculatorButtonId.FToE && !IsAngleCommand(operation);

    private static bool ClearsFToEState(CalculatorButtonId operation) =>
        operation is CalculatorButtonId.Clear
            or CalculatorButtonId.ClearEntry
            or CalculatorButtonId.IsStandardMode
            or CalculatorButtonId.IsProgrammerMode;

    private static bool IsOperandEntryCommand(Command command) =>
        command is >= Command.Command0 and <= Command.Command9
            or Command.CommandPNT
            or Command.CommandBACK
            or Command.CommandEXP;

    static RadixType GetRadixTypeFromNumberBase(NumberBase @base)
    {
        switch (@base)
        {
            case NumberBase.BinBase:
                return RadixType.Binary;
            case NumberBase.HexBase:
                return RadixType.Hex;
            case NumberBase.OctBase:
                return RadixType.Octal;
            default:
                return RadixType.Dec;
        }
    }

    public void OnCopyCommand(object? parameter)
    {
        CopyPasteManager.CopyToClipboard(GetRawDisplayValue());
        string announcement = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.DisplayCopied);
        Announcement = NarratorAnnouncement.GetDisplayCopiedAnnouncement(announcement);
    }

    public async void OnPasteCommand(object? parameter)
    {
        var that = (this);
        ViewMode mode;
        BitLength bitLengthType = BitLength.Unknown;
        NumberBase numberBase = NumberBase.Unknown;
        if (IsScientific)
        {
            mode = ViewMode.Scientific;
        }
        else if (IsProgrammer)
        {
            mode = ViewMode.Programmer;
            bitLengthType = m_valueBitLength;
            numberBase = CurrentRadixType;
        }
        else
        {
            mode = ViewMode.Standard;
        }

        // if there's nothing to copy early out
        if (IsEditingEnabled || !await CopyPasteManager.HasStringToPasteAsync().ConfigureAwait(true))
        {
            return;
        }

        // Ensure that the paste happens on the UI thread
        string pastedString = await CopyPasteManager.GetStringToPaste(mode, NavCategoryStates.GetGroupType(mode), numberBase, bitLengthType).ConfigureAwait(true);
        // Execute the paste operation
        OnPaste(pastedString);
        // Ensure that the paste happens on the UI thread
        // create_task(CopyPasteManager.GetStringToPaste(mode, NavCategoryStates.GetGroupType(mode), numberBase, bitLengthType))
        //    .then([that, mode](String   pastedString) { that.OnPaste(pastedString); }, concurrency.task_continuation_context.use_current());
    }

    static CalculationManager.Command ConvertToOperatorsEnum(CalculatorButtonId operation)
    {
        return (Command)(operation);
    }

    void OnPaste(String pastedString)
    {
        // If pastedString is invalid("NoOp") then display pasteError else process the string
        if (CopyPasteManager.IsErrorMessage(pastedString))
        {
            this.DisplayPasteError();
            return;
        }

        TraceLogger.LogInputPasted(GetCalculatorMode());
        bool isFirstLegalChar = true;
        m_standardCalculatorManager.SendCommand(Command.CommandCENTR);
        bool sendNegate = false;
        bool isPreviousOperator = false;
        var negateStack = new List<bool>();
        // Iterate through each character pasted, and if it's valid, send it to the model.
        var it = 0;
        while (it < pastedString.Length)
        {
            var buttonInfo = MapCharacterToButtonId(pastedString[it]);
            CalculatorButtonId mappedNumOp = buttonInfo.ButtonId;
            bool canSendNegate = buttonInfo.CanSendNegate;
            if (mappedNumOp == CalculatorButtonId.None)
            {
                ++it;
                continue;
            }

            bool sendCommand = PreparePasteCommand(
                mappedNumOp,
                ref isFirstLegalChar,
                ref isPreviousOperator,
                ref sendNegate,
                ref canSendNegate,
                negateStack);

            if (sendCommand)
            {
                SendPasteCommand(mappedNumOp, canSendNegate, ref sendNegate);
            }

            it += ConsumeExponentSign(pastedString, it, mappedNumOp);
            ++it;
        }
    }

    private static bool PreparePasteCommand(
        CalculatorButtonId operation,
        ref bool isFirstLegalCharacter,
        ref bool isPreviousOperator,
        ref bool sendNegate,
        ref bool canSendNegate,
        List<bool> negateStack)
    {
        bool sendCommand = true;
        if (isFirstLegalCharacter || isPreviousOperator)
        {
            isFirstLegalCharacter = false;
            isPreviousOperator = false;
            if (operation == CalculatorButtonId.Subtract)
            {
                sendNegate = true;
                sendCommand = false;
            }
            else if (operation == CalculatorButtonId.Add)
            {
                sendCommand = false;
            }
        }

        switch (operation)
        {
            case CalculatorButtonId.OpenParenthesis:
                negateStack.Add(sendNegate);
                sendNegate = false;
                break;
            case CalculatorButtonId.CloseParenthesis when negateStack.Count != 0:
                sendNegate = negateStack[^1];
                negateStack.RemoveAt(negateStack.Count - 1);
                canSendNegate = true;
                break;
            case CalculatorButtonId.CloseParenthesis:
                sendCommand = false;
                break;
            case CalculatorButtonId.Add:
            case CalculatorButtonId.Subtract:
            case CalculatorButtonId.Multiply:
            case CalculatorButtonId.Divide:
                isPreviousOperator = true;
                break;
        }

        return sendCommand;
    }

    private void SendPasteCommand(
        CalculatorButtonId operation,
        bool canSendNegate,
        ref bool sendNegate)
    {
        m_standardCalculatorManager.SendCommand(ConvertToOperatorsEnum(operation));
        if (!sendNegate)
        {
            return;
        }

        if (canSendNegate)
        {
            m_standardCalculatorManager.SendCommand(ConvertToOperatorsEnum(CalculatorButtonId.Negate));
        }

        if (operation is not CalculatorButtonId.Zero and not CalculatorButtonId.DecimalSeparator)
        {
            sendNegate = false;
        }
    }

    private int ConsumeExponentSign(string pastedString, int index, CalculatorButtonId operation)
    {
        if (operation != CalculatorButtonId.Exp || index + 1 >= pastedString.Length)
        {
            return 0;
        }

        CalculatorButtonId nextOperation = MapCharacterToButtonId(pastedString[index + 1]).ButtonId;
        if (nextOperation == CalculatorButtonId.Subtract)
        {
            m_standardCalculatorManager.SendCommand(ConvertToOperatorsEnum(CalculatorButtonId.Negate));
            return 1;
        }

        return nextOperation == CalculatorButtonId.Add ? 1 : 0;
    }

    void OnClearMemoryCommand(object? parameter)
    {
        m_standardCalculatorManager.MemorizedNumberClearAll();
        TraceLogger.Instance.UpdateButtonUsage(CalculatorButtonId.MemoryClear, GetCalculatorMode());
        if (m_localizedMemoryCleared == null)
        {
            m_localizedMemoryCleared = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.MemoryCleared);
        }

        Announcement = NarratorAnnouncement.GetMemoryClearedAnnouncement(m_localizedMemoryCleared);
    }

    ButtonInfo MapCharacterToButtonId(char ch)
    {
        if (TryMapDigit(ch, out CalculatorButtonId digit))
        {
            return new ButtonInfo
            {
                ButtonId = digit,
                CanSendNegate = digit != CalculatorButtonId.Zero
            };
        }

        CalculatorButtonId operation = ch switch
        {
            '^' when IsScientific => CalculatorButtonId.XPowerY,
            '%' when IsScientific || IsProgrammer => CalculatorButtonId.Mod,
            'e' or 'E' => IsProgrammer ? CalculatorButtonId.E : CalculatorButtonId.Exp,
            _ when ch == m_decimalSeparator => CalculatorButtonId.DecimalSeparator,
            _ => MapFixedCharacter(ch)
        };

        return new ButtonInfo { ButtonId = operation, CanSendNegate = false };
    }

    private static bool TryMapDigit(char character, out CalculatorButtonId operation)
    {
        int digitOffset;
        if (character is >= '0' and <= '9')
        {
            digitOffset = character - '0';
        }
        else
        {
            LocalizationSettings localization = LocalizationSettings.Instance;
            if (!localization.IsLocalizedDigit(character))
            {
                operation = CalculatorButtonId.None;
                return false;
            }

            digitOffset = character - localization.GetDigitSymbolFromEnUsDigit('0');
        }

        operation = (CalculatorButtonId)((int)CalculatorButtonId.Zero + digitOffset);
        return true;
    }

    private static CalculatorButtonId MapFixedCharacter(char character) => character switch
    {
        '*' => CalculatorButtonId.Multiply,
        '+' => CalculatorButtonId.Add,
        '-' => CalculatorButtonId.Subtract,
        '/' => CalculatorButtonId.Divide,
        '=' => CalculatorButtonId.Equals,
        '(' => CalculatorButtonId.OpenParenthesis,
        ')' => CalculatorButtonId.CloseParenthesis,
        'a' or 'A' => CalculatorButtonId.A,
        'b' or 'B' => CalculatorButtonId.B,
        'c' or 'C' => CalculatorButtonId.C,
        'd' or 'D' => CalculatorButtonId.D,
        'f' or 'F' => CalculatorButtonId.F,
        _ => CalculatorButtonId.None
    };

    public void InputChanged()
    {
        IsInputEmpty = m_standardCalculatorManager.IsInputEmpty();
    }

    public void OnMemoryButtonPressed()
    {
        m_standardCalculatorManager.MemorizeNumber();
        TraceLogger.Instance.UpdateButtonUsage(CalculatorButtonId.Memory, GetCalculatorMode());
        if (m_localizedMemorySavedAutomationFormat == null)
        {
            m_localizedMemorySavedAutomationFormat = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.MemorySave);
        }

        string announcement = LocalizationStringUtil.GetLocalizedString(m_localizedMemorySavedAutomationFormat, m_DisplayValue);
        Announcement = NarratorAnnouncement.GetMemoryItemAddedAnnouncement(announcement);
    }

    public void MemoryItemChanged(uint indexOfMemory)
    {
        if (indexOfMemory < MemorizedNumbers.Count)
        {
            MemoryItemViewModel memSlot = MemorizedNumbers[(int)indexOfMemory];
            string localizedValue = memSlot.Value;
            string localizedIndex = (indexOfMemory + 1).ToString(CultureInfo.InvariantCulture);
            LocalizationSettings.LocalizeDisplayValue(ref localizedIndex);
            if (m_localizedMemoryItemChangedAutomationFormat == null)
            {
                m_localizedMemoryItemChangedAutomationFormat = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.MemoryItemChanged);
            }

            string announcement = LocalizationStringUtil.GetLocalizedString(m_localizedMemoryItemChangedAutomationFormat, (localizedIndex), localizedValue);
            Announcement = NarratorAnnouncement.GetMemoryItemChangedAnnouncement(announcement);
        }
    }

    public void OnMemoryItemPressed(object? memoryItemPosition)
    {
        if (MemorizedNumbers != null && MemorizedNumbers.Count > 0 && memoryItemPosition is int boxedPosition)
        {
            m_standardCalculatorManager.MemorizedNumberLoad(boxedPosition);
            HideMemoryClicked?.Invoke(this, EventArgs.Empty);
            var mode = IsStandard ? ViewMode.Standard : IsScientific ? ViewMode.Scientific : ViewMode.Programmer;
            TraceLogger.LogMemoryItemLoad(mode, MemorizedNumbers.Count, boxedPosition);
        }
    }

    public void OnMemoryAdd(object? memoryItemPosition)
    {
        // M+ will add display to memorylist if memory list is empty.
        if (MemorizedNumbers != null && memoryItemPosition is int boxedPosition)
        {
            TraceLogger.Instance.UpdateButtonUsage(CalculatorButtonId.MemoryAdd, GetCalculatorMode());
            m_standardCalculatorManager.MemorizedNumberAdd(boxedPosition);
        }
    }

    public void OnMemorySubtract(object? memoryItemPosition)
    {
        // M- will add negative of displayed number to memorylist if memory list is empty.
        if (MemorizedNumbers != null && memoryItemPosition is int boxedPosition)
        {
            TraceLogger.Instance.UpdateButtonUsage(CalculatorButtonId.MemorySubtract, GetCalculatorMode());
            m_standardCalculatorManager.MemorizedNumberSubtract(boxedPosition);
        }
    }

    public void OnMemoryClear(object? memoryItemPosition)
    {
        if (MemorizedNumbers != null && MemorizedNumbers.Count > 0 && memoryItemPosition is int boxedPosition)
        {
            if (boxedPosition >= 0)
            {
                var unsignedPosition = (boxedPosition);
                m_standardCalculatorManager.MemorizedNumberClear(unsignedPosition);
                MemorizedNumbers.RemoveAt(unsignedPosition);
                for (int i = 0; i < MemorizedNumbers.Count; i++)
                {
                    MemorizedNumbers[i].Position = i;
                }

                if (MemorizedNumbers.Count == 0)
                {
                    IsMemoryEmpty = true;
                }

                TraceLogger.Instance.UpdateButtonUsage(CalculatorButtonId.MemoryClear, GetCalculatorMode());
                string localizedIndex = (boxedPosition + 1).ToString(CultureInfo.InvariantCulture);
                LocalizationSettings.LocalizeDisplayValue(ref localizedIndex);
                if (m_localizedMemoryItemClearedAutomationFormat == null)
                {
                    m_localizedMemoryItemClearedAutomationFormat = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.MemoryItemCleared);
                }

                string announcement = LocalizationStringUtil.GetLocalizedString(m_localizedMemoryItemClearedAutomationFormat, (localizedIndex));
                Announcement = NarratorAnnouncement.GetMemoryClearedAnnouncement(announcement);
            }
        }
    }

    void OnPropertyChanged(String propertyname)
    {
        if (propertyname == nameof(IsScientific)) // IsScientificPropertyName)
        {
            if (IsScientific)
            {
                OnButtonPressed(CalculatorButtonId.IsScientificMode);
            }
        }
        else if (propertyname == nameof(IsProgrammer)) // == IsProgrammerPropertyName)
        {
            if (IsProgrammer)
            {
                OnButtonPressed(CalculatorButtonId.IsProgrammerMode);
            }
        }
        else if (propertyname == nameof(IsStandard)) // == IsStandardPropertyName)
        {
            if (IsStandard)
            {
                OnButtonPressed(CalculatorButtonId.IsStandardMode);
            }
        }
        else if (propertyname == nameof(DisplayValue)) // == DisplayValuePropertyName)
        {
            RaisePropertyChanged(nameof(CalculationResultAutomationName));
            Announcement = GetDisplayUpdatedNarratorAnnouncement();
        }
        else if (propertyname == IsBitFlipCheckedPropertyName)
        {
            TraceLogger.Instance.UpdateButtonUsage(IsBitFlipChecked ? CalculatorButtonId.BitflipButton : CalculatorButtonId.FullKeypadButton, ViewMode.Programmer);
        }
        else if (propertyname == nameof(IsAlwaysOnTop)) // == IsAlwaysOnTopPropertyName)
        {
            string announcement;
            if (IsAlwaysOnTop)
            {
                announcement = AppResourceProvider.Instance.GetResourceString(CalcAlwaysOnTop);
            }
            else
            {
                announcement = AppResourceProvider.Instance.GetResourceString(CalcBackToFullView);
            }

            Announcement = NarratorAnnouncement.GetAlwaysOnTopChangedAnnouncement(announcement);
        }
    }

    public void SetCalculatorType(ViewMode targetState)
    {
        // Reset error state so that commands caused by the mode change are still
        // sent if calc is currently in error state.
        IsInError = false;
        // Setting one of these properties to true will set the others to false.
        switch (targetState)
        {
            case ViewMode.Standard:
                IsStandard = true;
                ResetRadixAndUpdateMemory(true);
                SetPrecision(StandardModePrecision);
                UpdateMaxIntDigits();
                break;
            case ViewMode.Scientific:
                IsScientific = true;
                ResetRadixAndUpdateMemory(true);
                SetPrecision(ScientificModePrecision);
                break;
            case ViewMode.Programmer:
                IsProgrammer = true;
                ResetRadixAndUpdateMemory(false);
                SetPrecision(ProgrammerModePrecision);
                break;
        }
    }

    String GetRawDisplayValue()
    {
        if (IsInError)
        {
            return DisplayValue;
        }
        else
        {
            return LocalizationSettings.Instance.RemoveGroupSeparators(DisplayValue);
        }
    }

    // Given a format string, returns a string with the input display value inserted.
    //     'format' is a localized string containing a %1 formatting mark where the display value should be inserted.
    //     'displayValue' is a localized string containing a numerical value to be displayed to the user.
    static String GetLocalizedStringFormat(String format, string displayValue)
    {
        return LocalizationStringUtil.GetLocalizedString(format, displayValue);
    }

    public void ResetRadixAndUpdateMemory(bool resetRadix)
    {
        if (resetRadix)
        {
            AreHEXButtonsEnabled = false;
            CurrentRadixType = NumberBase.DecBase;
            m_standardCalculatorManager.SetRadix(RadixType.Dec);
        }
        else
        {
            m_standardCalculatorManager.SetMemorizedNumbersString();
        }
    }

    public void SetPrecision(int precision)
    {
        m_standardCalculatorManager.SetPrecision(precision);
    }

    public void SwitchProgrammerModeBase(NumberBase numberBase)
    {
        if (IsInError)
        {
            m_standardCalculatorManager.SendCommand(Command.CommandCLEAR);
        }

        AreHEXButtonsEnabled = numberBase == NumberBase.HexBase;
        CurrentRadixType = numberBase;
        m_standardCalculatorManager.SetRadix(GetRadixTypeFromNumberBase(numberBase));
    }

    public void SetMemorizedNumbersString()
    {
        m_standardCalculatorManager.SetMemorizedNumbersString();
    }

    static AngleType GetAngleTypeFromCommand(Command command)
    {
        switch (command)
        {
            case Command.CommandDEG:
                return AngleType.Degrees;
            case Command.CommandRAD:
                return AngleType.Radians;
            case Command.CommandGRAD:
                return AngleType.Gradians;
            default:
                throw new InvalidDataException("Invalid command type");
        }
    }

    void SaveEditedCommand(int tokenPosition, Command command)
    {
        bool handleOperand = false;
        string updatedToken = "";
        (string, int) token = m_tokens[tokenPosition];
        IExpressionCommand tokenCommand = m_commands[token.Item2];
        if (IsUnaryOp(command) && command != Command.CommandSIGN)
        {
            int angleCmd = (int)m_standardCalculatorManager.CurrentDegreeMode;
            AngleType angleType = GetAngleTypeFromCommand((Command)(angleCmd));
            if (IsTrigOp(command))
            {
                IUnaryCommand spUnaryCommand = (IUnaryCommand)(tokenCommand);
                spUnaryCommand.SetCommands(angleCmd, (int)(command));
            }
            else
            {
                IUnaryCommand spUnaryCommand = (IUnaryCommand)(tokenCommand);
                spUnaryCommand.SetCommand((int)(command));
            }

            switch (command)
            {
                case Command.CommandASIN:
                    updatedToken = m_standardCalculatorManager.GetUnaryOperatorDisplayName((int)Command.CommandSIN, true, angleType);
                    break;
                case Command.CommandACOS:
                    updatedToken = m_standardCalculatorManager.GetUnaryOperatorDisplayName((int)Command.CommandCOS, true, angleType);
                    break;
                case Command.CommandATAN:
                    updatedToken = m_standardCalculatorManager.GetUnaryOperatorDisplayName((int)Command.CommandTAN, true, angleType);
                    break;
                case Command.CommandASINH:
                    updatedToken = m_standardCalculatorManager.GetUnaryOperatorDisplayName((int)Command.CommandSINH, true, angleType);
                    break;
                case Command.CommandACOSH:
                    updatedToken = m_standardCalculatorManager.GetUnaryOperatorDisplayName((int)Command.CommandCOSH, true, angleType);
                    break;
                case Command.CommandATANH:
                    updatedToken = m_standardCalculatorManager.GetUnaryOperatorDisplayName((int)Command.CommandTANH, true, angleType);
                    break;
                case Command.CommandPOWE:
                    updatedToken = m_standardCalculatorManager.GetUnaryOperatorDisplayName((int)Command.CommandLN, true, angleType);
                    break;
                default:
                    updatedToken = m_standardCalculatorManager.GetUnaryOperatorDisplayName((int)command, false, angleType);
                    break;
            }

            if ((token.Item1.Length > 0) && (token.Item1[token.Item1.Length - 1] == '('))
            {
                updatedToken += '(';
            }
        }
        else if (IsBinOp(command))
        {
            IBinaryCommand spBinaryCommand = (IBinaryCommand)(tokenCommand);
            spBinaryCommand.SetCommand((int)(command));
            updatedToken = m_standardCalculatorManager.GetOperatorDisplayName((int)command);
        }
        else if (IsOpnd(command) || command == Command.CommandBACK)
        {
            HandleUpdatedOperandData(command);
            handleOperand = true;
        }
        else if (command == Command.CommandSIGN)
        {
            if (tokenCommand.GetCommandType() == CommandType.UnaryCommand)
            {
                IExpressionCommand spSignCommand = new CUnaryCommand((int)(command));
                m_commands.Insert(token.Item2 + 1, spSignCommand);
            }
            else
            {
                IOpndCommand spOpndCommand = (IOpndCommand)(tokenCommand);
                spOpndCommand.ToggleSign();
                updatedToken = spOpndCommand.GetToken(m_standardCalculatorManager.DecimalSeparator());
            }

            IsOperandUpdatedUsingViewModel = true;
        }

        if (!handleOperand)
        {
            (m_commands)[token.Item2] = tokenCommand;
            (string m_tX, int m_tY) = m_tokens[tokenPosition];
            m_tokens[tokenPosition] = (updatedToken, m_tY);
            //.Item1 = updatedToken;
            DisplayExpressionToken displayExpressionToken = ExpressionTokens[tokenPosition];
            displayExpressionToken.Token = (updatedToken);
            // Special casing
            if (command == Command.CommandSIGN && tokenCommand.GetCommandType() == CommandType.UnaryCommand)
            {
                IsEditingEnabled = false;
                Recalculate();
            }
        }
    }

    void Recalculate(bool fromHistory = false)
    {
        // Recalculate
        Command currentDegreeMode = m_standardCalculatorManager.CurrentDegreeMode;
        List<IExpressionCommand> savedCommands = new List<IExpressionCommand>(m_commands);
        List<int> currentCommands = GetCommandsFromExpressionCommands(m_commands);
        List<(string, int)> savedTokens = new List<(string, int)>();
        foreach (var currentToken in m_tokens)
        {
            savedTokens.Add(currentToken);
        }

        m_standardCalculatorManager.Reset(false);
        if (IsScientific)
        {
            m_standardCalculatorManager.SendCommand(Command.ModeScientific);
        }

        if (IsFToEChecked)
        {
            m_standardCalculatorManager.SendCommand(Command.CommandFE);
        }

        m_standardCalculatorManager.SendCommand(currentDegreeMode);
        foreach (int command in currentCommands)
        {
            m_standardCalculatorManager.SendCommand((CalculationManager.Command)(command));
        }

        if (fromHistory) // This is for the cases where the expression is loaded from history
        {
            // To maintain F-E state of the engine, as the last operand hasn't reached engine by now
            m_standardCalculatorManager.SendCommand(Command.CommandFE);
            m_standardCalculatorManager.SendCommand(Command.CommandFE);
        }

        // After recalculation. If there is an error then
        // IsInError should be set synchronously.
        if (IsInError)
        {
            SetExpressionDisplay(savedTokens, savedCommands);
        }
    }

    static readonly Command[] opnd =
    {
        Command.Command0,
        Command.Command1,
        Command.Command2,
        Command.Command3,
        Command.Command4,
        Command.Command5,
        Command.Command6,
        Command.Command7,
        Command.Command8,
        Command.Command9,
        Command.CommandPNT
    };
    static bool IsOpnd(Command command)
    {
        return opnd.Contains(command); // find(begin(opnd), end(opnd), command) != end(opnd);
    }

    static readonly Command[] unaryOp =
    {
        Command.CommandSQRT,
        Command.CommandFAC,
        Command.CommandSQR,
        Command.CommandLOG,
        Command.CommandPOW10,
        Command.CommandPOWE,
        Command.CommandLN,
        Command.CommandREC,
        Command.CommandSIGN,
        Command.CommandSINH,
        Command.CommandASINH,
        Command.CommandCOSH,
        Command.CommandACOSH,
        Command.CommandTANH,
        Command.CommandATANH,
        Command.CommandCUB
    };
    static readonly Command[] trigOp =
    {
        Command.CommandSIN,
        Command.CommandCOS,
        Command.CommandTAN,
        Command.CommandASIN,
        Command.CommandACOS,
        Command.CommandATAN
    };
    static readonly Command[] binOp =
    {
        Command.CommandADD,
        Command.CommandSUB,
        Command.CommandMUL,
        Command.CommandDIV,
        Command.CommandEXP,
        Command.CommandROOT,
        Command.CommandMOD,
        Command.CommandPWR
    };
    static readonly Command[] recoverableCommands =
    {
        Command.CommandA,
        Command.CommandB,
        Command.CommandC,
        Command.CommandD,
        Command.CommandE,
        Command.CommandF
    };
    static bool IsUnaryOp(Command command)
    {
        if (unaryOp.Contains(command)) //(find(begin(unaryOp), end(unaryOp), command) != end(unaryOp))
        {
            return true;
        }

        if (IsTrigOp(command))
        {
            return true;
        }

        return false;
    }

    static bool IsTrigOp(Command command)
    {
        return trigOp.Contains(command); //find(begin(trigOp), end(trigOp), command) != end(trigOp);
    }

    static bool IsBinOp(Command command)
    {
        return binOp.Contains(command); //find(begin(binOp), end(binOp), command) != end(binOp);
    }

    static bool IsRecoverableCommand(Command command)
    {
        if (IsOpnd(command))
        {
            return true;
        }

        // Programmer mode, bit flipping
        if (CalculationManager.Command.CommandBINEDITSTART <= command && command <= CalculationManager.Command.BinEditEnd)
        {
            return true;
        }

        return recoverableCommands.Contains(command); //find(begin(recoverableCommands), end(recoverableCommands), command) != end(recoverableCommands);
    }

    static int LengthWithoutPadding(string str)
    {
        return str.Trim(' ').Length; //..Length - count(str.begin(), str.end(), ' ');
    }

    static string AddPadding(string binaryString)
    {
        if (LocalizationSettings.Instance.GetEnglishValueFromLocalizedDigits((binaryString)) == "0")
        {
            return binaryString;
        }

        int pad = 4 - LengthWithoutPadding(binaryString) % 4;
        if (pad == 4)
        {
            pad = 0;
        }

        return new string('0', pad) + binaryString;
    }

    void UpdateProgrammerPanelDisplay()
    {
        int precision = 64;
        string hexDisplayString = "";
        string decimalDisplayString = "";
        string octalDisplayString = "";
        string binaryDisplayString = "";
        if (!IsInError)
        {
            // we want the precision to be set to maximum value so that the autoconversions result as desired
            if (string.IsNullOrEmpty((hexDisplayString = m_standardCalculatorManager.GetResultForRadix(16, precision, true))))
            {
                hexDisplayString = DisplayValue;
                decimalDisplayString = DisplayValue;
                octalDisplayString = DisplayValue;
                binaryDisplayString = DisplayValue;
            }
            else
            {
                decimalDisplayString = m_standardCalculatorManager.GetResultForRadix(10, precision, true);
                octalDisplayString = m_standardCalculatorManager.GetResultForRadix(8, precision, true);
                binaryDisplayString = m_standardCalculatorManager.GetResultForRadix(2, precision, true);
            }
        }

        LocalizationSettings localizer = LocalizationSettings.Instance;
        binaryDisplayString = AddPadding(binaryDisplayString);
        LocalizationSettings.LocalizeDisplayValue(ref hexDisplayString);
        LocalizationSettings.LocalizeDisplayValue(ref decimalDisplayString);
        LocalizationSettings.LocalizeDisplayValue(ref octalDisplayString);
        LocalizationSettings.LocalizeDisplayValue(ref binaryDisplayString);
        HexDisplayValue = (hexDisplayString);
        DecimalDisplayValue = (decimalDisplayString);
        OctalDisplayValue = (octalDisplayString);
        BinaryDisplayValue = (binaryDisplayString);
        HexDisplayValueAutomationName = GetLocalizedStringFormat(m_localizedHexaDecimalAutomationFormat, GetNarratorStringReadRawNumbers(HexDisplayValue));
        DecDisplayValueAutomationName = GetLocalizedStringFormat(m_localizedDecimalAutomationFormat, DecimalDisplayValue);
        OctDisplayValueAutomationName = GetLocalizedStringFormat(m_localizedOctalAutomationFormat, GetNarratorStringReadRawNumbers(OctalDisplayValue));
        BinDisplayValueAutomationName = GetLocalizedStringFormat(m_localizedBinaryAutomationFormat, GetNarratorStringReadRawNumbers(BinaryDisplayValue));
        //var binaryValueArray = new List<bool>(64, false);
        //var binaryValue = m_standardCalculatorManager.GetResultForRadix(2, precision, false);
        //int i = 0;
        //// To get bit 0, grab from opposite end of string.
        //for (string.reverse_iterator it = binaryValue.rbegin(); it != binaryValue.rend(); ++it)
        //{
        //    binaryValueArray.SetAt(i++, *it == '1');
        //}
        var binaryValueArray = new List<bool>(Enumerable.Repeat(false, 64));
        var binaryValue = m_standardCalculatorManager.GetResultForRadix(2, precision, false);
        int i = 0;
        // To get bit 0, grab from opposite end of string.
        for (int index = binaryValue.Length - 1; index >= 0; index--)
        {
            binaryValueArray[i++] = binaryValue[index] == '1';
        }

        BinaryDigits = new ObservableCollection<bool>(binaryValueArray);
    }

    public void SwitchAngleType(CalculatorButtonId num)
    {
        OnButtonPressed(num);
    }

    public void UpdateOperand(int pos, string text)
    {
        System.ArgumentNullException.ThrowIfNull(text);
        (string, int) p = m_tokens[pos];
        string englishString = LocalizationSettings.Instance.GetEnglishValueFromLocalizedDigits(text);
        p.Item1 = englishString;
        int commandPos = p.Item2;
        IExpressionCommand exprCmd = m_commands[commandPos];
        var operandCommand = (IOpndCommand)(exprCmd);
        if (operandCommand != null)
        {
            List<int> commands = new();
            int length = p.Item1.Length;
            if (length > 0)
            {
                int num = 0;
                for (int i = 0; i < length; ++i)
                {
                    if (p.Item1[i] == '.')
                    {
                        num = (int)(Command.CommandPNT);
                    }
                    else if (p.Item1[i] == 'e')
                    {
                        num = (int)(Command.CommandEXP);
                    }
                    else if (p.Item1[i] == '-')
                    {
                        num = (int)(Command.CommandSIGN);
                        if (i == 0)
                        {
                            IOpndCommand spOpndCommand = (IOpndCommand)(exprCmd);
                            if (!spOpndCommand.IsNegative())
                            {
                                spOpndCommand.ToggleSign();
                            }

                            continue;
                        }
                    }
                    else
                    {
                        num = (int)(p.Item1[i]) - ASCII_0;
                        num += CCommand.Idc0;
                        if (num == (int)(Command.CommandMPLUS))
                        {
                            continue;
                        }
                    }

                    commands.Add(num);
                }
            }
            else
            {
                commands.Add(0);
            }

            operandCommand.SetCommands(commands);
        }
    }

    public void MaxDigitsReached()
    {
        if (m_localizedMaxDigitsReachedAutomationFormat == null)
        {
            m_localizedMaxDigitsReachedAutomationFormat = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.MaxDigitsReachedFormat);
        }

        string announcement = LocalizationStringUtil.GetLocalizedString(m_localizedMaxDigitsReachedAutomationFormat, m_CalculationResultAutomationName);
        Announcement = NarratorAnnouncement.GetMaxDigitsReachedAnnouncement(announcement);
    }

    public void BinaryOperatorReceived()
    {
        Announcement = GetDisplayUpdatedNarratorAnnouncement();
    }

    NarratorAnnouncement GetDisplayUpdatedNarratorAnnouncement()
    {
        string announcement;
        if (m_feedbackForButtonPress == null || m_feedbackForButtonPress.Length == 0)
        {
            announcement = m_CalculationResultAutomationName;
        }
        else
        {
            if (m_localizedButtonPressFeedbackAutomationFormat == null)
            {
                m_localizedButtonPressFeedbackAutomationFormat = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.ButtonPressFeedbackFormat);
            }

            announcement = LocalizationStringUtil.GetLocalizedString(m_localizedButtonPressFeedbackAutomationFormat, m_CalculationResultAutomationName, m_feedbackForButtonPress);
        }

        // Make sure we don't accidentally repeat an announcement.
        m_feedbackForButtonPress = null;
        return NarratorAnnouncement.GetDisplayUpdatedAnnouncement(announcement);
    }

    ViewMode GetCalculatorMode()
    {
        if (IsStandard)
        {
            return ViewMode.Standard;
        }
        else if (IsScientific)
        {
            return ViewMode.Scientific;
        }

        return ViewMode.Programmer;
    }

    void ValueBitLengthSet(CalculatorApp.ViewModel.Common.BitLength value)
    {
        if (m_valueBitLength != value)
        {
            m_valueBitLength = value;
            RaisePropertyChanged(nameof(ValueBitLength));
            switch (value)
            {
                case BitLength.SixtyFourBits:
                    ButtonPressed.Execute(CalculatorButtonId.Qword);
                    break;
                case BitLength.ThirtyTwoBits:
                    ButtonPressed.Execute(CalculatorButtonId.Dword);
                    break;
                case BitLength.SixteenBits:
                    ButtonPressed.Execute(CalculatorButtonId.Word);
                    break;
                case BitLength.EightBits:
                    ButtonPressed.Execute(CalculatorButtonId.Byte);
                    break;
            }

            // update memory list according to bit length
            SetMemorizedNumbersString();
        }
    }

    public void SelectHistoryItem(HistoryItemViewModel item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        var tokens = item.Tokens;
        var cmds = item.Commands;
        SetHistoryExpressionDisplay(tokens, cmds);
        SetExpressionDisplay(tokens, cmds);
        SetPrimaryDisplay(item.Result, false);
        IsFToEEnabled = false;
    }

    void ResetCalcManager(bool clearMemory)
    {
        m_standardCalculatorManager.Reset(clearMemory);
    }

    void SendCommandToCalcManager(int commandId)
    {
        m_standardCalculatorManager.SendCommand((Command)(commandId));
    }

    public void SetBitshiftRadioButtonCheckedAnnouncement(string announcement)
    {
        Announcement = NarratorAnnouncement.GetBitShiftRadioButtonCheckedAnnouncement(announcement);
    }

    public StandardCalculatorSnapshot Snapshot
    {
        get
        {
            var result = new CalculatorApp.ViewModel.Snapshot.StandardCalculatorSnapshot();
            result.CalcManager = new CalculatorApp.ViewModel.Snapshot.CalcManagerSnapshot(m_standardCalculatorManager);
            result.PrimaryDisplay = new CalculatorApp.ViewModel.Snapshot.PrimaryDisplaySnapshot(m_DisplayValue, m_IsInError);
            if (m_tokens.Count != 0 && m_commands.Count != 0)
            {
                result.ExpressionDisplay = new CalculatorApp.ViewModel.Snapshot.ExpressionDisplaySnapshot(m_tokens, m_commands);
            }

            result.DisplayCommands = m_standardCalculatorManager.GetDisplayCommandsSnapshot()
                .Select(static command => command.CreateExprCommand())
                .ToImmutableArray();

            return result;
        }

        set
        {
            System.ArgumentNullException.ThrowIfNull(value);
            var snapshot = value;
            {
                //Debug.Assert(snapshot != null);
                m_standardCalculatorManager.Reset();
                if (!snapshot.CalcManager.HistoryItems.IsDefaultOrEmpty)
                {
                    m_standardCalculatorManager.SetHistoryItems(snapshot.CalcManager.HistoryItems.ToUnderlying());
                }

                if (snapshot.ExpressionDisplay is { } expressionDisplay)
                {
                    if (snapshot.DisplayCommands.IsDefaultOrEmpty)
                    {
                        // use case: the current expression was evaluated before. load from history.
                        Debug.Assert(!snapshot.PrimaryDisplay.IsError);
                        RawTokenCollection rawTokens = new RawTokenCollection();
                        foreach (var token in expressionDisplay.Tokens)
                        {
                            rawTokens.Add((token.OpCodeName, token.CommandIndex));
                        }

                        var tokens = new RawTokenCollection(rawTokens);
                        var commands = new List<IExpressionCommand>(expressionDisplay.Commands.ToUnderlying());
                        SetHistoryExpressionDisplay(tokens, commands);
                        SetExpressionDisplay(tokens, commands);
                        SetPrimaryDisplay(snapshot.PrimaryDisplay.DisplayValue, false);
                    }
                    else
                    {
                        // use case: the current expression was not evaluated before, or it was an error.
                        var displayCommands = GetCommandsFromExpressionCommands((snapshot.DisplayCommands.ToUnderlying()));
                        foreach (var cmd in displayCommands)
                        {
                            m_standardCalculatorManager.SendCommand((Command)(cmd));
                        }

                        if (snapshot.PrimaryDisplay.IsError)
                        {
                            SetPrimaryDisplay(snapshot.PrimaryDisplay.DisplayValue, true);
                        }
                    }
                }
                else
                {
                    if (snapshot.PrimaryDisplay.IsError)
                    {
                        // use case: user copy-pasted an invalid expression to Calculator and caused an error.
                        SetPrimaryDisplay(snapshot.PrimaryDisplay.DisplayValue, true);
                    }
                    else
                    {
                        // use case: there was no expression but user was inputing some numbers (including negative numbers).
                        var commands = GetCommandsFromExpressionCommands((snapshot.DisplayCommands.ToUnderlying()));
                        foreach (var cmd in commands)
                        {
                            m_standardCalculatorManager.SendCommand((Command)(cmd));
                        }
                    }
                }
            }
        }
    }
}
