// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using CalculatorApp.ViewModel;

namespace CalculatorApp.Controls;

internal sealed class SupplementaryContentPresenterAutomationPeer(SupplementaryContentPresenter owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => Array.Empty<AutomationPeer>();
}
