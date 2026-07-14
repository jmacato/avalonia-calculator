// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;

namespace CalculatorApp;

public sealed partial class EquationInputArea : UserControl
{
    public EquationInputArea()
    {
        InitializeComponent();
    }

    public void SetDefaultFocus() => ExpressionEditor.Focus();
}
