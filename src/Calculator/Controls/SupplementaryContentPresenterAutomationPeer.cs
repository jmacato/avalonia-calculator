// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Automation.Peers;

namespace CalculatorApp.Controls;

internal sealed class SupplementaryContentPresenterAutomationPeer(SupplementaryContentPresenter owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Text;
    }

    protected override IReadOnlyList<AutomationPeer> GetChildrenCore()
    {
        return Array.Empty<AutomationPeer>();
    }
}
