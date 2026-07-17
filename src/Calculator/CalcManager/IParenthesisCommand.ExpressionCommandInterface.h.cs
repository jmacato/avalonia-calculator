// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using CalcEngine;
using WString = string;
using wchar_t = char;

internal interface IParenthesisCommand : IExpressionCommand
{
    int GetCommand();
};
