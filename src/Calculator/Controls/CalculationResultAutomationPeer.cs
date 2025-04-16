// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace CalculatorApp
{
    namespace Controls
    {
        public sealed class CalculationResultAutomationPeer : FrameworkElementAutomationPeer,
                                                              IInvokeProvider
        {
            public CalculationResultAutomationPeer(FrameworkElement owner) : base(owner)
            {
            }

            protected override AutomationControlType GetAutomationControlTypeCore()
            {
                return AutomationControlType.Text;
            }

            protected override object GetPatternCore(PatternInterface pattern)
            {
                return pattern == PatternInterface.Invoke ? this : base.GetPatternCore(pattern);
            }

            public void Invoke()
            {
                var owner = (CalculationResult)this.Owner;
                owner.ProgrammaticSelect();
            }
        }
    }
}
