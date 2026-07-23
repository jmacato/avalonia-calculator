// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Avalonia.Automation;
using Avalonia.Controls;
using CalculatorApp.ViewModel;

namespace CalculatorApp.Controls;
/// <summary>
/// Direct Avalonia port of the original supplementary-results ItemsControl.
/// Each result gets a text automation container with the localized complete
/// value/unit name rather than exposing its visual fragments separately.
/// </summary>
public sealed class SupplementaryItemsControl : ItemsControl
{
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        bool needsContainer = item is not SupplementaryContentPresenter;
        recycleKey = needsContainer ? nameof(SupplementaryContentPresenter) : null;
        return needsContainer;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new SupplementaryContentPresenter();
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (item is SupplementaryResult supplementaryResult)
        {
            AutomationProperties.SetName(container, supplementaryResult.LocalizedAutomationName);
        }
    }
}
