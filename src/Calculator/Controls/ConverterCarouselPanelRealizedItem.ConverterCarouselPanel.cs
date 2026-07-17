// Copyright (c) Microsoft Corporation and the Avalonia contributors.
// Licensed under the MIT License.
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace CalculatorApp.Controls;

internal sealed class ConverterCarouselPanelRealizedItem(int itemIndex, int logicalIndex, Control control)
{
    public int ItemIndex { get; } = itemIndex;
    public int LogicalIndex { get; set; } = logicalIndex;
    public Control Control { get; } = control;
}
