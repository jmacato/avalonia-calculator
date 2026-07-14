// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.Automation;
using FluentAvalonia.Styling;

namespace CalculatorApp;

public sealed partial class Settings : UserControl
{
    private bool _initializingTheme;

    public Settings()
    {
        InitializeComponent();
    }

    public event EventHandler<RoutedEventArgs>? BackButtonClick;

    public void SetDefaultFocus() => AppThemeExpander.Focus();

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _initializingTheme = true;
        FluentAvaloniaTheme? fluentTheme = GetFluentTheme();
        ThemeVariant requested = Application.Current?.RequestedThemeVariant ?? ThemeVariant.Default;
        bool followsSystemTheme = fluentTheme?.PreferSystemTheme ?? requested == ThemeVariant.Default;
        if (followsSystemTheme)
        {
            SystemThemeRadioButton.IsChecked = true;
        }
        else if (requested == ThemeVariant.Light)
        {
            LightThemeRadioButton.IsChecked = true;
        }
        else if (requested == ThemeVariant.Dark)
        {
            DarkThemeRadioButton.IsChecked = true;
        }
        _initializingTheme = false;

        string text = AppResourceProvider.GetInstance()
            .GetResourceString("SettingsPageOpenedAnnouncement");
        NarratorNotifier.Announce(
            NarratorAnnouncement.GetSettingsPageOpenedAnnouncement(text));
        SetDefaultFocus();
    }

    private void OnThemeSelectionChanged(object? sender, RoutedEventArgs e)
    {
        if (_initializingTheme || sender is not RadioButton { IsChecked: true } selected)
        {
            return;
        }

        FluentAvaloniaTheme? fluentTheme = GetFluentTheme();
        string? selectedTheme = selected.Tag?.ToString();
        if (selectedTheme == "Default")
        {
            if (fluentTheme is not null)
            {
                fluentTheme.PreferSystemTheme = true;
            }
            else
            {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
            }

            return;
        }

        if (fluentTheme is not null)
        {
            fluentTheme.PreferSystemTheme = false;
        }

        Application.Current!.RequestedThemeVariant = selectedTheme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private static FluentAvaloniaTheme? GetFluentTheme() =>
        Application.Current?.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();

    private void OpenLink_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string uri } || !Uri.TryCreate(uri, UriKind.Absolute, out _))
        {
            return;
        }

        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e) =>
        BackButtonClick?.Invoke(this, e);
}
