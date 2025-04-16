// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include  "pch.h"
// #include  "Common/ExpressionCommandSerializer.h"

using CalculatorApp.ViewModel.Common;
using Windows.Storage.Streams;
using CalcEngine;

namespace CalculatorApp.ViewModel.Common;

public partial class SerializeCommandVisitor : ISerializeCommandVisitor
{
    public SerializeCommandVisitor(DataWriter dataWriter)

    {
        m_dataWriter = (dataWriter);
    }

    public void Visit(COpndCommand opndCmd)
    {
        m_dataWriter.WriteBoolean(opndCmd.IsNegative());
        m_dataWriter.WriteBoolean(opndCmd.IsDecimalPresent());
        m_dataWriter.WriteBoolean(opndCmd.IsSciFmt());

        var opndCmds = opndCmd.GetCommands();
        uint opndCmdSize = (uint)(opndCmds.Count);
        m_dataWriter.WriteUInt32(opndCmdSize);
        foreach (int eachOpndcmd in opndCmds)
        {
            m_dataWriter.WriteInt32(eachOpndcmd);
        }
    }

    public void Visit(CUnaryCommand unaryCmd)
    {
        var cmds = unaryCmd.GetCommands();
        uint cmdSize = (uint)(cmds.Count);
        m_dataWriter.WriteUInt32(cmdSize);
        foreach (int eachOpndcmd in cmds)
        {
            m_dataWriter.WriteInt32(eachOpndcmd);
        }
    }

    public void Visit(CBinaryCommand binaryCmd)
    {
        int cmd = binaryCmd.GetCommand();
        m_dataWriter.WriteInt32(cmd);
    }

    public void Visit(CParentheses paraCmd)
    {
        int parenthesisCmd = paraCmd.GetCommand();
        m_dataWriter.WriteInt32(parenthesisCmd);
    }
}
