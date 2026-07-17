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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Windows.UI.Core;
using static CalcEngine.RatPak;
using RawTokenCollection = System.Collections.Generic.List<(string, int)>;

namespace CalculatorApp.ViewModel;

public partial class StandardCalculatorViewModel
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
                IList<int> unaryCommands = spCommand.GetCommands();
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
                IList<int> opndCommands = spCommand.GetCommands();
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
        m_valueBitLength = (BitLength.QuadWord);
        m_isBitFlipChecked = (false);
        m_IsBinaryBitFlippingEnabled = (false);
        m_CurrentRadixType = (NumberBase.DecBase);
        m_CurrentAngleType = (CalculatorButtonId.Degree);
        m_Announcement = (null);
        m_OpenParenthesisCount = (0);
        m_feedbackForButtonPress = (null);
        m_isRtlLanguage = (false);
        m_localizedMaxDigitsReachedAutomationFormat = (null);
        m_localizedButtonPressFeedbackAutomationFormat = (null);
        m_localizedMemorySavedAutomationFormat = (null);
        m_localizedMemoryItemChangedAutomationFormat = (null);
        m_localizedMemoryItemClearedAutomationFormat = (null);
        m_localizedMemoryCleared = (null);
        m_localizedOpenParenthesisCountChangedAutomationFormat = (null);
        m_localizedNoRightParenthesisAddedFormat = (null);
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
        if (CoreWindow.GetForCurrentThread() != null)
        {
            // Must have a CoreWindow to access the resource context.
            m_isRtlLanguage = LocalizationService.GetInstance().IsRtlLayout();
        }

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
        if (Utilities.IsLastCharacterTarget(displayValue, m_decimalSeparator))
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

    public void SetPrimaryDisplay(string pszText, bool isError)
    {
        if (pszText is null)
        {
            throw new ArgumentNullException(nameof(pszText));
        }

        string localizedDisplayStringValue = LocalizeDisplayValue(pszText);
        // Set this variable before the DisplayValue is modified, Otherwise the DisplayValue will
        // not match what the narrator is saying
        m_CalculationResultAutomationName = CalculateNarratorDisplayValue(pszText, localizedDisplayStringValue);
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
            expression += LocalizationService.GetNarratorReadableToken(token.Token);
        }

        return GetLocalizedStringFormat(m_expressionAutomationNameFormat, expression);
    }

    public void SetMemorizedNumbers(IList<string> memorizedNumbers)
    {
        if (memorizedNumbers is null)
        {
            throw new ArgumentNullException(nameof(memorizedNumbers));
        }

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

        int length = 0;
        char[] temp = new char[100];
        char[] data = m_selectedExpressionLastData.ToCharArray();
        int i = 0, j = 0;
        int commandIndex = displayExpressionToken.CommandIndex;
        if (IsOperandTextCompletelySelected)
        {
            // Clear older text;
            m_selectedExpressionLastData = "";
            if (ch == 'x')
            {
                temp[0] = '\0';
                commandIndex = 0;
            }
            else
            {
                temp[0] = ch;
                temp[1] = '\0';
                commandIndex = 1;
            }

            IsOperandTextCompletelySelected = false;
        }
        else
        {
            if (ch == 'x')
            {
                if (commandIndex == 0)
                {
                    return;
                }

                length = m_selectedExpressionLastData.Length;
                for (; j < length; ++j)
                {
                    if (j == commandIndex - 1)
                    {
                        continue;
                    }

                    temp[i++] = data[j];
                }

                temp[i] = '\0';
                commandIndex -= 1;
            }
            else
            {
                length = m_selectedExpressionLastData.Length + 1;
                if (length > 50)
                {
                    return;
                }

                for (; i < length; ++i)
                {
                    if (i == commandIndex)
                    {
                        temp[i] = ch;
                        continue;
                    }

                    temp[i] = data[j++];
                }

                temp[i] = '\0';
                commandIndex += 1;
            }
        }

        string updatedData = new String(temp);
        UpdateOperand(m_TokenPosition, updatedData);
        displayExpressionToken.Token = updatedData;
        IsOperandUpdatedUsingViewModel = true;
        displayExpressionToken.CommandIndex = commandIndex;
    }

    static bool TryGetOperandEditCharacter(Command command, out char character)
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

    static bool IsOperator(Command cmdenum)
    {
        if ((cmdenum >= Command.Command0 && cmdenum <= Command.Command9) || (cmdenum == Command.CommandPNT) || (cmdenum == Command.CommandBACK) || (cmdenum == Command.CommandEXP) || (cmdenum == Command.CommandFE) || (cmdenum == Command.ModeBasic) || (cmdenum == Command.ModeProgrammer) || (cmdenum == Command.ModeScientific) || (cmdenum == Command.CommandINV) || (cmdenum == Command.CommandCENTR) || (cmdenum == Command.CommandDEG) || (cmdenum == Command.CommandRAD) || (cmdenum == Command.CommandGRAD) || ((cmdenum >= Command.CommandBINEDITSTART) && (cmdenum <= Command.CommandBINEDITEND)))
        {
            return false;
        }

        return true;
    }

    void OnButtonPressed(Object parameter)
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

        if (ShouldSaveEditedCommand(numOpEnum))
        {
            if (!m_KeyPressed)
            {
                SaveEditedCommand(m_TokenPosition, cmdenum);
            }

            return;
        }

        HandleDirectButtonPress(numOpEnum, cmdenum);
    }

    bool ShouldSaveEditedCommand(CalculatorButtonId button)
    {
        return IsEditingEnabled
            && !IsModeSelectionButton(button)
            && button != CalculatorButtonId.FToE
            && !IsAngleSelectionButton(button);
    }

    void HandleDirectButtonPress(CalculatorButtonId button, Command command)
    {
        if (IsModeSelectionButton(button))
        {
            IsEditingEnabled = false;
        }

        if (button == CalculatorButtonId.Memory)
        {
            OnMemoryButtonPressed();
            return;
        }

        if (ClearsFToEState(button) && IsFToEChecked)
        {
            // C, CE, and calculator-mode changes clear exponential display state.
            IsFToEChecked = false;
        }

        if (IsAngleSelectionButton(button))
        {
            m_CurrentAngleType = button;
        }

        IsOperatorCommand = !IsOperandEntryCommand(command);

        if (m_isLastOperationHistoryLoad && !IsAngleSelectionButton(button))
        {
            IsFToEEnabled = true;
            m_isLastOperationHistoryLoad = false;
        }

        TraceLogger.Instance.UpdateButtonUsage(button, GetCalculatorMode());
        m_standardCalculatorManager.SendCommand(command);
    }

    static bool IsModeSelectionButton(CalculatorButtonId button)
    {
        return button is CalculatorButtonId.IsStandardMode
            or CalculatorButtonId.IsScientificMode
            or CalculatorButtonId.IsProgrammerMode;
    }

    static bool IsAngleSelectionButton(CalculatorButtonId button)
    {
        return button is CalculatorButtonId.Degree
            or CalculatorButtonId.Radians
            or CalculatorButtonId.Grads;
    }

    static bool ClearsFToEState(CalculatorButtonId button)
    {
        return button is CalculatorButtonId.Clear
            or CalculatorButtonId.ClearEntry
            or CalculatorButtonId.IsStandardMode
            or CalculatorButtonId.IsProgrammerMode;
    }

    static bool IsOperandEntryCommand(Command command)
    {
        return (command >= Command.Command0 && command <= Command.Command9)
            || command is Command.CommandPNT or Command.CommandBACK or Command.CommandEXP;
    }

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

    public void OnCopyCommand(Object parameter)
    {
        CopyPasteManager.CopyToClipboard(GetRawDisplayValue());
        string announcement = AppResourceProvider.Instance.GetResourceString(CalculatorResourceKeys.DisplayCopied);
        Announcement = NarratorAnnouncement.GetDisplayCopiedAnnouncement(announcement);
    }

    public async void OnPasteCommand(Object parameter)
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
        if (IsEditingEnabled || !CopyPasteManager.HasStringToPaste())
        {
            return;
        }

        // Ensure that the paste happens on the UI thread
        string pastedString = await CopyPasteManager.GetStringToPaste(mode, NavCategoryStates.GetGroupType(mode), numberBase, bitLengthType).ConfigureAwait(false);
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
        bool sentEquals = false;
        bool isPreviousOperator = false;
        List<bool> negateStack = new List<bool>();
        // Iterate through each character pasted, and if it's valid, send it to the model.
        var it = 0;
        while (it < pastedString.Length)
        {
            bool sendCommand = true;
            var buttonInfo = MapCharacterToButtonId(pastedString[it]);
            CalculatorButtonId mappedNumOp = buttonInfo.ButtonId;
            bool canSendNegate = buttonInfo.CanSendNegate;
            if (mappedNumOp == CalculatorButtonId.None)
            {
                ++it;
                continue;
            }

            if (isFirstLegalChar || isPreviousOperator)
            {
                isFirstLegalChar = false;
                isPreviousOperator = false;
                // If the character is a - sign, send negate
                // after sending the next legal character.  Send nothing now, or
                // it will be ignored.
                if (CalculatorButtonId.Subtract == mappedNumOp)
                {
                    sendNegate = true;
                    sendCommand = false;
                }

                // Support (+) sign prefix
                if (CalculatorButtonId.Add == mappedNumOp)
                {
                    sendCommand = false;
                }
            }

            switch (mappedNumOp)
            {
                // Opening parenthesis starts a new expression and pushes negation state onto the stack
                case CalculatorButtonId.OpenParenthesis:
                    negateStack.Add(sendNegate);
                    sendNegate = false;
                    break;
                // Closing parenthesis pops the negation state off the stack and sends it down to the calc engine
                case CalculatorButtonId.CloseParenthesis:
                    if (negateStack.Count != 0)
                    {
                        sendNegate = negateStack.Last();
                        negateStack.RemoveAt(negateStack.Count - 1); //.pop_back();
                        canSendNegate = true;
                    }
                    else
                    {
                        // Don't send a closing parenthesis if a matching opening parenthesis hasn't been sent already
                        sendCommand = false;
                    }

                    break;
                case CalculatorButtonId.Add:
                case CalculatorButtonId.Subtract:
                case CalculatorButtonId.Multiply:
                case CalculatorButtonId.Divide:
                    isPreviousOperator = true;
                    break;
            }

            if (sendCommand)
            {
                sentEquals = (mappedNumOp == CalculatorButtonId.Equals);
                Command cmdenum = ConvertToOperatorsEnum(mappedNumOp);
                m_standardCalculatorManager.SendCommand(cmdenum);
                // The CalcEngine state machine won't allow the negate command to be sent before any
                // other digits, so instead a flag is set and the command is sent after the first appropriate
                // command.
                if (sendNegate)
                {
                    if (canSendNegate)
                    {
                        Command cmdNegate = ConvertToOperatorsEnum(CalculatorButtonId.Negate);
                        m_standardCalculatorManager.SendCommand(cmdNegate);
                    }

                    // Can't send negate on a leading zero, so wait until the appropriate time to send it.
                    if (CalculatorButtonId.Zero != mappedNumOp && CalculatorButtonId.DecimalPoint != mappedNumOp)
                    {
                        sendNegate = false;
                    }
                }
            }

            // Handle exponent and exponent sign (...e+... or ...e-... or ...e...)
            if (mappedNumOp == CalculatorButtonId.Exp)
            {
                // Check the following item
                switch (MapCharacterToButtonId(pastedString[it + 1]).ButtonId)
                {
                    case CalculatorButtonId.Subtract:
                        {
                            Command cmdNegate = ConvertToOperatorsEnum(CalculatorButtonId.Negate);
                            m_standardCalculatorManager.SendCommand(cmdNegate);
                            ++it;
                        }

                        break;
                    case CalculatorButtonId.Add:
                        {
                            // Nothing to do, skip to the next item
                            ++it;
                        }

                        break;
                }
            }

            ++it;
        }
    }

    void OnClearMemoryCommand(Object parameter)
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
        if (ch >= '0' && ch <= '9')
        {
            return new ButtonInfo
            {
                ButtonId = (int)CalculatorButtonId.Zero + (CalculatorButtonId)(ch - '0'),
                CanSendNegate = ch != '0'
            };
        }

        ButtonInfo result = new()
        {
            ButtonId = MapOperatorCharacter(ch),
            CanSendNegate = false
        };

        if (result.ButtonId == CalculatorButtonId.None)
        {
            result.ButtonId = MapLetterCharacter(ch);
        }

        if (result.ButtonId == CalculatorButtonId.None && ch == m_decimalSeparator)
        {
            result.ButtonId = CalculatorButtonId.DecimalPoint;
        }

        if (result.ButtonId == CalculatorButtonId.None && LocalizationSettings.Instance.IsLocalizedDigit(ch))
        {
            result.ButtonId = (int)CalculatorButtonId.Zero
                + (CalculatorButtonId)(ch - LocalizationSettings.Instance.GetDigitSymbolFromEnUsDigit('0'));
            result.CanSendNegate = result.ButtonId != CalculatorButtonId.Zero;
        }

        return result;
    }

    CalculatorButtonId MapOperatorCharacter(char character)
    {
        return character switch
        {
            '*' => CalculatorButtonId.Multiply,
            '+' => CalculatorButtonId.Add,
            '-' => CalculatorButtonId.Subtract,
            '/' => CalculatorButtonId.Divide,
            '^' when IsScientific => CalculatorButtonId.XPowerY,
            '%' when IsScientific || IsProgrammer => CalculatorButtonId.Mod,
            '=' => CalculatorButtonId.Equals,
            '(' => CalculatorButtonId.OpenParenthesis,
            ')' => CalculatorButtonId.CloseParenthesis,
            _ => CalculatorButtonId.None
        };
    }

    CalculatorButtonId MapLetterCharacter(char character)
    {
        return char.ToUpperInvariant(character) switch
        {
            'A' => CalculatorButtonId.A,
            'B' => CalculatorButtonId.B,
            'C' => CalculatorButtonId.C,
            'D' => CalculatorButtonId.D,
            'E' when IsProgrammer => CalculatorButtonId.E,
            'E' => CalculatorButtonId.Exp,
            'F' => CalculatorButtonId.F,
            _ => CalculatorButtonId.None
        };
    }

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

    public void OnMemoryItemPressed(Object memoryItemPosition)
    {
        if (MemorizedNumbers != null && MemorizedNumbers.Count > 0 && memoryItemPosition is int boxedPosition)
        {
            m_standardCalculatorManager.MemorizedNumberLoad(boxedPosition);
            HideMemoryClicked?.Invoke(this, EventArgs.Empty);
            var mode = IsStandard ? ViewMode.Standard : IsScientific ? ViewMode.Scientific : ViewMode.Programmer;
            TraceLogger.LogMemoryItemLoad(mode, MemorizedNumbers.Count, boxedPosition);
        }
    }

    public void OnMemoryAdd(Object memoryItemPosition)
    {
        // M+ will add display to memorylist if memory list is empty.
        if (MemorizedNumbers != null && memoryItemPosition is int boxedPosition)
        {
            TraceLogger.Instance.UpdateButtonUsage(CalculatorButtonId.MemoryAdd, GetCalculatorMode());
            m_standardCalculatorManager.MemorizedNumberAdd(boxedPosition);
        }
    }

    public void OnMemorySubtract(Object memoryItemPosition)
    {
        // M- will add negative of displayed number to memorylist if memory list is empty.
        if (MemorizedNumbers != null && memoryItemPosition is int boxedPosition)
        {
            TraceLogger.Instance.UpdateButtonUsage(CalculatorButtonId.MemorySubtract, GetCalculatorMode());
            m_standardCalculatorManager.MemorizedNumberSubtract(boxedPosition);
        }
    }

    public void OnMemoryClear(Object memoryItemPosition)
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

    static Command[] opnd =
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

    static Command[] unaryOp =
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
    static Command[] trigOp =
    {
        Command.CommandSIN,
        Command.CommandCOS,
        Command.CommandTAN,
        Command.CommandASIN,
        Command.CommandACOS,
        Command.CommandATAN
    };
    static Command[] binOp =
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
    static Command[] recoverableCommands =
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
        if (CalculationManager.Command.CommandBINEDITSTART <= command && command <= CalculationManager.Command.CommandBINEDITEND)
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
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

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
                case BitLength.QuadWord:
                    ButtonPressed.Execute(CalculatorButtonId.Qword);
                    break;
                case BitLength.DoubleWord:
                    ButtonPressed.Execute(CalculatorButtonId.Dword);
                    break;
                case BitLength.Word:
                    ButtonPressed.Execute(CalculatorButtonId.Word);
                    break;
                case BitLength.Byte:
                    ButtonPressed.Execute(CalculatorButtonId.Byte);
                    break;
            }

            // update memory list according to bit length
            SetMemorizedNumbersString();
        }
    }

    public void SelectHistoryItem(HistoryItemViewModel item)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

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

            foreach (var cmd in m_standardCalculatorManager.GetDisplayCommandsSnapshot())
            {
                result.DisplayCommands.Add((cmd.CreateExprCommand()));
            }

            return result;
        }

        set
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var snapshot = value;
            {
                //Debug.Assert(snapshot != null);
                m_standardCalculatorManager.Reset();
                if (snapshot.CalcManager.HistoryItems != null)
                {
                    m_standardCalculatorManager.SetHistoryItems(snapshot.CalcManager.HistoryItems.ToUnderlying());
                }

                if (snapshot.ExpressionDisplay is { } expressionDisplay)
                {
                    if (snapshot.DisplayCommands.Count == 0)
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
