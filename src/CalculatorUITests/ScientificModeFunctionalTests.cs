// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

using CalculatorUITestFramework;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CalculatorUITests
{
    [TestClass]
    public sealed class ScientificModeFunctionalTests
    {
        private static readonly ScientificCalculatorPage page = new ScientificCalculatorPage();

        /// <summary>
        /// Initializes the WinAppDriver web driver session.
        /// </summary>
        /// <param name="context"></param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Create session to launch a Calculator window
            CalculatorDriver.Instance.SetupCalculatorSession(context);

            // Ensure that calculator is in scientific mode
            page.NavigateToScientificCalculator();

            // Ensure that calculator window is large enough to display the memory/history panel; a good size for most tests
            page.MemoryPanel.ResizeWindowToDisplayMemoryLabel();
        }

        /// <summary>
        /// Closes the app and WinAppDriver web driver session.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            // Tear down Calculator session.
            CalculatorDriver.Instance.TearDownCalculatorSession();
        }

        /// <summary>
        /// Ensures the calculator is in a cleared state
        /// </summary>
        [TestInitialize]
        public void TestInit()
        {
            if ("0" != page.CalculatorResults.GetCalculatorResultText())
            {
                page.ClearAll();
            }
            CalculatorApp.EnsureCalculatorHasFocus();
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);
            page.ScientificOperators.ResetFEButton(FEButtonState.Normal);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            page.ClearAll();
        }

        #region Smoke Tests
        [TestMethod]
        [Priority(0)]
        public void SmokeTestCube()
        {
            page.StandardOperators.NumberPad.Input(3);
            page.ScientificOperators.ShiftButton.Click();
            page.ScientificOperators.XPower3Button.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("27", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestSin()
        {
            page.StandardOperators.NumberPad.Input(90);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.SinButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestTanh()
        {
            page.StandardOperators.NumberPad.Input(90);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.TanhButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestInvCos()
        {
            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.InvCosButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestFloor()
        {
            page.StandardOperators.NumberPad.Input(5.9);
            page.ScientificOperators.FuncButton.Click();
            page.ScientificOperators.FloorButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("5", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestParentheses()
        {
            page.StandardOperators.NumberPad.Input(3);
            page.StandardOperators.MultiplyButton.Click();
            page.ScientificOperators.ParenthesisLeftButton.Click();
            page.StandardOperators.NumberPad.Input(2);
            page.StandardOperators.PlusButton.Click();
            page.StandardOperators.NumberPad.Input(2);
            page.ScientificOperators.ParenthesisRightButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("12", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestRadianAngleOperator()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Radians);

            page.ScientificOperators.PiButton.Click();
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.CosButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("-1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestGradianAngleOperator()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Gradians);

            page.StandardOperators.NumberPad.Input(100);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.SinButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestFixedToExponential()
        {
            page.ScientificOperators.FixedToExponentialButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0.e+0", page.CalculatorResults.GetCalculatorResultText());
        }
        #endregion

        #region Advanced Arithmetic Tests
        [TestMethod]
        [Priority(1)]
        public void OperatorXPowerY()
        {
            page.StandardOperators.NumberPad.Input(3);
            page.ScientificOperators.XPowerYButton.Click();
            page.StandardOperators.NumberPad.Input(5);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("243", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorPowerOf10Button()
        {
            page.StandardOperators.NumberPad.Input(5);
            page.ScientificOperators.PowerOf10Button.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("100,000", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorLogButton()
        {
            page.StandardOperators.NumberPad.Input(10000);
            page.ScientificOperators.LogButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("4", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorLnButton()
        {
            page.ScientificOperators.EulerButton.Click();
            page.ScientificOperators.LnButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorAbsButton()
        {
            page.StandardOperators.NumberPad.Input(25);
            page.ScientificOperators.NegateButton.Click();
            page.ScientificOperators.AbsButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("25", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorExpButton()
        {
            page.StandardOperators.NumberPad.Input(4);
            page.ScientificOperators.ExpButton.Click();
            page.StandardOperators.NumberPad.Input(4);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("40,000", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorModButton()
        {
            page.StandardOperators.NumberPad.Input(53);
            page.ScientificOperators.ModButton.Click();
            page.StandardOperators.NumberPad.Input(10);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("3", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorFactorialButton()
        {
            page.StandardOperators.NumberPad.Input(4);
            page.ScientificOperators.FactorialButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("24", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorCeilingButton()
        {
            page.StandardOperators.NumberPad.Input(4.1);
            page.ScientificOperators.FuncButton.Click();
            page.ScientificOperators.CeilButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("5", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorRandomButton()
        {
            page.ScientificOperators.FuncButton.Click();
            page.ScientificOperators.RandButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.IsTrue(page.CalculatorResults.GetCalculatorResultText().StartsWith("0.", StringComparison.Ordinal));
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorDmsButton()
        {
            page.StandardOperators.NumberPad.Input(2.999);
            page.ScientificOperators.FuncButton.Click();
            page.ScientificOperators.DmsButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("2.59564", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void OperatorDegreesButton()
        {
            page.StandardOperators.NumberPad.Input(2.59564);
            page.ScientificOperators.FuncButton.Click();
            page.ScientificOperators.DegreesButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("2.999", page.CalculatorResults.GetCalculatorResultText());
        }
        #endregion

        #region Trigonometry Tests
        [TestMethod]
        [Priority(2)]
        public void TrigCosButton()
        {

            page.StandardOperators.NumberPad.Input(180);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.CosButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("-1", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigTanButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(45);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TanButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigSecButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(180);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.SecButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("-1", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigCscButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(90);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.CscButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigCotButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(45);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.CotButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvSinButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.InvSinButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("90", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvTanButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.InvTanButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("45", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvSecButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.NegateButton.Click();
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.InvSecButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("180", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvCscButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.InvCscButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("90", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvCotButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.InvCotButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("45", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigSinhButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.SinhButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.IsTrue(page.CalculatorResults.GetCalculatorResultText().StartsWith("1.175201", StringComparison.Ordinal));

        }

        [TestMethod]
        [Priority(2)]
        public void TrigCoshButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.CoshButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.IsTrue(page.CalculatorResults.GetCalculatorResultText().StartsWith("1.54308", StringComparison.Ordinal));

        }

        [TestMethod]
        [Priority(2)]
        public void TrigSechButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.SechButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.IsTrue(page.CalculatorResults.GetCalculatorResultText().StartsWith("0.64805", StringComparison.Ordinal));

        }

        [TestMethod]
        [Priority(2)]
        public void TrigCschButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.CschButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.IsTrue(page.CalculatorResults.GetCalculatorResultText().StartsWith("0.850918", StringComparison.Ordinal));

        }

        [TestMethod]
        [Priority(2)]
        public void TrigCothButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(45);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.CothButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvSinhButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.InvSinhButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.IsTrue(page.CalculatorResults.GetCalculatorResultText().StartsWith("0.881373", StringComparison.Ordinal));

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvCoshButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.InvCoshButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvTanhButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(0.0);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.InvTanhButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvSechButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.InvSechButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0", page.CalculatorResults.GetCalculatorResultText());

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvCschButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(1);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.InvCschButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.IsTrue(page.CalculatorResults.GetCalculatorResultText().StartsWith("0.881373", StringComparison.Ordinal));

        }

        [TestMethod]
        [Priority(2)]
        public void TrigInvCothButton()
        {
            page.ScientificOperators.SetAngleOperator(AngleOperatorState.Degrees);

            page.StandardOperators.NumberPad.Input(2);
            page.ScientificOperators.TrigButton.Click();
            page.ScientificOperators.TrigShiftButton.Click();
            page.ScientificOperators.HypShiftButton.Click();
            page.ScientificOperators.InvCothButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.IsTrue(page.CalculatorResults.GetCalculatorResultText().StartsWith("0.549306", StringComparison.Ordinal));

        }
        #endregion
    }
}
