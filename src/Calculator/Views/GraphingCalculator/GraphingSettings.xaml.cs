// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CalculatorApp.ViewModel;
using GraphControl;

namespace CalculatorApp;

public sealed partial class GraphingSettings : UserControl
{
    public GraphingSettings()
    {
        InitializeComponent();
        Model = new GraphingSettingsViewModel();
        DataContext = Model;
    }

    public GraphingSettingsViewModel Model { get; }

    public bool IsMatchAppTheme => Model.IsMatchAppTheme;

    public event Action<bool>? GraphThemeSettingChanged
    {
        add => Model.GraphThemeSettingChanged += value;
        remove => Model.GraphThemeSettingChanged -= value;
    }

    public void SetGrapher(Grapher grapher) => Model.SetGrapher(grapher);

    private void OnRangeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        IInputElement? next = topLevel?.FocusManager?.FindNextElement(
            NavigationDirection.Next,
            new FindNextElementOptions { SearchRoot = this });
        next?.Focus();
        e.Handled = true;
    }

    private void OnResetViewClicked(object? sender, RoutedEventArgs e) => Model.ResetView();
}
