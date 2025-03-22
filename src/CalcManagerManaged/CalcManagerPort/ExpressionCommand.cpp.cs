// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using uint8_t = System.Byte;
using uint32_t = System.UInt32;
using uint64_t = System.UInt64;
using int32_t = System.Int32;
using wchar_t = System.Char;
using wstring_view = string;
using wstring = string;
using MANTTYPE = System.UInt32;
using TWO_MANTTYPE = System.UInt64;
using PNUMBER = CalcEngine.RatPak.NUMBER;
using PPNUMBER = CalcEngine.RatPak.NUMBER;
using PRAT = CalcEngine.RatPak.RAT;
using size_t = int;

namespace CalcEngine;

public partial class CParentheses : IParenthesisCommand
{
    public CParentheses(int command)
    {
        m_command = command;
    }

    public int GetCommand()
    {
        return m_command;
    }

    public CalculationManager.CommandType GetCommandType()
    {
        return CalculationManager.CommandType.Parentheses;
    }

    public void Accept(ISerializeCommandVisitor commandVisitor)
    {
        commandVisitor.Visit(this);
    }
}

public partial class CUnaryCommand : IUnaryCommand
{
    public CUnaryCommand(int command)
    {
        m_command = new List<int>();
        m_command.Add(command);
    }

public    CUnaryCommand(int command1, int command2)
    {
        m_command = new List<int>();
        m_command.Add(command1);
        m_command.Add(command2);
    }

    public List<int> GetCommands()
    {
        return m_command;
    }

    public CalculationManager.CommandType GetCommandType()
    {
        return CalculationManager.CommandType.UnaryCommand;
    }

    public void SetCommand(int command)
    {
        m_command.Clear();
        m_command.Add(command);
    }

    public void SetCommands(int command1, int command2)
    {
        m_command.Clear();
        m_command.Add(command1);
        m_command.Add(command2);
    }

    public void Accept(ISerializeCommandVisitor commandVisitor)
    {
        commandVisitor.Visit(this);
    }
}

public partial class CBinaryCommand : IBinaryCommand
{
    public CBinaryCommand(int command)
    {
        m_command = command;
    }

    public void SetCommand(int command)
    {
        m_command = command;
    }

    public int GetCommand()
    {
        return m_command;
    }

    public CalculationManager.CommandType GetCommandType()
    {
        return CalculationManager.CommandType.BinaryCommand;
    }

    public void Accept(ISerializeCommandVisitor commandVisitor)
    {
        commandVisitor.Visit(this);
    }
}

public partial class COpndCommand : IOpndCommand
{
    private const int IDC_SIGN = 80;
    private const int IDC_CLEAR = 81;
    private const int IDC_CENTR = 82;
    private const int IDC_BACK = 83;
    private const int IDC_PNT = 84;
    private const int IDC_0 = 130; // The controls for 0 through F must be consecutive and in order
    private const int IDC_EXP = 127; // Exponent

    private const wchar_t chNegate = '-';
    private const wchar_t chExp = 'e';
    private const wchar_t chPlus = '+';

    public COpndCommand(List<int> commands, bool fNegative, bool fDecimal, bool fSciFmt)

    {
        m_commands = commands;
        m_fNegative = fNegative;
        m_fSciFmt = fSciFmt;
        m_fDecimal = fDecimal;
        m_fInitialized = false;
        m_value = null;
    }


    public void Initialize(Rational rat)
    {
        m_value = rat;
        m_fInitialized = true;
    }

    public List<int> GetCommands()
    {
        return m_commands;
    }

    public void SetCommands(List<int> commands)
    {
        m_commands = commands;
    }

    public void AppendCommand(int command)
    {
        if (m_fSciFmt)
        {
            ClearAllAndAppendCommand((CalculationManager.Command)command);
        }
        else
        {
            m_commands.Add(command);
        }

        if (command == IDC_PNT)
        {
            m_fDecimal = true;
        }
    }

    public void ToggleSign()
    {
        foreach (var nOpCode in m_commands)
        {
            if (nOpCode != IDC_0)
            {
                m_fNegative = !m_fNegative;
                break;
            }
        }
    }

    public void RemoveFromEnd()
    {
        if (m_fSciFmt)
        {
            ClearAllAndAppendCommand(CalculationManager.Command.Command0);
        }
        else
        {
            var nCommands = m_commands.Count;

            if (nCommands == 1)
            {
                ClearAllAndAppendCommand(CalculationManager.Command.Command0);
            }
            else
            {
                var nOpCode = m_commands[nCommands - 1];

                if (nOpCode == IDC_PNT)
                {
                    m_fDecimal = false;
                }

                m_commands.RemoveAt(m_commands.Count - 1);
            }
        }
    }

    public bool IsNegative()
    {
        return m_fNegative;
    }

    public bool IsSciFmt()
    {
        return m_fSciFmt;
    }

    public bool IsDecimalPresent()
    {
        return m_fDecimal;
    }

    public CalculationManager.CommandType GetCommandType()
    {
        return CalculationManager.CommandType.OperandCommand;
    }

    void ClearAllAndAppendCommand(CalculationManager.Command command)
    {
        m_commands.Clear();
        m_commands.Add((int)command);
        m_fSciFmt = false;
        m_fNegative = false;
        m_fDecimal = false;
    }

    public wstring GetToken(wchar_t decimalSymbol)
    {
        // Rewritten to match C++ logic more closely
        var chZero = '0';
        var nCommands = m_commands.Count();
        m_token = "";

        for (var i = 0; i < nCommands; i++)
        {
            var nOpCode = m_commands[i];

            if (nOpCode == IDC_PNT)
            {
                m_token += decimalSymbol;
            }
            else if (nOpCode == IDC_EXP)
            {
                m_token += chExp;
                var nextOpCode = m_commands[i + 1];
                if (nextOpCode != IDC_SIGN)
                {
                    m_token += chPlus;
                }
            }
            else if (nOpCode == IDC_SIGN)
            {
                m_token += chNegate;
            }
            else
            {
                m_token += (char)('0' + (nOpCode - IDC_0));
            }
        }

        // Remove zeros
        for (var i = 0; i < m_token.Length; i++)
        {
            if (m_token[i] != chZero)
            {
                if (m_token[i] == decimalSymbol)
                {
                    m_token = m_token.Substring(i - 1);
                }
                else
                {
                    m_token = m_token.Substring(i);
                }

                if (m_fNegative)
                {
                    m_token = chNegate + m_token;
                }

                return m_token;
            }
        }

        // If all zeros, return just one zero
        m_token = chZero.ToString();
        return m_token;
    }
    public wstring GetString(uint32_t radix, int32_t precision)
    {
        if (m_fInitialized)
        {
            return m_value.ToString(radix, RatPak.NumberFormat.Float, precision);
        }

        return string.Empty;
    }

    public void Accept(ISerializeCommandVisitor commandVisitor)
    {
        commandVisitor.Visit(this);
    }
}
