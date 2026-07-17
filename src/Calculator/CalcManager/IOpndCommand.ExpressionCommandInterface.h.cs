// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using CalcEngine;
using WString = string;
using wchar_t = char;

internal interface IOpndCommand : IExpressionCommand
{
    List<int> GetCommands();
    void AppendCommand(int command);
    void ToggleSign();
    void RemoveFromEnd();
    bool IsNegative();
    bool IsSciFmt();
    bool IsDecimalPresent();
    WString GetToken(wchar_t decimalSymbol);
    void SetCommands(List<int> commands);
};
