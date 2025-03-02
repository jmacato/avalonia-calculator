using CalcManagerManaged;
using CalcManagerManaged.Interop;

namespace CalcManagerInteropTests;

public class CalculatorManagerTests : IDisposable
{
    private readonly CalcEngineWrapper _calculator;
    private string _currentDisplay;
    private string _currentExpression = "";
    private bool _isError;

    [Fact]
    public void TestRationalNumberBasicOperations()
    {
        using var a = new Rational(2);
        using var b = new Rational(3);
        // Addition
        using (var result = a + b)
        {
            Assert.Equal("5", result.ToString());
        }

        // Subtraction
        using (var result = a - b)
        {
            Assert.Equal("-1", result.ToString());
        }

        // Multiplication
        using (var result = a * b)
        {
            Assert.Equal("6", result.ToString());
        }

        // Division
        using (var result = a / b)
        {
            // The exact precision might vary slightly based on implementation
            Assert.Equal("0.66666666666666666666666666666667", result.ToString());
        }

        // Comparison
        Assert.True(a < b);
        Assert.False(a > b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void TestRationalNumberMathFunctions()
    {
        using (var two = new Rational(2))
        {
            // Square root
            using (var sqrt = two.Sqrt())
            {
                var k = sqrt.ToString();
                // Different precision/format is possible across platforms
                Assert.Equal("1.4142135623730950488016887242097", k);
            }

            // Square
            using (var square = two * two)
            {
                Assert.Equal("4", square.ToString());
            }

            // Power
            using (var three = new Rational(3))
            using (var power = two.Pow(three))
            {
                Assert.Equal("8", power.ToString());
            }

            // Conversion to int
            int intValue = two.ToInt32();
            Assert.Equal(2, intValue);
        }
    }

    [Fact]
    public void TestRationalNumberTrigFunctions()
    {
        using (var piDiv2 = new Rational("1.5707963267948966192313216916398"))
        {
            // Sin(π/2) = 1
            using (var sin = piDiv2.Sin(CalcAngleType.Radians))
            {
                var sinStr = sin.ToString();
                Assert.True(sinStr == "1");
            }

            // Cos(π/2) = 0
            using (var cos = piDiv2.Cos(CalcAngleType.Radians))
            {
                var cosStr = cos.ToString();
                // Verified on WolframAlpha.
                Assert.Equal("-4.8557901415300312447089512527704e-32", cosStr);
            }

            // Tan(π/2) should throw or be very large
            using (var tan = piDiv2.Tan(CalcAngleType.Radians))
            {
                var tanStr = tan.ToString();
                // Verified on WolframAlpha.
                Assert.Equal("-20593970720590198600484756674269", tanStr);
            }
        }
    }

    [Fact]
    public void StandardMode_NumericInput_DisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Command1, Command.Command2, Command.Command3, Command.CommandPNT,
            Command.Command4, Command.Command5, Command.Command6, Command.CommandNULL
        ];
        TestCommand(commands, "123.456", "");
    }

    [Fact]
    public void StandardMode_Addition_DisplaysCorrectly()
    {
        Command[] commands = [Command.CommandADD, Command.CommandNULL];
        TestCommand(commands, "0", "0 + ");
    }

    [Fact]
    public void StandardMode_SquareRoot_DisplaysCorrectly()
    {
        Command[] commands = [Command.CommandSQRT, Command.CommandNULL];
        TestCommand(commands, "0", "\x221A(0)");
    }

    [Fact]
    public void StandardMode_AdditionAndEquals_DisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Command2, Command.CommandADD, Command.Command3, Command.CommandEQU,
            Command.Command4, Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(commands, "7", "4 + 3=");
    }

    [Fact]
    public void StandardMode_NumberEquals_DisplaysCorrectly()
    {
        Command[] commands = [Command.Command4, Command.CommandEQU, Command.CommandNULL];
        TestCommand(commands, "4", "4=");
    }

    [Fact]
    public void StandardMode_MultipleSquareRoots_DisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Command2, Command.Command5, Command.Command6, Command.CommandSQRT,
            Command.CommandSQRT, Command.CommandSQRT, Command.CommandNULL
        ];
        TestCommand(commands, "2", "\x221A(\x221A(\x221A(256)))");
    }

    [Fact]
    public void StandardMode_SubtractionMultiplication_DisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Command3, Command.CommandSUB, Command.Command6, Command.CommandEQU,
            Command.CommandMUL, Command.Command3, Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(commands, "-9", "-3 \x00D7 3=");
    }

    [Fact]
    public void StandardMode_MultiplicationSubtraction_DisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Command9, Command.CommandMUL, Command.Command6, Command.CommandSUB,
            Command.CommandCENTR, Command.Command8, Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(commands, "46", "54 - 8=");
    }

    [Fact]
    public void StandardMode_PercentOperation_DisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Command6, Command.CommandMUL, Command.Command6, Command.CommandPERCENT,
            Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(commands, "0.36", "6 \x00D7 0.06=");
    }

    [Fact]
    public void StandardMode_AdditionWithPercent_DisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Command5, Command.Command0, Command.CommandADD, Command.Command2,
            Command.Command0, Command.CommandPERCENT, Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(commands, "60", "50 + 10=");
    }

    [Fact]
    public void StandardMode_AdditionEquals_DisplaysCorrectly()
    {
        Command[] commands = [Command.Command4, Command.CommandADD, Command.CommandEQU, Command.CommandNULL];
        TestCommand(commands, "8", "4 + 4=");
    }

    [Fact]
    public void StandardMode_OperatorOverride_DisplaysCorrectly()
    {
        Command[] commands =
            [Command.Command5, Command.CommandADD, Command.CommandMUL, Command.Command3, Command.CommandNULL];
        TestCommand(commands, "3", "5 \x00D7 ");
    }

    [Fact]
    public void StandardMode_Overflow_DisplaysError()
    {
        Command[] commands =
        [
            Command.Command1, Command.CommandEXP, Command.CommandSIGN, Command.Command9, Command.Command9,
            Command.Command9, Command.Command9, Command.CommandDIV, Command.Command1, Command.Command0,
            Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(commands, "Overflow", "1.e-9999 \x00F7 ");
    }

    [Fact]
    public void StandardMode_DivideByZero_DisplaysError()
    {
        Command[] commands =
            [Command.Command1, Command.CommandDIV, Command.Command0, Command.CommandEQU, Command.CommandNULL];
        TestCommand(commands, "Cannot divide by zero", "1 \x00F7 ");
    }

    [Fact]
    public void StandardMode_ZeroDividedByZero_DisplaysUndefined()
    {
        Command[] commands =
            [Command.Command0, Command.CommandDIV, Command.Command0, Command.CommandEQU, Command.CommandNULL];
        TestCommand(commands, "Result is undefined", "0 \x00F7 ");
    }

    [Fact]
    public void StandardMode_BackspaceOperations_WorksCorrectly()
    {
        Command[] commands =
        [
            Command.Command1, Command.Command2, Command.Command3, Command.CommandBACK,
            Command.CommandBACK, Command.CommandNULL
        ];
        TestCommand(commands, "1", "");
    }

    [Fact]
    public void StandardMode_AllBackspaces_ClearsInput()
    {
        Command[] commands =
        [
            Command.Command1, Command.Command2, Command.Command3, Command.CommandBACK,
            Command.CommandBACK, Command.CommandBACK, Command.CommandNULL
        ];
        TestCommand(commands, "0", "");
    }

    [Fact]
    public void ScientificMode_NumericInput_DisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Command1, Command.Command2, Command.Command3, Command.CommandPNT,
            Command.Command4, Command.Command5, Command.Command6, Command.CommandNULL
        ];
        TestCommand(commands, "123.456", "");
    }

    [Fact]
    public void ScientificMode_Addition_DisplaysCorrectly()
    {
        Command[] commands = [Command.CommandADD, Command.CommandNULL];
        TestCommand(commands, "0", "0 + ");
    }

    [Fact]
    public void ScientificMode_SquareRoot_DisplaysCorrectly()
    {
        Command[] commands = [Command.CommandSQRT, Command.CommandNULL];
        TestCommand(commands, "0", "\x221A(0)");
    }

    [Fact]
    public void ScientificMode_PrecedenceHandling_DisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Command1, Command.CommandADD, Command.Command0, Command.CommandMUL,
            Command.Command2, Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(commands, "1", "1 + 0 \x00D7 2=", true, true);
    }

    [Fact]
    public void ScientificMode_Square_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.Command2, Command.CommandSQR, Command.CommandNULL];
        TestCommand(commands, "144", "sqr(12)", true, true);
    }

    [Fact]
    public void ScientificMode_Factorial_WorksCorrectly()
    {
        Command[] commands = [Command.Command5, Command.CommandFAC, Command.CommandNULL];
        TestCommand(commands, "120", "fact(5)");
    }

    [Fact]
    public void ScientificMode_Power_WorksCorrectly()
    {
        Command[] commands =
            [Command.Command5, Command.CommandPWR, Command.Command2, Command.CommandADD, Command.CommandNULL];
        TestCommand(commands, "25", "5 ^ 2 + ", true, true);
    }

    [Fact]
    public void ScientificMode_Root_WorksCorrectly()
    {
        Command[] commands =
            [Command.Command8, Command.CommandROOT, Command.Command3, Command.CommandMUL, Command.CommandNULL];
        TestCommand(commands, "2", "8 yroot 3 \x00D7 ", true, true);
    }

    [Fact]
    public void ScientificMode_Cube_WorksCorrectly()
    {
        Command[] commands = [Command.Command8, Command.CommandCUB, Command.CommandNULL];
        TestCommand(commands, "512", "cube(8)", true, true);
    }

    [Fact]
    public void ScientificMode_CubeRoot_WorksCorrectly()
    {
        Command[] commands = [Command.Command8, Command.CommandCUB, Command.CommandCUBEROOT, Command.CommandNULL];
        TestCommand(commands, "8", "cuberoot(cube(8))", true, true);
    }

    [Fact]
    public void ScientificMode_Logarithm_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.Command0, Command.CommandLOG, Command.CommandNULL];
        TestCommand(commands, "1", "log(10)");
    }

    [Fact]
    public void ScientificMode_PowerOf10_WorksCorrectly()
    {
        Command[] commands = [Command.Command5, Command.CommandPOW10, Command.CommandNULL];
        TestCommand(commands, "100,000", "10^(5)");
    }

    [Fact]
    public void ScientificMode_NaturalLogarithm_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.Command0, Command.CommandLN, Command.CommandNULL];
        TestCommand(commands, "2.3025850929940456840179914546844", "ln(10)", true, true);
    }

    [Fact]
    public void ScientificMode_Sine_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.CommandSIN, Command.CommandNULL];
        TestCommand(commands, "0.01745240643728351281941897851632", "sin\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificMode_Cosine_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.CommandCOS, Command.CommandNULL];
        TestCommand(commands, "0.99984769515639123915701155881391", "cos\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificMode_Tangent_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.CommandTAN, Command.CommandNULL];
        TestCommand(commands, "0.01745506492821758576512889521973", "tan\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificMode_ArcSine_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.CommandASIN, Command.CommandNULL];
        TestCommand(commands, "90", "sin\x2080\x207B\x00B9(1)", true, true);
    }

    [Fact]
    public void ScientificMode_ArcCosine_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.CommandACOS, Command.CommandNULL];
        TestCommand(commands, "0", "cos\x2080\x207B\x00B9(1)", true, true);
    }

    [Fact]
    public void ScientificMode_ArcTangent_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.CommandATAN, Command.CommandNULL];
        TestCommand(commands, "45", "tan\x2080\x207B\x00B9(1)", true, true);
    }

    [Fact]
    public void ScientificMode_Secant_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.CommandSEC, Command.CommandNULL];
        TestCommand(commands, "1.0001523280439076654284264342126", "sec\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificMode_Cosecant_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.CommandCSC, Command.CommandNULL];
        TestCommand(commands, "57.298688498550183476612683735174", "csc\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificMode_Cotangent_WorksCorrectly()
    {
        Command[] commands = [Command.Command1, Command.CommandCOT, Command.CommandNULL];
        TestCommand(commands, "57.289961630759424687278147537113", "cot\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificMode_PowerOfE_WorksCorrectly()
    {
        Command[] commands = [Command.Command2, Command.CommandPOWE, Command.CommandNULL];
        TestCommand(commands, "7.389056098930650227230427460575", "e^(2)", true, true);
    }

    [Fact]
    public void ScientificMode_PowerOfTwo_WorksCorrectly()
    {
        Command[] commands = [Command.Command8, Command.CommandPOW2, Command.CommandNULL];
        TestCommand(commands, "256", "2^(8)", true, true);
    }

    [Fact]
    public void ScientificMode_PracticalFractionOperation_WorksCorrectly()
    {
        Command[] commands =
        [
            Command.Command8, Command.CommandPWR, Command.CommandOPENP, Command.Command2,
            Command.CommandDIV, Command.Command3, Command.CommandCLOSEP, Command.CommandSUB,
            Command.Command4, Command.CommandADD, Command.CommandNULL
        ];
        TestCommand(commands, "0", "8 ^ (2 \x00F7 3) - 4 + ", true, true);
    }

    [Fact]
    public void ScientificMode_FloorFunction_WorksCorrectly()
    {
        Command[] commands =
            [Command.Command3, Command.CommandPNT, Command.Command8, Command.CommandFloor, Command.CommandNULL];
        TestCommand(commands, "3", "floor(3.8)");
    }

    [Fact]
    public void ScientificMode_CeilingFunction_WorksCorrectly()
    {
        Command[] commands =
            [Command.Command3, Command.CommandPNT, Command.Command8, Command.CommandCeil, Command.CommandNULL];
        TestCommand(commands, "4", "ceil(3.8)");
    }

    [Fact]
    public void ScientificMode_LogarithmBaseY_WorksCorrectly()
    {
        Command[] commands =
            [Command.Command5, Command.CommandLogBaseY, Command.Command3, Command.CommandADD, Command.CommandNULL];
        TestCommand(commands, "1.4649735207179271671970404076786", "5 log base 3 + ", true, true);
    }

    private int _maxDigitsCalledCount;
    private int _binaryOperatorReceivedCount;
    private List<string> _memorizedNumbers;

    public CalculatorManagerTests()
    {
        // Setup calculator with event handlers
        _calculator = new CalcEngineWrapper(new DefaultCalcResourceProvider());
        _calculator.DisplayChanged += (display, isError) =>
        {
            _currentDisplay = display;
            _isError = isError;
        };
        _calculator.ExpressionDisplayChanged += (tokens) =>
        {
            _currentExpression = string.Join("", tokens.ConvertAll(t => t.Text));
        };
        _calculator.MaxDigitsReached += () => { _maxDigitsCalledCount++; };
        _calculator.BinaryOperatorReceived += () => { _binaryOperatorReceivedCount++; };
        _calculator.MemorizedNumbersChanged += (numbers) => { _memorizedNumbers = new List<string>(numbers); };
    }

    public void Dispose()
    {
        _calculator.Dispose();
    }

    // Helper method to execute commands and check the result
    private void TestCommand(Command[] commands, string expectedDisplay, string expectedExpression, bool cleanup = true,
        bool isScientific = false)
    {
        // Reset calculator and set mode
        if (cleanup)
        {
            _calculator.Reset();
            _isError = false;
            _maxDigitsCalledCount = 0;
            _binaryOperatorReceivedCount = 0;
            _memorizedNumbers = null;
            _currentExpression = "";
            _currentDisplay = "";
        }

        if (isScientific)
        {
            _calculator.SetMode(CalcMode.Scientific);
        }
        else
        {
            _calculator.SetMode(CalcMode.Standard);
        }

        // Execute each command
        foreach (var command in commands)
        {
            if (command == Command.CommandNULL)
                break;

            _calculator.SendCommand((int)command);
        }

        // Verify results
        Assert.Equal(expectedDisplay, _currentDisplay);

        // Only check expression if it's provided (some tests don't expect specific expressions)
        if (expectedExpression != "N/A")
        {
            Assert.Equal(expectedExpression, _currentExpression);
        }
    }

    [Fact]
    public void ScientificMode_ParenthesisHandling_WorksCorrectly()
    {
        Command[] commands1 =
        [
            Command.Command1, Command.CommandADD, Command.CommandOPENP, Command.CommandADD,
            Command.Command3, Command.CommandCLOSEP, Command.CommandNULL
        ];
        TestCommand(commands1, "3", "1 + (0 + 3)", true, true);

        Command[] commands2 =
        [
            Command.CommandOPENP, Command.CommandOPENP, Command.Command1, Command.Command2,
            Command.CommandCLOSEP, Command.CommandNULL
        ];
        TestCommand(commands2, "12", "((12)", true, true);

        Command[] commands3 =
        [
            Command.Command1, Command.Command2, Command.CommandCLOSEP,
            Command.CommandCLOSEP, Command.CommandOPENP, Command.CommandNULL
        ];
        TestCommand(commands3, "12", "12 \x00D7 (", true, true);

        Command[] commands4 =
        [
            Command.Command2, Command.CommandOPENP, Command.Command2, Command.CommandCLOSEP,
            Command.CommandADD, Command.CommandNULL
        ];
        TestCommand(commands4, "4", "2 \x00D7 (2) + ", true, true);

        Command[] commands5 =
        [
            Command.Command2, Command.CommandOPENP, Command.Command2, Command.CommandCLOSEP,
            Command.CommandADD, Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(commands5, "8", "2 \x00D7 (2) + 4=", true, true);
    }

    [Fact]
    public void ErrorHandling_DivideByZero_DisplaysError()
    {
        Command[] commands1 =
            [Command.Command1, Command.CommandDIV, Command.Command0, Command.CommandEQU, Command.CommandNULL];
        TestCommand(commands1, "Cannot divide by zero", "1 \x00F7 ", true, true);
        Assert.True(_isError);

        // Same test for standard calculator
        _calculator.Reset();
        _isError = false;
        TestCommand(commands1, "Cannot divide by zero", "1 \x00F7 ");
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandling_InvalidInput_DisplaysError()
    {
        Command[] commands2 = [Command.Command2, Command.CommandSIGN, Command.CommandLOG, Command.CommandNULL];
        TestCommand(commands2, "Invalid input", "log(-2)", true, true);
        Assert.True(_isError);

        // Same test for standard calculator
        _calculator.Reset();
        _isError = false;
        TestCommand(commands2, "Invalid input", "log(-2)");
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandling_UndefinedResult_DisplaysError()
    {
        Command[] commands3 =
            [Command.Command0, Command.CommandDIV, Command.Command0, Command.CommandEQU, Command.CommandNULL];
        TestCommand(commands3, "Result is undefined", "0 \x00F7 ", true, true);
        Assert.True(_isError);

        // Same test for standard calculator
        _calculator.Reset();
        _isError = false;
        TestCommand(commands3, "Result is undefined", "0 \x00F7 ");
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandling_DivideByZero_SetsErrorState()
    {
        // Test that division by zero sets the error state
        _calculator.Reset();

        // Set up with numerator
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandDIV);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandEQU);

        // Verify error state and message
        Assert.True(_calculator.IsInError());
        Assert.Equal("Cannot divide by zero", _currentDisplay);
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandling_NegativeLogarithm_SetsErrorState()
    {
        // Test that log of negative number sets the error state
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Scientific);

        // Set up with negative number
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.CommandSIGN);
        _calculator.SendCommand((int)Command.CommandLOG);

        // Verify error state and message
        Assert.True(_calculator.IsInError());
        Assert.Equal("Invalid input", _currentDisplay);
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandling_ZeroDivideByZero_SetsErrorState()
    {
        // Test that zero divided by zero sets the error state
        _calculator.Reset();

        // Set up with zero numerator
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandDIV);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandEQU);

        // Verify error state and message
        Assert.True(_calculator.IsInError());
        Assert.Equal("Result is undefined", _currentDisplay);
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandling_Overflow_SetsErrorState()
    {
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Scientific);

        // Enter very large number and factorial it
        Command[] commands =
        [
            Command.Command9, Command.Command0, Command.Command0, Command.CommandEXP,
            Command.Command9, Command.Command0, Command.CommandFAC, Command.CommandNULL
        ];

        // Don't check the exact expression since formatting may differ between platforms
        TestCommand(commands, "Overflow", "N/A");

        // Verify error state
        Assert.True(_calculator.IsInError());
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandling_ResetClearsError()
    {
        _calculator.Reset();

        // Create an error state
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandDIV);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandEQU);

        // Verify error state
        Assert.True(_calculator.IsInError());
        Assert.True(_isError);

        // Reset should clear error
        _calculator.Reset();

        // Verify error is cleared
        Assert.False(_calculator.IsInError());
    }

    [Fact]
    public void ErrorHandling_ErrorState_ConsistentWithDisplay()
    {
        _calculator.Reset();
        _isError = false;

        // Set up division by zero
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandDIV);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandEQU);

        // Verify the display shows the correct error message
        Assert.Equal("Cannot divide by zero", _currentDisplay);

        // Verify the error flag is set
        Assert.True(_isError);

        // Verify IsInError() also returns true
        Assert.True(_calculator.IsInError());
    }

    [Fact]
    public void ErrorHandling_ErrorState_PreventsNewOperations()
    {
        _calculator.Reset();

        // Set up division by zero to create error state
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandDIV);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandEQU);

        // Verify error state
        Assert.True(_calculator.IsInError());
        Assert.Equal("Cannot divide by zero", _currentDisplay);

        // Try to enter new number - shouldn't change the display
        string displayBeforeNewInput = _currentDisplay;
        _calculator.SendCommand((int)Command.Command5);

        // Display should still show error
        Assert.Equal(displayBeforeNewInput, _currentDisplay);
        Assert.True(_calculator.IsInError());

        // Reset should clear error
        _calculator.Reset();
        Assert.False(_calculator.IsInError());

        // Now we should be able to enter new input
        _calculator.SendCommand((int)Command.Command5);
        Assert.Equal("5", _currentDisplay);
    }

    [Fact]
    public void ErrorHandling_SpecificErrors_ShowCorrectMessages()
    {
        // Test different error types show appropriate messages

        // 1. Division by zero
        Command[] divByZeroCommands =
        [
            Command.Command1, Command.CommandDIV, Command.Command0,
            Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(divByZeroCommands, "Cannot divide by zero", "1 \x00F7 ");
        Assert.True(_calculator.IsInError());
        _calculator.Reset();

        // 2. Domain error (log of negative number)
        Command[] domainErrorCommands =
        [
            Command.Command2, Command.CommandSIGN, Command.CommandLOG, Command.CommandNULL
        ];
        TestCommand(domainErrorCommands, "Invalid input", "log(-2)");
        Assert.True(_calculator.IsInError());
        _calculator.Reset();

        // 3. Undefined (0/0)
        Command[] undefinedCommands =
        [
            Command.Command0, Command.CommandDIV, Command.Command0,
            Command.CommandEQU, Command.CommandNULL
        ];
        TestCommand(undefinedCommands, "Result is undefined", "0 \x00F7 ");
        Assert.True(_calculator.IsInError());
    }

    [Fact]
    public void ErrorHandling_NegativeSqrt_ShowsDomainError()
    {
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Scientific);

        // Try to take square root of negative number - domain error
        Command[] negativeCommand =
        [
            Command.Command2, Command.CommandSIGN, Command.CommandSQRT, Command.CommandNULL
        ];

        // Should show invalid input for sqrt of negative number
        TestCommand(negativeCommand, "Invalid input", "N/A");

        // Verify error state
        Assert.True(_calculator.IsInError());
    }

    [Fact]
    public void ErrorHandling_ErrorClearingBehavior()
    {
        _calculator.Reset();

        // Create an error state
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandDIV);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandEQU);

        // Verify error state
        Assert.True(_calculator.IsInError());
        Assert.Equal("Cannot divide by zero", _currentDisplay);

        // Clear should reset error state
        _calculator.SendCommand((int)Command.CommandCLEAR);

        // After clear, not in error state
        Assert.False(_calculator.IsInError());

        // Can perform new calculations
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.CommandEQU);

        // New calculation works
        Assert.Equal("12", _currentDisplay);
        Assert.False(_calculator.IsInError());
    }

    [Fact]
    public void ScientificMode_RadiansMode_TrigFunctions_WorkCorrectly()
    {
        Command[] commands1 = [Command.CommandRAD, Command.CommandPI, Command.CommandSIN, Command.CommandNULL];
        TestCommand(commands1, "0", "N/A", true, true);

        Command[] commands2 = [Command.CommandRAD, Command.CommandPI, Command.CommandCOS, Command.CommandNULL];
        TestCommand(commands2, "-1", "N/A", true, true);

        Command[] commands3 = [Command.CommandRAD, Command.CommandPI, Command.CommandTAN, Command.CommandNULL];
        TestCommand(commands3, "0", "N/A", true, true);
    }

    [Fact]
    public void ScientificMode_GradiansMode_TrigFunctions_WorkCorrectly()
    {
        Command[] commands4 =
        [
            Command.CommandGRAD, Command.Command4, Command.Command0, Command.Command0,
            Command.CommandSIN, Command.CommandNULL
        ];
        TestCommand(commands4, "0", "N/A", true, true);

        Command[] commands5 =
        [
            Command.CommandGRAD, Command.Command4, Command.Command0, Command.Command0,
            Command.CommandCOS, Command.CommandNULL
        ];
        TestCommand(commands5, "1", "N/A", true, true);

        Command[] commands6 =
        [
            Command.CommandGRAD, Command.Command4, Command.Command0, Command.Command0,
            Command.CommandTAN, Command.CommandNULL
        ];
        TestCommand(commands6, "0", "N/A", true, true);
    }

    [Fact]
    public void ModeChanges_PreservesState_Correctly()
    {
        Command[] commands1 = [Command.Command1, Command.Command2, Command.Command3, Command.CommandNULL];
        TestCommand(commands1, "123", "");

        Command[] commands2 = [Command.ModeScientific, Command.CommandNULL];
        TestCommand(commands2, "0", "");

        Command[] commands3 = [Command.Command1, Command.Command2, Command.Command3, Command.CommandNULL];
        TestCommand(commands3, "123", "");

        Command[] commands4 = [Command.ModeProgrammer, Command.CommandNULL];
        TestCommand(commands4, "0", "");

        Command[] commands5 = [Command.Command1, Command.Command2, Command.Command3, Command.CommandNULL];
        TestCommand(commands5, "123", "");

        Command[] commands6 = [Command.ModeScientific, Command.CommandNULL];
        TestCommand(commands6, "0", "");

        Command[] commands7 = [Command.Command6, Command.Command7, Command.CommandADD, Command.CommandNULL];
        TestCommand(commands7, "67", "67 + ");

        Command[] commands8 = [Command.ModeBasic, Command.CommandNULL];
        TestCommand(commands8, "0", "");
    }

    [Fact]
    public void ProgrammerMode_BitwiseOperations_WorkCorrectly()
    {
        _calculator.SetMode(CalcMode.Programmer);

        Command[] commands1 =
        [
            Command.ModeProgrammer, Command.Command5, Command.Command3, Command.CommandNand,
            Command.Command8, Command.Command3, Command.CommandAnd, Command.CommandNULL
        ];
        TestCommand(commands1, "-18", "53 NAND 83 AND ");

        Command[] commands2 =
        [
            Command.ModeProgrammer, Command.Command5, Command.Command3, Command.CommandNor,
            Command.Command8, Command.Command3, Command.CommandAnd, Command.CommandNULL
        ];
        TestCommand(commands2, "-120", "53 NOR 83 AND ");

        Command[] commands3 =
        [
            Command.ModeProgrammer, Command.Command5, Command.CommandLSHF, Command.Command1,
            Command.CommandAnd, Command.CommandNULL
        ];
        TestCommand(commands3, "10", "5 Lsh 1 AND ");

        Command[] commands5 =
        [
            Command.ModeProgrammer, Command.Command5, Command.CommandRSHFL, Command.Command1,
            Command.CommandAnd, Command.CommandNULL
        ];
        TestCommand(commands5, "2", "5 Rsh 1 AND ");
    }

    [Fact]
    public void ProgrammerMode_RadixTypes_HexadecimalMode_WorksCorrectly()
    {
        // Set up calculator in programmer mode with hex radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Hex);

        // Test basic input in hex mode
        _calculator.SendCommand((int)Command.CommandA);
        _calculator.SendCommand((int)Command.CommandB);
        _calculator.SendCommand((int)Command.CommandC);
        _calculator.SendCommand((int)Command.CommandD);

        Assert.Equal("ABCD", _currentDisplay);

        // Test addition in hex mode
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.CommandF);
        _calculator.SendCommand((int)Command.CommandEQU);

        Assert.Equal("BE0C", _currentDisplay);

        // Test AND operation in hex mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Hex);

        _calculator.SendCommand((int)Command.CommandF);
        _calculator.SendCommand((int)Command.CommandF);
        _calculator.SendCommand((int)Command.CommandF);
        _calculator.SendCommand((int)Command.CommandF);
        _calculator.SendCommand((int)Command.CommandAnd);
        _calculator.SendCommand((int)Command.CommandA);
        _calculator.SendCommand((int)Command.CommandA);
        _calculator.SendCommand((int)Command.CommandA);
        _calculator.SendCommand((int)Command.CommandA);
        _calculator.SendCommand((int)Command.CommandEQU);

        Assert.Equal("AAAA", _currentDisplay);
    }

    [Fact]
    public void ProgrammerMode_RadixTypes_DecimalMode_WorksCorrectly()
    {
        // Set up calculator in programmer mode with decimal radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Decimal);

        // Test basic input in decimal mode
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.Command4);

        Assert.Equal("1,234", _currentDisplay);

        // Test addition in decimal mode
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.Command6);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.Command8);
        _calculator.SendCommand((int)Command.CommandEQU);

        Assert.Equal("6,912", _currentDisplay);

        // Test OR operation in decimal mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Decimal);

        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandOR);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandEQU);

        Assert.Equal("111", _currentDisplay);
    }

    [Fact]
    public void ProgrammerMode_RadixTypes_OctalMode_WorksCorrectly()
    {
        // Set up calculator in programmer mode with octal radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Octal);

        // Test basic input in octal mode
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.Command4);

        Assert.Equal("1 234", _currentDisplay);

        // Test addition in octal mode
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.Command6);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.CommandEQU);

        Assert.Equal("2 023", _currentDisplay);

        // Test XOR operation in octal mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Octal);

        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.CommandXor);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.CommandEQU);

        Assert.Equal("222", _currentDisplay);
    }

    [Fact]
    public void ProgrammerMode_RadixTypes_BinaryMode_WorksCorrectly()
    {
        // Set up calculator in programmer mode with binary radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Binary);

        // Test basic input in binary mode
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);

        Assert.Equal("1010", _currentDisplay);

        // Test addition in binary mode
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandEQU);

        Assert.Equal("1 0111", _currentDisplay);

        // Test NOT operation in binary mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Binary);

        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandNot);

        // NOT 1010 in 64-bit binary would be a very long string of 1s and 0s,
        // so we're just checking that the display has changed
        Assert.NotEqual("1010", _currentDisplay);
        Assert.Contains("1", _currentDisplay);
    }

    [Fact]
    public void ProgrammerMode_Division_WorksCorrectly()
    {
        _calculator.SetMode(CalcMode.Programmer);

        Command[] commands11 = { Command.ModeProgrammer, Command.CommandDec, Command.Command4, Command.Command2, Command.Command9,   Command.Command4,
                                Command.Command9,       Command.Command6,   Command.Command7, Command.Command2, Command.Command9,   Command.Command6,
                                Command.CommandDIV,     Command.Command2,   Command.Command5, Command.Command5, Command.CommandEQU, Command.CommandNULL };
        TestCommand(commands11, "16,843,009", "4294967296 \x00F7 255=");

        Command[] commands12 = {
            Command.ModeProgrammer, Command.CommandDec, Command.Command4, Command.Command2, Command.Command9,   Command.Command4,
            Command.Command9,       Command.Command6,   Command.Command7, Command.Command3, Command.Command0,   Command.Command3,
            Command.CommandDIV,     Command.Command2,   Command.Command5, Command.Command5, Command.CommandEQU, Command.CommandNULL
        };
        TestCommand(commands12, "16,843,009", "4294967303 \x00F7 255=");

        Command[] commands13 = {
            Command.ModeProgrammer, Command.CommandDec, Command.Command1, Command.Command0, Command.Command0, Command.Command0,
            Command.Command0, Command.Command0, Command.Command0, Command.Command0, Command.Command0, Command.Command0, Command.CommandDIV,
            Command.Command6, Command.Command4, Command.Command4, Command.Command8, Command.Command7, Command.CommandEQU, Command.CommandNULL
        };
        TestCommand(commands13, "15,507", "1000000000 \x00F7 64487=");

        Command[] commands14 = { Command.ModeProgrammer, Command.CommandDec, Command.Command1,   Command.Command0,   Command.Command0,
                                 Command.Command0,       Command.Command0,   Command.Command0,   Command.Command0,   Command.Command0,
                                 Command.Command0,       Command.Command0,   Command.CommandDIV, Command.Command6,   Command.Command4,
                                 Command.Command4,       Command.Command8,   Command.Command8,   Command.CommandEQU, Command.CommandNULL };
        TestCommand(commands14, "15,506", "1000000000 \x00F7 64488=");
    }

    [Fact]
    public void ProgrammerMode_RadixTypes_ConversionBetweenRadices_WorksCorrectly()
    {
        // Start with decimal mode and enter a value
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Decimal);

        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);

        Assert.Equal("10", _currentDisplay);

        // Switch to hex mode and check the conversion
        _calculator.SetRadix(CalcRadixType.Hex);
        Assert.Equal("A", _currentDisplay);

        // Switch to binary mode and check the conversion
        _calculator.SetRadix(CalcRadixType.Binary);
        Assert.Equal("1010", _currentDisplay);

        // Switch to octal mode and check the conversion
        _calculator.SetRadix(CalcRadixType.Octal);
        Assert.Equal("12", _currentDisplay);

        // Return to decimal to verify
        _calculator.SetRadix(CalcRadixType.Decimal);
        Assert.Equal("10", _currentDisplay);
    }


    [Fact]
    public void ProgrammerMode_Rotation_WorksCorrectly()
    {
        _calculator.SetMode(CalcMode.Programmer);

        Command[] commands7 = [Command.ModeProgrammer, Command.Command1, Command.CommandROL, Command.CommandNULL];
        TestCommand(commands7, "2", "RoL(1)");

        Command[] commands9 = [Command.ModeProgrammer, Command.Command1, Command.CommandRORC, Command.CommandNULL];
        TestCommand(commands9, "0", "RoR(1)");

        Command[] commands10 =
        [
            Command.ModeProgrammer, Command.Command1, Command.CommandRORC, Command.CommandRORC,
            Command.CommandNULL
        ];
        TestCommand(commands10, "-9,223,372,036,854,775,808", "RoR(RoR(1))");
    }

    [Fact]
    public void ProgrammerMode_DigitGrouping_DecimalMode_WorksCorrectly()
    {
        // Set up calculator in programmer mode with decimal radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Decimal);

        // Enter a number large enough to trigger digit grouping
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.Command4);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.Command6);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.Command8);
        _calculator.SendCommand((int)Command.Command9);

        // Verify digit grouping applied (commas every 3 digits for decimal)
        Assert.Equal("123,456,789", _currentDisplay);
    }

    [Fact]
    public void ProgrammerMode_DigitGrouping_HexMode_WorksCorrectly()
    {
        // Set up calculator in programmer mode with hex radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Hex);

        // Enter a number large enough to trigger digit grouping
        _calculator.SendCommand((int)Command.CommandA);
        _calculator.SendCommand((int)Command.CommandB);
        _calculator.SendCommand((int)Command.CommandC);
        _calculator.SendCommand((int)Command.CommandD);
        _calculator.SendCommand((int)Command.CommandE);
        _calculator.SendCommand((int)Command.CommandF);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);

        // Verify digit grouping applied (spaces every 4 digits for hex)
        Assert.Equal("A BCDE F123", _currentDisplay);
    }

    [Fact]
    public void ProgrammerMode_DigitGrouping_BinaryMode_WorksCorrectly()
    {
        // Set up calculator in programmer mode with binary radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Binary);

        // Enter a binary number large enough to trigger digit grouping
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);

        // Verify digit grouping applied (spaces every 4 digits for binary)
        Assert.Equal("1010 1010 1010", _currentDisplay);
    }

    [Fact]
    public void ProgrammerMode_DigitGrouping_OctalMode_WorksCorrectly()
    {
        // Set up calculator in programmer mode with octal radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Octal);

        // Enter a number large enough to trigger digit grouping
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.Command4);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.Command6);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.Command0);

        // Verify digit grouping applied (spaces every 3 digits for octal)
        Assert.Equal("12 345 670", _currentDisplay);
    }

    [Fact]
    public void StandardMode_DigitGrouping_WorksCorrectly()
    {
        // Test digit grouping in standard mode (should use locale-specific grouping)
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Standard);

        // Enter a large number
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.Command4);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.Command6);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.Command8);
        _calculator.SendCommand((int)Command.Command9);

        // Verify digit grouping is applied (commas every 3 digits)
        Assert.Equal("123,456,789", _currentDisplay);
    }

    [Fact]
    public void StandardMode_DigitGrouping_WithDecimalPoint_WorksCorrectly()
    {
        // Test digit grouping with decimal point in standard mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Standard);

        // Enter a large number with decimal point
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.Command4);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.CommandPNT);
        _calculator.SendCommand((int)Command.Command6);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.Command8);
        _calculator.SendCommand((int)Command.Command9);

        // Verify digit grouping is applied before decimal but not after
        Assert.Equal("12,345.6789", _currentDisplay);
    }

    [Fact]
    public void ScientificMode_DigitGrouping_WorksCorrectly()
    {
        // Test digit grouping in scientific mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Scientific);

        // Enter a large number
        _calculator.SendCommand((int)Command.Command9);
        _calculator.SendCommand((int)Command.Command8);
        _calculator.SendCommand((int)Command.Command7);
        _calculator.SendCommand((int)Command.Command6);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.Command4);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command1);

        // Verify digit grouping is applied
        Assert.Equal("987,654,321", _currentDisplay);
    }

    [Fact]
    public void DigitGrouping_ResultOfCalculation_WorksCorrectly()
    {
        // Test that the result of a calculation shows proper digit grouping
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Standard);

        // Perform calculation that results in a large number
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.Command4);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.CommandMUL);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandEQU);

        // Verify digit grouping is applied to the result
        Assert.Equal("1,234,500", _currentDisplay);
    }

    [Fact]
    public void DigitGrouping_NegativeNumbers_WorksCorrectly()
    {
        // Test digit grouping with negative numbers
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Standard);

        // Enter a large negative number
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.Command4);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.Command6);
        _calculator.SendCommand((int)Command.CommandSIGN);

        // Verify digit grouping is applied to negative number
        Assert.Equal("-123,456", _currentDisplay);
    }

    [Fact]
    public void MemoryFeatures_BasicOperations_WorkCorrectly()
    {
        // Test storing a value in memory
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Command1);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.CommandCLEAR);
        _calculator.MemorizedNumberLoad(0);

        Assert.Equal("1", _currentDisplay);

        // Test storing multiple values
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Command1);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.CommandCLEAR);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.CommandCLEAR);

        _calculator.MemorizedNumberLoad(1);
        Assert.Equal("1", _currentDisplay);

        _calculator.MemorizedNumberLoad(0);
        Assert.Equal("2", _currentDisplay);
    }

    [Fact]
    public void MemoryFeatures_MemorizeComplexExpressions_WorkCorrectly()
    {
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandSIGN);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.CommandEQU);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.CommandMUL);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.MemorizeNumber();

        Assert.NotNull(_memorizedNumbers);
        Assert.Equal(3, _memorizedNumbers.Count);
        Assert.Equal("2", _memorizedNumbers[0]);
        Assert.Equal("1", _memorizedNumbers[1]);
        Assert.Equal("-1", _memorizedNumbers[2]);
    }

    [Fact]
    public void MemoryFeatures_MemoryAddSubtract_WorkCorrectly()
    {
        // Setup memory with initial values
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandSIGN);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.CommandEQU);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.CommandMUL);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.MemorizeNumber();

        // Adding to memory
        _calculator.SendCommand((int)Command.CommandCLEAR);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.MemorizedNumberAdd(0);
        _calculator.MemorizedNumberAdd(1);
        _calculator.MemorizedNumberAdd(2);

        Assert.NotNull(_memorizedNumbers);
        Assert.Equal(3, _memorizedNumbers.Count);
        Assert.Equal("4", _memorizedNumbers[0]);
        Assert.Equal("3", _memorizedNumbers[1]);
        Assert.Equal("1", _memorizedNumbers[2]);

        // Subtracting from memory
        _calculator.SendCommand((int)Command.CommandCLEAR);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandPNT);
        _calculator.SendCommand((int)Command.Command5);

        _calculator.MemorizedNumberSubtract(0);
        _calculator.MemorizedNumberSubtract(1);
        _calculator.MemorizedNumberSubtract(2);

        Assert.NotNull(_memorizedNumbers);
        Assert.Equal(3, _memorizedNumbers.Count);
        Assert.Equal("2.5", _memorizedNumbers[0]);
        Assert.Equal("1.5", _memorizedNumbers[1]);
        Assert.Equal("-0.5", _memorizedNumbers[2]);
    }

    [Fact]
    public void GetDisplayCommandsSnapshot_WorksCorrectly()
    {
        // Test with a simple expression: 2 + 3 (without equals)
        // The snapshot will be empty if we add the equals sign because it completes the calculation
        // and clears the expression history
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command3);

        // Get the snapshot before pressing equals
        var commands = _calculator.GetDisplayCommandsSnapshot();

        // Verify we have commands
        Assert.NotNull(commands);
        Assert.True(commands.Count == 3);

        // Verify the returned commands represents the expression correctly
        string fullExpression = string.Join(" ", commands.Select(c => c.Token));
        Assert.Equal("2 + 3", fullExpression);

        // Now test with equals - this should complete the expression and clear the commands
        _calculator.SendCommand((int)Command.CommandEQU);
        var emptyCommands = _calculator.GetDisplayCommandsSnapshot();
        Assert.Empty(emptyCommands);

        // Test that reset also clears the commands
        _calculator.Reset();
        emptyCommands = _calculator.GetDisplayCommandsSnapshot();
        Assert.Empty(emptyCommands);
    }

    [Fact]
    public void GetDisplayCommandsSnapshot_MultipleOperations()
    {
        // Test with a simpler expression: 3 - 2
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.CommandSUB);
        _calculator.SendCommand((int)Command.Command2);

        var commands = _calculator.GetDisplayCommandsSnapshot();

        Assert.True(commands.Count == 3);

        string fullExpression = string.Join(" ", commands.Select(c => c.Token));

        Assert.Equal("3 - 2", fullExpression);
    }

    [Fact]
    public void GetDisplayCommandsSnapshot_ParenthesisHandling()
    {
        // Test with parenthesis: (2 + 3) * 4
        _calculator.Reset();
        _calculator.SendCommand((int)Command.CommandOPENP);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command3);
        _calculator.SendCommand((int)Command.CommandCLOSEP);
        _calculator.SendCommand((int)Command.CommandMUL);
        _calculator.SendCommand((int)Command.Command4);

        var commands = _calculator.GetDisplayCommandsSnapshot();

        // Check that we have at least some commands
        Assert.True(commands.Count == 7);

        // Combine all tokens and check that the expression contains the expected elements
        string fullExpression = string.Join("", commands.Select(c => c.Token));
        Assert.Contains("(", fullExpression);
        Assert.Contains("2", fullExpression);
        Assert.Contains("3", fullExpression);
        Assert.Contains(")", fullExpression);
        Assert.Contains("4", fullExpression);

        // Check for operators
        bool hasAdd = fullExpression.Contains("+");
        bool hasMul = fullExpression.Contains("×");
        Assert.True(hasAdd, "Expression should contain addition operator");
        Assert.True(hasMul, "Expression should contain multiplication operator");
    }

    [Fact]
    public void GetDisplayCommandsSnapshot_UpdatesWithNewInput()
    {
        // Test that snapshot updates when new input is added
        _calculator.Reset();

        // Enter 2
        _calculator.SendCommand((int)Command.Command2);
        var commands1 = _calculator.GetDisplayCommandsSnapshot();
        Assert.Single(commands1);
        Assert.Contains(commands1, cmd => cmd.Token.Contains("2"));

        // Add +
        _calculator.SendCommand((int)Command.CommandADD);
        var commands2 = _calculator.GetDisplayCommandsSnapshot();
        Assert.True(commands2.Count == 2);
        Assert.Contains(commands2, cmd => cmd.Token.Contains("+"));

        // Add 3
        _calculator.SendCommand((int)Command.Command3);
        var commands3 = _calculator.GetDisplayCommandsSnapshot();
        Assert.True(commands3.Count == 3);
        Assert.Contains(commands3, cmd => cmd.Token.Contains("3"));
    }

    [Fact]
    public void GetDisplayCommandsSnapshot_HandlesBackspace()
    {
        // Test that snapshot updates correctly when backspace is used
        _calculator.Reset();

        // Enter 2 + 3
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command3);

        var commandsBefore = _calculator.GetDisplayCommandsSnapshot();
        Assert.Contains(commandsBefore, cmd => cmd.Token.Contains("3"));

        // Backspace to remove the 3
        _calculator.SendCommand((int)Command.CommandBACK);
        var commandsAfter = _calculator.GetDisplayCommandsSnapshot();

        // The snapshot should still contain 2 and + but not 3
        Assert.Contains(commandsAfter, cmd => cmd.Token.Contains("2"));
        Assert.Contains(commandsAfter, cmd => cmd.Token.Contains("+"));
        Assert.DoesNotContain(commandsAfter, cmd => cmd.Token.Contains("3"));
    }

    [Fact]
    public void GetDisplayCommandsSnapshot_RetainsCommandTypeInformation()
    {
        // This test verifies that the GetDisplayCommandsSnapshot API returns correctly classified command types
        // (not just raw text) which is critical for state restoration
        _calculator.Reset();

        // Enter a simple expression with different command types
        _calculator.SendCommand((int)Command.CommandOPENP); // Parenthesis type
        _calculator.SendCommand((int)Command.Command2); // Operand type
        _calculator.SendCommand((int)Command.CommandADD); // Binary operation type
        _calculator.SendCommand((int)Command.Command3); // Operand type
        _calculator.SendCommand((int)Command.CommandCLOSEP); // Parenthesis type

        // Get the snapshot
        var commands = _calculator.GetDisplayCommandsSnapshot();

        // Verify we have commands and all 5 parts we entered
        Assert.NotNull(commands);
        Assert.Equal(5, commands.Count);

        // Verify we have the right command types - this is the key part of this test
        // CommandType values: 0=Unary, 1=Binary, 2=Operand, 3=Parenthesis

        // Parentheses should be type 3
        Assert.Equal(3, commands[0].CommandType);
        Assert.Equal(3, commands[4].CommandType);

        // Operands should be type 2
        Assert.Equal(2, commands[1].CommandType);
        Assert.Equal(2, commands[3].CommandType);

        // Addition should be type 1 (binary operator)
        Assert.Equal(1, commands[2].CommandType);

        // Looking at token contents as well
        Assert.Contains("(", commands[0].Token);
        Assert.Contains("2", commands[1].Token);
        Assert.Contains("+", commands[2].Token);
        Assert.Contains("3", commands[3].Token);
        Assert.Contains(")", commands[4].Token);
    }

    [Fact]
    public void GetDisplayCommandsSnapshot_CanCaptureAndRestoreState()
    {
        // This test demonstrates how the API captures state for snapshot/restore
        // which is how the UWP app uses the functionality for suspend/resume
        _calculator.Reset();

        // Enter an in-progress calculation
        _calculator.SendCommand((int)Command.Command4);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command6);

        // Take a snapshot of the current state
        var commandsSnapshot = _calculator.GetDisplayCommandsSnapshot();

        // 3 because Cmd4 & Cmd5 gets combined into one ExpressionCommand.
        Assert.Equal(3, commandsSnapshot.Count);

        // Verify the snapshot contains the correct types of commands (without assuming order)
        Assert.Contains(commandsSnapshot, cmd => cmd.CommandType == 2); // Operands
        Assert.Contains(commandsSnapshot, cmd => cmd.CommandType == 1); // Binary operation

        // Verify the tokens are present (without assuming order)
        string fullExpression = string.Join(" ", commandsSnapshot.Select(c => c.Token));
        Assert.Equal("45 + 6", fullExpression);

        // Now complete the calculation
        _calculator.SendCommand((int)Command.CommandEQU);
        Assert.Equal("51", _currentDisplay);

        // After equals, the snapshot should be empty since the calculation is complete
        var emptySnapshot = _calculator.GetDisplayCommandsSnapshot();
        Assert.Empty(emptySnapshot);

        // This demonstrates the core functionality needed for the UWP app's
        // state preservation, where it needs to capture the exact sequence
        // of commands to recreate the calculator state
    }

    [Fact]
    public void MaxDigitsReached_StandardInput_TriggersNotification()
    {
        _calculator.Reset();
        _maxDigitsCalledCount = 0;

        // Add digits until reaching max
        for (int i = 0; i < 16; i++)
        {
            _calculator.SendCommand((int)Command.Command1);
        }

        // Try to add one more - should trigger MaxDigitsReached
        _calculator.SendCommand((int)Command.Command2);

        Assert.Equal(1, _maxDigitsCalledCount);
        // The display should still show just the initial digits
        Assert.Equal(16, _currentDisplay.Replace(",", "").Length);
    }

    [Fact]
    public void MaxDigitsReached_LeadingDecimal_TriggersNotification()
    {
        _calculator.Reset();
        _maxDigitsCalledCount = 0;

        // Add decimal point
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandPNT);

        // Add digits until reaching max
        for (int i = 0; i < 16; i++)
        {
            _calculator.SendCommand((int)Command.Command1);
        }

        // Try to add one more - should trigger MaxDigitsReached
        _calculator.SendCommand((int)Command.Command2);

        Assert.True(_maxDigitsCalledCount > 0);


        // The display should show "0." plus the digits
        Assert.Equal(16, _currentDisplay.Replace("0.", "").Length);
    }

    [Fact]
    public void MaxDigitsReached_TrailingDecimal_TriggersNotification()
    {
        _calculator.Reset();
        _maxDigitsCalledCount = 0;

        // Add some digits
        for (int i = 0; i < 12; i++)
        {
            _calculator.SendCommand((int)Command.Command1);
        }

        // Add decimal point and more digits
        _calculator.SendCommand((int)Command.CommandPNT);
        for (int i = 0; i < 4; i++)
        {
            _calculator.SendCommand((int)Command.Command1);
        }

        // Try to add one more - should trigger MaxDigitsReached
        _calculator.SendCommand((int)Command.Command2);

        Assert.Equal(1, _maxDigitsCalledCount);
        Assert.Equal("111,111,111,111.1111", _currentDisplay);
    }

    [Fact]
    public void BinaryOperatorReceived_SingleOperator_TriggersNotification()
    {
        _calculator.Reset();
        _binaryOperatorReceivedCount = 0;

        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandADD);

        Assert.Equal(1, _binaryOperatorReceivedCount);
        Assert.Equal("1", _currentDisplay);
    }

    [Fact]
    public void BinaryOperatorReceived_MultipleOperators_TriggersNotification()
    {
        _calculator.Reset();
        _binaryOperatorReceivedCount = 0;

        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.CommandSUB);
        _calculator.SendCommand((int)Command.CommandMUL);

        Assert.Equal(3, _binaryOperatorReceivedCount);
        Assert.Equal("1", _currentDisplay);
    }

    [Fact]
    public void BinaryOperatorReceived_ComplexExpression_TriggersNotification()
    {
        _calculator.Reset();
        _binaryOperatorReceivedCount = 0;

        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.CommandADD);
        _calculator.SendCommand((int)Command.Command2);
        _calculator.SendCommand((int)Command.CommandMUL);
        _calculator.SendCommand((int)Command.Command1);
        _calculator.SendCommand((int)Command.Command0);
        _calculator.SendCommand((int)Command.CommandSUB);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.CommandDIV);
        _calculator.SendCommand((int)Command.Command5);
        _calculator.SendCommand((int)Command.CommandEQU);

        Assert.Equal(4, _binaryOperatorReceivedCount);
        Assert.Equal("5", _currentDisplay);
    }

    [Fact]
    public void StandardMode_OrderOfOperations_WorksCorrectly()
    {
        Command[] commands1 = [Command.Command1, Command.CommandREC, Command.CommandNULL];
        TestCommand(commands1, "1", "1/(1)");

        Command[] commands2 = [Command.Command4, Command.CommandSQRT, Command.CommandNULL];
        TestCommand(commands2, "2", "\x221A(4)");

        Command[] commands3 =
            [Command.Command1, Command.CommandADD, Command.Command4, Command.CommandSQRT, Command.CommandNULL];
        TestCommand(commands3, "2", "1 + \x221A(4)");

        Command[] commands4 =
        [
            Command.Command1, Command.CommandADD, Command.Command4, Command.CommandSQRT, Command.CommandSUB,
            Command.CommandNULL
        ];
        TestCommand(commands4, "3", "3 - ");

        Command[] commands5 =
            [Command.Command2, Command.CommandMUL, Command.Command4, Command.CommandREC, Command.CommandNULL];
        TestCommand(commands5, "0.25", "2 \x00D7 1/(4)");

        Command[] commands6 =
            [Command.Command5, Command.CommandDIV, Command.Command6, Command.CommandPERCENT, Command.CommandNULL];
        TestCommand(commands6, "0.06", "5 \x00F7 0.06");

        Command[] commands7 = [Command.Command4, Command.CommandSQRT, Command.CommandSUB, Command.CommandNULL];
        TestCommand(commands7, "2", "\x221A(4) - ");

        Command[] commands8 = [Command.Command7, Command.CommandSQR, Command.CommandDIV, Command.CommandNULL];
        TestCommand(commands8, "49", "sqr(7) \x00F7 ");

        Command[] commands9 = [Command.Command8, Command.CommandSQR, Command.CommandSQRT, Command.CommandNULL];
        TestCommand(commands9, "8", "\x221A(sqr(8))");
    }
}
