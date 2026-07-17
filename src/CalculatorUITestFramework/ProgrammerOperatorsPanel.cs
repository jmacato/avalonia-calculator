// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;

namespace CalculatorUITestFramework
{
    /// <summary>
    /// UI elements unique to programmer mode
    /// </summary>
    public class ProgrammerOperatorsPanel
    {
        private readonly CalculatorDriver driver = CalculatorDriver.Instance;
        private WindowsDriver<WindowsElement> Session => driver.CalculatorSession;
        public NumberPad NumberPad { get; } = new();

        public WindowsElement HexButton => Session.TryFindElementByAccessibilityId("hexButton");
        public WindowsElement DecButton => Session.TryFindElementByAccessibilityId("decimalButton");
        public WindowsElement OctButton => Session.TryFindElementByAccessibilityId("octolButton");
        public WindowsElement BinButton => Session.TryFindElementByAccessibilityId("binaryButton");
        public WindowsElement FullKeypad => Session.TryFindElementByAccessibilityId("fullKeypad");
        public WindowsElement BitFlip => Session.TryFindElementByAccessibilityId("bitFlip");
        public WindowsElement WordButton => Session.TryFindElementByAccessibilityId("wordButton");
        public WindowsElement QWordButton => Session.TryFindElementByAccessibilityId("qwordButton");
        public WindowsElement DWordButton => Session.TryFindElementByAccessibilityId("dwordButton");
        public WindowsElement ByteButton => Session.TryFindElementByAccessibilityId("byteButton");
        public WindowsElement BitwiseButton => Session.TryFindElementByAccessibilityId("bitwiseButton");
        public WindowsElement BitShiftButton => Session.TryFindElementByAccessibilityId("bitShiftButton");
        public WindowsElement AndButton => Session.TryFindElementByAccessibilityId("andButton");
        public WindowsElement NandButton => Session.TryFindElementByAccessibilityId("nandButton");
        public WindowsElement OrButton => Session.TryFindElementByAccessibilityId("orButton");
        public WindowsElement NorButton => Session.TryFindElementByAccessibilityId("norButton");
        public WindowsElement NotButton => Session.TryFindElementByAccessibilityId("notButton");
        public WindowsElement XorButton => Session.TryFindElementByAccessibilityId("xorButton");
        public WindowsElement ArithmeticShiftButton => Session.TryFindElementByAccessibilityId("arithmeticShiftButton");
        public WindowsElement LogicalShiftButton => Session.TryFindElementByAccessibilityId("logicalShiftButton");
        public WindowsElement RotateCircularButton => Session.TryFindElementByAccessibilityId("rotateCircularButton");
        public WindowsElement RotateCarryShiftButton => Session.TryFindElementByAccessibilityId("rotateCarryShiftButton");
        public WindowsElement AButton => Session.TryFindElementByAccessibilityId("aButton");
        public WindowsElement BButton => Session.TryFindElementByAccessibilityId("bButton");
        public WindowsElement CButton => Session.TryFindElementByAccessibilityId("cButton");
        public WindowsElement DButton => Session.TryFindElementByAccessibilityId("dButton");
        public WindowsElement EButton => Session.TryFindElementByAccessibilityId("eButton");
        public WindowsElement FButton => Session.TryFindElementByAccessibilityId("fButton");
        public WindowsElement LeftShiftButton => Session.TryFindElementByAccessibilityId("lshButton");
        public WindowsElement RightShiftButton => Session.TryFindElementByAccessibilityId("rshButton");
        public WindowsElement LeftShiftLogicalButton => Session.TryFindElementByAccessibilityId("lshLogicalButton");
        public WindowsElement RightShiftLogicalButton => Session.TryFindElementByAccessibilityId("rshLogicalButton");
        public WindowsElement RoLButton => Session.TryFindElementByAccessibilityId("rolButton");
        public WindowsElement RoRButton => Session.TryFindElementByAccessibilityId("rorButton");
        public WindowsElement RoRCarryButton => Session.TryFindElementByAccessibilityId("rorCarryButton");
        public WindowsElement Bit0 => Session.TryFindElementByAccessibilityId("Bit0");
        public WindowsElement Bit1 => Session.TryFindElementByAccessibilityId("Bit1");
        public WindowsElement Bit2 => Session.TryFindElementByAccessibilityId("Bit2");
        public WindowsElement Bit3 => Session.TryFindElementByAccessibilityId("Bit3");
        public WindowsElement Bit4 => Session.TryFindElementByAccessibilityId("Bit4");
        public WindowsElement Bit5 => Session.TryFindElementByAccessibilityId("Bit5");
        public WindowsElement Bit6 => Session.TryFindElementByAccessibilityId("Bit6");
        public WindowsElement Bit7 => Session.TryFindElementByAccessibilityId("Bit7");
        public WindowsElement Bit8 => Session.TryFindElementByAccessibilityId("Bit8");
        public WindowsElement Bit9 => Session.TryFindElementByAccessibilityId("Bit9");
        public WindowsElement Bit10 => Session.TryFindElementByAccessibilityId("Bit10");
        public WindowsElement Bit11 => Session.TryFindElementByAccessibilityId("Bit11");
        public WindowsElement Bit12 => Session.TryFindElementByAccessibilityId("Bit12");
        public WindowsElement Bit13 => Session.TryFindElementByAccessibilityId("Bit13");
        public WindowsElement Bit14 => Session.TryFindElementByAccessibilityId("Bit14");
        public WindowsElement Bit15 => Session.TryFindElementByAccessibilityId("Bit15");
        public WindowsElement Bit16 => Session.TryFindElementByAccessibilityId("Bit16");
        public WindowsElement Bit17 => Session.TryFindElementByAccessibilityId("Bit17");
        public WindowsElement Bit18 => Session.TryFindElementByAccessibilityId("Bit18");
        public WindowsElement Bit19 => Session.TryFindElementByAccessibilityId("Bit19");
        public WindowsElement Bit20 => Session.TryFindElementByAccessibilityId("Bit20");
        public WindowsElement Bit21 => Session.TryFindElementByAccessibilityId("Bit21");
        public WindowsElement Bit22 => Session.TryFindElementByAccessibilityId("Bit22");
        public WindowsElement Bit23 => Session.TryFindElementByAccessibilityId("Bit23");
        public WindowsElement Bit24 => Session.TryFindElementByAccessibilityId("Bit24");
        public WindowsElement Bit25 => Session.TryFindElementByAccessibilityId("Bit25");
        public WindowsElement Bit26 => Session.TryFindElementByAccessibilityId("Bit26");
        public WindowsElement Bit27 => Session.TryFindElementByAccessibilityId("Bit27");
        public WindowsElement Bit28 => Session.TryFindElementByAccessibilityId("Bit28");
        public WindowsElement Bit29 => Session.TryFindElementByAccessibilityId("Bit29");
        public WindowsElement Bit30 => Session.TryFindElementByAccessibilityId("Bit30");
        public WindowsElement Bit31 => Session.TryFindElementByAccessibilityId("Bit31");
        public WindowsElement Bit32 => Session.TryFindElementByAccessibilityId("Bit32");
        public WindowsElement Bit33 => Session.TryFindElementByAccessibilityId("Bit33");
        public WindowsElement Bit34 => Session.TryFindElementByAccessibilityId("Bit34");
        public WindowsElement Bit35 => Session.TryFindElementByAccessibilityId("Bit35");
        public WindowsElement Bit36 => Session.TryFindElementByAccessibilityId("Bit36");
        public WindowsElement Bit37 => Session.TryFindElementByAccessibilityId("Bit37");
        public WindowsElement Bit38 => Session.TryFindElementByAccessibilityId("Bit38");
        public WindowsElement Bit39 => Session.TryFindElementByAccessibilityId("Bit39");
        public WindowsElement Bit40 => Session.TryFindElementByAccessibilityId("Bit40");
        public WindowsElement Bit41 => Session.TryFindElementByAccessibilityId("Bit41");
        public WindowsElement Bit42 => Session.TryFindElementByAccessibilityId("Bit42");
        public WindowsElement Bit43 => Session.TryFindElementByAccessibilityId("Bit43");
        public WindowsElement Bit44 => Session.TryFindElementByAccessibilityId("Bit44");
        public WindowsElement Bit45 => Session.TryFindElementByAccessibilityId("Bit45");
        public WindowsElement Bit46 => Session.TryFindElementByAccessibilityId("Bit46");
        public WindowsElement Bit47 => Session.TryFindElementByAccessibilityId("Bit47");
        public WindowsElement Bit48 => Session.TryFindElementByAccessibilityId("Bit48");
        public WindowsElement Bit49 => Session.TryFindElementByAccessibilityId("Bit49");
        public WindowsElement Bit50 => Session.TryFindElementByAccessibilityId("Bit50");
        public WindowsElement Bit51 => Session.TryFindElementByAccessibilityId("Bit51");
        public WindowsElement Bit52 => Session.TryFindElementByAccessibilityId("Bit52");
        public WindowsElement Bit53 => Session.TryFindElementByAccessibilityId("Bit53");
        public WindowsElement Bit54 => Session.TryFindElementByAccessibilityId("Bit54");
        public WindowsElement Bit55 => Session.TryFindElementByAccessibilityId("Bit55");
        public WindowsElement Bit56 => Session.TryFindElementByAccessibilityId("Bit56");
        public WindowsElement Bit57 => Session.TryFindElementByAccessibilityId("Bit57");
        public WindowsElement Bit58 => Session.TryFindElementByAccessibilityId("Bit58");
        public WindowsElement Bit59 => Session.TryFindElementByAccessibilityId("Bit59");
        public WindowsElement Bit60 => Session.TryFindElementByAccessibilityId("Bit60");
        public WindowsElement Bit61 => Session.TryFindElementByAccessibilityId("Bit61");
        public WindowsElement Bit62 => Session.TryFindElementByAccessibilityId("Bit62");
        public WindowsElement Bit63 => Session.TryFindElementByAccessibilityId("Bit63");

        public void SetArithmeticShift()
        {
            BitShiftButton.Click();
            if (this.ArithmeticShiftButton.GetAttribute("isEnabled") != "True")
            {
                ArithmeticShiftButton.Click();
            }
            else
            {
                BitShiftButton.Click();
            }
        }
        public void SetLogicalShift()
        {
            BitShiftButton.Click();
            if (this.LogicalShiftButton.GetAttribute("isEnabled") != "True")
            {
                LogicalShiftButton.Click();
            }
            else
            {
                BitShiftButton.Click();
            }
        }
        public void SetRotateCircularShift()
        {
            BitShiftButton.Click();
            if (this.RotateCircularButton.GetAttribute("isEnabled") != "True")
            {
                RotateCircularButton.Click();
            }
            else
            {
                BitShiftButton.Click();
            }
        }
        public void SetRotateThroughCarryCircularShift()
        {
            BitShiftButton.Click();
            if (this.RotateCarryShiftButton.GetAttribute("isEnabled") != "True")
            {
                RotateCarryShiftButton.Click();
            }
            else
            {
                BitShiftButton.Click();
            }
        }
        public void ResetWordSize()
        {
            string source = Session.PageSource;
            if (source.Contains("qwordButton", System.StringComparison.Ordinal))
            {
                return;
            }
            else if (source.Contains("dwordButton", System.StringComparison.Ordinal))
            {
                DWordButton.Click();
                ResetWordSize();
            }
            else if (source.Contains("wordButton", System.StringComparison.Ordinal))
            {
                WordButton.Click();
                ResetWordSize();
            }
            else if (source.Contains("byteButton", System.StringComparison.Ordinal))
            {
                ByteButton.Click();
                ResetWordSize();
            }
            else
            {
                throw new NotFoundException("Could not find word size buttons in page source");
            }
        }
        public void ResetNumberSystem()
        {
            DecButton.Click();
        }
    }
}
