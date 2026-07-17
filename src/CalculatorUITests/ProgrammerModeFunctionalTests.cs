// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorUITestFramework;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using OpenQA.Selenium;

using System;

namespace CalculatorUITests
{
    [TestClass]
    public sealed class ProgrammerModeFunctionalTests
    {
        private static readonly ProgrammerCalculatorPage page = new ProgrammerCalculatorPage();

        /// <summary>
        /// Initializes the WinAppDriver web driver session.
        /// </summary>
        /// <param name="context"></param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Create session to launch a Calculator window
            CalculatorDriver.Instance.SetupCalculatorSession(context);

            // Ensure that calculator is in programmer mode
            page.NavigateToProgrammerCalculator();

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
        /// Ensures the calculator is in a cleared state with the correct default options
        /// </summary>
        [TestInitialize]
        public void TestInit()
        {
            if ("0" != page.CalculatorResults.GetCalculatorResultText())
            {
                page.ClearAll();
            }
            CalculatorApp.EnsureCalculatorHasFocus();
            page.ProgrammerOperators.SetArithmeticShift();
            page.ProgrammerOperators.ResetWordSize();
            page.ProgrammerOperators.ResetNumberSystem();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            page.ClearAll();
        }

        /// <summary>
        /// Arithmetic shift, decimal notation, QWord
        /// </summary>
        #region Smoke Tests
        [TestMethod]
        [Priority(0)]
        public void SmokeTestArithmeticDecimalLeftShift()
        {
            page.StandardOperators.NumberPad.Input(5);
            page.ProgrammerOperators.LeftShiftButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("10", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestArithmeticDecimalRightShift()
        {
            page.StandardOperators.NumberPad.Input(25);
            page.StandardOperators.NegateButton.Click();
            page.ProgrammerOperators.RightShiftButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("-13", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestArithmeticDecimalAnd()
        {
            page.StandardOperators.NumberPad.Input(25);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.AndButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestArithmeticDecimalNand()
        {
            page.StandardOperators.NumberPad.Input(25);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NandButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("-2", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestArithmeticDecimalOr()
        {
            page.StandardOperators.NumberPad.Input(25);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.OrButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("31", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestArithmeticDecimalNor()
        {
            page.StandardOperators.NumberPad.Input(25);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NorButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("-32", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestArithmeticDecimalNot()
        {
            page.StandardOperators.NumberPad.Input(25);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NotButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("-26", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(0)]
        public void SmokeTestArithmeticDecimalXor()
        {
            page.StandardOperators.NumberPad.Input(25);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.XorButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("30", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Checks for the highest value bit that should be enabled according to the Word size setting
        /// </summary>
        [TestMethod]
        [Priority(0)]
        public void SmokeTestWordSize()
        {
            page.ProgrammerOperators.BitFlip.Click();
            if (page.ProgrammerOperators.Bit63.GetAttribute("IsEnabled") != "True")
            {
                throw new NotFoundException("According to the current Word size setting, some bits that should be enabled are disabled");
            }
            else
            {
                page.ProgrammerOperators.QWordButton.Click();
                if (page.ProgrammerOperators.Bit31.GetAttribute("IsEnabled") != "True")
                {
                    throw new NotFoundException("According to the current Word size setting, some bits that should be enabled are disabled");
                }
                else
                {
                    page.ProgrammerOperators.DWordButton.Click();
                    if (page.ProgrammerOperators.Bit15.GetAttribute("IsEnabled") != "True")
                    {
                        throw new NotFoundException("According to the current Word size setting, some bits that should be enabled are disabled");
                    }
                    else
                    {
                        page.ProgrammerOperators.WordButton.Click();
                        if (page.ProgrammerOperators.Bit7.GetAttribute("IsEnabled") != "True")
                        {
                            throw new NotFoundException("According to the current Word size setting, some bits that should be enabled are disabled");
                        }
                        else
                        {
                            page.ProgrammerOperators.ByteButton.Click();
                            if (page.ProgrammerOperators.Bit63.GetAttribute("IsEnabled") != "True")
                            {
                                throw new NotFoundException("According to the current Word size setting, some bits that should be enabled are disabled");
                            }
                        }
                    }
                }
                page.ProgrammerOperators.FullKeypad.Click();
            }
        }


        /// <summary>
        /// Toggles each bit on and off
        /// </summary>
        [TestMethod]
        [Priority(0)]
        public void SmokeTestBitFlipKeypad()
        {
            page.ProgrammerOperators.BitFlip.Click();
            page.ProgrammerOperators.Bit63.Click();
            Assert.AreEqual("-9,223,372,036,854,775,808", page.CalculatorResults.GetCalculatorResultText());
            page.ProgrammerOperators.FullKeypad.Click();
        }
        #endregion

        /// <summary>
        /// Arithmetic shift, octal notation, QWord
        /// </summary>
        #region Arithmetic logic operators
        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorOctalLeftShift()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.ProgrammerOperators.LeftShiftButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 6", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorOctalRightShift()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.StandardOperators.NumberPad.Input(25);
            page.StandardOperators.NegateButton.Click();
            page.ProgrammerOperators.RightShiftButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1  7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 6 5", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorOctalAnd()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.StandardOperators.NumberPad.Input(16);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.AndButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("6", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorOctalNand()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.StandardOperators.NumberPad.Input(16);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NandButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1  7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 7 1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorOctalOr()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.StandardOperators.NumberPad.Input(16);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.OrButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 7", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorOctalNor()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.StandardOperators.NumberPad.Input(16);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NorButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1  7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 6 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorOctalNot()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.StandardOperators.NumberPad.Input(16);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NotButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1  7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 6 1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorOctalXor()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.StandardOperators.NumberPad.Input(16);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.XorButton.Click();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 1", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Arithmetic shift, binary notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorBinaryLeftShift()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.LeftShiftButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0 0 0 1  0 1 0 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorBinaryRightShift()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.RightShiftButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0 1 0 1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorBinaryAnd()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.AndButton.Click();
            page.StandardOperators.NumberPad.Input(1000);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 0 0 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorBinaryNand()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NandButton.Click();
            page.StandardOperators.NumberPad.Input(1000);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  0 1 1 1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorBinaryOr()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.OrButton.Click();
            page.StandardOperators.NumberPad.Input(1100);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 1 1 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorBinaryNor()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NorButton.Click();
            page.StandardOperators.NumberPad.Input(1100);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  0 0 0 1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorBinaryNot()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NotButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  0 1 0 1", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorBinaryXor()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.XorButton.Click();
            page.StandardOperators.NumberPad.Input(1100);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0 1 1 0", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Arithmetic shift, hexadecimal notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorHexLeftShift()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.AButton.Click();
            page.ProgrammerOperators.LeftShiftButton.Click();
            page.ProgrammerOperators.BButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("5 0 0 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorHexRightShift()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.FButton.Click();
            page.ProgrammerOperators.RightShiftButton.Click();
            page.StandardOperators.NumberPad.Input(2);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("3", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorHexAnd()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.AButton.Click();
            page.ProgrammerOperators.BButton.Click();
            page.ProgrammerOperators.CButton.Click();
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.AndButton.Click();
            page.ProgrammerOperators.FButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("C", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorHexNand()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.AButton.Click();
            page.ProgrammerOperators.BButton.Click();
            page.ProgrammerOperators.CButton.Click();
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NandButton.Click();
            page.ProgrammerOperators.FButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("F F F F  F F F F  F F F F  F F F 3", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorHexOr()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.AButton.Click();
            page.ProgrammerOperators.BButton.Click();
            page.ProgrammerOperators.CButton.Click();
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.OrButton.Click();
            page.ProgrammerOperators.FButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("A B F", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorHexNor()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.AButton.Click();
            page.ProgrammerOperators.BButton.Click();
            page.ProgrammerOperators.CButton.Click();
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NorButton.Click();
            page.ProgrammerOperators.FButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("F F F F  F F F F  F F F F  F 5 4 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorHexNot()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.AButton.Click();
            page.ProgrammerOperators.BButton.Click();
            page.ProgrammerOperators.CButton.Click();
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.NotButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("F F F F  F F F F  F F F F  F 5 4 3", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ArithmeticOperatorHexXor()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.AButton.Click();
            page.ProgrammerOperators.BButton.Click();
            page.ProgrammerOperators.CButton.Click();
            page.ProgrammerOperators.BitwiseButton.Click();
            page.ProgrammerOperators.XorButton.Click();
            page.ProgrammerOperators.FButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("A B 3", page.CalculatorResults.GetCalculatorResultText());
        }
        #endregion

        /// <summary>
        /// Logical shift, decimal notation, QWord - AND/NAND/OR/NOR/NOT/XOR will always be the same regardless of what shift setting is selected, so the previous section should have those covered
        /// </summary>
        #region Logical-shift operators
        [TestMethod]
        [Priority(1)]
        public void LogicalOperatorDecimalLeftShift()
        {
            page.ProgrammerOperators.DecButton.Click();
            page.ProgrammerOperators.SetLogicalShift();
            page.StandardOperators.NumberPad.Input(7);
            page.ProgrammerOperators.LeftShiftLogicalButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("14", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void LogicalOperatorDecimalRightShift()
        {
            page.ProgrammerOperators.DecButton.Click();
            page.ProgrammerOperators.SetLogicalShift();
            page.StandardOperators.NumberPad.Input(16);
            page.StandardOperators.NegateButton.Click();
            page.ProgrammerOperators.RightShiftLogicalButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("9,223,372,036,854,775,800", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Logical shift, octal notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void LogicalOperatorOctalLeftShift()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.ProgrammerOperators.SetLogicalShift();
            page.StandardOperators.NumberPad.Input(7);
            page.ProgrammerOperators.LeftShiftLogicalButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 6", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void LogicalOperatorOctalRightShift()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.ProgrammerOperators.SetLogicalShift();
            page.StandardOperators.NumberPad.Input(16);
            page.StandardOperators.NegateButton.Click();
            page.ProgrammerOperators.RightShiftLogicalButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 7 7  7 7 1", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Logical shift, binary notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void LogicalOperatorBinaryLeftShift()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.ProgrammerOperators.SetLogicalShift();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.LeftShiftLogicalButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0 0 0 1  0 1 0 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void LogicalOperatorBinaryRightShift()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.ProgrammerOperators.SetLogicalShift();
            page.StandardOperators.NumberPad.Input(1010);
            page.StandardOperators.NegateButton.Click();
            page.ProgrammerOperators.RightShiftLogicalButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.IsTrue(string.Equals(page.CalculatorResults.GetCalculatorResultText(), "0 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 1 1 1  1 0 1 1", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Logical shift, hexadecimal notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void LogicalOperatorHexLeftShift()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.SetLogicalShift();
            page.ProgrammerOperators.AButton.Click();
            page.ProgrammerOperators.LeftShiftLogicalButton.Click();
            page.ProgrammerOperators.BButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("5 0 0 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void LogicalOperatorHexRightShift()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.SetLogicalShift();
            page.ProgrammerOperators.FButton.Click();
            page.StandardOperators.NegateButton.Click();
            page.ProgrammerOperators.RightShiftLogicalButton.Click();
            page.StandardOperators.NumberPad.Input(1);
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("7 F F F  F F F F  F F F F  F F F 8", page.CalculatorResults.GetCalculatorResultText());
        }
        #endregion

        /// <summary>
        /// Rotate circular shift, decimal notation, QWord
        /// </summary>
        #region Rotate-Circular-shift operators
        [TestMethod]
        [Priority(1)]
        public void CircularOperatorDecimalLeftShift()
        {
            page.ProgrammerOperators.SetRotateCircularShift();
            page.StandardOperators.NumberPad.Input(7);
            page.ProgrammerOperators.RoLButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("14", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void CircularOperatorDecimalRightShift()
        {
            page.ProgrammerOperators.SetRotateCircularShift();
            page.StandardOperators.NumberPad.Input(16);
            page.StandardOperators.NegateButton.Click();
            page.ProgrammerOperators.RoRButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("9,223,372,036,854,775,800", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Rotate circular shift, octal notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void CircularOperatorOctalLeftShift()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.ProgrammerOperators.SetRotateCircularShift();
            page.StandardOperators.NumberPad.Input(25);
            page.ProgrammerOperators.RoLButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("5 2", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void CircularOperatorOctalRightShift()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.ProgrammerOperators.SetRotateCircularShift();
            page.StandardOperators.NumberPad.Input(25);
            page.ProgrammerOperators.RoRButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1  0 0 0  0 0 0  0 0 0  0 0 0  0 0 0  0 0 0  0 1 2", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Rotate circular shift, binary notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void CircularOperatorBinaryLeftShift()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.ProgrammerOperators.SetRotateCircularShift();
            page.StandardOperators.NumberPad.Input(1011);
            page.ProgrammerOperators.RoLButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0 0 0 1  0 1 1 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void CircularOperatorBinaryRightShift()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.ProgrammerOperators.SetRotateCircularShift();
            page.StandardOperators.NumberPad.Input(1011);
            page.ProgrammerOperators.RoRButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 0 0 0  0 1 0 1", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Rotate circular shift, hexadecimal notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void CircularOperatorHexLeftShift()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.SetRotateCircularShift();
            page.ProgrammerOperators.AButton.Click();
            page.ProgrammerOperators.RoLButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("1 4", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void CircularOperatorHexRightShift()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.SetRotateCircularShift();
            page.ProgrammerOperators.FButton.Click();
            page.ProgrammerOperators.RoRButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("8 0 0 0  0 0 0 0  0 0 0 0  0 0 0 7", page.CalculatorResults.GetCalculatorResultText());
        }
        #endregion

        /// <summary>
        /// Rotate through carry circular shift, decimal notation, QWord
        /// </summary>
        #region Rotate-Through-Carry-Circular-shift operators
        [TestMethod]
        [Priority(1)]
        public void ThroughCarryOperatorDecimalLeftShift()
        {
            page.ProgrammerOperators.SetRotateThroughCarryCircularShift();
            page.StandardOperators.NumberPad.Input(7);
            page.StandardOperators.NegateButton.Click();
            page.ProgrammerOperators.RoLButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("-14", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ThroughCarryOperatorDecimalRightShift()
        {
            page.ProgrammerOperators.SetRotateThroughCarryCircularShift();
            page.StandardOperators.NumberPad.Input(17);
            page.ProgrammerOperators.RoRCarryButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("8", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Rotate through carry circular shift, octal notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void ThroughCarryOperatorOctalLeftShift()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.ProgrammerOperators.SetRotateThroughCarryCircularShift();
            page.StandardOperators.NumberPad.Input(111);
            page.ProgrammerOperators.RoLButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("2 2 2", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ThroughCarryOperatorOctalRightShift()
        {
            page.ProgrammerOperators.OctButton.Click();
            page.ProgrammerOperators.SetRotateThroughCarryCircularShift();
            page.StandardOperators.NumberPad.Input(111);
            page.ProgrammerOperators.RoRCarryButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("4 4", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Rotate through carry circular shift, binary notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void ThroughCarryOperatorBinaryLeftShift()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.ProgrammerOperators.SetRotateThroughCarryCircularShift();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.RoLButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0 0 0 1  0 1 0 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ThroughCarryOperatorBinaryRightShift()
        {
            page.ProgrammerOperators.BinButton.Click();
            page.ProgrammerOperators.SetRotateThroughCarryCircularShift();
            page.StandardOperators.NumberPad.Input(1011);
            page.ProgrammerOperators.RoRCarryButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("0 1 0 1", page.CalculatorResults.GetCalculatorResultText());
        }

        /// <summary>
        /// Rotate through carry circular shift, hexadecimal notation, QWord
        /// </summary>
        [TestMethod]
        [Priority(1)]
        public void ThroughCarryOperatorHexLeftShift()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.SetRotateThroughCarryCircularShift();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.RoLButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("2 0 2 0", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void ThroughCarryOperatorHexRightShift()
        {
            page.ProgrammerOperators.HexButton.Click();
            page.ProgrammerOperators.SetRotateThroughCarryCircularShift();
            page.StandardOperators.NumberPad.Input(1010);
            page.ProgrammerOperators.RoRCarryButton.Click();
            page.StandardOperators.EqualButton.Click();
            Assert.AreEqual("8 0 8", page.CalculatorResults.GetCalculatorResultText());
        }
        #endregion

        /// <summary>
        /// Copy and Paste the numbers into/from the calculator
        /// </summary>
        #region Copy-Paste operations
        [TestMethod]
        [Priority(1)]
        public void CopyAndPasteSimpleNumber()
        {
            page.ProgrammerOperators.BitFlip.Click();
            page.ProgrammerOperators.Bit1.Click();
            CalculatorApp.Window.SendKeys(Keys.Control + "c" + Keys.Control);
            page.ProgrammerOperators.FullKeypad.Click();
            page.StandardOperators.ClearEntryButton.Click();
            CalculatorApp.Window.SendKeys(Keys.Control + "v" + Keys.Control);
            Assert.AreEqual("2", page.CalculatorResults.GetCalculatorResultText());
        }

        [TestMethod]
        [Priority(1)]
        public void CopyAndPasteInvalidNumber()
        {
            page.ProgrammerOperators.BitFlip.Click();
            page.ProgrammerOperators.Bit63.Click();
            CalculatorApp.Window.SendKeys(Keys.Control + "c" + Keys.Control);
            page.ProgrammerOperators.FullKeypad.Click();
            page.StandardOperators.ClearEntryButton.Click();
            page.ProgrammerOperators.QWordButton.Click();
            CalculatorApp.Window.SendKeys(Keys.Control + "v" + Keys.Control);
            Assert.AreEqual("Invalid input", page.CalculatorResults.GetCalculatorResultText());
        }
        #endregion
    }
}
