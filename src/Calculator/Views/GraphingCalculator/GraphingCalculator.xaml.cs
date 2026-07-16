// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;
using GraphControl;
using AvaloniaColor = Avalonia.Media.Color;

namespace CalculatorApp;

public sealed partial class GraphingCalculator : UserControl
{
    private const double SmallStateWidth = 800;
    private GraphingCalculatorViewModel? _model;
    private EquationInputArea? _equationInputAreaControl;
    private KeyGraphFeaturesPanel? _keyGraphFeaturesControl;
    private GraphingSettings? _graphSettingsControl;
    private Flyout? _graphSettingsFlyout;

    public GraphingCalculator()
    {
        InitializeComponent();
        GraphingControl.GraphViewChanged += OnGraphViewChanged;
        GraphingControl.GraphPlotted += OnGraphPlotted;
        GraphingControl.TracingValueChanged += OnTracingValueChanged;
        GraphingControl.VariablesUpdated += OnVariablesUpdated;
        ActualThemeVariantChanged += (_, _) => UpdateGraphTheme();
        UpdateGraphTheme();
    }

    public void SetDefaultFocus()
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        bool graphOnly = Bounds.Width < SmallStateWidth && SwitchModeToggleButton.IsChecked != true;
        if (graphOnly)
        {
            GraphingControl.Focus();
            return;
        }

        EnsureEquationUi().SetDefaultFocus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_model is not null)
        {
            _model.Equations.CollectionChanged -= OnEquationsCollectionChanged;
            _model.VariableUpdated -= OnVariableUpdated;
        }

        base.OnDataContextChanged(e);
        _model = DataContext as GraphingCalculatorViewModel;
        if (_model is not null)
        {
            _model.Equations.CollectionChanged += OnEquationsCollectionChanged;
            _model.VariableUpdated += OnVariableUpdated;
            SynchronizeGraphEquations();
            SynchronizeVariables();
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        SynchronizeGraphEquations();
        SynchronizeVariables();
        SetDefaultFocus();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    private void OnSwitchModeChanged(object? sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        UpdateSwitchModeAccessibility();
        if (SwitchModeToggleButton.IsChecked == true)
        {
            EnsureEquationUi().SetDefaultFocus();
        }
        else
        {
            GraphingControl.Focus();
        }
    }

    private void OnEquationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SynchronizeGraphEquations();

    private void SynchronizeGraphEquations()
    {
        if (_model is not null)
        {
            GraphingControl.ReplaceEquations(_model.Equations.Select(equation => equation.GraphEquation));
        }
    }

    private void OnVariablesUpdated(object? sender, EventArgs e) => SynchronizeVariables();

    private void SynchronizeVariables() => _model?.UpdateVariables(GraphingControl.Variables);

    private void OnVariableUpdated(object? sender, VariableChangedEventArgs e) =>
        GraphingControl.SetVariable(e.VariableName, e.NewValue);

    private void OnZoomInClicked(object? sender, RoutedEventArgs e) => GraphingControl.ZoomFromCenter(0.9);

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e) => GraphingControl.ZoomFromCenter(1.1);

    private void OnResetClicked(object? sender, RoutedEventArgs e) => GraphingControl.ResetGrid();

    private void OnActiveTracingChanged(object? sender, RoutedEventArgs e)
    {
        bool tracing = ActiveTracingButton.IsChecked == true;
        GraphingControl.ActiveTracing = tracing;
        TraceValuePopup.IsVisible = tracing;
        if (!tracing)
        {
            TraceValue.Text = string.Empty;
        }
    }

    private void OnTracingValueChanged(double x, double y)
    {
        if (GraphingControl.ActiveTracing)
        {
            TraceValue.Text = string.Create(
                CultureInfo.CurrentCulture,
                $"x = {x:G8}, y = {y:G8}");
        }
    }

    private void OnGraphViewChanged(object? sender, GraphViewChangedReason reason)
    {
        _graphSettingsControl?.Model.InitRanges();
        UpdateGraphAutomationName();
    }

    private void OnGraphPlotted(object? sender, EventArgs e) => UpdateGraphAutomationName();

    private void OnGraphSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (_graphSettingsFlyout?.IsOpen == true)
        {
            _graphSettingsFlyout.Hide();
            return;
        }

        GraphingSettings settings = EnsureGraphSettings();
        settings.SetGrapher(GraphingControl);
        (_graphSettingsFlyout ??= new Flyout { Content = settings }).ShowAt(GraphSettingsButton);
    }

    private void OnEquationFormatRequested(object? sender, MathRichEditBoxFormatRequest e)
    {
        string linear = GraphingControl.ConvertToLinear(e.OriginalText);
        if (!string.IsNullOrEmpty(linear))
        {
            e.FormattedText = GraphingControl.FormatMathML(linear);
        }
    }

    private void OnKeyGraphFeaturesRequested(object? sender, EquationViewModel equation)
    {
        KeyGraphFeaturesInfo? info = GraphingControl.AnalyzeEquation(equation.GraphEquation);
        if (info is null)
        {
            return;
        }

        equation.PopulateKeyGraphFeatures(info);
        KeyGraphFeaturesPanel panel = EnsureKeyGraphFeaturesPanel();
        panel.DataContext = equation;
        KeyGraphFeaturesHost.IsVisible = true;
        EquationInputHost.IsVisible = false;
        GraphingNumPadHost.IsVisible = false;
    }

    private void OnKeyGraphFeaturesClosed(object? sender, RoutedEventArgs e)
    {
        KeyGraphFeaturesHost.IsVisible = false;
        if (_keyGraphFeaturesControl is not null)
        {
            // Keep the lightweight panel shell for reuse, but release the
            // equation, generated item containers and math layouts while the
            // analysis view is closed.
            _keyGraphFeaturesControl.DataContext = null;
        }

        EquationInputHost.IsVisible = true;
        GraphingNumPadHost.IsVisible = true;
        _equationInputAreaControl?.SetDefaultFocus();
    }

    private async void OnCopyGraphClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ReadOnlyMemory<byte> png = await GraphingControl.GetGraphBitmapAsync();
            if (png.IsEmpty || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                return;
            }

            var bitmap = new Bitmap(new MemoryStream(png.ToArray(), writable: false));
            var item = new DataTransferItem();
            item.SetBitmap(bitmap);
            var transfer = new DataTransfer();
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer);
        }
        catch (Exception)
        {
            // Clipboard access can be denied in sandboxed browser contexts; the
            // graph and editor state remain unchanged in that case.
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        bool small = Bounds.Width < SmallStateWidth;
        bool equationMode = SwitchModeToggleButton.IsChecked == true;
        if (!small || equationMode)
        {
            EnsureEquationUi();
        }

        SwitchModeToggleButton.IsVisible = small;
        Grid.SetColumn(LeftGrid, 0);
        Grid.SetColumnSpan(LeftGrid, small ? 2 : 1);
        Grid.SetColumn(RightGrid, small ? 0 : 1);
        Grid.SetColumnSpan(RightGrid, small ? 2 : 1);
        LeftGrid.IsVisible = !small || !equationMode;
        RightGrid.IsVisible = !small || equationMode;
        UpdateSwitchModeAccessibility();
    }

    private void UpdateSwitchModeAccessibility()
    {
        bool equationMode = SwitchModeToggleButton.IsChecked == true;
        string nextMode = AppResourceProvider.GetInstance().GetResourceString(
            equationMode ? "GraphSwitchToGraphMode" : "GraphSwitchToEquationMode");
        AutomationProperties.SetName(SwitchModeToggleButton, nextMode);
        ToolTip.SetTip(SwitchModeToggleButton, nextMode);
    }

    private void UpdateGraphAutomationName()
    {
        GraphingControl.GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax);
        AutomationProperties.SetHelpText(
            GraphingControl,
            string.Create(
                CultureInfo.CurrentCulture,
                $"Graph range x {xMin:G5} to {xMax:G5}, y {yMin:G5} to {yMax:G5}"));
    }

    private void UpdateGraphTheme()
    {
        bool matchAppTheme = _graphSettingsControl?.IsMatchAppTheme ?? App.SettingsStore.Current.GraphThemeMatchApp;
        if (matchAppTheme && IsAppThemeDark())
        {
            GraphingControl.GraphBackground = AvaloniaColor.FromRgb(0x20, 0x20, 0x20);
            GraphingControl.AxesColor = AvaloniaColor.FromRgb(0xE0, 0xE0, 0xE0);
            GraphingControl.GridLinesColor = AvaloniaColor.FromRgb(0x48, 0x48, 0x48);
        }
        else
        {
            GraphingControl.GraphBackground = AvaloniaColor.FromRgb(0xFF, 0xFF, 0xFF);
            GraphingControl.AxesColor = AvaloniaColor.FromRgb(0x00, 0x00, 0x00);
            GraphingControl.GridLinesColor = AvaloniaColor.FromRgb(0xC6, 0xC6, 0xC6);
        }
    }

    private bool IsAppThemeDark()
    {
        ThemeVariant requestedTheme = Application.Current?.RequestedThemeVariant ?? ThemeVariant.Default;
        if (requestedTheme == ThemeVariant.Dark)
        {
            return true;
        }

        if (requestedTheme == ThemeVariant.Light)
        {
            return false;
        }

        if (ActualThemeVariant == ThemeVariant.Dark)
        {
            return true;
        }

        return Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
    }

    private EquationInputArea EnsureEquationUi()
    {
        if (_equationInputAreaControl is not null)
        {
            return _equationInputAreaControl;
        }

        // Keep these direct constructors: they are both linker-visible for NativeAOT
        // and avoid loading the sizeable editor/numpad XAML while narrow graph mode
        // has the entire right pane hidden.
        var inputArea = new EquationInputArea();
        var numPad = new GraphingNumPad();
        inputArea.EquationFormatRequested += OnEquationFormatRequested;
        inputArea.KeyGraphFeaturesRequested += OnKeyGraphFeaturesRequested;
        _equationInputAreaControl = inputArea;
        EquationInputHost.Child = inputArea;
        GraphingNumPadHost.Child = numPad;
        return inputArea;
    }

    private KeyGraphFeaturesPanel EnsureKeyGraphFeaturesPanel()
    {
        if (_keyGraphFeaturesControl is not null)
        {
            return _keyGraphFeaturesControl;
        }

        // This view owns MathExpressionView instances and the math font. Do not
        // construct either until analysis has actually been requested.
        var panel = new KeyGraphFeaturesPanel();
        panel.KeyGraphFeaturesClosed += OnKeyGraphFeaturesClosed;
        _keyGraphFeaturesControl = panel;
        KeyGraphFeaturesHost.Child = panel;
        return panel;
    }

    private GraphingSettings EnsureGraphSettings()
    {
        if (_graphSettingsControl is not null)
        {
            return _graphSettingsControl;
        }

        var settings = new GraphingSettings();
        settings.GraphThemeSettingChanged += OnGraphThemeSettingChanged;
        _graphSettingsControl = settings;
        return settings;
    }

    private void OnGraphThemeSettingChanged(bool _) => UpdateGraphTheme();
}
