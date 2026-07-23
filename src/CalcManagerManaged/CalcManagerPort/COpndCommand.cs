// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public class COpndCommand(IList<int> commands, bool fNegative, bool fDecimal, bool fSciFmt)
    : IOpndCommand
{
    bool m_fNegative = fNegative;

    bool m_fSciFmt = fSciFmt;

    bool m_fDecimal = fDecimal;

    bool m_fInitialized;

    wstring m_token = "";

    IList<int> m_commands = commands;

    Rational? m_value;

    private const int IdcSign = 80;

    private const int IdcPnt = 84;

    private const int Idc0 = 130; // The controls for 0 through F must be consecutive and in order

    private const int IdcExp = 127; // Exponent

    private const wchar_t chNegate = '-';

    private const wchar_t chExp = 'e';

    private const wchar_t chPlus = '+';

    public void Initialize(Rational rat)
    {
        m_value = rat;
        m_fInitialized = true;
    }

    public IList<int> GetCommands()
    {
        return m_commands;
    }

    public void SetCommands(IList<int> commands)
    {
        m_commands = commands;
    }

    public void AppendCommand(int command)
    {
        if (m_fSciFmt)
        {
            ClearAllAndAppendCommand((Command)command);
        }
        else
        {
            m_commands.Add(command);
        }

        if (command == IdcPnt)
        {
            m_fDecimal = true;
        }
    }

    public void ToggleSign()
    {
        foreach (var nOpCode in m_commands)
        {
            if (nOpCode != Idc0)
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
            ClearAllAndAppendCommand(Command.Num0);
        }
        else
        {
            var nCommands = m_commands.Count;

            if (nCommands == 1)
            {
                ClearAllAndAppendCommand(Command.Num0);
            }
            else
            {
                var nOpCode = m_commands[nCommands - 1];

                if (nOpCode == IdcPnt)
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

    void ClearAllAndAppendCommand(Command command)
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
        var nCommands = m_commands.Count;
        m_token = "";

        for (var i = 0; i < nCommands; i++)
        {
            var nOpCode = m_commands[i];

            if (nOpCode == IdcPnt)
            {
                m_token += decimalSymbol;
            }
            else if (nOpCode == IdcExp)
            {
                m_token += chExp;
                var nextOpCode = m_commands[i + 1];
                if (nextOpCode != IdcSign)
                {
                    m_token += chPlus;
                }
            }
            else if (nOpCode == IdcSign)
            {
                m_token += chNegate;
            }
            else
            {
                m_token += (char)('0' + (nOpCode - Idc0));
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
            return m_value!.ToString(radix, NumberFormat.FloatingPoint, precision);
        }

        return string.Empty;
    }

    public void Accept(ISerializeCommandVisitor commandVisitor)
    {
        if (commandVisitor is null)
        {
            throw new ArgumentNullException(nameof(commandVisitor));
        }

        commandVisitor.Visit(this);
    }
}
