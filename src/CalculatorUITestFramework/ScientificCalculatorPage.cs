// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OpenQA.Selenium.Appium.Windows;

namespace CalculatorUITestFramework
{
    /// <summary>
    /// This class contains the UI automation objects and helper methods available when the Calculator is in Scientific Mode.
    /// </summary>
    public class ScientificCalculatorPage
    {
        private readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private WindowsDriver<WindowsElement> Session => driver.CalculatorSession;
        public ScientificOperatorsPanel ScientificOperators { get; } = new();
        public StandardOperatorsPanel StandardOperators { get; } = new();
        public MemoryPanel MemoryPanel { get; } = new();
        public HistoryPanel HistoryPanel { get; } = new();
        public NavigationMenu NavigationMenu { get; } = new();
        public WindowsElement Header => Session.TryFindElementByAccessibilityId("Header");

        public CalculatorResults CalculatorResults { get; } = new();

        public void NavigateToScientificCalculator()
        {
            // Ensure that calculator is in scientific mode
            this.NavigationMenu.ChangeCalculatorMode(CalculatorMode.ScientificCalculator);
        }

        /// <summary>
        /// Clear the Calculator display, Memory Panel and optionally the History Panel
        /// </summary>
        public void ClearAll()
        {
            string source = Session.PageSource;

            if (source.Contains("clearEntryButton", System.StringComparison.Ordinal))
            {
                this.StandardOperators.ClearEntryButton.Click();
                source = Session.PageSource;
            }
            if (source.Contains("clearButton", System.StringComparison.Ordinal))
            {
                this.StandardOperators.ClearButton.Click();
            }
            MemoryPanel.ResizeWindowToDisplayMemoryLabel();
            HistoryPanel.ClearHistory();
        }
    }
}
