// Copyright (c) Microsoft Corporation and the Avalonia contributors.
// Licensed under the MIT License.

using Avalonia.Controls;

namespace CalculatorApp.Controls;

internal sealed class ConverterCarouselPanelRealizedItem(int itemIndex, int logicalIndex, Control control)
{
    public int ItemIndex { get; } = itemIndex;
    public int LogicalIndex { get; set; } = logicalIndex;
    public Control Control { get; } = control;
}
