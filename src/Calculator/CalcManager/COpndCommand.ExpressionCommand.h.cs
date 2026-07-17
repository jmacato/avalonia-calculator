// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using WString = string;
using wchar_t = char;

namespace CalcEngine;
//
internal sealed partial class COpndCommand : IOpndCommand
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
    //     const std::WString GetToken(wchar_t decimalSymbol) override;
    //     CalculationManager::CommandType GetCommandType() const override;
    //     void Accept( ISerializeCommandVisitor commandVisitor) override;
    //     std::WString GetString(uint32_t radix, int32_t precision);
    //
    // private:
    bool m_fNegative;
    bool m_fSciFmt;
    bool m_fDecimal;
    bool m_fInitialized;
    WString m_token = string.Empty;
    List<int> m_commands = [];
    Rational? m_value;
    //     void ClearAllAndAppendCommand(CalculationManager::Command command);
}
