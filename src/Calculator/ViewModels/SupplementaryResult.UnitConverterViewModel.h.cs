// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Channels;
using System.Windows.Input;
using Avalonia.Threading;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using UnitConversionManager;

namespace CalculatorApp.ViewModel;

public sealed class SupplementaryResult : INotifyPropertyChanged
{
    internal SupplementaryResult(string value, Unit unit)
    {
        Value = value;
        Unit = unit;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Value { get; }
    public Unit Unit { get; }

    public bool IsWhimsical() => Unit.IsWhimsical;
    public string LocalizedAutomationName
    {
        get
        {
            string format = AppResourceProvider.Instance.GetResourceString("SupplementaryUnit_AutomationName");
            return LocalizationStringUtil.GetLocalizedString(format, Value, Unit.Name);
        }
    }

    internal void RaisePropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
