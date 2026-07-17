// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include  "pch.h"
// #include  "Expressionh"

using System;
using System.Collections.Generic;
using Windows.Foundation.Metadata;
using CalculatorApp.ViewModel.Common;
using Windows.Storage.Streams;
using CalcEngine;


namespace CalculatorApp.ViewModel.Common;

internal sealed partial class CommandDeserializer
{
    public CommandDeserializer(DataReader dataReader)

    {
        m_dataReader = (dataReader);
    }

    IExpressionCommand Deserialize(CalculationManager.CommandType cmdType)
    {
        switch (cmdType)
        {
            case CalculationManager.CommandType.OperandCommand:
                return (COpndCommand)(DeserializeOperand());

            case CalculationManager.CommandType.Parentheses:
                return (CParentheses)(DeserializeParentheses());

            case CalculationManager.CommandType.UnaryCommand:
                return (CUnaryCommand)(DeserializeUnary());

            case CalculationManager.CommandType.BinaryCommand:
                return (CBinaryCommand)(DeserializeBinary());

            default:
                throw new ArgumentException(("Unknown command type"));
        }
    }

    COpndCommand DeserializeOperand()
    {
        bool fNegative = m_dataReader.ReadBoolean();
        bool fDecimal = m_dataReader.ReadBoolean();
        bool fSciFmt = m_dataReader.ReadBoolean();

        List<int> cmdVector = new List<int>();
        var cmdVectorSize = m_dataReader.ReadUInt32();

        for (uint j = 0; j < cmdVectorSize; ++j)
        {
            int eachOpndcmd = m_dataReader.ReadInt32();
            cmdVector.Add(eachOpndcmd);
        }

        return new COpndCommand(cmdVector, fNegative, fDecimal, fSciFmt);
    }

    CParentheses DeserializeParentheses()
    {
        int parenthesisCmd = m_dataReader.ReadInt32();
        return new CParentheses(parenthesisCmd);
    }

    CUnaryCommand DeserializeUnary()
    {
        var cmdSize = m_dataReader.ReadUInt32();

        if (cmdSize == 1)
        {
            int eachOpndcmd = m_dataReader.ReadInt32();
            return new CUnaryCommand(eachOpndcmd);
        }
        else
        {
            int eachOpndcmd1 = m_dataReader.ReadInt32();
            int eachOpndcmd2 = m_dataReader.ReadInt32();
            return new CUnaryCommand(eachOpndcmd1, eachOpndcmd2);
        }
    }

    CBinaryCommand DeserializeBinary()
    {
        int cmd = m_dataReader.ReadInt32();
        return new CBinaryCommand(cmd);
    }
}
