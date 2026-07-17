// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Callback interface to be implemented by the clients of CCalcEngine if they require equation history
using System.Collections.Generic;

namespace CalcEngine;

internal interface IHistoryDisplay
{
    uint AddToHistory(
        List<(string, int)> tokens,
        List<IExpressionCommand> commands,
        string result);
};
