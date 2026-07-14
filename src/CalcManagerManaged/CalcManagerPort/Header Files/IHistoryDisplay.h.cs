// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Callback interface to be implemented by the clients of CCalcEngine if they require equation history
namespace CalcEngine;

public interface IHistoryDisplay
{
    uint AddToHistory(
        IList<(string, int)> tokens,
        IList<IExpressionCommand> commands,
        string result);
};
