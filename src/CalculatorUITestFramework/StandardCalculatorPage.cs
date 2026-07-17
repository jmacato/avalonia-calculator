// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OpenQA.Selenium.Appium.Windows;

namespace CalculatorUITestFramework
{
    /// <summary>
    /// This class contains the UI automation objects and helper methods available when the Calculator is in Standard Mode.
    /// </summary>
    public class StandardCalculatorPage
    {
        public StandardOperatorsPanel StandardOperators { get; } = new();
        public MemoryPanel MemoryPanel { get; } = new();
        public HistoryPanel HistoryPanel { get; } = new();
        public NavigationMenu NavigationMenu { get; } = new();
        public StandardAoTCalculatorPage StandardAoTCalculatorPage { get; } = new();
        public CalculatorResults CalculatorResults { get; } = new();

        private readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private WindowsDriver<WindowsElement> Session => driver.CalculatorSession;

        /// <summary>
        /// Navigates the calculator to Standard mode and ensures that it is in standard mode
        /// </summary>
        public void NavigateToStandardCalculator()
        {
            // Ensure that calculator is in standard mode
            this.NavigationMenu.ChangeCalculatorMode(CalculatorMode.StandardCalculator);
            this.CalculatorResults.IsResultsDisplayPresent();
        }

        /// <summary>
        /// Clear the Calculator display, Memory Panel and optionally the History Panel
        /// </summary>
        public void ClearAll()
        {
            this.StandardAoTCalculatorPage.NavigateToStandardMode();
            this.MemoryPanel.ResizeWindowToDisplayMemoryLabel();
            this.StandardOperators.ClearButton.Click();
            this.MemoryPanel.NumberpadMCButton.Click();
            this.HistoryPanel.ClearHistory();
        }

        ///// <summary>
        ///// Ensures that the calculator result text is zero; if not, clears all
        ///// </summary>
        public void EnsureCalculatorResultTextIsZero()
        {
            if ("0" != this.CalculatorResults.GetCalculatorResultText())
            {
                this.ClearAll();
            }
        }

        ///// <summary>
        ///// Ensures that the calculator is in Standard Mode
        ///// </summary>
        public void EnsureCalculatorIsInStandardMode()
        {
            string source = CalculatorDriver.Instance.CalculatorSession.PageSource;
            if (source.Contains("Header", System.StringComparison.Ordinal))
            {
                string header = CalculatorApp.Header.Text;
                if (header == "Standard")
                {
                    return;
                }
                else
                {
                    this.NavigateToStandardCalculator();
                }
            }
            else
            {
                this.StandardAoTCalculatorPage.NavigateToStandardMode();
            }
        }

    }

}
