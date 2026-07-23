// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public interface IOperatorCommand : IExpressionCommand
{
    void SetCommand(int command);
};
