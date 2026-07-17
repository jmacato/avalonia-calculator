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

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
