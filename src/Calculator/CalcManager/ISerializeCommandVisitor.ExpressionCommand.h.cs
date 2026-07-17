// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using WString = string;
using wchar_t = char;

namespace CalcEngine;

internal interface ISerializeCommandVisitor
{
    void Visit(COpndCommand opndCmd);
    void Visit(CUnaryCommand unaryCmd);
    void Visit(CBinaryCommand binaryCmd);
    void Visit(CParentheses paraCmd);
};
