// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;

namespace CalculatorApp.Controls;

/// <summary>
/// XAML-configurable parameters for a coordinated selection-indicator motion.
/// </summary>
public sealed class NavigationSelectionTransitionSettings
{
    public string IndicatorName { get; set; } = "SelectionIndicator";

    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(600);

    public double PhaseBoundary { get; set; } = 0.333;

    public Point StretchControlPoint1 { get; set; } = new(0.9, 0.1);

    public Point StretchControlPoint2 { get; set; } = new(1, 0.2);

    public Point ContractControlPoint1 { get; set; } = new(0.1, 0.9);

    public Point ContractControlPoint2 { get; set; } = new(0.2, 1);
}
