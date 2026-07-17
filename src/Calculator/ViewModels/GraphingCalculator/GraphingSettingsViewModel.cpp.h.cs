// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using GraphControl;
using Graphing;

namespace CalculatorApp.ViewModel;

public sealed partial class GraphingSettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _settingsStore;
    private Grapher? _graph;
    private string _xMin = string.Empty;
    private string _xMax = string.Empty;
    private string _yMin = string.Empty;
    private string _yMax = string.Empty;
    private bool _xMinError;
    private bool _xMaxError;
    private bool _yMinError;
    private bool _yMaxError;
    private bool _updatingRanges;
    private double _xMinValue;
    private double _xMaxValue;
    private double _yMinValue;
    private double _yMaxValue;
    private int _selectedLineWidthIndex = 1;
    private bool _isMatchAppTheme;
    public GraphingSettingsViewModel() : this(App.SettingsStore)
    {
    }

    internal GraphingSettingsViewModel(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _isMatchAppTheme = settingsStore.Current.GraphThemeMatchApp;
        AppResourceProvider resources = AppResourceProvider.Instance;
        AvailableLineWidths = [new GraphLineWidthChoice
        {
            Width = 1,
            AutomationName = resources.GetResourceString("SmallLineWidthAutomationName")
        }, new GraphLineWidthChoice
        {
            Width = 2,
            AutomationName = resources.GetResourceString("MediumLineWidthAutomationName")
        }, new GraphLineWidthChoice
        {
            Width = 3,
            AutomationName = resources.GetResourceString("LargeLineWidthAutomationName")
        }, new GraphLineWidthChoice
        {
            Width = 4,
            AutomationName = resources.GetResourceString("ExtraLargeLineWidthAutomationName")
        }

        ];
    }

    public IReadOnlyList<GraphLineWidthChoice> AvailableLineWidths { get; }

    public int SelectedLineWidthIndex
    {
        get => _selectedLineWidthIndex;
        set
        {
            int index = Math.Clamp(value, 0, AvailableLineWidths.Count - 1);
            if (SetProperty(ref _selectedLineWidthIndex, index) && Graph is not null)
            {
                Graph.LineWidth = AvailableLineWidths[index].Width;
            }
        }
    }

    public bool IsMatchAppTheme
    {
        get => _isMatchAppTheme;
        set
        {
            if (!SetProperty(ref _isMatchAppTheme, value))
            {
                return;
            }

            _settingsStore.Update(settings => settings with { GraphThemeMatchApp = value });
            OnPropertyChanged(nameof(IsAlwaysLightTheme));
            GraphThemeSettingChanged?.Invoke(this, new GraphThemeSettingChangedEventArgs(value));
        }
    }

    public bool IsAlwaysLightTheme
    {
        get => !IsMatchAppTheme;
        set
        {
            if (value)
            {
                IsMatchAppTheme = false;
            }
        }
    }

    public event EventHandler<GraphThemeSettingChangedEventArgs>? GraphThemeSettingChanged;
    public Grapher? Graph { get => _graph; private set => SetProperty(ref _graph, value); }
    public string XMin { get => _xMin; set => SetRangeValue(ref _xMin, value, ref _xMinValue, ref _xMinError, nameof(XMin), nameof(XMinError)); }
    public string XMax { get => _xMax; set => SetRangeValue(ref _xMax, value, ref _xMaxValue, ref _xMaxError, nameof(XMax), nameof(XMaxError)); }
    public string YMin { get => _yMin; set => SetRangeValue(ref _yMin, value, ref _yMinValue, ref _yMinError, nameof(YMin), nameof(YMinError)); }
    public string YMax { get => _yMax; set => SetRangeValue(ref _yMax, value, ref _yMaxValue, ref _yMaxError, nameof(YMax), nameof(YMaxError)); }
    public bool XMinError => _xMinError;
    public bool XMaxError => _xMaxError;
    public bool YMinError => _yMinError;
    public bool YMaxError => _yMaxError;
    public bool XError => !_xMinError && !_xMaxError && _xMinValue >= _xMaxValue;
    public bool YError => !_yMinError && !_yMaxError && _yMinValue >= _yMaxValue;
    public bool XMinHasError => XMinError || XError;
    public bool XMaxHasError => XMaxError || XError;
    public bool YMinHasError => YMinError || YError;
    public bool YMaxHasError => YMaxError || YError;

    public bool TrigModeRadians
    {
        get => Graph?.TrigUnitMode == (int)EvalTrigUnitMode.Radians;
        set
        {
            if (value && Graph is not null)
            {
                Graph.TrigUnitMode = (int)EvalTrigUnitMode.Radians;
                RaiseTrigProperties();
            }
        }
    }

    public bool TrigModeDegrees
    {
        get => Graph?.TrigUnitMode == (int)EvalTrigUnitMode.Degrees;
        set
        {
            if (value && Graph is not null)
            {
                Graph.TrigUnitMode = (int)EvalTrigUnitMode.Degrees;
                RaiseTrigProperties();
            }
        }
    }

    public bool TrigModeGradians
    {
        get => Graph?.TrigUnitMode == (int)EvalTrigUnitMode.Grads;
        set
        {
            if (value && Graph is not null)
            {
                Graph.TrigUnitMode = (int)EvalTrigUnitMode.Grads;
                RaiseTrigProperties();
            }
        }
    }

    public void SetGrapher(Grapher grapher)
    {
        ArgumentNullException.ThrowIfNull(grapher);
        Graph = grapher;
        if (grapher.TrigUnitMode == (int)EvalTrigUnitMode.Invalid)
        {
            grapher.TrigUnitMode = (int)EvalTrigUnitMode.Radians;
        }

        InitRanges();
        RaiseTrigProperties();
        int widthIndex = AvailableLineWidths.Select((choice, index) => (choice, index)).OrderBy(pair => Math.Abs(pair.choice.Width - grapher.LineWidth)).First().index;
        if (_selectedLineWidthIndex != widthIndex)
        {
            _selectedLineWidthIndex = widthIndex;
            OnPropertyChanged(nameof(SelectedLineWidthIndex));
        }
    }

    public void ClearGrapher()
    {
        Graph = null;
    }

    public void InitRanges()
    {
        Graph?.GetDisplayRanges(out _xMinValue, out _xMaxValue, out _yMinValue, out _yMaxValue);
        _updatingRanges = true;
        try
        {
            XMin = Format(_xMinValue);
            XMax = Format(_xMaxValue);
            YMin = Format(_yMinValue);
            YMax = Format(_yMaxValue);
        }
        finally
        {
            _updatingRanges = false;
        }
    }

    public void ResetView()
    {
        Graph?.ResetGrid();
        _xMinError = _xMaxError = _yMinError = _yMaxError = false;
        InitRanges();
        RaiseRangeErrorProperties();
    }

    private void SetRangeValue(ref string field, string? value, ref double numericField, ref bool errorField, string propertyName, string errorPropertyName)
    {
        value ??= string.Empty;
        if (!SetProperty(ref field, value, propertyName))
        {
            return;
        }

        bool parsed = double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double number) || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        errorField = !parsed || !double.IsFinite(number);
        if (!errorField)
        {
            numericField = number;
        }

        OnPropertyChanged(errorPropertyName);
        OnPropertyChanged(propertyName.StartsWith('X') ? nameof(XError) : nameof(YError));
        RaiseRangeStyleProperties();
        UpdateDisplayRange();
    }

    private void UpdateDisplayRange()
    {
        if (!_updatingRanges && Graph is not null && !HasError())
        {
            Graph.SetDisplayRanges(_xMinValue, _xMaxValue, _yMinValue, _yMaxValue);
        }
    }

    private bool HasError() => _xMinError || _xMaxError || _yMinError || _yMaxError || XError || YError;
    private void RaiseTrigProperties()
    {
        OnPropertyChanged(nameof(TrigModeRadians));
        OnPropertyChanged(nameof(TrigModeDegrees));
        OnPropertyChanged(nameof(TrigModeGradians));
    }

    private void RaiseRangeErrorProperties()
    {
        OnPropertyChanged(nameof(XMinError));
        OnPropertyChanged(nameof(XMaxError));
        OnPropertyChanged(nameof(YMinError));
        OnPropertyChanged(nameof(YMaxError));
        OnPropertyChanged(nameof(XError));
        OnPropertyChanged(nameof(YError));
        RaiseRangeStyleProperties();
    }

    private void RaiseRangeStyleProperties()
    {
        OnPropertyChanged(nameof(XMinHasError));
        OnPropertyChanged(nameof(XMaxHasError));
        OnPropertyChanged(nameof(YMinHasError));
        OnPropertyChanged(nameof(YMaxHasError));
    }

    private static string Format(double value) => value.ToString("G8", CultureInfo.CurrentCulture);
}
