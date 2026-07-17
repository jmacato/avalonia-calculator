// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OpenQA.Selenium.Appium.Windows;

namespace CalculatorUITestFramework
{
    /// <summary>
    /// This class contains the UI automation objects and helper methods available when the Calculator is in Programmer Mode.
    /// </summary>
    public class ProgrammerCalculatorPage
    {
        private readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private WindowsDriver<WindowsElement> Session => driver.CalculatorSession;
        public ScientificOperatorsPanel ScientificOperators { get; } = new();
        public StandardOperatorsPanel StandardOperators { get; } = new();
        public ProgrammerOperatorsPanel ProgrammerOperators { get; } = new();
        public MemoryPanel MemoryPanel { get; } = new();
        public HistoryPanel HistoryPanel { get; } = new();
        public NavigationMenu NavigationMenu { get; } = new();
        public WindowsElement Header => Session.TryFindElementByAccessibilityId("Header");

        public CalculatorResults CalculatorResults { get; } = new();

        public void NavigateToProgrammerCalculator()
        {
            // Ensure that calculator is in scientific mode
            NavigationMenu.ChangeCalculatorMode(CalculatorMode.ProgrammerCalculator);
        }

        /// <summary>
        /// Clear the Calculator display, Memory Panel and optionally the History Panel
        /// </summary>
        public void ClearAll()
        {
            string source = Session.PageSource;

            if (source.Contains("clearEntryButton", System.StringComparison.Ordinal))
            {
                StandardOperators.ClearEntryButton.Click();
                StandardOperators.ClearButton.Click();
            }
            if (source.Contains("clearButton", System.StringComparison.Ordinal))
            {
                StandardOperators.ClearButton.Click();
            }
            MemoryPanel.ResizeWindowToDisplayMemoryLabel();
            if (source.Contains("ClearMemory", System.StringComparison.Ordinal))
            {
                MemoryPanel.NumberpadMCButton.Click();
            }
        }
    }
}
