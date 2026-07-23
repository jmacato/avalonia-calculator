// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public class CBinaryCommand(int command) : IBinaryCommand
{
    int m_command = command;

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
        if (commandVisitor is null)
        {
            throw new ArgumentNullException(nameof(commandVisitor));
        }

        commandVisitor.Visit(this);
    }
}
