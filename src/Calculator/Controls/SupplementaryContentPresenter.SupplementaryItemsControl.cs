// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using CalculatorApp.ViewModel;

namespace CalculatorApp.Controls;

public sealed class SupplementaryContentPresenter : ContentPresenter
{
    public SupplementaryContentPresenter()
    {
        // WinUI clips a container arranged to an empty rectangle. Avalonia's
        // ContentPresenter does not do so by default, so retain the original
        // no-overflow semantics explicitly.
        ClipToBounds = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SupplementaryContentPresenterAutomationPeer(this);
}
