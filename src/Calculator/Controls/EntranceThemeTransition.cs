// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.Controls;

/// <summary>
/// Moves and fades an element into its arranged position on the compositor.
/// </summary>
public sealed class EntranceThemeTransition : ThemeTransition
{
    public double FromHorizontalOffset { get; set; }

    public double FromVerticalOffset { get; set; }

    public bool IsStaggeringEnabled { get; set; }
}
