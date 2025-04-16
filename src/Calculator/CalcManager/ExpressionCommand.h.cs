// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using wstring = string;
using wchar_t = char;

namespace CalcEngine;

public partial class CParentheses : IParenthesisCommand
{
// public:
//     CParentheses( int command);
//     int GetCommand() const override;
//     CalculationManager::CommandType GetCommandType() const override;
//     void Accept( ISerializeCommandVisitor commandVisitor) override;
//
// private:
    int m_command;
};

//
public partial class CUnaryCommand : IUnaryCommand

{
// public:
//     CUnaryCommand(int command);
//     CUnaryCommand(int command1, int command2);
//     const std::shared_ptr<std::vector<int>> GetCommands() const override;
//     CalculationManager::CommandType GetCommandType() const override;
//     void SetCommand(int command) override;
//     void SetCommands(int command1, int command2) override;
//     void Accept( ISerializeCommandVisitor commandVisitor) override;
//
// private:
    List<int> m_command;
}

//
public partial class CBinaryCommand : IBinaryCommand
// class CBinaryCommand final : public IBinaryCommand
{
// public:
//     CBinaryCommand(int command);
//     void SetCommand(int command) override;
//     int GetCommand() const override;
//     CalculationManager::CommandType GetCommandType() const override;
//     void Accept( ISerializeCommandVisitor commandVisitor) override;
//
// private:
    int m_command;
}

//
public partial class COpndCommand : IOpndCommand
{
// public:
//     COpndCommand(std::shared_ptr<std::vector<int>> const commands, bool fNegative, bool fDecimal, bool fSciFmt);
//     void Initialize(CalcEngine::Rational const rat);
//
//     const std::shared_ptr<std::vector<int>> GetCommands() const override;
//     void SetCommands(std::shared_ptr<std::vector<int>> const commands) override;
//     void AppendCommand(int command) override;
//     void ToggleSign() override;
//     void RemoveFromEnd() override;
//     bool IsNegative() const override;
//     bool IsSciFmt() const override;
//     bool IsDecimalPresent() const override;
//     const std::wstring GetToken(wchar_t decimalSymbol) override;
//     CalculationManager::CommandType GetCommandType() const override;
//     void Accept( ISerializeCommandVisitor commandVisitor) override;
//     std::wstring GetString(uint32_t radix, int32_t precision);
//
// private:
    bool m_fNegative;
    bool m_fSciFmt;
    bool m_fDecimal;
    bool m_fInitialized;
    wstring m_token;
    List<int> m_commands;
    Rational m_value;
//     void ClearAllAndAppendCommand(CalculationManager::Command command);
}


public interface ISerializeCommandVisitor
{
    void Visit(COpndCommand opndCmd);
    void Visit(CUnaryCommand unaryCmd);
    void Visit(CBinaryCommand binaryCmd);
    void Visit(CParentheses paraCmd);
};
