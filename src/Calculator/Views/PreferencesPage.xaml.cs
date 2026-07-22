// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CalculatorApp.Controls;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.Automation;
using FluentAvalonia.Core;
using FluentAvalonia.Styling;

namespace CalculatorApp;

public sealed partial class PreferencesPage : UserControl
{
    private const double SettingsEntranceOffset = 50;
    private const double RepositionEpsilon = 0.01;
    private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(667);
    private static readonly TimeSpan EntranceStaggerDelay = TimeSpan.FromMilliseconds(35);
    private static readonly TimeSpan EntranceStaggerCap = TimeSpan.FromMilliseconds(333);
    private static readonly TimeSpan RepositionDuration = TimeSpan.FromMilliseconds(367);
    private static readonly SplineEasing ThemeTransitionEasing = new(0.1, 0.9, 0.2, 1);
    private readonly ISettingsStore _settingsStore;
    private readonly Dictionary<Control, double> _lastChildPositions = new();
    private readonly Dictionary<Control, (double Offset, long Started)> _repositionAnimations = new();
    private IDisposable? _entranceCompletion;
    private bool _isEntranceActive;
    private bool _initializingTheme;
    private bool _initializingConverterUnitDisplay;
    private bool _initializingAutomaticCurrencyRefresh;

    public PreferencesPage()
        : this(App.SettingsStore)
    {
    }

    internal PreferencesPage(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        InitializeComponent();
        SettingsItemsPanel.LayoutUpdated += OnSettingsItemsLayoutUpdated;
    }

    public event EventHandler<RoutedEventArgs>? BackButtonClick;

    public void SetDefaultFocus() => AppThemeExpander.Focus();

    public void BeginOpenAnimation()
    {
        ResetMotionVisuals();
        CaptureChildPositions();
        if (!FAUISettings.AreAnimationsEnabled())
        {
            return;
        }

        _ = WinUiCompositorMotion.AnimateOpacity(
            this,
            0,
            (float)Opacity,
            EntranceDuration,
            ThemeTransitionEasing);
        int index = 0;
        foreach (Control child in SettingsItemsPanel.Children)
        {
            TimeSpan delay = TimeSpan.FromMilliseconds(Math.Min(
                EntranceStaggerDelay.TotalMilliseconds * index,
                EntranceStaggerCap.TotalMilliseconds));
            _ = WinUiCompositorMotion.AnimateTranslation(
                child,
                new Vector3(0, (float)SettingsEntranceOffset, 0),
                Vector3.Zero,
                EntranceDuration,
                ThemeTransitionEasing,
                delay);
            _ = WinUiCompositorMotion.AnimateOpacity(
                child,
                0,
                (float)child.Opacity,
                EntranceDuration,
                ThemeTransitionEasing,
                delay);
            index++;
        }

        _isEntranceActive = true;
        _entranceCompletion = DispatcherTimer.RunOnce(
            CompleteEntranceAnimation,
            EntranceDuration + EntranceStaggerCap,
            DispatcherPriority.Render);
    }

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

        _initializingConverterUnitDisplay = true;
        RadioButton selectedUnitDisplay =
            _settingsStore.Current.ConverterUnitDisplayMode switch
            {
                ConverterUnitDisplayMode.Left => LeftUnitDisplayRadioButton,
                ConverterUnitDisplayMode.Right => RightUnitDisplayRadioButton,
                ConverterUnitDisplayMode.WindowsNative => WindowsNativeUnitDisplayRadioButton,
                _ => AutomaticUnitDisplayRadioButton
            };
        selectedUnitDisplay.IsChecked = true;
        _initializingConverterUnitDisplay = false;

        _initializingAutomaticCurrencyRefresh = true;
        AutomaticCurrencyRefreshToggle.IsChecked =
            _settingsStore.Current.AutomaticCurrencyRefresh;
        _initializingAutomaticCurrencyRefresh = false;

        string text = AppResourceProvider.Instance
            .GetResourceString("SettingsPageOpenedAnnouncement");
        NarratorNotifier.Announce(
            NarratorAnnouncement.GetSettingsPageOpenedAnnouncement(text));
        SetDefaultFocus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ResetMotionVisuals();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSettingsItemsLayoutUpdated(object? sender, EventArgs e)
    {
        if (_isEntranceActive)
        {
            CaptureChildPositions();
            return;
        }

        foreach (Control child in SettingsItemsPanel.Children)
        {
            double position = child.Bounds.Y;
            if (_lastChildPositions.TryGetValue(child, out double previousPosition))
            {
                double layoutOffset = previousPosition - position;
                if (Math.Abs(layoutOffset) > RepositionEpsilon)
                {
                    BeginReposition(child, layoutOffset);
                }
            }

            _lastChildPositions[child] = position;
        }
    }

    private void BeginReposition(Control child, double layoutOffset)
    {
        if (!FAUISettings.AreAnimationsEnabled())
        {
            _repositionAnimations.Remove(child);
            WinUiCompositorMotion.SetTranslation(child, Vector3.Zero);
            return;
        }

        double offset = CurrentRepositionOffset(child) + layoutOffset;
        _repositionAnimations[child] = (offset, Stopwatch.GetTimestamp());
        _ = WinUiCompositorMotion.AnimateTranslation(
            child,
            new Vector3(0, (float)offset, 0),
            Vector3.Zero,
            RepositionDuration,
            ThemeTransitionEasing);
    }

    private double CurrentRepositionOffset(Control child)
    {
        if (!_repositionAnimations.TryGetValue(child, out var animation))
        {
            return 0;
        }

        double progress = Math.Clamp(
            Stopwatch.GetElapsedTime(animation.Started).TotalMilliseconds / RepositionDuration.TotalMilliseconds,
            0,
            1);
        return animation.Offset * (1 - ThemeTransitionEasing.Ease(progress));
    }

    private void CompleteEntranceAnimation()
    {
        _entranceCompletion?.Dispose();
        _entranceCompletion = null;
        _isEntranceActive = false;
        CaptureChildPositions();
    }

    private void ResetMotionVisuals()
    {
        _entranceCompletion?.Dispose();
        _entranceCompletion = null;
        _isEntranceActive = false;
        WinUiCompositorMotion.SetOpacity(this, (float)Opacity);
        foreach (Control child in SettingsItemsPanel.Children)
        {
            WinUiCompositorMotion.SetTranslation(child, Vector3.Zero);
            WinUiCompositorMotion.SetOpacity(child, (float)child.Opacity);
        }

        _repositionAnimations.Clear();
    }

    private void CaptureChildPositions()
    {
        _lastChildPositions.Clear();
        foreach (Control child in SettingsItemsPanel.Children)
        {
            _lastChildPositions[child] = child.Bounds.Y;
        }
    }

    private void OnConverterUnitDisplaySelectionChanged(object? sender, RoutedEventArgs e)
    {
        if (_initializingConverterUnitDisplay ||
            sender is not RadioButton { IsChecked: true } selected ||
            !Enum.TryParse(selected.Tag?.ToString(), out ConverterUnitDisplayMode mode))
        {
            return;
        }

        _settingsStore.Update(settings =>
            settings with { ConverterUnitDisplayMode = mode });
    }

    private void OnAutomaticCurrencyRefreshChanged(object? sender, RoutedEventArgs e)
    {
        if (_initializingAutomaticCurrencyRefresh ||
            sender is not ToggleSwitch toggle)
        {
            return;
        }

        _settingsStore.Update(settings => settings with
        {
            AutomaticCurrencyRefresh = toggle.IsChecked == true
        });
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
