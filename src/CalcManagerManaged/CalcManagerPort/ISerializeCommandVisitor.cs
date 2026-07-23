// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public interface ISerializeCommandVisitor
{
    void Visit(COpndCommand opndCmd);
    void Visit(CUnaryCommand unaryCmd);
    void Visit(CBinaryCommand binaryCmd);
    void Visit(CParentheses paraCmd);
};
