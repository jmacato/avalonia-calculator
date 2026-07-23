// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Collections;
using GraphControl;

namespace CalculatorApp;

public sealed class EquationLineStyleChoice
{
    public required EquationLineStyle Style { get; init; }
    public required string AutomationName { get; init; }
    public AvaloniaList<double> DashArray { get; init; } = [];

    public override string ToString()
    {
        return AutomationName;
    }
}
