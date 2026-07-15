// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;
using FluentAvalonia.Core;

namespace CalculatorApp;

public sealed partial class UnitConverter : UserControl
{
    private static readonly AttachedProperty<double> ConverterScaleProperty =
        AvaloniaProperty.RegisterAttached<UnitConverter, Grid, double>("ConverterScale", 1d);

    private UnitConverterViewModel? _subscribedModel;
    private CalculationResult? _contextMenuTarget;
    private DispatcherTimer? _currencyLoadingDelayTimer;
    private bool _isUnitLoaded = true;
    private readonly FlowDirection _layoutDirection;
    private readonly HorizontalAlignment _flowDirectionHorizontalAlignment;

    static UnitConverter()
    {
        ConverterScaleProperty.Changed.AddClassHandler<Grid>(static (grid, args) =>
        {
            if (grid.RenderTransform is ScaleTransform transform
                && args.NewValue is double scale)
            {
                transform.ScaleX = scale;
                transform.ScaleY = scale;
            }
        });
    }

    public UnitConverter()
    {
        _layoutDirection = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        _flowDirectionHorizontalAlignment = _layoutDirection == FlowDirection.RightToLeft
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;

        InitializeComponent();
        ApplyFlowDirection();
        InitializeOfflineStatus();
    }

    public UnitConverterViewModel? Model => DataContext as UnitConverterViewModel;

    public void AnimateConverter()
    {
        if (!FAUISettings.AreAnimationsEnabled())
        {
            return;
        }

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(367),
            Easing = new WinUiExponentialEaseOut(5),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(ConverterScaleProperty, 0.92)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(ConverterScaleProperty, 1d)
                    }
                }
            }
        };

        _ = animation.RunAsync(ConverterNumPad);
    }

    public void SetDefaultFocus()
    {
        Control[] focusPrecedence =
        [
            Value1,
            CurrencyRefreshBlock,
            OfflineNetworkSettingsButton,
            ClearEntryButtonPos0
        ];

        foreach (Control control in focusPrecedence)
        {
            if (control.Focus())
            {
                break;
            }
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SubscribeToModel();
        ApplyResponsiveLayout();
        SetDefaultFocus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SubscribeToModel();
        ApplyResponsiveLayout();
    }

    private void SubscribeToModel()
    {
        if (ReferenceEquals(_subscribedModel, Model))
        {
            return;
        }

        if (_subscribedModel is { } oldModel)
        {
            oldModel.PropertyChanged -= OnModelPropertyChanged;
        }

        _subscribedModel = Model;
        if (_subscribedModel is { } model)
        {
            model.PropertyChanged += OnModelPropertyChanged;
            UpdateActiveValueState();
            UpdateDisplayUnitLayout();
        }

        UpdateCurrencyLoadingState();
        UpdateCurrencyNetworkState();
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UnitConverterViewModel.Value1Active)
            or nameof(UnitConverterViewModel.Value2Active))
        {
            UpdateActiveValueState();
        }

        if (e.PropertyName is nameof(UnitConverterViewModel.DisplayUnit1OnRight)
            or nameof(UnitConverterViewModel.DisplayUnit2OnRight)
            or nameof(UnitConverterViewModel.DisplayUnit1UseSpace)
            or nameof(UnitConverterViewModel.DisplayUnit2UseSpace))
        {
            UpdateDisplayUnitLayout();
        }

        if (e.PropertyName is nameof(UnitConverterViewModel.IsCurrencyLoadingVisible)
            or nameof(UnitConverterViewModel.IsCurrencyCurrentCategory))
        {
            UpdateCurrencyLoadingState();
        }

        if (e.PropertyName is nameof(UnitConverterViewModel.NetworkBehavior)
            or nameof(UnitConverterViewModel.CurrencyDataLoadFailed))
        {
            UpdateCurrencyNetworkState();
        }

        if (e.PropertyName == nameof(UnitConverterViewModel.CurrencyDataIsWeekOld))
        {
            UpdateCurrencyTimestampState();
        }

        if (e.PropertyName == nameof(UnitConverterViewModel.IsDropDownEnabled)
            && Model?.IsDropDownEnabled == true)
        {
            SetDefaultFocus();
        }
    }

    private void UpdateActiveValueState()
    {
        Value1.UpdateTextState();
        Value2.UpdateTextState();
    }

    private void UpdateCurrencyTimestampState()
    {
        if (Model?.CurrencyDataIsWeekOld == true)
        {
            CurrencyTimestampTextBlock.Classes.Remove("fresh");
        }
        else if (!CurrencyTimestampTextBlock.Classes.Contains("fresh"))
        {
            CurrencyTimestampTextBlock.Classes.Add("fresh");
        }
    }

    private void ApplyFlowDirection()
    {
        Value1.FlowDirection = _layoutDirection;
        Value2.FlowDirection = _layoutDirection;
        Units1.FlowDirection = _layoutDirection;
        Units2.FlowDirection = _layoutDirection;
        SupplementaryResultsPanelInGrid.FlowDirection = _layoutDirection;

        Value1Container.HorizontalAlignment = _flowDirectionHorizontalAlignment;
        Value2Container.HorizontalAlignment = _flowDirectionHorizontalAlignment;
        Units1.HorizontalAlignment = _flowDirectionHorizontalAlignment;
        Units2.HorizontalAlignment = _flowDirectionHorizontalAlignment;
        SupplementaryResultsPanelInGrid.HorizontalAlignment = _flowDirectionHorizontalAlignment;
    }

    private void UpdateDisplayUnitLayout()
    {
        if (Model is not { } model)
        {
            return;
        }

        ApplyDisplayUnitLayout(
            CurrencySymbol1Block,
            model.DisplayUnit1OnRight,
            model.DisplayUnit1UseSpace);
        ApplyDisplayUnitLayout(
            CurrencySymbol2Block,
            model.DisplayUnit2OnRight,
            model.DisplayUnit2UseSpace);
    }

    private static void ApplyDisplayUnitLayout(
        TextBlock displayUnit,
        bool onRight,
        bool useSpace)
    {
        Grid.SetColumn(displayUnit, onRight ? 2 : 0);
        double innerSpace = useSpace ? 8 : 0;
        displayUnit.Padding = onRight
            ? new Thickness(innerSpace, 0, 12, 0)
            : new Thickness(16, 0, innerSpace, 0);
    }

    private void UpdateCurrencyLoadingState()
    {
        bool isCurrency = Model?.IsCurrencyCurrentCategory == true;
        bool isLoading = isCurrency && Model?.IsCurrencyLoadingVisible == true;
        bool isLoaded = !isCurrency
            || (!isLoading && !string.IsNullOrEmpty(Model?.CurrencyTimestamp));
        bool animateLoadedState = isCurrency && !_isUnitLoaded && isLoaded;
        _isUnitLoaded = isLoaded;

        ApplyUnitLoadedState(isLoaded, animateLoadedState);

        if (isLoading)
        {
            StartProgressRingWithDelay();
        }
        else
        {
            HideProgressRing();
        }
    }

    private void ApplyUnitLoadedState(bool isLoaded, bool animateLoadedState)
    {
        CurrencyLoadingGrid.IsVisible = !isLoaded;
        Value1Container.IsVisible = isLoaded;
        Units1.IsVisible = isLoaded;
        Value2Container.IsVisible = isLoaded;
        Units2.IsVisible = isLoaded;
        NumberPad.IsEnabled = isLoaded;
        ClearEntryButtonPos0.IsEnabled = isLoaded;
        BackSpaceButtonSmall.IsEnabled = isLoaded;

        bool shouldAnimate = isLoaded
            && animateLoadedState
            && FAUISettings.AreAnimationsEnabled();
        foreach (Visual target in GetCurrencyLoadedAnimationTargets())
        {
            target.Transitions = shouldAnimate
                ? new Transitions
                {
                    new DoubleTransition
                    {
                        Property = Visual.OpacityProperty,
                        Duration = TimeSpan.FromSeconds(1)
                    }
                }
                : null;
            target.Opacity = isLoaded ? 1 : 0;
        }
    }

    private Visual[] GetCurrencyLoadedAnimationTargets() =>
    [
        CurrencyRatioEqualityBlock,
        CurrencyTimestampTextBlock,
        Units1,
        Value1Container,
        Units2,
        Value2Container
    ];

    private void StartProgressRingWithDelay()
    {
        HideProgressRing();

        _currencyLoadingDelayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _currencyLoadingDelayTimer.Tick += OnCurrencyLoadingDelayElapsed;
        _currencyLoadingDelayTimer.Start();
    }

    private void OnCurrencyLoadingDelayElapsed(object? sender, EventArgs e)
    {
        if (sender is DispatcherTimer timer)
        {
            timer.Stop();
            timer.Tick -= OnCurrencyLoadingDelayElapsed;
        }

        _currencyLoadingDelayTimer = null;
        CurrencyLoadingProgressRing.IsActive =
            Model is { IsCurrencyCurrentCategory: true, IsCurrencyLoadingVisible: true };
    }

    private void HideProgressRing()
    {
        if (_currencyLoadingDelayTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnCurrencyLoadingDelayElapsed;
            _currencyLoadingDelayTimer = null;
        }

        CurrencyLoadingProgressRing.IsActive = false;
    }

    private void InitializeOfflineStatus()
    {
        const string delimiter = "%HL%";
        string status = AppResourceProvider.GetInstance()
            .GetResourceString("OfflineStatusHyperlinkText");
        int firstDelimiter = status.IndexOf(delimiter, StringComparison.Ordinal);
        int secondDelimiter = firstDelimiter < 0
            ? -1
            : status.IndexOf(
                delimiter,
                firstDelimiter + delimiter.Length,
                StringComparison.Ordinal);

        if (firstDelimiter >= 0 && secondDelimiter >= 0)
        {
            OfflineRunBeforeLink.Text = status[..firstDelimiter];
            OfflineRunLink.Text = status[
                (firstDelimiter + delimiter.Length)..secondDelimiter];
            OfflineRunAfterLink.Text = status[(secondDelimiter + delimiter.Length)..];
        }
        else
        {
            OfflineRunBeforeLink.Text = status.Replace(delimiter, string.Empty, StringComparison.Ordinal);
            OfflineRunLink.Text = string.Empty;
            OfflineRunAfterLink.Text = string.Empty;
        }

        AutomationProperties.SetName(
            OfflineBlock,
            string.Join(
                ' ',
                OfflineRunBeforeLink.Text,
                OfflineRunLink.Text,
                OfflineRunAfterLink.Text));
        AutomationProperties.SetName(
            OfflineNetworkSettingsButton,
            OfflineRunLink.Text);

        OfflineNetworkSettingsButton.NavigateUri = OperatingSystem.IsWindows()
            ? new Uri("ms-settings:network-status")
            : OperatingSystem.IsMacOS()
                ? new Uri("x-apple.systempreferences:com.apple.Network-Settings.extension")
                : null;
        OfflineNetworkSettingsButton.IsEnabled = OfflineNetworkSettingsButton.NavigateUri is not null;
    }

    private void UpdateCurrencyNetworkState()
    {
        if (Model is not { } model)
        {
            CurrencyRefreshBlockControl.IsVisible = false;
            OfflineBlock.IsVisible = false;
            CurrencySecondaryStatus.Text = string.Empty;
            return;
        }

        bool isOffline = model.NetworkBehavior == NetworkAccessBehavior.Offline;
        CurrencyRefreshBlockControl.IsVisible = !isOffline;
        OfflineBlock.IsVisible = isOffline;

        CurrencySecondaryStatus.Text = model.NetworkBehavior switch
        {
            NetworkAccessBehavior.Normal when model.CurrencyDataLoadFailed =>
                AppResourceProvider.GetInstance().GetResourceString("FailedToRefresh"),
            NetworkAccessBehavior.OptIn when model.CurrencyDataLoadFailed =>
                AppResourceProvider.GetInstance().GetResourceString("FailedToRefresh"),
            NetworkAccessBehavior.OptIn =>
                AppResourceProvider.GetInstance().GetResourceString("DataChargesMayApply"),
            _ => string.Empty
        };
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    /// <summary>
    /// Direct equivalent of UnitConverter.xaml's AspectRatioTrigger and sizing
    /// VisualStates. The aspect trigger observes this control, while WinUI's
    /// AdaptiveTriggers observe the top-level window.
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        bool landscape = Bounds.Width >= Bounds.Height;
        var columns = UnitConverterRootGrid.ColumnDefinitions;
        var rows = UnitConverterRootGrid.RowDefinitions;

        columns[0].Width = new GridLength(0);
        columns[1].Width = new GridLength(1, GridUnitType.Star);
        columns[2].Width = landscape
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        columns[3].Width = new GridLength(0);

        if (landscape)
        {
            rows[1].Height = new GridLength(4, GridUnitType.Star);
            rows[2].Height = new GridLength(2, GridUnitType.Star);
            rows[3].Height = new GridLength(4, GridUnitType.Star);
            rows[4].Height = new GridLength(2, GridUnitType.Star);
            rows[5].Height = new GridLength(2, GridUnitType.Star);
            rows[6].MinHeight = 0;
            rows[6].Height = new GridLength(0);

            Grid.SetRow(ConverterNumPad, 1);
            Grid.SetRowSpan(ConverterNumPad, 5);
            Grid.SetColumn(ConverterNumPad, 2);
            Grid.SetColumnSpan(ConverterNumPad, 2);
            Grid.SetColumnSpan(CurrencyLoadingGrid, 2);
            SupplementaryResults.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        }
        else
        {
            rows[1].Height = new GridLength(56, GridUnitType.Star);
            rows[2].Height = new GridLength(32, GridUnitType.Star);
            rows[3].Height = new GridLength(56, GridUnitType.Star);
            rows[4].Height = new GridLength(32, GridUnitType.Star);
            rows[5].Height = GridLength.Auto;
            rows[6].MinHeight = 0;
            rows[6].Height = new GridLength(272, GridUnitType.Star);

            Grid.SetRow(ConverterNumPad, 6);
            Grid.SetRowSpan(ConverterNumPad, 1);
            Grid.SetColumn(ConverterNumPad, 1);
            Grid.SetColumnSpan(ConverterNumPad, 1);
            Grid.SetColumnSpan(CurrencyLoadingGrid, 4);
            SupplementaryResults.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }

        Size adaptiveTriggerSize = TopLevel.GetTopLevel(this)?.ClientSize ?? Bounds.Size;
        bool wide = adaptiveTriggerSize.Width >= 640;
        bool extraWide = adaptiveTriggerSize.Width >= 1280
            && adaptiveTriggerSize.Height >= 768;
        double currencyFontSize = wide ? 32 : 20;
        double unitHeight = wide ? 44 : 32;
        double commandFontSize = extraWide ? 24 : wide ? 20 : 14;
        double numberFontSize = extraWide ? 46 : wide ? 28 : 18;

        Value1.MaxFontSize = wide ? 46 : 40;
        Value2.MaxFontSize = wide ? 46 : 40;
        Value1.DisplayMargin = wide ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 4);
        Value2.DisplayMargin = wide ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 4);
        CurrencySymbol1Block.FontSize = currencyFontSize;
        CurrencySymbol2Block.FontSize = currencyFontSize;
        CurrencySymbol1Block.Margin = wide ? new Thickness(0, 0, 0, 17) : new Thickness(0, 0, 0, 8);
        CurrencySymbol2Block.Margin = wide ? new Thickness(0, 0, 0, 17) : new Thickness(0, 0, 0, 8);
        Units1.Height = unitHeight;
        Units2.Height = unitHeight;
        ClearEntryButtonPos0.FontSize = commandFontSize;
        BackSpaceButtonSmall.FontSize = commandFontSize;
        ConverterNegateButton.FontSize = extraWide ? 24 : wide ? 20 : 16;
        NumberPad.SetButtonFontSize(numberFontSize);
    }

    private void OnSupplementaryResultsPanelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Retained from the original WinUI handler. The epsilon prevents a
        // SizeChanged feedback loop caused by floating-point layout rounding.
        UnitConverterRootGrid.RowDefinitions[5].MinHeight = Math.Max(48, e.NewSize.Height + 0.01);
    }

    private void OnValueSelected(object sender)
    {
        if (sender is CalculationResult value)
        {
            value.UpdateTextState();
            value.IsActive = true;
        }
    }

    private async void OnResultContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not CalculationResult target)
        {
            return;
        }

        _contextMenuTarget = target;
        OnValueSelected(target);

        foreach (MenuItem item in target.ContextMenu?.Items.OfType<MenuItem>() ?? [])
        {
            if (item.Classes.Contains("paste"))
            {
                item.IsEnabled = false;
                item.IsEnabled = await CopyPasteManager.HasStringToPasteAsync();
            }
        }
    }

    private void OnResultContextMenuClosed(object? sender, RoutedEventArgs e) =>
        _contextMenuTarget = null;

    private void OnCopyMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (_contextMenuTarget is { } target)
        {
            CopyPasteManager.CopyToClipboard(target.GetRawDisplayValue());
        }
    }

    private async void OnPasteMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }

        string pastedString = await CopyPasteManager.GetStringToPaste(
            model.Mode,
            CategoryGroupType.Converter,
            NumberBase.Unknown,
            BitLength.BitLengthUnknown);
        model.OnPaste(pastedString);
    }

    private void CurrencyRefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        if (Model is { IsCurrencyLoadingVisible: false } model)
        {
            model.RefreshCurrencyCommand.Execute(null);
        }
    }

    private void UpdateDropDownState(object? sender, EventArgs e)
    {
        if (Model is { } model)
        {
            model.IsDropDownOpen = Units1.IsDropDownOpen || Units2.IsDropDownOpen;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }

        bool commandModifier = OperatingSystem.IsMacOS()
            ? e.KeyModifiers.HasFlag(KeyModifiers.Meta)
            : e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (OperatingSystem.IsBrowser())
        {
            commandModifier = e.KeyModifiers.HasFlag(KeyModifiers.Meta)
                || e.KeyModifiers.HasFlag(KeyModifiers.Control);
        }

        bool alternateCopy = !OperatingSystem.IsMacOS()
            && e.Key == Key.Insert
            && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool alternatePaste = !OperatingSystem.IsMacOS()
            && e.Key == Key.Insert
            && e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if ((commandModifier && e.Key == Key.C) || alternateCopy)
        {
            model.CopyCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if ((commandModifier && e.Key == Key.V) || alternatePaste)
        {
            model.PasteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (model.IsDropDownOpen)
        {
            return;
        }

        NumbersAndOperatorsEnum operation = e.Key switch
        {
            Key.D0 or Key.NumPad0 => NumbersAndOperatorsEnum.Zero,
            Key.D1 or Key.NumPad1 => NumbersAndOperatorsEnum.One,
            Key.D2 or Key.NumPad2 => NumbersAndOperatorsEnum.Two,
            Key.D3 or Key.NumPad3 => NumbersAndOperatorsEnum.Three,
            Key.D4 or Key.NumPad4 => NumbersAndOperatorsEnum.Four,
            Key.D5 or Key.NumPad5 => NumbersAndOperatorsEnum.Five,
            Key.D6 or Key.NumPad6 => NumbersAndOperatorsEnum.Six,
            Key.D7 or Key.NumPad7 => NumbersAndOperatorsEnum.Seven,
            Key.D8 or Key.NumPad8 => NumbersAndOperatorsEnum.Eight,
            Key.D9 or Key.NumPad9 => NumbersAndOperatorsEnum.Nine,
            Key.Decimal or Key.OemPeriod or Key.OemComma => NumbersAndOperatorsEnum.Decimal,
            Key.Back => NumbersAndOperatorsEnum.Backspace,
            Key.Delete or Key.Escape => NumbersAndOperatorsEnum.Clear,
            Key.Subtract or Key.OemMinus => NumbersAndOperatorsEnum.Negate,
            _ => NumbersAndOperatorsEnum.None
        };

        if (operation != NumbersAndOperatorsEnum.None)
        {
            model.ButtonPressed.Execute(operation);
            e.Handled = true;
        }
    }

    private sealed class WinUiExponentialEaseOut(double exponent) : Easing
    {
        public override double Ease(double progress)
        {
            if (Math.Abs(exponent) <= double.Epsilon)
            {
                return progress;
            }

            double inverseProgress = 1 - progress;
            double easeIn = (Math.Exp(exponent * inverseProgress) - 1)
                / (Math.Exp(exponent) - 1);
            return 1 - easeIn;
        }
    }
}
