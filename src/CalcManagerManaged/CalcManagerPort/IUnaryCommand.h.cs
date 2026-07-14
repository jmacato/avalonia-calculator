// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public interface IUnaryCommand : IOperatorCommand
{
    IList<int> GetCommands();
    void SetCommands(int command1, int command2);
};
