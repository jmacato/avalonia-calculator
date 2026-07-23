// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public class CParentheses(int command) : IParenthesisCommand
{
    public int GetCommand()
    {
        return command;
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
