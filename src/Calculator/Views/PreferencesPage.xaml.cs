// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel.Common.Automation;
using FluentAvalonia.Core;
using FluentAvalonia.Styling;
using GraphControl;

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
    private readonly AnimationFrameTimer _settingsMotionTimer;
    private readonly Dictionary<Control, (ITransform? Transform, RelativePoint Origin, double Opacity)> _savedChildVisuals = new();
    private readonly Dictionary<Control, TranslateTransform> _childTranslations = new();
    private readonly Dictionary<Control, double> _lastChildPositions = new();
    private readonly Dictionary<Control, (double Offset, long Started)> _repositionAnimations = new();
    private long _entranceStarted;
    private double _savedPageOpacity = 1;
    private bool _isEntranceActive;
    private bool _hasSavedPageOpacity;
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
        _settingsMotionTimer = new AnimationFrameTimer(OnSettingsMotionFrame);
        InitializeComponent();
        SettingsItemsPanel.LayoutUpdated += OnSettingsItemsLayoutUpdated;
    }

    public event EventHandler<RoutedEventArgs>? BackButtonClick;

    public void SetDefaultFocus() => AppThemeExpander.Focus();

    public void BeginOpenAnimation()
    {
        RestoreAllMotionVisuals();
        CaptureChildPositions();
        if (!FAUISettings.AreAnimationsEnabled())
        {
            return;
        }

        _savedPageOpacity = Opacity;
        _hasSavedPageOpacity = true;
        Opacity = 0;
        foreach (Control child in SettingsItemsPanel.Children)
        {
            SaveChildVisual(child);
            TranslateTransform translation = CreateChildTranslation(child);
            translation.Y = SettingsEntranceOffset;
            child.Opacity = 0;
        }

        _entranceStarted = Stopwatch.GetTimestamp();
        _isEntranceActive = true;
        if (!_settingsMotionTimer.Start(this))
        {
            RestoreAllMotionVisuals();
        }
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
        _settingsMotionTimer.Detach();
        RestoreAllMotionVisuals();
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
            RestoreChildVisual(child);
            return;
        }

        SaveChildVisual(child);
        TranslateTransform translation = _childTranslations.TryGetValue(child, out TranslateTransform? existing)
            ? existing
            : CreateChildTranslation(child);
        double offset = translation.Y + layoutOffset;
        translation.Y = offset;
        _repositionAnimations[child] = (offset, Stopwatch.GetTimestamp());
        if (!_settingsMotionTimer.Start(this))
        {
            RestoreChildVisual(child);
        }
    }

    private void OnSettingsMotionFrame(TimeSpan timestamp)
    {
        _ = timestamp;
        bool hasActiveAnimation = UpdateEntranceAnimation();
        foreach ((Control child, (double offset, long started)) in _repositionAnimations.ToArray())
        {
            double progress = Math.Clamp(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds / RepositionDuration.TotalMilliseconds,
                0,
                1);
            if (_childTranslations.TryGetValue(child, out TranslateTransform? translation))
            {
                translation.Y = offset * (1 - ThemeTransitionEasing.Ease(progress));
            }

            if (progress >= 1)
            {
                _repositionAnimations.Remove(child);
                RestoreChildVisual(child);
            }
            else
            {
                hasActiveAnimation = true;
            }
        }

        if (!hasActiveAnimation)
        {
            _settingsMotionTimer.Stop();
        }
    }

    private bool UpdateEntranceAnimation()
    {
        if (!_isEntranceActive)
        {
            return false;
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(_entranceStarted);
        double pageProgress = Math.Clamp(elapsed.TotalMilliseconds / EntranceDuration.TotalMilliseconds, 0, 1);
        Opacity = ThemeTransitionEasing.Ease(pageProgress);

        bool childrenComplete = true;
        int index = 0;
        foreach (Control child in SettingsItemsPanel.Children)
        {
            double delay = Math.Min(
                EntranceStaggerDelay.TotalMilliseconds * index,
                EntranceStaggerCap.TotalMilliseconds);
            double progress = Math.Clamp(
                (elapsed.TotalMilliseconds - delay) / EntranceDuration.TotalMilliseconds,
                0,
                1);
            double eased = ThemeTransitionEasing.Ease(progress);
            if (_childTranslations.TryGetValue(child, out TranslateTransform? translation))
            {
                translation.Y = SettingsEntranceOffset * (1 - eased);
            }

            child.Opacity = eased;
            childrenComplete &= progress >= 1;
            index++;
        }

        if (pageProgress < 1 || !childrenComplete)
        {
            return true;
        }

        _isEntranceActive = false;
        RestorePageOpacity();
        foreach (Control child in SettingsItemsPanel.Children.ToArray())
        {
            RestoreChildVisual(child);
        }

        CaptureChildPositions();
        return _repositionAnimations.Count > 0;
    }

    private void SaveChildVisual(Control child)
    {
        if (!_savedChildVisuals.ContainsKey(child))
        {
            _savedChildVisuals[child] = (child.RenderTransform, child.RenderTransformOrigin, child.Opacity);
        }
    }

    private TranslateTransform CreateChildTranslation(Control child)
    {
        var translation = new TranslateTransform();
        var transformGroup = new TransformGroup();
        if (_savedChildVisuals[child].Transform is { } existing)
        {
            transformGroup.Children.Add(existing as Transform ?? new MatrixTransform(existing.Value));
        }

        transformGroup.Children.Add(translation);
        child.SetCurrentValue(RenderTransformProperty, transformGroup);
        _childTranslations[child] = translation;
        return translation;
    }

    private void RestoreChildVisual(Control child)
    {
        _repositionAnimations.Remove(child);
        _childTranslations.Remove(child);
        if (!_savedChildVisuals.Remove(child, out var saved))
        {
            return;
        }

        child.SetCurrentValue(RenderTransformProperty, saved.Transform);
        child.SetCurrentValue(RenderTransformOriginProperty, saved.Origin);
        child.Opacity = saved.Opacity;
    }

    private void RestoreAllMotionVisuals()
    {
        _settingsMotionTimer.Stop();
        _isEntranceActive = false;
        RestorePageOpacity();
        foreach (Control child in _savedChildVisuals.Keys.ToArray())
        {
            RestoreChildVisual(child);
        }

        _repositionAnimations.Clear();
        _childTranslations.Clear();
    }

    private void RestorePageOpacity()
    {
        if (!_hasSavedPageOpacity)
        {
            return;
        }

        Opacity = _savedPageOpacity;
        _hasSavedPageOpacity = false;
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
