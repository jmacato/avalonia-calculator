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
        return (lastChild.DataContext as SupplementaryResult ?? (lastChild as ContentPresenter)?.Content as SupplementaryResult)?.IsWhimsical() == true;
    }
}
