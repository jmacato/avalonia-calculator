// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using OpenQA.Selenium.Appium.Windows;

namespace CalculatorUITestFramework
{
    public class UnitConverterOperatorsPanel
    {
        private readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private WindowsDriver<WindowsElement> Session => driver.CalculatorSession;
        public NumberPad NumberPad { get; } = new();
        public WindowsElement ClearButton => Session.TryFindElementByAccessibilityId("ClearEntryButtonPos0");
        public WindowsElement BackSpaceButton => Session.TryFindElementByAccessibilityId("BackSpaceButtonSmall");
        public WindowsElement Units1 => Session.TryFindElementByAccessibilityId("Units1");
        public WindowsElement Units2 => Session.TryFindElementByAccessibilityId("Units2");
    }
}
