// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;

namespace CalculatorApp.ViewModel;
/// <summary>
/// Avalonia wrapper for the converter category model. The wrapper is retained
/// from the WinUI implementation so the view does not depend on engine details.
/// </summary>
public sealed class Category : INotifyPropertyChanged
{
    private readonly UnitConversionManager.Category _original;
    internal Category(UnitConversionManager.Category category)
    {
        _original = category;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name => _original.Name;
    public bool SupportsNegative => _original.SupportsNegative;

    public int ModelCategoryId => _original.Id;
    internal UnitConversionManager.Category ModelCategory => _original;
    internal void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
