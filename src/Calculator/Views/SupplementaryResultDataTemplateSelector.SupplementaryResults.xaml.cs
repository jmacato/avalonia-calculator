// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;
using UnitConversionManager;

namespace CalculatorApp;
/// <summary>
/// Avalonia IDataTemplate equivalent of the original WinUI
/// SupplementaryResultDataTemplateSelector.
/// </summary>
public sealed class SupplementaryResultDataTemplateSelector : IDataTemplate
{
    public IDataTemplate RegularTemplate { get; set; } = null!;
    public IDataTemplate DelighterTemplate { get; set; } = null!;

    public Control? Build(object? parameter)
    {
        IDataTemplate template = parameter is SupplementaryResult result && result.IsWhimsical() ? DelighterTemplate : RegularTemplate;
        return template.Build(parameter);
    }

    public bool Match(object? data) => data is SupplementaryResult;
}
