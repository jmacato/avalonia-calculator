// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel;

public sealed class GraphThemeSettingChangedEventArgs(bool matchesAppTheme) : EventArgs
{
    public bool MatchesAppTheme { get; } = matchesAppTheme;
}
