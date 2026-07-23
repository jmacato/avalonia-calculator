// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public class CUnaryCommand : IUnaryCommand
{
    List<int> m_command;

    public CUnaryCommand(int command)
    {
        m_command = [command];
    }

    public CUnaryCommand(int command1, int command2)
    {
        m_command = [command1, command2];
    }

    public IList<int> GetCommands()
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
        if (commandVisitor is null)
        {
            throw new ArgumentNullException(nameof(commandVisitor));
        }

        commandVisitor.Visit(this);
    }
}
