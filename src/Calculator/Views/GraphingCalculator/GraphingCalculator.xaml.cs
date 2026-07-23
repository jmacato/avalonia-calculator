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
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;
using GraphControl;
using AvaloniaColor = Avalonia.Media.Color;

namespace CalculatorApp;

public sealed partial class GraphingCalculator : UserControl, IDisposable
{
    private const double SmallStateWidth = 800;
    private GraphingCalculatorViewModel? _model;
    private EquationInputArea? _equationInputAreaControl;
    private KeyGraphFeaturesPanel? _keyGraphFeaturesControl;
    private CancellationTokenSource? _keyGraphFeaturesAnalysisCancellation;
    private GraphingSettings? _graphSettingsControl;
    private Flyout? _graphSettingsFlyout;
    private int _disposed;

    public GraphingCalculator()
    {
        InitializeComponent();
        GraphingControl.GraphViewChanged += OnGraphViewChanged;
        GraphingControl.GraphPlotted += OnGraphPlotted;
        GraphingControl.TracingValueChanged += OnTracingValueChanged;
        GraphingControl.VariablesUpdated += OnVariablesUpdated;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        UpdateGraphTheme();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
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
        DisposeKeyGraphFeaturesPanel();
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

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        DisposeKeyGraphFeaturesPanel();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

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

    private void OnEquationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeGraphEquations();
    }

    private void SynchronizeGraphEquations()
    {
        if (_model is not null)
        {
            GraphingControl.ReplaceEquations(_model.Equations.Select(equation => equation.GraphEquation));
        }
    }

    private void OnVariablesUpdated(object? sender, EventArgs e)
    {
        SynchronizeVariables();
    }

    private void SynchronizeVariables()
    {
        _model?.UpdateVariables(GraphingControl.Variables);
    }

    private void OnVariableUpdated(object? sender, VariableChangedEventArgs e)
    {
        GraphingControl.SetVariable(e.VariableName, e.NewValue);
    }

    private void OnZoomInClicked(object? sender, RoutedEventArgs e)
    {
        GraphingControl.ZoomFromCenter(0.9);
    }

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e)
    {
        GraphingControl.ZoomFromCenter(1.1);
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        GraphingControl.ResetGrid();
    }

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

    private void OnTracingValueChanged(object? sender, TracingValueChangedEventArgs e)
    {
        if (GraphingControl.ActiveTracing)
        {
            TraceValue.Text = string.Create(
                CultureInfo.CurrentCulture,
                $"x = {e.X:G8}, y = {e.Y:G8}");
        }
    }

    private void OnGraphViewChanged(object? sender, GraphViewChangedEventArgs e)
    {
        _graphSettingsControl?.Model.InitRanges();
        UpdateGraphAutomationName();
    }

    private void OnGraphPlotted(object? sender, EventArgs e)
    {
        UpdateGraphAutomationName();
    }

    private void OnGraphSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (_graphSettingsFlyout?.IsOpen == true)
        {
            _graphSettingsFlyout.Hide();
            return;
        }

        GraphingSettings settings = EnsureGraphSettings();
        settings.SetGrapher(GraphingControl);
        Flyout flyout = _graphSettingsFlyout ??= new Flyout
        {
            Content = settings
        };
        flyout.ShowAt(GraphSettingsButton);
    }

    private async void OnKeyGraphFeaturesRequested(object? sender, KeyGraphFeaturesRequestedEventArgs e)
    {
        _ = sender;
        EquationViewModel equation = e.Equation;
        DisposeKeyGraphFeaturesPanel();
        var cancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellation.Token;
        _keyGraphFeaturesAnalysisCancellation = cancellation;
        var viewModel = new KeyGraphFeaturesViewModel(equation);
        KeyGraphFeaturesPanel panel = CreateKeyGraphFeaturesPanel();
        panel.DataContext = viewModel;
        KeyGraphFeaturesHost.IsVisible = true;
        EquationInputHost.IsVisible = false;
        GraphingNumPadHost.IsVisible = false;

        try
        {
            KeyGraphFeaturesInfo info = await GraphingControl.AnalyzeEquationAsync(
                equation.GraphEquation,
                cancellationToken).ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested ||
                !ReferenceEquals(_keyGraphFeaturesControl, panel) ||
                !ReferenceEquals(panel.DataContext, viewModel))
            {
                return;
            }

            viewModel.ApplyAnalysis(info);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A closed or superseded panel must not receive a late result.
        }
        catch (InvalidOperationException)
        {
            ApplyAnalysisFailure(panel, viewModel, cancellationToken);
        }
        catch (NotSupportedException)
        {
            ApplyAnalysisFailure(panel, viewModel, cancellationToken);
        }
        finally
        {
            if (ReferenceEquals(_keyGraphFeaturesAnalysisCancellation, cancellation))
            {
                _keyGraphFeaturesAnalysisCancellation = null;
                cancellation.Dispose();
            }
        }
    }

    private void ApplyAnalysisFailure(
        KeyGraphFeaturesPanel panel,
        KeyGraphFeaturesViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested &&
            ReferenceEquals(_keyGraphFeaturesControl, panel) &&
            ReferenceEquals(panel.DataContext, viewModel))
        {
            viewModel.ApplyAnalysis(
                new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed));
        }
    }

    private void OnKeyGraphFeaturesClosed(object? sender, RoutedEventArgs e)
    {
        DisposeKeyGraphFeaturesPanel();
        EquationInputHost.IsVisible = true;
        GraphingNumPadHost.IsVisible = true;
        _equationInputAreaControl?.SetDefaultFocus();
    }

    private async void OnCopyGraphClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            ReadOnlyMemory<byte> png = await GraphingControl.GetGraphBitmapAsync().ConfigureAwait(true);
            if (png.IsEmpty || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
            {
                return;
            }

            using var pngStream = new MemoryStream(png.ToArray(), writable: false);
            using var bitmap = new Bitmap(pngStream);
            var item = new DataTransferItem();
            item.SetBitmap(bitmap);
            using var transfer = new DataTransfer();
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer).ConfigureAwait(true);
        }
        catch (UnauthorizedAccessException)
        {
            // Clipboard access can be denied in sandboxed browser contexts.
        }
        catch (InvalidOperationException)
        {
            // The platform clipboard can become unavailable during a focus transition.
        }
        catch (NotSupportedException)
        {
            // Some targets do not expose bitmap clipboard support.
        }
        catch (IOException)
        {
            // Bitmap encoding can fail without changing graph or editor state.
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
        string nextMode = AppResourceProvider.Instance.GetResourceString(
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
        inputArea.KeyGraphFeaturesRequested += OnKeyGraphFeaturesRequested;
        _equationInputAreaControl = inputArea;
        EquationInputHost.Child = inputArea;
        GraphingNumPadHost.Child = numPad;
        return inputArea;
    }

    private KeyGraphFeaturesPanel CreateKeyGraphFeaturesPanel()
    {
        // Defer construction of the math display and its layout resources until
        // analysis has actually been requested.
        var panel = new KeyGraphFeaturesPanel();
        panel.KeyGraphFeaturesClosed += OnKeyGraphFeaturesClosed;
        _keyGraphFeaturesControl = panel;
        KeyGraphFeaturesHost.Child = panel;
        return panel;
    }

    private void DisposeKeyGraphFeaturesPanel()
    {
        CancellationTokenSource? cancellation = _keyGraphFeaturesAnalysisCancellation;
        _keyGraphFeaturesAnalysisCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
        KeyGraphFeaturesHost.IsVisible = false;
        if (_keyGraphFeaturesControl is not { } panel)
        {
            KeyGraphFeaturesHost.Child = null;
            return;
        }

        panel.KeyGraphFeaturesClosed -= OnKeyGraphFeaturesClosed;
        KeyGraphFeaturesHost.Child = null;
        panel.Dispose();
        _keyGraphFeaturesControl = null;
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

    private void OnGraphThemeSettingChanged(object? sender, GraphThemeSettingChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateGraphTheme();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_model is not null)
        {
            _model.Equations.CollectionChanged -= OnEquationsCollectionChanged;
            _model.VariableUpdated -= OnVariableUpdated;
            _model = null;
        }

        DisposeKeyGraphFeaturesPanel();
        _keyGraphFeaturesControl?.Dispose();
        _keyGraphFeaturesControl = null;
        GraphingControl.GraphViewChanged -= OnGraphViewChanged;
        GraphingControl.GraphPlotted -= OnGraphPlotted;
        GraphingControl.TracingValueChanged -= OnTracingValueChanged;
        GraphingControl.VariablesUpdated -= OnVariablesUpdated;
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        GraphingControl.Dispose();

        if (_equationInputAreaControl is not null)
        {
            _equationInputAreaControl.KeyGraphFeaturesRequested -= OnKeyGraphFeaturesRequested;
            _equationInputAreaControl.DataContext = null;
            _equationInputAreaControl.Dispose();
            _equationInputAreaControl = null;
        }

        if (_graphSettingsControl is not null)
        {
            _graphSettingsControl.GraphThemeSettingChanged -= OnGraphThemeSettingChanged;
            _graphSettingsControl.Dispose();
            _graphSettingsControl = null;
        }

        if (_graphSettingsFlyout is not null)
        {
            _graphSettingsFlyout.Hide();
            _graphSettingsFlyout.Content = null;
            _graphSettingsFlyout = null;
        }

        DataContext = null;
        GC.SuppressFinalize(this);
    }
}
