// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using CalcEngine;
using WString = string;
using wchar_t = char;

internal interface IBinaryCommand : IOperatorCommand
{
    new void SetCommand(int command);
    int GetCommand();
};
