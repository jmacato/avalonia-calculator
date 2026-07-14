// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;

namespace CalculatorApp.Controls;

// The original WinUI control's item host is a horizontal panel. Its two Scientific
// items fit at every supported width; Programmer will add the translated overflow path.
public sealed class OperatorPanelListView : StackPanel
{
    public OperatorPanelListView()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal;
    }
}
