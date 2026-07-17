// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using OpenQA.Selenium.Appium.Windows;
using System;

namespace CalculatorUITestFramework
{
    public class NavigationMenu
    {
        public WindowsElement NavigationMenuButton => Session.TryFindElementByAccessibilityId("TogglePaneButton");
        public WindowsElement NavigationMenuPane => Session.TryFindElementByClassName("SplitViewPane");
        private readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private WindowsDriver<WindowsElement> Session => driver.CalculatorSession;

        /// <summary>
        /// Changes the mode using the navigation menu in the UI
        /// </summary>
        /// <param name = "mode">The mode to be changed to</param>
        public void ChangeCalculatorMode(CalculatorMode mode)
        {
            string modeAccessibilityId = mode switch
            {
                CalculatorMode.StandardCalculator => "Standard",
                CalculatorMode.ScientificCalculator => "Scientific",
                CalculatorMode.ProgrammerCalculator => "Programmer",
                CalculatorMode.DateCalculator => "Date",
                CalculatorMode.Currency => "Currency",
                CalculatorMode.Volume => "Volume",
                CalculatorMode.Length => "Length",
                CalculatorMode.Weight => "Weight",
                CalculatorMode.Temperature => "Temperature",
                CalculatorMode.Energy => "Energy",
                CalculatorMode.Area => "Area",
                CalculatorMode.Speed => "Speed",
                CalculatorMode.Time => "Time",
                CalculatorMode.Power => "Power",
                CalculatorMode.Data => "Data",
                CalculatorMode.Pressure => "Pressure",
                CalculatorMode.Angle => "Angle",
                _ => throw (new ArgumentException("The mode is not valid"))
            };
            this.NavigationMenuButton.Click();
            this.NavigationMenuPane.WaitForDisplayed();
            Session.TryFindElementByAccessibilityId(modeAccessibilityId).Click();
        }
    }
}
