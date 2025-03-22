// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;
using uint32_t = System.UInt32;
using int32_t = System.Int32;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;
using wchar_t = char;
using wstring_view = string;
using wstring = string;
using size_t = int;

namespace CalcEngine;

public partial class CHistoryCollector
{
    const int ASCII_0 = 48;

    static void Truncate<T>(List<T> v, int index)
    {
        if (index >= v.Count)
        {
            throw new Exception("E_BOUNDS");
        }

        int countToRemove = v.Count - index;
        v.RemoveRange(index, countToRemove);
    }

    public void ReinitHistory()
    {
        m_lastOpStartIndex = -1;
        m_lastBinOpStartIndex = -1;
        m_curOperandIndex = 0;
        m_bLastOpndBrace = false;
        if (m_spTokens != null)
        {
            m_spTokens.Clear();
        }

        if (m_spCommands != null)
        {
            m_spCommands.Clear();
        }
    }

    // Constructor
    // Can throw Out of memory error
    public CHistoryCollector(CCalcEngine cCalcEngine, ICalcDisplay pCalcDisplay, IHistoryDisplay pHistoryDisplay,
        wchar_t decimalSymbol)

    {
        m_cCalcEngine = cCalcEngine;
        m_pHistoryDisplay = (pHistoryDisplay);
        m_pCalcDisplay = (pCalcDisplay);
        m_iCurLineHistStart = (-1);
        m_decimalSymbol = (decimalSymbol);
        ReinitHistory();
    }

    ~CHistoryCollector()
    {
        m_pHistoryDisplay = null;
        m_pCalcDisplay = null;

        if (m_spTokens != null)
        {
            m_spTokens.Clear();
        }
    }

    public void AddOpndToHistory(wstring_view numStr, Rational rat, bool fRepetition = false)
    {
        int iCommandEnd = AddCommand(GetOperandCommandsFromString(numStr, rat));
        m_lastOpStartIndex = IchAddSzToEquationSz(numStr, iCommandEnd);

        if (fRepetition)
        {
            SetExpressionDisplay();
        }

        m_bLastOpndBrace = false;
        m_lastBinOpStartIndex = -1;
    }

    public void RemoveLastOpndFromHistory()
    {
        TruncateEquationSzFromIch(m_lastOpStartIndex);
        SetExpressionDisplay();
        m_lastOpStartIndex = -1;
        // This will not restore the m_lastBinOpStartIndex, as it isn't possible to remove that also later
    }

    public void AddBinOpToHistory(int nOpCode, bool isIntegerMode, bool fNoRepetition = true)
    {
        int iCommandEnd = AddCommand(new CBinaryCommand(nOpCode));
        m_lastBinOpStartIndex = IchAddSzToEquationSz(" ", -1);

        IchAddSzToEquationSz(m_cCalcEngine.OpCodeToBinaryString(nOpCode, isIntegerMode), iCommandEnd);
        IchAddSzToEquationSz(" ", -1);

        if (fNoRepetition)
        {
            SetExpressionDisplay();
        }

        m_lastOpStartIndex = -1;
    }

    // This is expected to be called when a binary op in the last say 1+2+ is changing to another one say 1+2* (+ changed to *)
    // It needs to know by this change a Precedence inversion happened. i.e. previous op was lower or equal to its previous op, but the new
    // one isn't. (Eg. 1*2* to 1*2^). It can add explicit brackets to ensure the precedence is inverted. (Eg. (1*2) ^)
    public void ChangeLastBinOp(int nOpCode, bool fPrecInvToHigher, bool isIntegerMode)
    {
        TruncateEquationSzFromIch(m_lastBinOpStartIndex);
        if (fPrecInvToHigher)
        {
            EnclosePrecInversionBrackets();
        }

        AddBinOpToHistory(nOpCode, isIntegerMode);
    }

    public void PushLastOpndStart(int ichOpndStart = -1)
    {
        int ich = (ichOpndStart == -1) ? m_lastOpStartIndex : ichOpndStart;

        if (m_curOperandIndex < (int)(m_operandIndices.Length))
        {
            m_operandIndices[m_curOperandIndex++] = ich;
        }
    }

    public void PopLastOpndStart()
    {
        if (m_curOperandIndex > 0)
        {
            m_lastOpStartIndex = m_operandIndices[--m_curOperandIndex];
        }
    }

    public void AddOpenBraceToHistory()
    {
        AddCommand(new CParentheses(CCommand.IDC_OPENP));
        int ichOpndStart = IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IDC_OPENP), -1);
        PushLastOpndStart(ichOpndStart);

        SetExpressionDisplay();
        m_lastBinOpStartIndex = -1;
    }

    public void AddCloseBraceToHistory()
    {
        AddCommand(new CParentheses(CCommand.IDC_CLOSEP));
        IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IDC_CLOSEP), -1);
        SetExpressionDisplay();
        PopLastOpndStart();

        m_lastBinOpStartIndex = -1;
        m_bLastOpndBrace = true;
    }

    public void EnclosePrecInversionBrackets()
    {
        // Top of the Opnd starts index or 0 is nothing is in top
        int ichStart = (m_curOperandIndex > 0) ? m_operandIndices[m_curOperandIndex - 1] : 0;

        InsertSzInEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IDC_OPENP), -1, ichStart);
        IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IDC_CLOSEP), -1);
    }

    public bool FOpndAddedToHistory()
    {
        return (-1 != m_lastOpStartIndex);
    }

    // AddUnaryOpToHistory
    //
    // This is does the postfix to prefix translation of the input and adds the text to the history. Eg. doing 2 + 4 (sqrt),
    // this routine will ensure the last sqrt call unary operator, actually goes back in history and wraps 4 in sqrt(4)
    //
    public void AddUnaryOpToHistory(int nOpCode, bool fInv, RatPak.AngleType angletype)
    {
        int iCommandEnd;
        // When successfully applying a unary op, there should be an opnd already
        // A very special case of % which is a funny post op unary op.
        if (CCommand.IDC_PERCENT == nOpCode)
        {
            iCommandEnd = AddCommand(new CUnaryCommand(nOpCode));
            IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(nOpCode), iCommandEnd);
        }
        else // all the other unary ops
        {
            IOperatorCommand spExpressionCommand;
            if (CCommand.IDC_SIGN == nOpCode)
            {
                spExpressionCommand = new CUnaryCommand(nOpCode);
            }
            else
            {
                CalculationManager.Command angleOpCode;
                if (angletype == RatPak.AngleType.Degrees)
                {
                    angleOpCode = CalculationManager.Command.CommandDEG;
                }
                else if (angletype == RatPak.AngleType.Radians)
                {
                    angleOpCode = CalculationManager.Command.CommandRAD;
                }
                else // (angletype == AngleType.Gradians)
                {
                    angleOpCode = CalculationManager.Command.CommandGRAD;
                }

                int command = nOpCode;
                switch (nOpCode)
                {
                    case CCommand.IDC_SIN:
                        command = fInv ? (int)(CalculationManager.Command.CommandASIN) : CCommand.IDC_SIN;
                        spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                        break;
                    case CCommand.IDC_COS:
                        command = fInv ? (int)(CalculationManager.Command.CommandACOS) : CCommand.IDC_COS;
                        spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                        break;
                    case CCommand.IDC_TAN:
                        command = fInv ? (int)(CalculationManager.Command.CommandATAN) : CCommand.IDC_TAN;
                        spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                        break;
                    case CCommand.IDC_SINH:
                        command = fInv ? (int)(CalculationManager.Command.CommandASINH) : CCommand.IDC_SINH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_COSH:
                        command = fInv ? (int)(CalculationManager.Command.CommandACOSH) : CCommand.IDC_COSH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_TANH:
                        command = fInv ? (int)(CalculationManager.Command.CommandATANH) : CCommand.IDC_TANH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_SEC:
                        command = fInv ? (int)(CalculationManager.Command.CommandASEC) : CCommand.IDC_SEC;
                        spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                        break;
                    case CCommand.IDC_CSC:
                        command = fInv ? (int)(CalculationManager.Command.CommandACSC) : CCommand.IDC_CSC;
                        spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                        break;
                    case CCommand.IDC_COT:
                        command = fInv ? (int)(CalculationManager.Command.CommandACOT) : CCommand.IDC_COT;
                        spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                        break;
                    case CCommand.IDC_SECH:
                        command = fInv ? (int)(CalculationManager.Command.CommandASECH) : CCommand.IDC_SECH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_CSCH:
                        command = fInv ? (int)(CalculationManager.Command.CommandACSCH) : CCommand.IDC_CSCH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_COTH:
                        command = fInv ? (int)(CalculationManager.Command.CommandACOTH) : CCommand.IDC_COTH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_LN:
                        command = fInv ? (int)(CalculationManager.Command.CommandPOWE) : CCommand.IDC_LN;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    default:
                        spExpressionCommand = new CUnaryCommand(nOpCode);
                        break;
                }
            }

            iCommandEnd = AddCommand(spExpressionCommand);

            wstring operandStr =
                m_cCalcEngine.OpCodeToUnaryString(nOpCode, fInv, angletype);

            ;
            if (!m_bLastOpndBrace) // The opnd is already covered in braces. No need for additional braces around it
            {
                operandStr += (m_cCalcEngine.OpCodeToString(CCommand.IDC_OPENP));
            }

            InsertSzInEquationSz(operandStr, iCommandEnd, m_lastOpStartIndex);

            if (!m_bLastOpndBrace)
            {
                IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IDC_CLOSEP), -1);
            }
        }

        SetExpressionDisplay();
        m_bLastOpndBrace = false;
        // m_lastOpStartIndex remains the same as last opnd is just replaced by unaryop(lastopnd)
        m_lastBinOpStartIndex = -1;
    }

    // Called after = with the result of the equation
    // Responsible for clearing the top line of current running history display, as well as adding yet another element to
    // history of equations
    public void CompleteHistoryLine(wstring_view numStr)
    {
        if (null != m_pHistoryDisplay)
        {
            uint addedItemIndex = m_pHistoryDisplay.AddToHistory(m_spTokens, m_spCommands, numStr);
            m_pCalcDisplay.OnHistoryItemAdded(addedItemIndex);
        }

        m_spTokens = null;
        m_spCommands = null;
        m_iCurLineHistStart = -1; // It will get recomputed at the first Opnd
        ReinitHistory();
    }

    public void CompleteEquation(wstring_view numStr)
    {
        // Add only '=' token and not add EQU command, because
        // EQU command breaks loading from history (it duplicate history entries).
        IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IDC_EQU), -1);

        SetExpressionDisplay();
        CompleteHistoryLine(numStr);
    }

    public void ClearHistoryLine(wstring_view errStr)
    {
        if (string.IsNullOrEmpty(errStr)) // in case of error let the display stay as it is
        {
            if (null != m_pCalcDisplay)
            {
                m_pCalcDisplay.SetExpressionDisplay(new List<(string, int)>(), new List<IExpressionCommand>());
            }

            m_iCurLineHistStart = -1; // It will get recomputed at the first Opnd
            ReinitHistory();
        }
    }

    // Adds the given string psz to the globally maintained current equation string at the end.
    //  Also returns the 0 based index in the string just added. Can throw out of memory error
    public int IchAddSzToEquationSz(wstring_view str, int icommandIndex)
    {
        if (m_spTokens == null)
        {
            m_spTokens = new List<(wstring, int)>();
        }

        m_spTokens.Add(((str), icommandIndex));
        return (int)(m_spTokens.Count - 1);
    }

    // Inserts a given string into the global m_pszEquation at the given index ich taking care of reallocations etc.
    public void InsertSzInEquationSz(wstring_view str, int icommandIndex, int ich)
    {
        // m_spTokens.emplace(m_spTokens.begin() + ich, wstring(str), icommandIndex);
        m_spTokens.Insert(ich, (str, icommandIndex));
    }

    // Chops off the current equation string from the given index
    public void TruncateEquationSzFromIch(int ich)
    {
        // Truncate commands
        int minIdx = -1;
        int nTokens = (m_spTokens.Count());

        for (int i = ich; i < nTokens; i++)
        {
            var currentPair = (m_spTokens)[i];
            int curTokenId = currentPair.Item2;
            if (curTokenId != -1)
            {
                if ((minIdx != -1) || (curTokenId < minIdx))
                {
                    minIdx = curTokenId;
                    Truncate(m_spCommands, minIdx);
                }
            }
        }

        Truncate(m_spTokens, ich);
    }

    // Adds the m_pszEquation into the running history text
    public void SetExpressionDisplay()
    {
        if (null != m_pCalcDisplay)
        {
            m_pCalcDisplay.SetExpressionDisplay(m_spTokens, m_spCommands);
        }
    }

    public int AddCommand(IExpressionCommand spCommand)
    {
        if (m_spCommands == null)
        {
            m_spCommands = new List<IExpressionCommand>();
        }

        m_spCommands.Add(spCommand);
        return (int)(m_spCommands.Count - 1);
    }

    // To Update the operands in the Expression according to the current Radix
    public void UpdateHistoryExpression(uint32_t radix, int32_t precision)
    {
        if (m_spTokens == null)
        {
            return;
        }

        for (int token_i = 0; token_i < m_spTokens.Count; token_i++)

            // for (var token in m_spTokens)
        {
            int commandPosition = m_spTokens[token_i].Item2;
            if (commandPosition != -1)
            {
                var expCommand = m_spCommands[commandPosition];

                if (expCommand != null &&
                    CalculationManager.CommandType.OperandCommand == expCommand.GetCommandType())
                {
                    var opndCommand = (COpndCommand)(expCommand);
                    if (opndCommand != null)
                    {
                        m_spTokens[token_i] = (opndCommand.GetString(radix, precision), m_spTokens[token_i].Item2);
                        opndCommand.SetCommands(GetOperandCommandsFromString(m_spTokens[token_i].Item1));
                    }
                }
            }
        }

        SetExpressionDisplay();
    }

    public void SetDecimalSymbol(wchar_t decimalSymbol)
    {
        m_decimalSymbol = decimalSymbol;
    }

    // Update the commands corresponding to the passed string Number
    public List<int> GetOperandCommandsFromString(wstring_view numStr)
    {
        List<int> commands = new List<int>();
        // Check for negate
        bool fNegative = (numStr[0] == '-');

        for (size_t i = (fNegative ? 1 : 0); i < numStr.Length; i++)
        {
            if (numStr[i] == m_decimalSymbol)
            {
                commands.Add(CCommand.IDC_PNT);
            }
            else if (numStr[i] == 'e')
            {
                commands.Add(CCommand.IDC_EXP);
            }
            else if (numStr[i] == '-')
            {
                commands.Add(CCommand.IDC_SIGN);
            }
            else if (numStr[i] == '+')
            {
                // Ignore.
            }
            // Number
            else
            {
                int num = (int)(numStr[i]) - ASCII_0;
                num += CCommand.IDC_0;
                commands.Add(num);
            }
        }

        // If the number is negative, append a sign command at the end.
        if (fNegative)
        {
            commands.Add(CCommand.IDC_SIGN);
        }

        return commands;
    }

    public COpndCommand GetOperandCommandsFromString(wstring_view numStr, Rational rat)
    {
        List<int> commands = new List<int>();
        // Check for negate
        bool fNegative = (numStr[0] == '-');
        bool fSciFmt = false;
        bool fDecimal = false;

        for (size_t i = (fNegative ? 1 : 0); i < numStr.Length; i++)
        {
            if (numStr[i] == m_decimalSymbol)
            {
                commands.Add(CCommand.IDC_PNT);
                if (!fSciFmt)
                {
                    fDecimal = true;
                }
            }
            else if (numStr[i] == 'e')
            {
                commands.Add(CCommand.IDC_EXP);
                fSciFmt = true;
            }
            else if (numStr[i] == '-')
            {
                commands.Add(CCommand.IDC_SIGN);
            }
            else if (numStr[i] == '+')
            {
                // Ignore.
            }
            // Number
            else
            {
                int num = (int)(numStr[i]) - ASCII_0;
                num += CCommand.IDC_0;
                commands.Add(num);
            }
        }

        var operandCommand = new COpndCommand(commands, fNegative, fDecimal, fSciFmt);
        operandCommand.Initialize(rat);
        return operandCommand;
    }

    public List<IExpressionCommand> GetCommands()
    {
        List<IExpressionCommand> commands = null;
        if (m_spCommands != null)
        {
            commands = m_spCommands;
        }

        return commands;
    }
}
