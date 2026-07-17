// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using System;

namespace CalculatorUITestFramework
{
    /// <summary>
    /// UI elements unique to Scientific mode
    /// </summary>
    public class ScientificOperatorsPanel
    {
        private readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private WindowsDriver<WindowsElement> Session => driver.CalculatorSession;
        public WindowsElement XPower3Button => Session.TryFindElementByAccessibilityId("xpower3Button");
        public WindowsElement XPowerYButton => Session.TryFindElementByAccessibilityId("powerButton");
        public WindowsElement PowerOf10Button => Session.TryFindElementByAccessibilityId("powerOf10Button");
        public WindowsElement LogButton => Session.TryFindElementByAccessibilityId("logBase10Button");
        public WindowsElement LnButton => Session.TryFindElementByAccessibilityId("logBaseEButton");
        public WindowsElement PiButton => Session.TryFindElementByAccessibilityId("piButton");
        public WindowsElement EulerButton => Session.TryFindElementByAccessibilityId("eulerButton");
        public WindowsElement AbsButton => Session.TryFindElementByAccessibilityId("absButton");
        public WindowsElement ExpButton => Session.TryFindElementByAccessibilityId("expButton");
        public WindowsElement ModButton => Session.TryFindElementByAccessibilityId("modButton");
        public WindowsElement ParenthesisLeftButton => Session.TryFindElementByAccessibilityId("openParenthesisButton");
        public WindowsElement ParenthesisRightButton => Session.TryFindElementByAccessibilityId("closeParenthesisButton");
        public WindowsElement FactorialButton => Session.TryFindElementByAccessibilityId("factorialButton");
        public WindowsElement BackSpaceButton => Session.TryFindElementByAccessibilityId("backSpaceButton");
        public WindowsElement DegButton => Session.TryFindElementByAccessibilityId("degButton");
        public WindowsElement RadButton => Session.TryFindElementByAccessibilityId("radButton");
        public WindowsElement GradButton => Session.TryFindElementByAccessibilityId("gradButton");
        public WindowsElement AngleOperator => Session.TryFindElementByAccessibilityId("ScientificAngleOperators");
        public WindowsElement TrigButton => Session.TryFindElementByAccessibilityId("trigButton");
        public WindowsElement FuncButton => Session.TryFindElementByAccessibilityId("funcButton");
        public WindowsElement SinButton => Session.TryFindElementByAccessibilityId("sinButton");
        public WindowsElement CosButton => Session.TryFindElementByAccessibilityId("cosButton");
        public WindowsElement TanButton => Session.TryFindElementByAccessibilityId("tanButton");
        public WindowsElement CscButton => Session.TryFindElementByAccessibilityId("cscButton");
        public WindowsElement SecButton => Session.TryFindElementByAccessibilityId("secButton");
        public WindowsElement CotButton => Session.TryFindElementByAccessibilityId("cotButton");
        public WindowsElement TrigShiftButton => Session.TryFindElementByAccessibilityId("trigShiftButton");
        public WindowsElement HypShiftButton => Session.TryFindElementByAccessibilityId("hypShiftButton");
        public WindowsElement InvSinButton => Session.TryFindElementByAccessibilityId("invsinButton");
        public WindowsElement InvCosButton => Session.TryFindElementByAccessibilityId("invcosButton");
        public WindowsElement InvTanButton => Session.TryFindElementByAccessibilityId("invtanButton");
        public WindowsElement InvCscButton => Session.TryFindElementByAccessibilityId("invcscButton");
        public WindowsElement InvSecButton => Session.TryFindElementByAccessibilityId("invsecButton");
        public WindowsElement InvCotButton => Session.TryFindElementByAccessibilityId("invcotButton");
        public WindowsElement SinhButton => Session.TryFindElementByAccessibilityId("sinhButton");
        public WindowsElement CoshButton => Session.TryFindElementByAccessibilityId("coshButton");
        public WindowsElement TanhButton => Session.TryFindElementByAccessibilityId("tanhButton");
        public WindowsElement CschButton => Session.TryFindElementByAccessibilityId("cschButton");
        public WindowsElement SechButton => Session.TryFindElementByAccessibilityId("sechButton");
        public WindowsElement CothButton => Session.TryFindElementByAccessibilityId("cothButton");
        public WindowsElement InvSinhButton => Session.TryFindElementByAccessibilityId("invsinhButton");
        public WindowsElement InvCoshButton => Session.TryFindElementByAccessibilityId("invcoshButton");
        public WindowsElement InvTanhButton => Session.TryFindElementByAccessibilityId("invtanhButton");
        public WindowsElement InvCschButton => Session.TryFindElementByAccessibilityId("invcschButton");
        public WindowsElement InvSechButton => Session.TryFindElementByAccessibilityId("invsechButton");
        public WindowsElement InvCothButton => Session.TryFindElementByAccessibilityId("invcothButton");
        public WindowsElement FloorButton => Session.TryFindElementByAccessibilityId("floorButton");
        public WindowsElement CeilButton => Session.TryFindElementByAccessibilityId("ceilButton");
        public WindowsElement RandButton => Session.TryFindElementByAccessibilityId("randButton");
        public WindowsElement DmsButton => Session.TryFindElementByAccessibilityId("dmsButton");
        public WindowsElement DegreesButton => Session.TryFindElementByAccessibilityId("degreesButton");
        public WindowsElement FixedToExponentialButton => Session.TryFindElementByAccessibilityId("ftoeButton");
        public WindowsElement NegateButton => Session.TryFindElementByAccessibilityId("negateButton");
        public WindowsElement ShiftButton => Session.TryFindElementByAccessibilityId("shiftButton");
        public WindowsElement TrigFlyout => Session.TryFindElementByAccessibilityId("Trigflyout");
        public WindowsElement LightDismiss => Session.TryFindElementByAccessibilityId("Light Dismiss");
        private WindowsElement DegRadGradButton => GetAngleOperatorButton();

        private WindowsElement GetAngleOperatorButton()
        {
            string source = Session.PageSource;
            if (source.Contains("degButton", System.StringComparison.Ordinal))
            {
                return DegButton;
            }
            else if (source.Contains("gradButton", System.StringComparison.Ordinal))
            {
                return GradButton;
            }
            else if (source.Contains("radButton", System.StringComparison.Ordinal))
            {
                return RadButton;
            }

            throw new NotFoundException("Could not find deg, rad or grad button in page source");
        }

        /// <summary>
        /// Set the state of the degrees, radians and gradians buttons.
        /// </summary>
        public void SetAngleOperator(AngleOperatorState value)
        {
            //set the desired string value for the button
            string desiredId = value switch
            {
                AngleOperatorState.Degrees => "degButton",
                AngleOperatorState.Gradians => "gradButton",
                AngleOperatorState.Radians => "radButton",
                _ => throw new NotImplementedException()
            };
            while (this.DegRadGradButton.GetAttribute("AutomationId") != desiredId)
            {
                this.DegRadGradButton.Click();
            }
        }

        public void ResetFEButton(FEButtonState value)
        {
            if (this.FixedToExponentialButton.GetAttribute("Toggle.ToggleState") != "0")
            {
                FixedToExponentialButton.Click();
            }
        }

        public WindowsElement ResetTrigDropdownToggles()
        {
            TrigButton.Click();
            string source = Session.PageSource;
            if (source.Contains("sinButton", System.StringComparison.Ordinal))
            {
                LightDismiss.Click();
            }
            else if (source.Contains("invsinButton", System.StringComparison.Ordinal))
            {
                TrigShiftButton.Click();
            }
            else if (source.Contains("sinhButton", System.StringComparison.Ordinal))
            {
                HypShiftButton.Click();
            }
            else if (source.Contains("invsinhButton", System.StringComparison.Ordinal))
            {
                TrigShiftButton.Click();
                HypShiftButton.Click();
            }

            throw new NotFoundException("Could not find trig drop-down buttons in page source");
        }
    }
}
