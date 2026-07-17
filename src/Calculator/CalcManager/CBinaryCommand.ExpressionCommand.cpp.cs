// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Runtime.InteropServices;
using uint8_t = System.Byte;
using uint32_t = System.UInt32;
using uint64_t = System.UInt64;
using int32_t = System.Int32;
using wchar_t = System.Char;
using wstring_view = string;
using WString = string;
using MANTTYPE = System.UInt32;
using TWO_MANTTYPE = System.UInt64;
using PNUMBER = CalcEngine.RatPakNUMBER;
using PPNUMBER = CalcEngine.RatPakNUMBER;
using PRAT = CalcEngine.RatPakRAT;
using size_t = int;
using System.Collections.Generic;

namespace CalcEngine;

internal sealed partial class CBinaryCommand : IBinaryCommand
{
    public CBinaryCommand(int command)
    {
        m_command = command;
    }

    public void SetCommand(int command)
    {
        m_command = command;
    }

    public int GetCommand()
    {
        return m_command;
    }

    public CalculationManager.CommandType GetCommandType()
    {
        return CalculationManager.CommandType.BinaryCommand;
    }

    public void Accept(ISerializeCommandVisitor commandVisitor)
    {
        commandVisitor.Visit(this);
    }
}
