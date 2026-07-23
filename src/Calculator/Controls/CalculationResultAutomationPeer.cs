// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace CalculatorApp.Controls;

public sealed class CalculationResultAutomationPeer(CalculationResult owner)
    : ControlAutomationPeer(owner), IInvokeProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Text;
    }

    void IInvokeProvider.Invoke()
    {
        ((CalculationResult)Owner).ProgrammaticSelect();
    }
}
