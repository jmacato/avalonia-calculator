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
/// The original WinUI view selected a Segoe UI Symbol style by unit id. The
/// portable view performs the same lookup, but each resource contains the
/// exact extracted outline instead of relying on an installed Windows font.
/// </summary>
public sealed class DelighterUnitToVectorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Unit { IsWhimsical: true } unit || Application.Current is not { } app)
        {
            return null;
        }

        string key = $"Unit_{unit.Id}";
        if (!app.TryGetResource(key, null, out object? resource) || resource is not DelighterVectorGlyph glyph)
        {
            return null;
        }

        return parameter switch
        {
            "Geometry" => glyph.Geometry,
            "Width" => glyph.Width,
            "Margin" => glyph.Margin,
            _ => glyph
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Compiled vector resource corresponding to one original Delighter TextBlock
/// style. Width and margin preserve Segoe UI Symbol's advance and style data.
/// </summary>
public sealed class DelighterVectorGlyph
{
    public Geometry Geometry { get; set; } = new StreamGeometry();

    public double Width { get; set; }

    public Thickness Margin { get; set; }
}

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
        IDataTemplate template = parameter is SupplementaryResult result && result.IsWhimsical()
            ? DelighterTemplate
            : RegularTemplate;
        return template.Build(parameter);
    }

    public bool Match(object? data) => data is SupplementaryResult;
}

/// <summary>
/// Preserves the original rule that the final whimsical comparison remains
/// visible when the full horizontal result list does not fit.
/// </summary>
public sealed class SupplementaryResultNoOverflowStackPanel : HorizontalNoOverflowStackPanel
{
    protected override bool ShouldPrioritizeLastItem()
    {
        if (Children.Count == 0)
        {
            return false;
        }

        Control lastChild = Children[^1];
        return (lastChild.DataContext as SupplementaryResult
                ?? (lastChild as ContentPresenter)?.Content as SupplementaryResult)
            ?.IsWhimsical() == true;
    }
}

public sealed partial class SupplementaryResults : UserControl
{
    public static readonly StyledProperty<IEnumerable<SupplementaryResult>?> ResultsProperty =
        AvaloniaProperty.Register<SupplementaryResults, IEnumerable<SupplementaryResult>?>(nameof(Results));

    public SupplementaryResults()
    {
        InitializeComponent();
    }

    public IEnumerable<SupplementaryResult>? Results
    {
        get => GetValue(ResultsProperty);
        set => SetValue(ResultsProperty, value);
    }
}
