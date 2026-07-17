// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using WString = string;
using wchar_t = char;

namespace CalcEngine;
//
internal sealed partial class CBinaryCommand : IBinaryCommand
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
