using CalcEngine;
using CalculationManager;

namespace CalcEngineTests;

public class CalculatorManagerTest
{
    private class CalculatorManagerDisplayTester : ICalcDisplay
    {
        private bool m_isError;
        private int m_maxDigitsCalledCount;
        private int m_binaryOperatorReceivedCallCount;
        private string m_primaryDisplay;
        private string m_expression;
        private uint m_parenDisplay;
        private List<string> m_memorizedNumberStrings;

        public CalculatorManagerDisplayTester()
        {
            Reset();
        }

        public void Reset()
        {
            m_isError = false;
            m_maxDigitsCalledCount = 0;
            m_binaryOperatorReceivedCallCount = 0;
            m_memorizedNumberStrings = new List<string>();
        }

        public void SetPrimaryDisplay(string text, bool isError)
        {
            m_primaryDisplay = text;
            m_isError = isError;
        }

        public void SetIsInError(bool isError)
        {
            m_isError = isError;
        }

        public void SetExpressionDisplay(List<(string, int)> tokens, List<IExpressionCommand> commands)
        {
            m_expression = "";

            foreach (var currentPair in tokens)
            {
                m_expression += currentPair.Item1;
            }
        }

        public void SetMemorizedNumbers(List<string> numbers)
        {
            m_memorizedNumberStrings = numbers;
        }

        public void SetParenthesisNumber(uint parenthesisCount)
        {
            m_parenDisplay = parenthesisCount;
        }

        public void OnNoRightParenAdded()
        {
            // This method is used to create a narrator announcement when a close parenthesis cannot be added because there are no open parentheses
        }

        public string GetPrimaryDisplay()
        {
            return m_primaryDisplay;
        }

        public string GetExpression()
        {
            return m_expression;
        }

        public List<string> GetMemorizedNumbers()
        {
            return m_memorizedNumberStrings;
        }

        public bool GetIsError()
        {
            return m_isError;
        }

        public void OnHistoryItemAdded(uint addedItemIndex)
        {
        }

        public void MaxDigitsReached()
        {
            m_maxDigitsCalledCount++;
        }

        public void InputChanged()
        {
        }

        public int GetMaxDigitsCalledCount()
        {
            return m_maxDigitsCalledCount;
        }

        public void BinaryOperatorReceived()
        {
            m_binaryOperatorReceivedCallCount++;
        }

        public void MemoryItemChanged(uint indexOfMemory)
        {
        }

        public int GetBinaryOperatorReceivedCallCount()
        {
            return m_binaryOperatorReceivedCallCount;
        }
    }


    private class TestDriver
    {
        private static CalculatorManagerDisplayTester m_displayTester;
        private static CalculationManager.CalculatorManager m_calculatorManager;

        public static void Initialize(CalculatorManagerDisplayTester displayTester,
            CalculationManager.CalculatorManager calculatorManager)
        {
            m_displayTester = displayTester;
            m_calculatorManager = calculatorManager;
        }

        public static void Test(string expectedPrimary, string expectedExpression, Command[] testCommands,
            bool cleanup = true, bool isScientific = false)
        {
            if (cleanup)
            {
                m_calculatorManager.Reset();
            }

            if (isScientific)
            {
                m_calculatorManager.SendCommand(Command.ModeScientific);
            }

            var i = 0;
            while (testCommands[i] != Command.CommandNULL)
            {
                m_calculatorManager.SendCommand(testCommands[i++]);
            }

            Assert.Equal(expectedPrimary, m_displayTester.GetPrimaryDisplay());
            if (expectedExpression != "N/A")
            {
                Assert.Equal(expectedExpression, m_displayTester.GetExpression());
            }
        }
    }

    private CalculatorManagerDisplayTester m_calculatorDisplayTester;
    private IResourceProvider m_resourceProvider;
    private CalculationManager.CalculatorManager m_calculatorManager;

    public CalculatorManagerTest()
    {
        m_calculatorDisplayTester = new CalculatorManagerDisplayTester();
        m_resourceProvider = new DefaultResourceProvider();
        m_calculatorManager = new CalculatorManager(m_calculatorDisplayTester, m_resourceProvider);
        TestDriver.Initialize(m_calculatorDisplayTester, m_calculatorManager);
    }

    private void ExecuteCommands(Command[] commands)
    {
        var i = 0;
        while (commands[i] != Command.CommandNULL)
        {
            m_calculatorManager.SendCommand(commands[i++]);
        }
    }

    private void ExecuteCommands(List<Command> commands)
    {
        foreach (var command in commands)
        {
            if (command == Command.CommandNULL)
            {
                break;
            }

            m_calculatorManager.SendCommand(command);
        }
    }

    private void TestMaxDigitsReachedScenario(string constInput)
    {
        // Make sure we're in a clean state.
        Assert.Equal(0, m_calculatorDisplayTester.GetMaxDigitsCalledCount());

        var commands = CommandListFromStringInput(constInput);
        Assert.NotEmpty(commands);

        // The last element in the list should always cause MaxDigitsReached
        // Remember the command but remove from the actual input that is sent
        var finalInput = commands[commands.Count - 1];
        commands.RemoveAt(commands.Count - 1);
        var input = constInput.Substring(0, constInput.Length - 1);

        m_calculatorManager.SetStandardMode();
        ExecuteCommands(commands);

        var expectedDisplay = input;
        var display = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal(expectedDisplay, display);

        m_calculatorManager.SendCommand(finalInput);

        // Verify MaxDigitsReached
        display = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal(expectedDisplay, display);

        // MaxDigitsReached should have been called once
        Assert.True(m_calculatorDisplayTester.GetMaxDigitsCalledCount() > 0);
    }

    private List<Command> CommandListFromStringInput(string input)
    {
        var result = new List<Command>();
        foreach (var ch in input)
        {
            var asCommand = Command.CommandNULL;
            if (ch == '.')
            {
                asCommand = Command.CommandPNT;
            }
            else if ('0' <= ch && ch <= '9')
            {
                var diff = (int)ch - (int)'0';
                asCommand = (Command)((int)Command.Command0 + diff);
            }

            if (asCommand != Command.CommandNULL)
            {
                result.Add(asCommand);
            }
        }

        return result;
    }

    private void Cleanup()
    {
        m_calculatorManager.Reset();
        m_calculatorDisplayTester.Reset();
    }

    [Fact]
    public void CalculatorManagerTestStandard()
    {
        Command[] commands1 =
        {
            Command.Command1, Command.Command2, Command.Command3, Command.CommandPNT,
            Command.Command4, Command.Command5, Command.Command6, Command.CommandNULL
        };
        TestDriver.Test("123.456", "", commands1);

        Command[] commands2 = { Command.CommandADD, Command.CommandNULL };
        TestDriver.Test("0", "0 + ", commands2);

        Command[] commands3 = { Command.CommandSQRT, Command.CommandNULL };
        TestDriver.Test("0", "\x221A(0)", commands3);

        Command[] commands4 =
        {
            Command.Command2, Command.CommandADD, Command.Command3, Command.CommandEQU,
            Command.Command4, Command.CommandEQU, Command.CommandNULL
        };
        TestDriver.Test("7", "4 + 3=", commands4);

        Command[] commands5 = { Command.Command4, Command.CommandEQU, Command.CommandNULL };
        TestDriver.Test("4", "4=", commands5);

        Command[] commands6 =
        {
            Command.Command2, Command.Command5, Command.Command6, Command.CommandSQRT,
            Command.CommandSQRT, Command.CommandSQRT, Command.CommandNULL
        };
        TestDriver.Test("2", "\x221A(\x221A(\x221A(256)))", commands6);

        Command[] commands7 =
        {
            Command.Command3, Command.CommandSUB, Command.Command6, Command.CommandEQU,
            Command.CommandMUL, Command.Command3, Command.CommandEQU, Command.CommandNULL
        };
        TestDriver.Test("-9", "-3 \x00D7 3=", commands7);

        Command[] commands8 =
        {
            Command.Command9, Command.CommandMUL, Command.Command6, Command.CommandSUB,
            Command.CommandCENTR, Command.Command8, Command.CommandEQU, Command.CommandNULL
        };
        TestDriver.Test("46", "54 - 8=", commands8);

        Command[] commands9 =
        {
            Command.Command6, Command.CommandMUL, Command.Command6, Command.CommandPERCENT, Command.CommandEQU,
            Command.CommandNULL
        };
        TestDriver.Test("0.36", "6 \x00D7 0.06=", commands9);

        Command[] commands10 =
        {
            Command.Command5, Command.Command0, Command.CommandADD, Command.Command2,
            Command.Command0, Command.CommandPERCENT, Command.CommandEQU, Command.CommandNULL
        };
        TestDriver.Test("60", "50 + 10=", commands10);
    }

    [Fact]
    public void CalculatorManagerTestScientific()
    {
        Command[] commands1 =
        {
            Command.Command1, Command.Command2, Command.Command3, Command.CommandPNT,
            Command.Command4, Command.Command5, Command.Command6, Command.CommandNULL
        };
        TestDriver.Test("123.456", "", commands1, true, true);

        Command[] commands2 = { Command.CommandADD, Command.CommandNULL };
        TestDriver.Test("0", "0 + ", commands2, true, true);

        Command[] commands3 = { Command.CommandSQRT, Command.CommandNULL };
        TestDriver.Test("0", "\x221A(0)", commands3, true, true);

        Command[] commands4 =
        {
            Command.Command1, Command.CommandADD, Command.Command0, Command.CommandMUL,
            Command.Command2, Command.CommandEQU, Command.CommandNULL
        };
        TestDriver.Test("1", "1 + 0 \x00D7 2=", commands4, true, true);

        Command[] commands5 = { Command.Command4, Command.CommandEQU, Command.CommandNULL };
        TestDriver.Test("4", "4=", commands5, true, true);

        Command[] commands6 =
        {
            Command.Command2, Command.Command5, Command.Command6, Command.CommandSQRT,
            Command.CommandSQRT, Command.CommandSQRT, Command.CommandNULL
        };
        TestDriver.Test("2", "\x221A(\x221A(\x221A(256)))", commands6, true, true);

        Command[] commands7 =
        {
            Command.Command3, Command.CommandSUB, Command.Command6, Command.CommandEQU,
            Command.CommandMUL, Command.Command3, Command.CommandADD, Command.CommandNULL
        };
        TestDriver.Test("-9", "-3 \x00D7 3 + ", commands7, true, true);

        Command[] commands8 =
        {
            Command.Command9, Command.CommandMUL, Command.Command6, Command.CommandSUB, Command.CommandCENTR,
            Command.Command8, Command.CommandMUL, Command.Command2, Command.CommandADD, Command.CommandNULL
        };
        TestDriver.Test("38", "9 \x00D7 6 - 8 \x00D7 2 + ", commands8, true, true);

        Command[] commands9 =
        {
            Command.Command6, Command.CommandMUL, Command.Command6, Command.CommandSIGN, Command.CommandSQRT,
            Command.CommandNULL
        };
        TestDriver.Test("Invalid input", "6 \x00D7 \x221A(-6)", commands9, true, true);

        Command[] commands10 =
        {
            Command.Command5, Command.Command0, Command.CommandADD, Command.Command2,
            Command.Command0, Command.CommandREC, Command.CommandSUB, Command.CommandNULL
        };
        TestDriver.Test("50.05", "50 + 1/(20) - ", commands10, true, true);
    }

    [Fact]
    public void CalculatorManagerTestScientificParenthesis()
    {
        Command[] commands1 =
        {
            Command.Command1, Command.CommandADD, Command.CommandOPENP, Command.CommandADD,
            Command.Command3, Command.CommandCLOSEP, Command.CommandNULL
        };
        TestDriver.Test("3", "1 + (0 + 3)", commands1, true, true);

        Command[] commands2 =
        {
            Command.CommandOPENP, Command.CommandOPENP, Command.Command1, Command.Command2, Command.CommandCLOSEP,
            Command.CommandNULL
        };
        TestDriver.Test("12", "((12)", commands2, true, true);

        Command[] commands3 =
        {
            Command.Command1, Command.Command2, Command.CommandCLOSEP,
            Command.CommandCLOSEP, Command.CommandOPENP, Command.CommandNULL
        };
        TestDriver.Test("12", "12 \x00D7 (", commands3, true, true);

        Command[] commands4 =
        {
            Command.Command2, Command.CommandOPENP, Command.Command2, Command.CommandCLOSEP, Command.CommandADD,
            Command.CommandNULL
        };
        TestDriver.Test("4", "2 \x00D7 (2) + ", commands4, true, true);

        Command[] commands5 =
        {
            Command.Command2, Command.CommandOPENP, Command.Command2, Command.CommandCLOSEP,
            Command.CommandADD, Command.CommandEQU, Command.CommandNULL
        };
        TestDriver.Test("8", "2 \x00D7 (2) + 4=", commands5, true, true);
    }


    [Fact]
    public void CalculatorManagerTestScientificError()
    {
        Command[] commands1 =
            { Command.Command1, Command.CommandDIV, Command.Command0, Command.CommandEQU, Command.CommandNULL };
        TestDriver.Test("Cannot divide by zero", "1 \x00F7 ", commands1, true, true);
        Assert.True(m_calculatorDisplayTester.GetIsError());

        Command[] commands2 = { Command.Command2, Command.CommandSIGN, Command.CommandLOG, Command.CommandNULL };
        TestDriver.Test("Invalid input", "log(-2)", commands2, true, true);
        Assert.True(m_calculatorDisplayTester.GetIsError());

        Command[] commands3 =
            { Command.Command0, Command.CommandDIV, Command.Command0, Command.CommandEQU, Command.CommandNULL };
        TestDriver.Test("Result is undefined", "0 \x00F7 ", commands3, true, true);
        Assert.True(m_calculatorDisplayTester.GetIsError());

        // Do the same tests for the basic calculator
        TestDriver.Test("Cannot divide by zero", "1 \x00F7 ", commands1);
        Assert.True(m_calculatorDisplayTester.GetIsError());
        TestDriver.Test("Invalid input", "log(-2)", commands2);
        Assert.True(m_calculatorDisplayTester.GetIsError());
        TestDriver.Test("Result is undefined", "0 \x00F7 ", commands3);
        Assert.True(m_calculatorDisplayTester.GetIsError());
    }

    [Fact]
    public void CalculatorManagerTestScientificModeChange()
    {
        Command[] commands1 = { Command.CommandRAD, Command.CommandPI, Command.CommandSIN, Command.CommandNULL };
        TestDriver.Test("0", "N/A", commands1, true, true);

        Command[] commands2 = { Command.CommandRAD, Command.CommandPI, Command.CommandCOS, Command.CommandNULL };
        TestDriver.Test("-1", "N/A", commands2, true, true);

        Command[] commands3 = { Command.CommandRAD, Command.CommandPI, Command.CommandTAN, Command.CommandNULL };
        TestDriver.Test("0", "N/A", commands3, true, true);

        Command[] commands4 =
        {
            Command.CommandGRAD, Command.Command4, Command.Command0, Command.Command0, Command.CommandSIN,
            Command.CommandNULL
        };
        TestDriver.Test("0", "N/A", commands4, true, true);

        Command[] commands5 =
        {
            Command.CommandGRAD, Command.Command4, Command.Command0, Command.Command0, Command.CommandCOS,
            Command.CommandNULL
        };
        TestDriver.Test("1", "N/A", commands5, true, true);

        Command[] commands6 =
        {
            Command.CommandGRAD, Command.Command4, Command.Command0, Command.Command0, Command.CommandTAN,
            Command.CommandNULL
        };
        TestDriver.Test("0", "N/A", commands6, true, true);
    }

    [Fact]
    public void CalculatorManagerTestModeChange()
    {
        Command[] commands1 = { Command.Command1, Command.Command2, Command.Command3, Command.CommandNULL };
        TestDriver.Test("123", "", commands1, true, false);

        Command[] commands2 = { Command.ModeScientific, Command.CommandNULL };
        TestDriver.Test("0", "", commands2, true, false);

        Command[] commands3 = { Command.Command1, Command.Command2, Command.Command3, Command.CommandNULL };
        TestDriver.Test("123", "", commands3, true, false);

        Command[] commands4 = { Command.ModeProgrammer, Command.CommandNULL };
        TestDriver.Test("0", "", commands4, true, false);

        Command[] commands5 = { Command.Command1, Command.Command2, Command.Command3, Command.CommandNULL };
        TestDriver.Test("123", "", commands5, true, false);

        Command[] commands6 = { Command.ModeScientific, Command.CommandNULL };
        TestDriver.Test("0", "", commands6, true, false);

        Command[] commands7 = { Command.Command6, Command.Command7, Command.CommandADD, Command.CommandNULL };
        TestDriver.Test("67", "67 + ", commands7, true, false);

        Command[] commands8 = { Command.ModeBasic, Command.CommandNULL };
        TestDriver.Test("0", "", commands8, true, false);
    }

    [Fact]
    public void CalculatorManagerTestProgrammer()
    {
        Command[] commands1 =
        {
            Command.ModeProgrammer, Command.Command5, Command.Command3, Command.CommandNand,
            Command.Command8, Command.Command3, Command.CommandAnd, Command.CommandNULL
        };
        TestDriver.Test("-18", "53 NAND 83 AND ", commands1, true, false);

        Command[] commands2 =
        {
            Command.ModeProgrammer, Command.Command5, Command.Command3, Command.CommandNor,
            Command.Command8, Command.Command3, Command.CommandAnd, Command.CommandNULL
        };
        TestDriver.Test("-120", "53 NOR 83 AND ", commands2, true, false);

        Command[] commands3 =
        {
            Command.ModeProgrammer, Command.Command5, Command.CommandLSHF, Command.Command1, Command.CommandAnd,
            Command.CommandNULL
        };
        TestDriver.Test("10", "5 Lsh 1 AND ", commands3, true, false);

        Command[] commands5 =
        {
            Command.ModeProgrammer, Command.Command5, Command.CommandRSHFL, Command.Command1, Command.CommandAnd,
            Command.CommandNULL
        };
        TestDriver.Test("2", "5 Rsh 1 AND ", commands5, true, false);

        Command[] commands6 =
        {
            Command.ModeProgrammer, Command.CommandBINPOS63, Command.CommandRSHF, Command.Command5,
            Command.Command6, Command.CommandAnd, Command.CommandNULL
        };
        TestDriver.Test("-128", "-9223372036854775808 Rsh 56 AND ", commands6, true, false);

        Command[] commands7 = { Command.ModeProgrammer, Command.Command1, Command.CommandROL, Command.CommandNULL };
        TestDriver.Test("2", "RoL(1)", commands7, true, false);

        Command[] commands8 = { Command.ModeProgrammer, Command.Command1, Command.CommandROR, Command.CommandNULL };
        TestDriver.Test("-9,223,372,036,854,775,808", "RoR(1)", commands8, true, false);

        Command[] commands9 = { Command.ModeProgrammer, Command.Command1, Command.CommandRORC, Command.CommandNULL };
        TestDriver.Test("0", "RoR(1)", commands9, true, false);

        Command[] commands10 =
            { Command.ModeProgrammer, Command.Command1, Command.CommandRORC, Command.CommandRORC, Command.CommandNULL };
        TestDriver.Test("-9,223,372,036,854,775,808", "RoR(RoR(1))", commands10, true, false);
    }

    [Fact]
    public void CalculatorManagerTestMemory()
    {
        Command[] scientificCalculatorTest52 = { Command.Command1, Command.CommandSTORE, Command.CommandNULL };
        var expectedPrimaryDisplayTestScientific52 = "1";
        var expectedExpressionDisplayTestScientific52 = "";

        Command[] scientificCalculatorTest53 = { Command.Command1, Command.CommandNULL };
        var expectedPrimaryDisplayTestScientific53 = "1";
        var expectedExpressionDisplayTestScientific53 = "";

        var resultPrimary = "";
        var resultExpression = "";

        Cleanup();
        ExecuteCommands(scientificCalculatorTest52);
        resultPrimary = m_calculatorDisplayTester.GetPrimaryDisplay();
        resultExpression = m_calculatorDisplayTester.GetExpression();
        Assert.Equal(expectedPrimaryDisplayTestScientific52, resultPrimary);

        Cleanup();
        ExecuteCommands(scientificCalculatorTest53);
        m_calculatorManager.MemorizeNumber();
        m_calculatorManager.SendCommand(Command.CommandCLEAR);
        m_calculatorManager.MemorizedNumberLoad(0);
        resultPrimary = m_calculatorDisplayTester.GetPrimaryDisplay();
        resultExpression = m_calculatorDisplayTester.GetExpression();
        Assert.Equal(expectedPrimaryDisplayTestScientific52, resultPrimary);

        Cleanup();
        m_calculatorManager.SendCommand(Command.Command1);
        m_calculatorManager.MemorizeNumber();
        m_calculatorManager.SendCommand(Command.CommandCLEAR);
        m_calculatorManager.SendCommand(Command.Command2);
        m_calculatorManager.MemorizeNumber();
        m_calculatorManager.SendCommand(Command.CommandCLEAR);
        m_calculatorManager.MemorizedNumberLoad(1);
        resultPrimary = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("1", resultPrimary);

        m_calculatorManager.MemorizedNumberLoad(0);
        resultPrimary = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("2", resultPrimary);

        Cleanup();
        m_calculatorManager.SendCommand(Command.Command1);
        m_calculatorManager.SendCommand(Command.CommandSIGN);
        m_calculatorManager.MemorizeNumber();
        m_calculatorManager.SendCommand(Command.CommandADD);
        m_calculatorManager.SendCommand(Command.Command2);
        m_calculatorManager.SendCommand(Command.CommandEQU);
        m_calculatorManager.MemorizeNumber();
        m_calculatorManager.SendCommand(Command.CommandMUL);
        m_calculatorManager.SendCommand(Command.Command2);
        m_calculatorManager.MemorizeNumber();

        List<string> memorizedNumbers = m_calculatorDisplayTester.GetMemorizedNumbers();

        List<string> expectedMemorizedNumbers = new List<string>();
        expectedMemorizedNumbers.Add("2");
        expectedMemorizedNumbers.Add("1");
        expectedMemorizedNumbers.Add("-1");

        var isEqual = false;
        if (memorizedNumbers.Count < expectedMemorizedNumbers.Count)
        {
            isEqual = SequenceEqual(memorizedNumbers, expectedMemorizedNumbers.GetRange(0, memorizedNumbers.Count));
        }
        else
        {
            isEqual = SequenceEqual(expectedMemorizedNumbers,
                memorizedNumbers.GetRange(0, expectedMemorizedNumbers.Count));
        }

        Assert.True(isEqual);
    }

    private bool SequenceEqual<T>(List<T> list1, List<T> list2)
    {
        if (list1.Count != list2.Count)
        {
            return false;
        }

        for (var i = 0; i < list1.Count; i++)
        {
            if (!list1[i].Equals(list2[i]))
            {
                return false;
            }
        }

        return true;
    }

    [Theory]
    [InlineData("1,234,567,891,011,1213")]
    [InlineData("0.12345678910111213")]
    [InlineData("123,456,789,101,112.13")]
    public void CalculatorManagerTestMaxDigitsReached(string input)
    {
        TestMaxDigitsReachedScenario(input);
    }

    [Fact]
    public void CalculatorManagerTestBinaryOperatorReceived()
    {
        Assert.Equal(0, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());

        m_calculatorManager.SetStandardMode();
        ExecuteCommands(new Command[] { Command.Command1, Command.CommandADD, Command.CommandNULL });

        var display = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("1", display);

        // Verify BinaryOperatorReceived
        Assert.Equal(1, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());
    }

    [Fact]
    public void CalculatorManagerTestBinaryOperatorReceived_Multiple()
    {
        Assert.Equal(0, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());

        m_calculatorManager.SetStandardMode();
        ExecuteCommands(new Command[]
            { Command.Command1, Command.CommandADD, Command.CommandSUB, Command.CommandMUL, Command.CommandNULL });

        var display = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("1", display);

        // Verify BinaryOperatorReceived
        Assert.Equal(3, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());
    }

    [Fact]
    public void CalculatorManagerTestBinaryOperatorReceived_LongInput()
    {
        Assert.Equal(0, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());

        m_calculatorManager.SetStandardMode();
        ExecuteCommands(new Command[]
        {
            Command.Command1,
            Command.CommandADD,
            Command.Command2,
            Command.CommandMUL,
            Command.Command1,
            Command.Command0,
            Command.CommandSUB,
            Command.Command5,
            Command.CommandDIV,
            Command.Command5,
            Command.CommandEQU,
            Command.CommandNULL
        });

        var display = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("5", display);

        // Verify BinaryOperatorReceived
        Assert.Equal(4, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());
    }

    [Fact]
    public void CalculatorManagerTestScientific2()
    {
        Command[] commands1 = [Command.Command1, Command.Command2, Command.CommandSQR, Command.CommandNULL];
        TestDriver.Test("144", "sqr(12)", commands1, true, true);

        Command[] commands2 = [Command.Command5, Command.CommandFAC, Command.CommandNULL];
        TestDriver.Test("120", "fact(5)", commands2, true, true);

        Command[] commands3 =
            [Command.Command5, Command.CommandPWR, Command.Command2, Command.CommandADD, Command.CommandNULL];
        TestDriver.Test("25", "5 ^ 2 + ", commands3, true, true);

        Command[] commands4 =
            [Command.Command8, Command.CommandROOT, Command.Command3, Command.CommandMUL, Command.CommandNULL];
        TestDriver.Test("2", "8 yroot 3 \x00D7 ", commands4, true, true);

        Command[] commands5 = [Command.Command8, Command.CommandCUB, Command.CommandNULL];
        TestDriver.Test("512", "cube(8)", commands5, true, true);

        Command[] commands6 = [Command.Command8, Command.CommandCUB, Command.CommandCUBEROOT, Command.CommandNULL];
        TestDriver.Test("8", "cuberoot(cube(8))", commands6, true, true);

        Command[] commands7 = [Command.Command1, Command.Command0, Command.CommandLOG, Command.CommandNULL];
        TestDriver.Test("1", "log(10)", commands7, true, true);

        Command[] commands8 = [Command.Command5, Command.CommandPOW10, Command.CommandNULL];
        TestDriver.Test("100,000", "10^(5)", commands8, true, true);

        Command[] commands9 = [Command.Command1, Command.Command0, Command.CommandLN, Command.CommandNULL];
        TestDriver.Test("2.3025850929940456840179914546844", "ln(10)", commands9, true, true);

        Command[] commands10 = [Command.Command1, Command.CommandSIN, Command.CommandNULL];
        TestDriver.Test("0.01745240643728351281941897851632", "sin\x2080(1)", commands10, true, true);

        Command[] commands11 = [Command.Command1, Command.CommandCOS, Command.CommandNULL];
        TestDriver.Test("0.99984769515639123915701155881391", "cos\x2080(1)", commands11, true, true);

        Command[] commands12 = [Command.Command1, Command.CommandTAN, Command.CommandNULL];
        TestDriver.Test("0.01745506492821758576512889521973", "tan\x2080(1)", commands12, true, true);

        Command[] commands13 = [Command.Command1, Command.CommandASIN, Command.CommandNULL];
        TestDriver.Test("90", "sin\x2080\x207B\x00B9(1)", commands13, true, true);

        Command[] commands14 = [Command.Command1, Command.CommandACOS, Command.CommandNULL];
        TestDriver.Test("0", "cos\x2080\x207B\x00B9(1)", commands14, true, true);

        Command[] commands15 = [Command.Command1, Command.CommandATAN, Command.CommandNULL];
        TestDriver.Test("45", "tan\x2080\x207B\x00B9(1)", commands15, true, true);

        Command[] commands16 = [Command.Command2, Command.CommandPOWE, Command.CommandNULL];
        TestDriver.Test("7.389056098930650227230427460575", "e^(2)", commands16, true, true);

        Command[] commands17 =
            [Command.Command5, Command.CommandPWR, Command.Command0, Command.CommandADD, Command.CommandNULL];
        TestDriver.Test("1", "5 ^ 0 + ", commands17, true, true);

        Command[] commands18 =
            [Command.Command0, Command.CommandPWR, Command.Command0, Command.CommandADD, Command.CommandNULL];
        TestDriver.Test("1", "0 ^ 0 + ", commands18, true, true);

        Command[] commands19 =
        [
            Command.Command2, Command.Command7, Command.CommandSIGN, Command.CommandROOT, Command.Command3,
            Command.CommandADD, Command.CommandNULL
        ];
        TestDriver.Test("-3", "-27 yroot 3 + ", commands19, true, true);

        Command[] commands20 =
        [
            Command.Command8, Command.CommandPWR, Command.CommandOPENP, Command.Command2, Command.CommandDIV,
            Command.Command3, Command.CommandCLOSEP, Command.CommandSUB, Command.Command4, Command.CommandADD,
            Command.CommandNULL
        ];
        TestDriver.Test("0", "8 ^ (2 \x00F7 3) - 4 + ", commands20, true, true);

        Command[] commands21 =
        [
            Command.Command4, Command.CommandPWR, Command.CommandOPENP, Command.Command3, Command.CommandDIV,
            Command.Command2, Command.CommandCLOSEP, Command.CommandSUB, Command.Command8, Command.CommandADD,
            Command.CommandNULL
        ];
        TestDriver.Test("0", "4 ^ (3 \x00F7 2) - 8 + ", commands21, true, true);

        Command[] commands22 =
        [
            Command.Command1, Command.Command0, Command.CommandPWR, Command.Command1, Command.CommandPNT,
            Command.Command2, Command.Command3, Command.Command4, Command.Command5, Command.Command6,
            Command.CommandADD, Command.CommandNULL
        ];
        TestDriver.Test("17.161687912241792074207286679393", "10 ^ 1.23456 + ", commands22, true, true);

        Command[] commands23 = [Command.Command1, Command.CommandSEC, Command.CommandNULL];
        TestDriver.Test("1.0001523280439076654284264342126", "sec\x2080(1)", commands23, true, true);

        Command[] commands24 = [Command.Command1, Command.CommandCSC, Command.CommandNULL];
        TestDriver.Test("57.298688498550183476612683735174", "csc\x2080(1)", commands24, true, true);

        Command[] commands25 = [Command.Command1, Command.CommandCOT, Command.CommandNULL];
        TestDriver.Test("57.289961630759424687278147537113", "cot\x2080(1)", commands25, true, true);

        Command[] commands26 = [Command.Command1, Command.CommandASEC, Command.CommandNULL];
        TestDriver.Test("0", "sec\x2080\x207B\x00B9(1)", commands26, true, true);

        Command[] commands27 = [Command.Command1, Command.CommandACSC, Command.CommandNULL];
        TestDriver.Test("90", "csc\x2080\x207B\x00B9(1)", commands27, true, true);

        Command[] commands28 = [Command.Command1, Command.CommandACOT, Command.CommandNULL];
        TestDriver.Test("45", "cot\x2080\x207B\x00B9(1)", commands28, true, true);

        Command[] commands29 = [Command.Command1, Command.CommandSECH, Command.CommandNULL];
        TestDriver.Test("0.64805427366388539957497735322615", "sech(1)", commands29, true, true);

        Command[] commands30 = [Command.Command1, Command.CommandCSCH, Command.CommandNULL];
        TestDriver.Test("0.85091812823932154513384276328718", "csch(1)", commands30, true, true);

        Command[] commands31 = [Command.Command1, Command.CommandCOTH, Command.CommandNULL];
        TestDriver.Test("1.3130352854993313036361612469308", "coth(1)", commands31, true, true);

        Command[] commands32 = [Command.Command1, Command.CommandASECH, Command.CommandNULL];
        TestDriver.Test("0", "sech\x207B\x00B9(1)", commands32, true, true);

        Command[] commands33 = [Command.Command1, Command.CommandACSCH, Command.CommandNULL];
        TestDriver.Test("0.88137358701954302523260932497979", "csch\x207B\x00B9(1)", commands33, true, true);

        Command[] commands34 = [Command.Command2, Command.CommandACOTH, Command.CommandNULL];
        TestDriver.Test("0.54930614433405484569762261846126", "coth\x207B\x00B9(2)", commands34, true, true);

        Command[] commands35 = [Command.Command8, Command.CommandPOW2, Command.CommandNULL];
        TestDriver.Test("256", "2^(8)", commands35, true, true);

        Command[] commands36 = [Command.CommandRand, Command.CommandCeil, Command.CommandNULL];
        TestDriver.Test("1", "N/A", commands36, true, true);

        Command[] commands37 = [Command.CommandRand, Command.CommandFloor, Command.CommandNULL];
        TestDriver.Test("0", "N/A", commands37, true, true);

        Command[] commands38 = [Command.CommandRand, Command.CommandSIGN, Command.CommandCeil, Command.CommandNULL];
        TestDriver.Test("0", "N/A", commands38, true, true);

        Command[] commands39 = [Command.CommandRand, Command.CommandSIGN, Command.CommandFloor, Command.CommandNULL];
        TestDriver.Test("-1", "N/A", commands39, true, true);

        Command[] commands40 =
            [Command.Command3, Command.CommandPNT, Command.Command8, Command.CommandFloor, Command.CommandNULL];
        TestDriver.Test("3", "floor(3.8)", commands40, true, true);

        Command[] commands41 =
            [Command.Command3, Command.CommandPNT, Command.Command8, Command.CommandCeil, Command.CommandNULL];
        TestDriver.Test("4", "ceil(3.8)", commands41, true, true);

        Command[] commands42 =
            [Command.Command5, Command.CommandLogBaseY, Command.Command3, Command.CommandADD, Command.CommandNULL];
        TestDriver.Test("1.4649735207179271671970404076786", "5 log base 3 + ", commands42, true, true);
    }
}
