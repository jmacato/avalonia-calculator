// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.ComponentModel;
using CalculatorApp.ViewModel.Common;
using GraphControl;

namespace CalculatorApp.ViewModel;

public sealed class VariableViewModel : ViewModelBase
{
    public const int DefaultMinMaxRange = 10;
    private Variable _variable;
    private bool _sliderSettingsVisible;
    public VariableViewModel(string name, Variable variable)
    {
        Name = name;
        _variable = variable;
        _variable.PropertyChanged += OnVariablePropertyChanged;
    }

    public string Name { get; }

    public bool SliderSettingsVisible
    {
        get => _sliderSettingsVisible;
        set
        {
            if (SetProperty(ref _sliderSettingsVisible, value))
            {
                OnPropertyChanged(nameof(SliderSettingsChevron));
            }
        }
    }

    public string SliderSettingsChevron => SliderSettingsVisible ? "\uE70E" : "\uE70D";

    public double Min
    {
        get => _variable.Min;
        set
        {
            if (!double.IsFinite(value) || _variable.Min.Equals(value))
            {
                return;
            }

            if (value >= _variable.Max)
            {
                _variable.Max = value + DefaultMinMaxRange;
            }

            _variable.Min = value;
        }
    }

    public double Step
    {
        get => _variable.Step;
        set
        {
            if (double.IsFinite(value) && value > 0)
            {
                _variable.Step = value;
            }
        }
    }

    public double Max
    {
        get => _variable.Max;
        set
        {
            if (!double.IsFinite(value) || _variable.Max.Equals(value))
            {
                return;
            }

            if (value <= _variable.Min)
            {
                _variable.Min = value - DefaultMinMaxRange;
            }

            _variable.Max = value;
        }
    }

    public double Value
    {
        get => _variable.Value;
        set
        {
            if (!double.IsFinite(value))
            {
                return;
            }

            if (value < _variable.Min)
            {
                _variable.Min = value;
            }
            else if (value > _variable.Max)
            {
                _variable.Max = value;
            }

            if (!_variable.Value.Equals(value))
            {
                _variable.Value = value;
                VariableUpdated?.Invoke(this, new VariableChangedEventArgs(Name, value));
            }
        }
    }

    public string VariableAutomationName => LocalizationStringUtil.GetLocalizedString(AppResourceProvider.Instance.GetResourceString("VariableListViewItem"), Name);

    public event EventHandler<VariableChangedEventArgs>? VariableUpdated;
    internal void UpdateVariable(Variable variable)
    {
        if (ReferenceEquals(_variable, variable))
        {
            return;
        }

        _variable.PropertyChanged -= OnVariablePropertyChanged;
        _variable = variable;
        _variable.PropertyChanged += OnVariablePropertyChanged;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Min));
        OnPropertyChanged(nameof(Max));
        OnPropertyChanged(nameof(Step));
    }

    private void OnVariablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
    }
}
