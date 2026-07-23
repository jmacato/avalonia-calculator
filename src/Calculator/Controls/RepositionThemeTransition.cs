// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.Controls;

/// <summary>
/// Animates layout-position changes on the compositor.
/// </summary>
public sealed class RepositionThemeTransition : ThemeTransition
{
    public bool IsStaggeringEnabled { get; set; } = true;
}
