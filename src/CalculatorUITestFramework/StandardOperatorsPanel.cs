// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OpenQA.Selenium.Appium.Windows;

namespace CalculatorUITestFramework
{
    /// <summary>
    /// UI elements and helper methods to perform common mathematical standard operations.
    /// </summary>
    public class StandardOperatorsPanel
    {
        private readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private WindowsDriver<WindowsElement> Session => driver.CalculatorSession;
        public NumberPad NumberPad { get; } = new();

        public WindowsElement PercentButton => Session.TryFindElementByAccessibilityId("percentButton");
        public WindowsElement SquareRootButton => Session.TryFindElementByAccessibilityId("squareRootButton");
        public WindowsElement XPower2Button => Session.TryFindElementByAccessibilityId("xpower2Button");
        public WindowsElement XPower3Button => Session.TryFindElementByAccessibilityId("xpower3Button");
        public WindowsElement InvertButton => Session.TryFindElementByAccessibilityId("invertButton");
        public WindowsElement DivideButton => Session.TryFindElementByAccessibilityId("divideButton");
        public WindowsElement MultiplyButton => Session.TryFindElementByAccessibilityId("multiplyButton");
        public WindowsElement MinusButton => Session.TryFindElementByAccessibilityId("minusButton");
        public WindowsElement PlusButton => Session.TryFindElementByAccessibilityId("plusButton");
        public WindowsElement EqualButton => Session.TryFindElementByAccessibilityId("equalButton");
        public WindowsElement ClearEntryButton => Session.TryFindElementByAccessibilityId("clearEntryButton");
        public WindowsElement ClearButton => Session.TryFindElementByAccessibilityId("clearButton");
        public WindowsElement BackSpaceButton => Session.TryFindElementByAccessibilityId("backSpaceButton");
        public WindowsElement NegateButton => Session.TryFindElementByAccessibilityId("negateButton");
    }
}
