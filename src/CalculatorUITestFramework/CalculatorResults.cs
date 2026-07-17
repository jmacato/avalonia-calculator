// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Microsoft.VisualStudio.TestTools.UnitTesting;

using OpenQA.Selenium.Appium.Windows;

using System;

namespace CalculatorUITestFramework
{
    public class CalculatorResults
    {
        private readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private WindowsDriver<WindowsElement> Session => driver.CalculatorSession;
        private WindowsElement CalculatorAlwaysOnTopResults => Session.TryFindElementByAccessibilityId("CalculatorAlwaysOnTopResults");
        private WindowsElement CalculatorResult => Session.TryFindElementByAccessibilityId("CalculatorResults");
        private WindowsElement CalculatorExpression => Session.TryFindElementByAccessibilityId("CalculatorExpression");

        /// <summary>
        /// Gets the text from the display control in AoT mode and removes the narrator text that is not displayed in the UI.
        /// </summary>
        /// <returns>The string shown in the UI.</returns>
        public string GetAoTCalculatorResultText()
        {
            return this.CalculatorAlwaysOnTopResults.Text.Replace("Display is", string.Empty, System.StringComparison.Ordinal).Trim();
        }

        /// <summary>
        /// Gets the text from the display control and removes the narrator text that is not displayed in the UI.
        /// </summary>
        /// <returns>The string shown in the UI.</returns>
        public string GetCalculatorResultText()
        {
            return this.CalculatorResult.Text.Replace("Display is", string.Empty, System.StringComparison.Ordinal).Trim();
        }

        /// <summary>
        /// Gets the text from the Calculator Expression control and removes narrator text that is not displayed in the UI.
        /// </summary>
        /// <returns>The string shown in the UI.</returns>
        public string GetCalculatorExpressionText()
        {
            return this.CalculatorExpression.Text.Replace("Expression is", string.Empty, System.StringComparison.Ordinal).Trim();
        }

        /// <summary>
        /// Verifies that CalculatorResult is not null
        /// </summary>
        /// <returns>The string shown in the UI.</returns>
        public void IsResultsDisplayPresent()
        {
            Assert.IsNotNull(this.CalculatorResult);
        }

        /// <summary>
        /// Verifies that Calculator Expression is clear
        /// </summary>
        /// <returns>The string shown in the UI.</returns>
        public static void IsResultsExpressionClear()
        {
            string source = CalculatorDriver.Instance.CalculatorSession.PageSource;
            if (source.Contains("CalculatorExpression", System.StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Calculator Expression is not clear");
            }
        }
    }
}
