// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel;

public sealed class HistoryItemClickedEventArgs(HistoryItemViewModel item) : EventArgs
{
    public HistoryItemViewModel Item { get; } = item;
}
