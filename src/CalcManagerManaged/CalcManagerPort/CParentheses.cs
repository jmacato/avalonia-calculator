// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace CalcEngine;

public class CParentheses : IParenthesisCommand
{
    int m_command;

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
        if (commandVisitor is null)
        {
            throw new ArgumentNullException(nameof(commandVisitor));
        }

        commandVisitor.Visit(this);
    }
}
