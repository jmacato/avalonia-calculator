// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;

namespace CalcEngine;

public class CHistoryCollector
{
    const ulong MAXPRECDEPTH = 25;

    // private:
    IHistoryDisplay m_pHistoryDisplay;

    ICalcDisplay m_pCalcDisplay;

    // a sort of state, set to the index before 2 after 2 in the expression 2 + 3 say. Useful for auto correct portion of history and for
    // attaching the unary op around the last operand
    int m_lastOpStartIndex;    // index of the beginning of the last operand added to the history

    int m_lastBinOpStartIndex; // index of the beginning of the last binary operator added to the history

    int[] m_operandIndices = new int[MAXPRECDEPTH];  // Stack of index of opnd's beginning for each '('. A parallel array to m_hnoParNum, but abstracted independently of that

    int m_curOperandIndex; // Stack index for the above stack

    bool m_bLastOpndBrace; // iff the last opnd in history is already braced so we can avoid putting another one for unary operator

    wchar_t m_decimalSymbol;

    List<(wstring, int)>? m_spTokens;

    List<IExpressionCommand>? m_spCommands;

    private readonly CCalcEngine m_cCalcEngine;

    const int ASCII_0 = 48;

    static void Truncate<T>(List<T> v, int index)
    {
        if (index >= v.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
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
        m_decimalSymbol = (decimalSymbol);
        ReinitHistory();
    }

    ~CHistoryCollector()
    {
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
        AddCommand(new CParentheses(CCommand.IdcOpenp));
        int ichOpndStart = IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IdcOpenp), -1);
        PushLastOpndStart(ichOpndStart);

        SetExpressionDisplay();
        m_lastBinOpStartIndex = -1;
    }

    public void AddCloseBraceToHistory()
    {
        AddCommand(new CParentheses(CCommand.IdcClosep));
        IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IdcClosep), -1);
        SetExpressionDisplay();
        PopLastOpndStart();

        m_lastBinOpStartIndex = -1;
        m_bLastOpndBrace = true;
    }

    public void EnclosePrecInversionBrackets()
    {
        // Top of the Opnd starts index or 0 is nothing is in top
        int ichStart = (m_curOperandIndex > 0) ? m_operandIndices[m_curOperandIndex - 1] : 0;

        InsertSzInEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IdcOpenp), -1, ichStart);
        IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IdcClosep), -1);
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
    public void AddUnaryOpToHistory(int nOpCode, bool fInv, AngleType angletype)
    {
        int iCommandEnd;
        // When successfully applying a unary op, there should be an opnd already
        // A very special case of % which is a funny post op unary op.
        if (CCommand.IdcPercent == nOpCode)
        {
            iCommandEnd = AddCommand(new CUnaryCommand(nOpCode));
            IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(nOpCode), iCommandEnd);
        }
        else // all the other unary ops
        {
            IOperatorCommand spExpressionCommand;
            if (CCommand.IdcSign == nOpCode)
            {
                spExpressionCommand = new CUnaryCommand(nOpCode);
            }
            else
            {
                spExpressionCommand = CreateUnaryCommand(nOpCode, fInv, angletype);
            }

            iCommandEnd = AddCommand(spExpressionCommand);

            wstring operandStr =
                m_cCalcEngine.OpCodeToUnaryString(nOpCode, fInv, angletype);

            ;
            if (!m_bLastOpndBrace) // The opnd is already covered in braces. No need for additional braces around it
            {
                operandStr += (m_cCalcEngine.OpCodeToString(CCommand.IdcOpenp));
            }

            InsertSzInEquationSz(operandStr, iCommandEnd, m_lastOpStartIndex);

            if (!m_bLastOpndBrace)
            {
                IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IdcClosep), -1);
            }
        }

        SetExpressionDisplay();
        m_bLastOpndBrace = false;
        // m_lastOpStartIndex remains the same as last opnd is just replaced by unaryop(lastopnd)
        m_lastBinOpStartIndex = -1;
    }

    private static IOperatorCommand CreateUnaryCommand(int nOpCode, bool fInv, AngleType angletype)
    {
        CalculationManager.Command angleOpCode;
        if (angletype == AngleType.Degrees)
        {
            angleOpCode = CalculationManager.Command.Deg;
        }
        else if (angletype == AngleType.Radians)
        {
            angleOpCode = CalculationManager.Command.Rad;
        }
        else // (angletype == AngleType.Gradians)
        {
            angleOpCode = CalculationManager.Command.Grad;
        }

        IOperatorCommand spExpressionCommand;
        int command = nOpCode;
        switch (nOpCode)
        {
            case CCommand.IdcSin:
                command = fInv ? (int)(CalculationManager.Command.Asin) : CCommand.IdcSin;
                spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                break;
            case CCommand.IdcCos:
                command = fInv ? (int)(CalculationManager.Command.Acos) : CCommand.IdcCos;
                spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                break;
            case CCommand.IdcTan:
                command = fInv ? (int)(CalculationManager.Command.Atan) : CCommand.IdcTan;
                spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                break;
            case CCommand.IdcSinh:
                command = fInv ? (int)(CalculationManager.Command.Asinh) : CCommand.IdcSinh;
                spExpressionCommand = new CUnaryCommand(command);
                break;
            case CCommand.IdcCosh:
                command = fInv ? (int)(CalculationManager.Command.Acosh) : CCommand.IdcCosh;
                spExpressionCommand = new CUnaryCommand(command);
                break;
            case CCommand.IdcTanh:
                command = fInv ? (int)(CalculationManager.Command.Atanh) : CCommand.IdcTanh;
                spExpressionCommand = new CUnaryCommand(command);
                break;
            default:
                spExpressionCommand = CreateExtendedUnaryCommand(nOpCode, fInv, angleOpCode);
                break;
        }

        return spExpressionCommand;
    }

    private static IOperatorCommand CreateExtendedUnaryCommand(int nOpCode, bool fInv, CalculationManager.Command angleOpCode)
    {
        IOperatorCommand spExpressionCommand;
        int command = nOpCode;
        switch (nOpCode)
        {
            case CCommand.IdcSec:
                command = fInv ? (int)(CalculationManager.Command.Asec) : CCommand.IdcSec;
                spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                break;
            case CCommand.IdcCsc:
                command = fInv ? (int)(CalculationManager.Command.Acsc) : CCommand.IdcCsc;
                spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                break;
            case CCommand.IdcCot:
                command = fInv ? (int)(CalculationManager.Command.Acot) : CCommand.IdcCot;
                spExpressionCommand = new CUnaryCommand((int)(angleOpCode), command);
                break;
            case CCommand.IdcSech:
                command = fInv ? (int)(CalculationManager.Command.Asech) : CCommand.IdcSech;
                spExpressionCommand = new CUnaryCommand(command);
                break;
            case CCommand.IdcCsch:
                command = fInv ? (int)(CalculationManager.Command.Acsch) : CCommand.IdcCsch;
                spExpressionCommand = new CUnaryCommand(command);
                break;
            case CCommand.IdcCoth:
                command = fInv ? (int)(CalculationManager.Command.Acoth) : CCommand.IdcCoth;
                spExpressionCommand = new CUnaryCommand(command);
                break;
            case CCommand.IdcLn:
                command = fInv ? (int)(CalculationManager.Command.PowE) : CCommand.IdcLn;
                spExpressionCommand = new CUnaryCommand(command);
                break;
            default:
                spExpressionCommand = new CUnaryCommand(nOpCode);
                break;
        }

        return spExpressionCommand;
    }

    // Called after = with the result of the equation
    // Responsible for clearing the top line of current running history display, as well as adding yet another element to
    // history of equations
    public void CompleteHistoryLine(wstring_view numStr)
    {
        if (null != m_pHistoryDisplay)
        {
            uint addedItemIndex = m_pHistoryDisplay.AddToHistory(m_spTokens!, m_spCommands!, numStr);
            m_pCalcDisplay.OnHistoryItemAdded(addedItemIndex);
        }

        m_spTokens = null;
        m_spCommands = null;
        ReinitHistory();
    }

    public void CompleteEquation(wstring_view numStr)
    {
        // Add only '=' token and not add EQU command, because
        // EQU command breaks loading from history (it duplicate history entries).
        IchAddSzToEquationSz(m_cCalcEngine.OpCodeToString(CCommand.IdcEqu), -1);

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
        m_spTokens!.Insert(ich, (str, icommandIndex));
    }

    // Chops off the current equation string from the given index
    public void TruncateEquationSzFromIch(int ich)
    {
        // Truncate commands
        int minIdx = -1;
        int nTokens = m_spTokens!.Count;

        for (int i = ich; i < nTokens; i++)
        {
            var currentPair = (m_spTokens)[i];
            int curTokenId = currentPair.Item2;
            if (curTokenId != -1)
            {
                if ((minIdx != -1) || (curTokenId < minIdx))
                {
                    minIdx = curTokenId;
                    Truncate(m_spCommands!, minIdx);
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
            m_pCalcDisplay.SetExpressionDisplay(m_spTokens!, m_spCommands!);
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
                var expCommand = m_spCommands![commandPosition];

                if (expCommand != null &&
                    CalculationManager.CommandType.OperandCommand == expCommand.GetCommandType())
                {
                    var opndCommand = (COpndCommand)(expCommand);
                    m_spTokens[token_i] = (opndCommand.GetString(radix, precision), m_spTokens[token_i].Item2);
                    opndCommand.SetCommands(GetOperandCommandsFromString(m_spTokens[token_i].Item1));
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
    public IList<int> GetOperandCommandsFromString(wstring_view numStr)
    {
        if (numStr is null)
        {
            throw new ArgumentNullException(nameof(numStr));
        }

        List<int> commands = new List<int>();
        // Check for negate
        bool fNegative = (numStr[0] == '-');

        for (int i = (fNegative ? 1 : 0); i < numStr.Length; i++)
        {
            if (numStr[i] == m_decimalSymbol)
            {
                commands.Add(CCommand.IdcPnt);
            }
            else if (numStr[i] == 'e')
            {
                commands.Add(CCommand.IdcExp);
            }
            else if (numStr[i] == '-')
            {
                commands.Add(CCommand.IdcSign);
            }
            else if (numStr[i] == '+')
            {
                // Ignore.
            }
            // Number
            else
            {
                int num = (int)(numStr[i]) - ASCII_0;
                num += CCommand.Idc0;
                commands.Add(num);
            }
        }

        // If the number is negative, append a sign command at the end.
        if (fNegative)
        {
            commands.Add(CCommand.IdcSign);
        }

        return commands;
    }

    public COpndCommand GetOperandCommandsFromString(wstring_view numStr, Rational rat)
    {
        if (numStr is null)
        {
            throw new ArgumentNullException(nameof(numStr));
        }

        List<int> commands = new List<int>();
        // Check for negate
        bool fNegative = (numStr[0] == '-');
        bool fSciFmt = false;
        bool fDecimal = false;

        for (int i = (fNegative ? 1 : 0); i < numStr.Length; i++)
        {
            if (numStr[i] == m_decimalSymbol)
            {
                commands.Add(CCommand.IdcPnt);
                if (!fSciFmt)
                {
                    fDecimal = true;
                }
            }
            else if (numStr[i] == 'e')
            {
                commands.Add(CCommand.IdcExp);
                fSciFmt = true;
            }
            else if (numStr[i] == '-')
            {
                commands.Add(CCommand.IdcSign);
            }
            else if (numStr[i] == '+')
            {
                // Ignore.
            }
            // Number
            else
            {
                int num = (int)(numStr[i]) - ASCII_0;
                num += CCommand.Idc0;
                commands.Add(num);
            }
        }

        var operandCommand = new COpndCommand(commands, fNegative, fDecimal, fSciFmt);
        operandCommand.Initialize(rat);
        return operandCommand;
    }

    public IList<IExpressionCommand>? Commands
    {
        get
        {
            return m_spCommands;
        }
    }
}
