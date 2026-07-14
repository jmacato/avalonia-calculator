// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public interface IExpressionCommand
{
    CalculationManager.CommandType GetCommandType();
    void Accept(ISerializeCommandVisitor commandVisitor);
};
