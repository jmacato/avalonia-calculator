// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public interface IOpndCommand : IExpressionCommand
{
    IList<int> GetCommands();
    void AppendCommand(int command);
    void ToggleSign();
    void RemoveFromEnd();
    bool IsNegative();
    bool IsSciFmt();
    bool IsDecimalPresent();
    wstring GetToken(wchar_t decimalSymbol);
    void SetCommands(IList<int> commands);
};
