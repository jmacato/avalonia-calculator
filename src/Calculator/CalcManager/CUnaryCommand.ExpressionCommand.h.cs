// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using WString = string;
using wchar_t = char;

namespace CalcEngine;
//
internal sealed partial class CUnaryCommand : IUnaryCommand
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
