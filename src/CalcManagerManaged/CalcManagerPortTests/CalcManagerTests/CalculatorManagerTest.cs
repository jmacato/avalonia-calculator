using CalcEngine;
using CalculationManager;

namespace CalcEngineTests;

public class CalculatorManagerTest
{

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
        while (commands[i] != Command.None)
        {
            m_calculatorManager.SendCommand(commands[i++]);
        }
    }

    private void ExecuteCommands(List<Command> commands)
    {
        foreach (var command in commands)
        {
            if (command == Command.None)
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

    private static List<Command> CommandListFromStringInput(string input)
    {
        var result = new List<Command>();
        foreach (var ch in input)
        {
            var asCommand = Command.None;
            if (ch == '.')
            {
                asCommand = Command.Pnt;
            }
            else if ('0' <= ch && ch <= '9')
            {
                var diff = (int)ch - (int)'0';
                asCommand = (Command)((int)Command.Num0 + diff);
            }

            if (asCommand != Command.None)
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
            Command.Num1, Command.Num2, Command.Num3, Command.Pnt,
            Command.Num4, Command.Num5, Command.Num6, Command.None
        };
        TestDriver.Test("123.456", "", commands1);

        Command[] commands2 = { Command.Add, Command.None };
        TestDriver.Test("0", "0 + ", commands2);

        Command[] commands3 = { Command.Sqrt, Command.None };
        TestDriver.Test("0", "\x221A(0)", commands3);

        Command[] commands4 =
        {
            Command.Num2, Command.Add, Command.Num3, Command.Equ,
            Command.Num4, Command.Equ, Command.None
        };
        TestDriver.Test("7", "4 + 3=", commands4);

        Command[] commands5 = { Command.Num4, Command.Equ, Command.None };
        TestDriver.Test("4", "4=", commands5);

        Command[] commands6 =
        {
            Command.Num2, Command.Num5, Command.Num6, Command.Sqrt,
            Command.Sqrt, Command.Sqrt, Command.None
        };
        TestDriver.Test("2", "\x221A(\x221A(\x221A(256)))", commands6);

        Command[] commands7 =
        {
            Command.Num3, Command.Sub, Command.Num6, Command.Equ,
            Command.Mul, Command.Num3, Command.Equ, Command.None
        };
        TestDriver.Test("-9", "-3 \x00D7 3=", commands7);

        Command[] commands8 =
        {
            Command.Num9, Command.Mul, Command.Num6, Command.Sub,
            Command.Centr, Command.Num8, Command.Equ, Command.None
        };
        TestDriver.Test("46", "54 - 8=", commands8);

        Command[] commands9 =
        {
            Command.Num6, Command.Mul, Command.Num6, Command.Percent, Command.Equ,
            Command.None
        };
        TestDriver.Test("0.36", "6 \x00D7 0.06=", commands9);

        Command[] commands10 =
        {
            Command.Num5, Command.Num0, Command.Add, Command.Num2,
            Command.Num0, Command.Percent, Command.Equ, Command.None
        };
        TestDriver.Test("60", "50 + 10=", commands10);
    }

    [Fact]
    public void CalculatorManagerTestScientific()
    {
        Command[] commands1 =
        {
            Command.Num1, Command.Num2, Command.Num3, Command.Pnt,
            Command.Num4, Command.Num5, Command.Num6, Command.None
        };
        TestDriver.Test("123.456", "", commands1, true, true);

        Command[] commands2 = { Command.Add, Command.None };
        TestDriver.Test("0", "0 + ", commands2, true, true);

        Command[] commands3 = { Command.Sqrt, Command.None };
        TestDriver.Test("0", "\x221A(0)", commands3, true, true);

        Command[] commands4 =
        {
            Command.Num1, Command.Add, Command.Num0, Command.Mul,
            Command.Num2, Command.Equ, Command.None
        };
        TestDriver.Test("1", "1 + 0 \x00D7 2=", commands4, true, true);

        Command[] commands5 = { Command.Num4, Command.Equ, Command.None };
        TestDriver.Test("4", "4=", commands5, true, true);

        Command[] commands6 =
        {
            Command.Num2, Command.Num5, Command.Num6, Command.Sqrt,
            Command.Sqrt, Command.Sqrt, Command.None
        };
        TestDriver.Test("2", "\x221A(\x221A(\x221A(256)))", commands6, true, true);

        Command[] commands7 =
        {
            Command.Num3, Command.Sub, Command.Num6, Command.Equ,
            Command.Mul, Command.Num3, Command.Add, Command.None
        };
        TestDriver.Test("-9", "-3 \x00D7 3 + ", commands7, true, true);

        Command[] commands8 =
        {
            Command.Num9, Command.Mul, Command.Num6, Command.Sub, Command.Centr,
            Command.Num8, Command.Mul, Command.Num2, Command.Add, Command.None
        };
        TestDriver.Test("38", "9 \x00D7 6 - 8 \x00D7 2 + ", commands8, true, true);

        Command[] commands9 =
        {
            Command.Num6, Command.Mul, Command.Num6, Command.Sign, Command.Sqrt,
            Command.None
        };
        TestDriver.Test("Invalid input", "6 \x00D7 \x221A(-6)", commands9, true, true);

        Command[] commands10 =
        {
            Command.Num5, Command.Num0, Command.Add, Command.Num2,
            Command.Num0, Command.Rec, Command.Sub, Command.None
        };
        TestDriver.Test("50.05", "50 + 1/(20) - ", commands10, true, true);
    }

    [Fact]
    public void CalculatorManagerTestScientificParenthesis()
    {
        Command[] commands1 =
        {
            Command.Num1, Command.Add, Command.OpenP, Command.Add,
            Command.Num3, Command.CloseP, Command.None
        };
        TestDriver.Test("3", "1 + (0 + 3)", commands1, true, true);

        Command[] commands2 =
        {
            Command.OpenP, Command.OpenP, Command.Num1, Command.Num2, Command.CloseP,
            Command.None
        };
        TestDriver.Test("12", "((12)", commands2, true, true);

        Command[] commands3 =
        {
            Command.Num1, Command.Num2, Command.CloseP,
            Command.CloseP, Command.OpenP, Command.None
        };
        TestDriver.Test("12", "12 \x00D7 (", commands3, true, true);

        Command[] commands4 =
        {
            Command.Num2, Command.OpenP, Command.Num2, Command.CloseP, Command.Add,
            Command.None
        };
        TestDriver.Test("4", "2 \x00D7 (2) + ", commands4, true, true);

        Command[] commands5 =
        {
            Command.Num2, Command.OpenP, Command.Num2, Command.CloseP,
            Command.Add, Command.Equ, Command.None
        };
        TestDriver.Test("8", "2 \x00D7 (2) + 4=", commands5, true, true);
    }

    [Fact]
    public void CalculatorManagerTestScientificError()
    {
        Command[] commands1 =
            { Command.Num1, Command.Div, Command.Num0, Command.Equ, Command.None };
        TestDriver.Test("Cannot divide by zero", "1 \x00F7 ", commands1, true, true);
        Assert.True(m_calculatorDisplayTester.GetIsError());

        Command[] commands2 = { Command.Num2, Command.Sign, Command.Log, Command.None };
        TestDriver.Test("Invalid input", "log(-2)", commands2, true, true);
        Assert.True(m_calculatorDisplayTester.GetIsError());

        Command[] commands3 =
            { Command.Num0, Command.Div, Command.Num0, Command.Equ, Command.None };
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
        Command[] commands1 = { Command.Rad, Command.NumPI, Command.Sin, Command.None };
        TestDriver.Test("0", "N/A", commands1, true, true);

        Command[] commands2 = { Command.Rad, Command.NumPI, Command.Cos, Command.None };
        TestDriver.Test("-1", "N/A", commands2, true, true);

        Command[] commands3 = { Command.Rad, Command.NumPI, Command.Tan, Command.None };
        TestDriver.Test("0", "N/A", commands3, true, true);

        Command[] commands4 =
        {
            Command.Grad, Command.Num4, Command.Num0, Command.Num0, Command.Sin,
            Command.None
        };
        TestDriver.Test("0", "N/A", commands4, true, true);

        Command[] commands5 =
        {
            Command.Grad, Command.Num4, Command.Num0, Command.Num0, Command.Cos,
            Command.None
        };
        TestDriver.Test("1", "N/A", commands5, true, true);

        Command[] commands6 =
        {
            Command.Grad, Command.Num4, Command.Num0, Command.Num0, Command.Tan,
            Command.None
        };
        TestDriver.Test("0", "N/A", commands6, true, true);
    }

    [Fact]
    public void CalculatorManagerTestModeChange()
    {
        Command[] commands1 = { Command.Num1, Command.Num2, Command.Num3, Command.None };
        TestDriver.Test("123", "", commands1, true, false);

        Command[] commands2 = { Command.ModeScientific, Command.None };
        TestDriver.Test("0", "", commands2, true, false);

        Command[] commands3 = { Command.Num1, Command.Num2, Command.Num3, Command.None };
        TestDriver.Test("123", "", commands3, true, false);

        Command[] commands4 = { Command.ModeProgrammer, Command.None };
        TestDriver.Test("0", "", commands4, true, false);

        Command[] commands5 = { Command.Num1, Command.Num2, Command.Num3, Command.None };
        TestDriver.Test("123", "", commands5, true, false);

        Command[] commands6 = { Command.ModeScientific, Command.None };
        TestDriver.Test("0", "", commands6, true, false);

        Command[] commands7 = { Command.Num6, Command.Num7, Command.Add, Command.None };
        TestDriver.Test("67", "67 + ", commands7, true, false);

        Command[] commands8 = { Command.ModeBasic, Command.None };
        TestDriver.Test("0", "", commands8, true, false);
    }

    [Fact]
    public void CalculatorManagerTestProgrammer()
    {
        Command[] commands1 =
        {
            Command.ModeProgrammer, Command.Num5, Command.Num3, Command.Nand,
            Command.Num8, Command.Num3, Command.And, Command.None
        };
        TestDriver.Test("-18", "53 NAND 83 AND ", commands1, true, false);

        Command[] commands2 =
        {
            Command.ModeProgrammer, Command.Num5, Command.Num3, Command.Nor,
            Command.Num8, Command.Num3, Command.And, Command.None
        };
        TestDriver.Test("-120", "53 NOR 83 AND ", commands2, true, false);

        Command[] commands3 =
        {
            Command.ModeProgrammer, Command.Num5, Command.Lshf, Command.Num1, Command.And,
            Command.None
        };
        TestDriver.Test("10", "5 Lsh 1 AND ", commands3, true, false);

        Command[] commands5 =
        {
            Command.ModeProgrammer, Command.Num5, Command.Rshfl, Command.Num1, Command.And,
            Command.None
        };
        TestDriver.Test("2", "5 Rsh 1 AND ", commands5, true, false);

        Command[] commands6 =
        {
            Command.ModeProgrammer, Command.BinPos63, Command.Rshf, Command.Num5,
            Command.Num6, Command.And, Command.None
        };
        TestDriver.Test("-128", "-9223372036854775808 Rsh 56 AND ", commands6, true, false);

        Command[] commands7 = { Command.ModeProgrammer, Command.Num1, Command.Rol, Command.None };
        TestDriver.Test("2", "RoL(1)", commands7, true, false);

        Command[] commands8 = { Command.ModeProgrammer, Command.Num1, Command.Ror, Command.None };
        TestDriver.Test("-9,223,372,036,854,775,808", "RoR(1)", commands8, true, false);

        Command[] commands9 = { Command.ModeProgrammer, Command.Num1, Command.Rorc, Command.None };
        TestDriver.Test("0", "RoR(1)", commands9, true, false);

        Command[] commands10 =
            { Command.ModeProgrammer, Command.Num1, Command.Rorc, Command.Rorc, Command.None };
        TestDriver.Test("-9,223,372,036,854,775,808", "RoR(RoR(1))", commands10, true, false);
    }

    [Fact]
    public void CalculatorManagerTestMemory()
    {
        Command[] scientificCalculatorTest52 = { Command.Num1, Command.Store, Command.None };
        var expectedPrimaryDisplayTestScientific52 = "1";

        Command[] scientificCalculatorTest53 = { Command.Num1, Command.None };

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
        m_calculatorManager.SendCommand(Command.Clear);
        m_calculatorManager.MemorizedNumberLoad(0);
        resultPrimary = m_calculatorDisplayTester.GetPrimaryDisplay();
        resultExpression = m_calculatorDisplayTester.GetExpression();
        Assert.Equal(expectedPrimaryDisplayTestScientific52, resultPrimary);

        Cleanup();
        m_calculatorManager.SendCommand(Command.Num1);
        m_calculatorManager.MemorizeNumber();
        m_calculatorManager.SendCommand(Command.Clear);
        m_calculatorManager.SendCommand(Command.Num2);
        m_calculatorManager.MemorizeNumber();
        m_calculatorManager.SendCommand(Command.Clear);
        m_calculatorManager.MemorizedNumberLoad(1);
        resultPrimary = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("1", resultPrimary);

        m_calculatorManager.MemorizedNumberLoad(0);
        resultPrimary = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("2", resultPrimary);

        Cleanup();
        m_calculatorManager.SendCommand(Command.Num1);
        m_calculatorManager.SendCommand(Command.Sign);
        m_calculatorManager.MemorizeNumber();
        m_calculatorManager.SendCommand(Command.Add);
        m_calculatorManager.SendCommand(Command.Num2);
        m_calculatorManager.SendCommand(Command.Equ);
        m_calculatorManager.MemorizeNumber();
        m_calculatorManager.SendCommand(Command.Mul);
        m_calculatorManager.SendCommand(Command.Num2);
        m_calculatorManager.MemorizeNumber();

        IList<string> memorizedNumbers = m_calculatorDisplayTester.GetMemorizedNumbers();

        List<string> expectedMemorizedNumbers = new List<string>();
        expectedMemorizedNumbers.Add("2");
        expectedMemorizedNumbers.Add("1");
        expectedMemorizedNumbers.Add("-1");

        var isEqual = false;
        if (memorizedNumbers.Count < expectedMemorizedNumbers.Count)
        {
            isEqual = SequenceEqual(memorizedNumbers.ToList(), expectedMemorizedNumbers.GetRange(0, memorizedNumbers.Count));
        }
        else
        {
            isEqual = SequenceEqual(expectedMemorizedNumbers,
                memorizedNumbers.Take(expectedMemorizedNumbers.Count).ToList());
        }

        Assert.True(isEqual);
    }

    private static bool SequenceEqual<T>(List<T> list1, List<T> list2)
    {
        if (list1.Count != list2.Count)
        {
            return false;
        }

        for (var i = 0; i < list1.Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(list1[i], list2[i]))
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
        ArgumentNullException.ThrowIfNull(input);

        TestMaxDigitsReachedScenario(input);
    }

    [Fact]
    public void CalculatorManagerTestBinaryOperatorReceived()
    {
        Assert.Equal(0, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());

        m_calculatorManager.SetStandardMode();
        ExecuteCommands(new Command[] { Command.Num1, Command.Add, Command.None });

        var display = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("1", display);

        // Verify BinaryOperatorReceived
        Assert.Equal(1, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());
    }

    [Fact]
    public void CalculatorManagerTestBinaryOperatorReceivedMultiple()
    {
        Assert.Equal(0, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());

        m_calculatorManager.SetStandardMode();
        ExecuteCommands(new Command[]
            { Command.Num1, Command.Add, Command.Sub, Command.Mul, Command.None });

        var display = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("1", display);

        // Verify BinaryOperatorReceived
        Assert.Equal(3, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());
    }

    [Fact]
    public void CalculatorManagerTestBinaryOperatorReceivedLongInput()
    {
        Assert.Equal(0, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());

        m_calculatorManager.SetStandardMode();
        ExecuteCommands(new Command[]
        {
            Command.Num1,
            Command.Add,
            Command.Num2,
            Command.Mul,
            Command.Num1,
            Command.Num0,
            Command.Sub,
            Command.Num5,
            Command.Div,
            Command.Num5,
            Command.Equ,
            Command.None
        });

        var display = m_calculatorDisplayTester.GetPrimaryDisplay();
        Assert.Equal("5", display);

        // Verify BinaryOperatorReceived
        Assert.Equal(4, m_calculatorDisplayTester.GetBinaryOperatorReceivedCallCount());
    }

    [Fact]
    public void CalculatorManagerTestScientific2()
    {
        Command[] commands1 = [Command.Num1, Command.Num2, Command.Sqr, Command.None];
        TestDriver.Test("144", "sqr(12)", commands1, true, true);

        Command[] commands2 = [Command.Num5, Command.Fac, Command.None];
        TestDriver.Test("120", "fact(5)", commands2, true, true);

        Command[] commands3 =
            [Command.Num5, Command.Pwr, Command.Num2, Command.Add, Command.None];
        TestDriver.Test("25", "5 ^ 2 + ", commands3, true, true);

        Command[] commands4 =
            [Command.Num8, Command.Root, Command.Num3, Command.Mul, Command.None];
        TestDriver.Test("2", "8 yroot 3 \x00D7 ", commands4, true, true);

        Command[] commands5 = [Command.Num8, Command.Cub, Command.None];
        TestDriver.Test("512", "cube(8)", commands5, true, true);

        Command[] commands6 = [Command.Num8, Command.Cub, Command.CubeRoot, Command.None];
        TestDriver.Test("8", "cuberoot(cube(8))", commands6, true, true);

        Command[] commands7 = [Command.Num1, Command.Num0, Command.Log, Command.None];
        TestDriver.Test("1", "log(10)", commands7, true, true);

        Command[] commands8 = [Command.Num5, Command.Pow10, Command.None];
        TestDriver.Test("100,000", "10^(5)", commands8, true, true);

        Command[] commands9 = [Command.Num1, Command.Num0, Command.NumLN, Command.None];
        TestDriver.Test("2.3025850929940456840179914546844", "ln(10)", commands9, true, true);

        Command[] commands10 = [Command.Num1, Command.Sin, Command.None];
        TestDriver.Test("0.01745240643728351281941897851632", "sin\x2080(1)", commands10, true, true);

        Command[] commands11 = [Command.Num1, Command.Cos, Command.None];
        TestDriver.Test("0.99984769515639123915701155881391", "cos\x2080(1)", commands11, true, true);

        Command[] commands12 = [Command.Num1, Command.Tan, Command.None];
        TestDriver.Test("0.01745506492821758576512889521973", "tan\x2080(1)", commands12, true, true);

        Command[] commands13 = [Command.Num1, Command.Asin, Command.None];
        TestDriver.Test("90", "sin\x2080\x207B\x00B9(1)", commands13, true, true);

        Command[] commands14 = [Command.Num1, Command.Acos, Command.None];
        TestDriver.Test("0", "cos\x2080\x207B\x00B9(1)", commands14, true, true);

        Command[] commands15 = [Command.Num1, Command.Atan, Command.None];
        TestDriver.Test("45", "tan\x2080\x207B\x00B9(1)", commands15, true, true);

        Command[] commands16 = [Command.Num2, Command.PowE, Command.None];
        TestDriver.Test("7.389056098930650227230427460575", "e^(2)", commands16, true, true);

        Command[] commands17 =
            [Command.Num5, Command.Pwr, Command.Num0, Command.Add, Command.None];
        TestDriver.Test("1", "5 ^ 0 + ", commands17, true, true);

        Command[] commands18 =
            [Command.Num0, Command.Pwr, Command.Num0, Command.Add, Command.None];
        TestDriver.Test("1", "0 ^ 0 + ", commands18, true, true);

        Command[] commands19 =
        [
            Command.Num2, Command.Num7, Command.Sign, Command.Root, Command.Num3,
            Command.Add, Command.None
        ];
        TestDriver.Test("-3", "-27 yroot 3 + ", commands19, true, true);

        Command[] commands20 =
        [
            Command.Num8, Command.Pwr, Command.OpenP, Command.Num2, Command.Div,
            Command.Num3, Command.CloseP, Command.Sub, Command.Num4, Command.Add,
            Command.None
        ];
        TestDriver.Test("0", "8 ^ (2 \x00F7 3) - 4 + ", commands20, true, true);

        Command[] commands21 =
        [
            Command.Num4, Command.Pwr, Command.OpenP, Command.Num3, Command.Div,
            Command.Num2, Command.CloseP, Command.Sub, Command.Num8, Command.Add,
            Command.None
        ];
        TestDriver.Test("0", "4 ^ (3 \x00F7 2) - 8 + ", commands21, true, true);

        Command[] commands22 =
        [
            Command.Num1, Command.Num0, Command.Pwr, Command.Num1, Command.Pnt,
            Command.Num2, Command.Num3, Command.Num4, Command.Num5, Command.Num6,
            Command.Add, Command.None
        ];
        TestDriver.Test("17.161687912241792074207286679393", "10 ^ 1.23456 + ", commands22, true, true);

        Command[] commands23 = [Command.Num1, Command.Sec, Command.None];
        TestDriver.Test("1.0001523280439076654284264342126", "sec\x2080(1)", commands23, true, true);

        Command[] commands24 = [Command.Num1, Command.Csc, Command.None];
        TestDriver.Test("57.298688498550183476612683735174", "csc\x2080(1)", commands24, true, true);

        Command[] commands25 = [Command.Num1, Command.Cot, Command.None];
        TestDriver.Test("57.289961630759424687278147537113", "cot\x2080(1)", commands25, true, true);

        Command[] commands26 = [Command.Num1, Command.Asec, Command.None];
        TestDriver.Test("0", "sec\x2080\x207B\x00B9(1)", commands26, true, true);

        Command[] commands27 = [Command.Num1, Command.Acsc, Command.None];
        TestDriver.Test("90", "csc\x2080\x207B\x00B9(1)", commands27, true, true);

        Command[] commands28 = [Command.Num1, Command.Acot, Command.None];
        TestDriver.Test("45", "cot\x2080\x207B\x00B9(1)", commands28, true, true);

        Command[] commands29 = [Command.Num1, Command.Sech, Command.None];
        TestDriver.Test("0.64805427366388539957497735322615", "sech(1)", commands29, true, true);

        Command[] commands30 = [Command.Num1, Command.Csch, Command.None];
        TestDriver.Test("0.85091812823932154513384276328718", "csch(1)", commands30, true, true);

        Command[] commands31 = [Command.Num1, Command.Coth, Command.None];
        TestDriver.Test("1.3130352854993313036361612469308", "coth(1)", commands31, true, true);

        Command[] commands32 = [Command.Num1, Command.Asech, Command.None];
        TestDriver.Test("0", "sech\x207B\x00B9(1)", commands32, true, true);

        Command[] commands33 = [Command.Num1, Command.Acsch, Command.None];
        TestDriver.Test("0.88137358701954302523260932497979", "csch\x207B\x00B9(1)", commands33, true, true);

        Command[] commands34 = [Command.Num2, Command.Acoth, Command.None];
        TestDriver.Test("0.54930614433405484569762261846126", "coth\x207B\x00B9(2)", commands34, true, true);

        Command[] commands35 = [Command.Num8, Command.Pow2, Command.None];
        TestDriver.Test("256", "2^(8)", commands35, true, true);

        Command[] commands36 = [Command.Rand, Command.Ceil, Command.None];
        TestDriver.Test("1", "N/A", commands36, true, true);

        Command[] commands37 = [Command.Rand, Command.Floor, Command.None];
        TestDriver.Test("0", "N/A", commands37, true, true);

        Command[] commands38 = [Command.Rand, Command.Sign, Command.Ceil, Command.None];
        TestDriver.Test("0", "N/A", commands38, true, true);

        Command[] commands39 = [Command.Rand, Command.Sign, Command.Floor, Command.None];
        TestDriver.Test("-1", "N/A", commands39, true, true);

        Command[] commands40 =
            [Command.Num3, Command.Pnt, Command.Num8, Command.Floor, Command.None];
        TestDriver.Test("3", "floor(3.8)", commands40, true, true);

        Command[] commands41 =
            [Command.Num3, Command.Pnt, Command.Num8, Command.Ceil, Command.None];
        TestDriver.Test("4", "ceil(3.8)", commands41, true, true);

        Command[] commands42 =
            [Command.Num5, Command.LogBaseY, Command.Num3, Command.Add, Command.None];
        TestDriver.Test("1.4649735207179271671970404076786", "5 log base 3 + ", commands42, true, true);
    }
}
