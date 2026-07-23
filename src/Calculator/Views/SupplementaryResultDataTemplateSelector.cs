// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CalculatorApp.ViewModel;

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

    public bool Match(object? data)
    {
        return data is SupplementaryResult;
    }
}
