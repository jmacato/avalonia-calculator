// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;

using System;
using System.Drawing;

namespace CalculatorUITestFramework
{
    /// <summary>
    /// This class contains the UI automation objects and helper methods available when the Calculator is in any mode.
    /// </summary>
    public static class CalculatorApp
    {
        public static WindowsElement Window => Session.FindElementByClassName("Windows.UI.Core.CoreWindow");

        internal static WindowsElement Header => Session.TryFindElementByAccessibilityId("Header");

        private static readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private static WindowsDriver<WindowsElement> Session => driver.CalculatorSession;
        private static WindowsElement AppName => Session.TryFindElementByAccessibilityId("AppName");

        /// <summary>
        /// Gets the text from the Header
        /// </summary>
        /// <returns>The string shown in the UI.</returns>
        public static string CalculatorHeaderText => Header.Text;

        ///// <summary>
        ///// Clicks the AppName element on Windows Calculator to ensure that the app has focus
        ///// </summary>
        public static void EnsureCalculatorHasFocus()
        {
            AppName.Click();
        }

        /// <summary>
        /// Click the window (to lose focus on components)
        /// </summary>
        public static void ClickOnWindow()
        {
            Window.Click();
        }

        /// <summary>
        /// If the the Dock Panel for the History and Memory lists is not displayed, resize the window
        /// Two attempts are made, the the lable is not found a "not found" exception is thrown
        /// </summary>
        public static void ResizeWindowToDisplayMemoryHistoryDockPanel()
        {
            // Put the calculator in the upper left region of the screen
            CalculatorDriver.Instance.CalculatorSession.Manage().Window.Position = new Point(8, 8);
            GrowWindowToShowDock(CalculatorDriver.Instance.CalculatorSession.Manage().Window.Size.Width);
        }

        /// <summary>
        /// Increases the size of the window until Dock Panel for the History/Memory panel is visible
        /// </summary>
        private static void GrowWindowToShowDock(int width)
        {
            if (width > 2100)
            {
                throw new NotFoundException("Could not the Dock Panel for the History and Memory lists");
            }

            if (!Session.PageSource.Contains("DockPanel", StringComparison.Ordinal))
            {
                var height = CalculatorDriver.Instance.CalculatorSession.Manage().Window.Size.Height;
                CalculatorDriver.Instance.CalculatorSession.Manage().Window.Size = new Size(width, height);
                //give window time to render new size
                System.Threading.Thread.Sleep(10);
                GrowWindowToShowDock(width + 100);
            }
        }
    }

}
