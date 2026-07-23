// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

public interface IBinaryCommand : IOperatorCommand
{
    int GetCommand();
};
