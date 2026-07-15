// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CalculatorApp.ViewModel;

namespace CalculatorApp.TemplateSelectors;

/// <summary>
/// Avalonia port of the native function-analysis template selector. Symbolic
/// expressions use the math template, monotonicity uses the two-column grid,
/// and localized explanatory messages remain normal text.
/// </summary>
public sealed class KeyGraphFeaturesTemplateSelector : IDataTemplate
{
    public IDataTemplate RichEditTemplate { get; set; } = null!;

    public IDataTemplate GridTemplate { get; set; } = null!;

    public IDataTemplate TextBlockTemplate { get; set; } = null!;

    public Control? Build(object? parameter)
    {
        IDataTemplate template = parameter is KeyGraphFeaturesItem item && !item.IsText
            ? item.DisplayItems.Count != 0
                ? RichEditTemplate
                : item.GridItems.Count != 0
                    ? GridTemplate
                    : TextBlockTemplate
            : TextBlockTemplate;

        return template.Build(parameter);
    }

    public bool Match(object? data) => data is KeyGraphFeaturesItem;
}
