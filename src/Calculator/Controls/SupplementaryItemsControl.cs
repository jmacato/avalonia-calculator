// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
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

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new SupplementaryContentPresenter();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);

        if (item is SupplementaryResult supplementaryResult)
        {
            AutomationProperties.SetName(container, supplementaryResult.LocalizedAutomationName);
        }
    }
}

public sealed class SupplementaryContentPresenter : ContentPresenter
{
    public SupplementaryContentPresenter()
    {
        // WinUI clips a container arranged to an empty rectangle. Avalonia's
        // ContentPresenter does not do so by default, so retain the original
        // no-overflow semantics explicitly.
        ClipToBounds = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new SupplementaryContentPresenterAutomationPeer(this);
}

internal sealed class SupplementaryContentPresenterAutomationPeer(SupplementaryContentPresenter owner)
    : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => Array.Empty<AutomationPeer>();
}
