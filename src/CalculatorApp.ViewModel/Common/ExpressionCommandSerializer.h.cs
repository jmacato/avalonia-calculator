// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #pragma  once

// #include  "CalcManager/ExpressionCommand.h"

using CalcEngine;

namespace CalculatorApp.ViewModel.Common
{
    public partial class SerializeCommandVisitor : ISerializeCommandVisitor
    {
        // public:
        //     SerializeCommandVisitor( Windows.Storage.Streams.DataWriter  dataWriter);
        //
        //     void Visit( COpndCommand& opndCmd);
        //     void Visit( CUnaryCommand& unaryCmd);
        //     void Visit( CBinaryCommand& binaryCmd);
        //     void Visit( CParentheses& paraCmd);

        // private:
        Windows.Storage.Streams.DataWriter m_dataWriter;
    };
}
