// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel;

public sealed class GraphingInputRequestedEventArgs(GraphingInputAction action, string text = "", int cursorOffset = 0, int selectionLength = 0) : EventArgs
{
    public GraphingInputAction Action { get; } = action;
    public string Text { get; } = text;
    public int CursorOffset { get; } = cursorOffset;
    public int SelectionLength { get; } = selectionLength;
}
