// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;
using wstring = string;
using wchar_t = char;

public interface IExpressionCommand
{
    CalculationManager.CommandType GetCommandType();
    void Accept(ISerializeCommandVisitor commandVisitor);
};

public interface IOperatorCommand : IExpressionCommand
{
    void SetCommand(int command);
};

public interface IUnaryCommand : IOperatorCommand
{
    List<int> GetCommands();
    void SetCommands(int command1, int command2);
};

public interface IBinaryCommand : IOperatorCommand
{
    void SetCommand(int command);
    int GetCommand();
};

public interface IOpndCommand : IExpressionCommand
{
    List<int> GetCommands();
    void AppendCommand(int command);
    void ToggleSign();
    void RemoveFromEnd();
    bool IsNegative();
    bool IsSciFmt();
    bool IsDecimalPresent();
    wstring GetToken(wchar_t decimalSymbol);
    void SetCommands(List<int> commands);
};

public interface IParenthesisCommand : IExpressionCommand
{
    int GetCommand();
};
