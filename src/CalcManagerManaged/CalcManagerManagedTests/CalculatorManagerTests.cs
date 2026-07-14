using CalcManagerManaged;
using CalcManagerManaged.Interop;

namespace CalcManagerInteropTests;

public sealed class CalculatorManagerTests : IDisposable
{
    private readonly CalcEngineWrapper _calculator;
    private string _currentDisplay = "";
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
    public void StandardModeNumericInputDisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Num1, Command.Num2, Command.Num3, Command.Pnt,
            Command.Num4, Command.Num5, Command.Num6, Command.None
        ];
        TestCommand(commands, "123.456", "");
    }

    [Fact]
    public void StandardModeAdditionDisplaysCorrectly()
    {
        Command[] commands = [Command.Add, Command.None];
        TestCommand(commands, "0", "0 + ");
    }

    [Fact]
    public void StandardModeSquareRootDisplaysCorrectly()
    {
        Command[] commands = [Command.Sqrt, Command.None];
        TestCommand(commands, "0", "\x221A(0)");
    }

    [Fact]
    public void StandardModeAdditionAndEqualsDisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Num2, Command.Add, Command.Num3, Command.Equ,
            Command.Num4, Command.Equ, Command.None
        ];
        TestCommand(commands, "7", "4 + 3=");
    }

    [Fact]
    public void StandardModeNumberEqualsDisplaysCorrectly()
    {
        Command[] commands = [Command.Num4, Command.Equ, Command.None];
        TestCommand(commands, "4", "4=");
    }

    [Fact]
    public void StandardModeMultipleSquareRootsDisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Num2, Command.Num5, Command.Num6, Command.Sqrt,
            Command.Sqrt, Command.Sqrt, Command.None
        ];
        TestCommand(commands, "2", "\x221A(\x221A(\x221A(256)))");
    }

    [Fact]
    public void StandardModeSubtractionMultiplicationDisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Num3, Command.Sub, Command.Num6, Command.Equ,
            Command.Mul, Command.Num3, Command.Equ, Command.None
        ];
        TestCommand(commands, "-9", "-3 \x00D7 3=");
    }

    [Fact]
    public void StandardModeMultiplicationSubtractionDisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Num9, Command.Mul, Command.Num6, Command.Sub,
            Command.Centr, Command.Num8, Command.Equ, Command.None
        ];
        TestCommand(commands, "46", "54 - 8=");
    }

    [Fact]
    public void StandardModePercentOperationDisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Num6, Command.Mul, Command.Num6, Command.Percent,
            Command.Equ, Command.None
        ];
        TestCommand(commands, "0.36", "6 \x00D7 0.06=");
    }

    [Fact]
    public void StandardModeAdditionWithPercentDisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Num5, Command.Num0, Command.Add, Command.Num2,
            Command.Num0, Command.Percent, Command.Equ, Command.None
        ];
        TestCommand(commands, "60", "50 + 10=");
    }

    [Fact]
    public void StandardModeAdditionEqualsDisplaysCorrectly()
    {
        Command[] commands = [Command.Num4, Command.Add, Command.Equ, Command.None];
        TestCommand(commands, "8", "4 + 4=");
    }

    [Fact]
    public void StandardModeOperatorOverrideDisplaysCorrectly()
    {
        Command[] commands =
            [Command.Num5, Command.Add, Command.Mul, Command.Num3, Command.None];
        TestCommand(commands, "3", "5 \x00D7 ");
    }

    [Fact]
    public void StandardModeOverflowDisplaysError()
    {
        Command[] commands =
        [
            Command.Num1, Command.Exp, Command.Sign, Command.Num9, Command.Num9,
            Command.Num9, Command.Num9, Command.Div, Command.Num1, Command.Num0,
            Command.Equ, Command.None
        ];
        TestCommand(commands, "Overflow", "1.e-9999 \x00F7 ");
    }

    [Fact]
    public void StandardModeDivideByZeroDisplaysError()
    {
        Command[] commands =
            [Command.Num1, Command.Div, Command.Num0, Command.Equ, Command.None];
        TestCommand(commands, "Cannot divide by zero", "1 \x00F7 ");
    }

    [Fact]
    public void StandardModeZeroDividedByZeroDisplaysUndefined()
    {
        Command[] commands =
            [Command.Num0, Command.Div, Command.Num0, Command.Equ, Command.None];
        TestCommand(commands, "Result is undefined", "0 \x00F7 ");
    }

    [Fact]
    public void StandardModeBackspaceOperationsWorksCorrectly()
    {
        Command[] commands =
        [
            Command.Num1, Command.Num2, Command.Num3, Command.Back,
            Command.Back, Command.None
        ];
        TestCommand(commands, "1", "");
    }

    [Fact]
    public void StandardModeAllBackspacesClearsInput()
    {
        Command[] commands =
        [
            Command.Num1, Command.Num2, Command.Num3, Command.Back,
            Command.Back, Command.Back, Command.None
        ];
        TestCommand(commands, "0", "");
    }

    [Fact]
    public void ScientificModeNumericInputDisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Num1, Command.Num2, Command.Num3, Command.Pnt,
            Command.Num4, Command.Num5, Command.Num6, Command.None
        ];
        TestCommand(commands, "123.456", "");
    }

    [Fact]
    public void ScientificModeAdditionDisplaysCorrectly()
    {
        Command[] commands = [Command.Add, Command.None];
        TestCommand(commands, "0", "0 + ");
    }

    [Fact]
    public void ScientificModeSquareRootDisplaysCorrectly()
    {
        Command[] commands = [Command.Sqrt, Command.None];
        TestCommand(commands, "0", "\x221A(0)");
    }

    [Fact]
    public void ScientificModePrecedenceHandlingDisplaysCorrectly()
    {
        Command[] commands =
        [
            Command.Num1, Command.Add, Command.Num0, Command.Mul,
            Command.Num2, Command.Equ, Command.None
        ];
        TestCommand(commands, "1", "1 + 0 \x00D7 2=", true, true);
    }

    [Fact]
    public void ScientificModeSquareWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Num2, Command.Sqr, Command.None];
        TestCommand(commands, "144", "sqr(12)", true, true);
    }

    [Fact]
    public void ScientificModeFactorialWorksCorrectly()
    {
        Command[] commands = [Command.Num5, Command.Fac, Command.None];
        TestCommand(commands, "120", "fact(5)");
    }

    [Fact]
    public void ScientificModePowerWorksCorrectly()
    {
        Command[] commands =
            [Command.Num5, Command.Pwr, Command.Num2, Command.Add, Command.None];
        TestCommand(commands, "25", "5 ^ 2 + ", true, true);
    }

    [Fact]
    public void ScientificModeRootWorksCorrectly()
    {
        Command[] commands =
            [Command.Num8, Command.Root, Command.Num3, Command.Mul, Command.None];
        TestCommand(commands, "2", "8 yroot 3 \x00D7 ", true, true);
    }

    [Fact]
    public void ScientificModeCubeWorksCorrectly()
    {
        Command[] commands = [Command.Num8, Command.Cub, Command.None];
        TestCommand(commands, "512", "cube(8)", true, true);
    }

    [Fact]
    public void ScientificModeCubeRootWorksCorrectly()
    {
        Command[] commands = [Command.Num8, Command.Cub, Command.CubeRoot, Command.None];
        TestCommand(commands, "8", "cuberoot(cube(8))", true, true);
    }

    [Fact]
    public void ScientificModeLogarithmWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Num0, Command.Log, Command.None];
        TestCommand(commands, "1", "log(10)");
    }

    [Fact]
    public void ScientificModePowerOf10WorksCorrectly()
    {
        Command[] commands = [Command.Num5, Command.Pow10, Command.None];
        TestCommand(commands, "100,000", "10^(5)");
    }

    [Fact]
    public void ScientificModeNaturalLogarithmWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Num0, Command.NumLN, Command.None];
        TestCommand(commands, "2.3025850929940456840179914546844", "ln(10)", true, true);
    }

    [Fact]
    public void ScientificModeSineWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Sin, Command.None];
        TestCommand(commands, "0.01745240643728351281941897851632", "sin\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificModeCosineWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Cos, Command.None];
        TestCommand(commands, "0.99984769515639123915701155881391", "cos\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificModeTangentWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Tan, Command.None];
        TestCommand(commands, "0.01745506492821758576512889521973", "tan\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificModeArcSineWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Asin, Command.None];
        TestCommand(commands, "90", "sin\x2080\x207B\x00B9(1)", true, true);
    }

    [Fact]
    public void ScientificModeArcCosineWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Acos, Command.None];
        TestCommand(commands, "0", "cos\x2080\x207B\x00B9(1)", true, true);
    }

    [Fact]
    public void ScientificModeArcTangentWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Atan, Command.None];
        TestCommand(commands, "45", "tan\x2080\x207B\x00B9(1)", true, true);
    }

    [Fact]
    public void ScientificModeSecantWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Sec, Command.None];
        TestCommand(commands, "1.0001523280439076654284264342126", "sec\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificModeCosecantWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Csc, Command.None];
        TestCommand(commands, "57.298688498550183476612683735174", "csc\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificModeCotangentWorksCorrectly()
    {
        Command[] commands = [Command.Num1, Command.Cot, Command.None];
        TestCommand(commands, "57.289961630759424687278147537113", "cot\x2080(1)", true, true);
    }

    [Fact]
    public void ScientificModePowerOfEWorksCorrectly()
    {
        Command[] commands = [Command.Num2, Command.PowE, Command.None];
        TestCommand(commands, "7.389056098930650227230427460575", "e^(2)", true, true);
    }

    [Fact]
    public void ScientificModePowerOfTwoWorksCorrectly()
    {
        Command[] commands = [Command.Num8, Command.Pow2, Command.None];
        TestCommand(commands, "256", "2^(8)", true, true);
    }

    [Fact]
    public void ScientificModePracticalFractionOperationWorksCorrectly()
    {
        Command[] commands =
        [
            Command.Num8, Command.Pwr, Command.OpenP, Command.Num2,
            Command.Div, Command.Num3, Command.CloseP, Command.Sub,
            Command.Num4, Command.Add, Command.None
        ];
        TestCommand(commands, "0", "8 ^ (2 \x00F7 3) - 4 + ", true, true);
    }

    [Fact]
    public void ScientificModeFloorFunctionWorksCorrectly()
    {
        Command[] commands =
            [Command.Num3, Command.Pnt, Command.Num8, Command.Floor, Command.None];
        TestCommand(commands, "3", "floor(3.8)");
    }

    [Fact]
    public void ScientificModeCeilingFunctionWorksCorrectly()
    {
        Command[] commands =
            [Command.Num3, Command.Pnt, Command.Num8, Command.Ceil, Command.None];
        TestCommand(commands, "4", "ceil(3.8)");
    }

    [Fact]
    public void ScientificModeLogarithmBaseYWorksCorrectly()
    {
        Command[] commands =
            [Command.Num5, Command.LogBaseY, Command.Num3, Command.Add, Command.None];
        TestCommand(commands, "1.4649735207179271671970404076786", "5 log base 3 + ", true, true);
    }

    private int _maxDigitsCalledCount;
    private int _binaryOperatorReceivedCount;
    private List<string>? _memorizedNumbers;

    public CalculatorManagerTests()
    {
        // Setup calculator with event handlers
        _calculator = new CalcEngineWrapper(new DefaultCalcResourceProvider());
        _calculator.DisplayChanged += (sender, e) =>
        {
            _currentDisplay = e.DisplayText;
            _isError = e.IsError;
        };
        _calculator.ExpressionDisplayChanged += (sender, e) =>
        {
            _currentExpression = string.Join("", e.Tokens.Select(t => t.Text));
        };
        _calculator.MaxDigitsReached += (sender, e) => { _maxDigitsCalledCount++; };
        _calculator.BinaryOperatorReceived += (sender, e) => { _binaryOperatorReceivedCount++; };
        _calculator.MemorizedNumbersChanged += (sender, e) => { _memorizedNumbers = new List<string>(e.MemorizedNumbers); };
    }

    public void Dispose()
    {
        _calculator.Dispose();
        GC.SuppressFinalize(this);
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
            if (command == Command.None)
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
    public void ScientificModeParenthesisHandlingWorksCorrectly()
    {
        Command[] commands1 =
        [
            Command.Num1, Command.Add, Command.OpenP, Command.Add,
            Command.Num3, Command.CloseP, Command.None
        ];
        TestCommand(commands1, "3", "1 + (0 + 3)", true, true);

        Command[] commands2 =
        [
            Command.OpenP, Command.OpenP, Command.Num1, Command.Num2,
            Command.CloseP, Command.None
        ];
        TestCommand(commands2, "12", "((12)", true, true);

        Command[] commands3 =
        [
            Command.Num1, Command.Num2, Command.CloseP,
            Command.CloseP, Command.OpenP, Command.None
        ];
        TestCommand(commands3, "12", "12 \x00D7 (", true, true);

        Command[] commands4 =
        [
            Command.Num2, Command.OpenP, Command.Num2, Command.CloseP,
            Command.Add, Command.None
        ];
        TestCommand(commands4, "4", "2 \x00D7 (2) + ", true, true);

        Command[] commands5 =
        [
            Command.Num2, Command.OpenP, Command.Num2, Command.CloseP,
            Command.Add, Command.Equ, Command.None
        ];
        TestCommand(commands5, "8", "2 \x00D7 (2) + 4=", true, true);
    }

    [Fact]
    public void ErrorHandlingDivideByZeroDisplaysError()
    {
        Command[] commands1 =
            [Command.Num1, Command.Div, Command.Num0, Command.Equ, Command.None];
        TestCommand(commands1, "Cannot divide by zero", "1 \x00F7 ", true, true);
        Assert.True(_isError);

        // Same test for standard calculator
        _calculator.Reset();
        _isError = false;
        TestCommand(commands1, "Cannot divide by zero", "1 \x00F7 ");
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandlingInvalidInputDisplaysError()
    {
        Command[] commands2 = [Command.Num2, Command.Sign, Command.Log, Command.None];
        TestCommand(commands2, "Invalid input", "log(-2)", true, true);
        Assert.True(_isError);

        // Same test for standard calculator
        _calculator.Reset();
        _isError = false;
        TestCommand(commands2, "Invalid input", "log(-2)");
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandlingUndefinedResultDisplaysError()
    {
        Command[] commands3 =
            [Command.Num0, Command.Div, Command.Num0, Command.Equ, Command.None];
        TestCommand(commands3, "Result is undefined", "0 \x00F7 ", true, true);
        Assert.True(_isError);

        // Same test for standard calculator
        _calculator.Reset();
        _isError = false;
        TestCommand(commands3, "Result is undefined", "0 \x00F7 ");
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandlingDivideByZeroSetsErrorState()
    {
        // Test that division by zero sets the error state
        _calculator.Reset();

        // Set up with numerator
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Div);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Equ);

        // Verify error state and message
        Assert.True(_calculator.IsInError());
        Assert.Equal("Cannot divide by zero", _currentDisplay);
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandlingNegativeLogarithmSetsErrorState()
    {
        // Test that log of negative number sets the error state
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Scientific);

        // Set up with negative number
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Sign);
        _calculator.SendCommand((int)Command.Log);

        // Verify error state and message
        Assert.True(_calculator.IsInError());
        Assert.Equal("Invalid input", _currentDisplay);
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandlingZeroDivideByZeroSetsErrorState()
    {
        // Test that zero divided by zero sets the error state
        _calculator.Reset();

        // Set up with zero numerator
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Div);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Equ);

        // Verify error state and message
        Assert.True(_calculator.IsInError());
        Assert.Equal("Result is undefined", _currentDisplay);
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandlingOverflowSetsErrorState()
    {
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Scientific);

        // Enter very large number and factorial it
        Command[] commands =
        [
            Command.Num9, Command.Num0, Command.Num0, Command.Exp,
            Command.Num9, Command.Num0, Command.Fac, Command.None
        ];

        // Don't check the exact expression since formatting may differ between platforms
        TestCommand(commands, "Overflow", "N/A");

        // Verify error state
        Assert.True(_calculator.IsInError());
        Assert.True(_isError);
    }

    [Fact]
    public void ErrorHandlingResetClearsError()
    {
        _calculator.Reset();

        // Create an error state
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Div);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Equ);

        // Verify error state
        Assert.True(_calculator.IsInError());
        Assert.True(_isError);

        // Reset should clear error
        _calculator.Reset();

        // Verify error is cleared
        Assert.False(_calculator.IsInError());
    }

    [Fact]
    public void ErrorHandlingErrorStateConsistentWithDisplay()
    {
        _calculator.Reset();
        _isError = false;

        // Set up division by zero
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Div);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Equ);

        // Verify the display shows the correct error message
        Assert.Equal("Cannot divide by zero", _currentDisplay);

        // Verify the error flag is set
        Assert.True(_isError);

        // Verify IsInError() also returns true
        Assert.True(_calculator.IsInError());
    }

    [Fact]
    public void ErrorHandlingErrorStatePreventsNewOperations()
    {
        _calculator.Reset();

        // Set up division by zero to create error state
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Div);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Equ);

        // Verify error state
        Assert.True(_calculator.IsInError());
        Assert.Equal("Cannot divide by zero", _currentDisplay);

        // Try to enter new number - shouldn't change the display
        string displayBeforeNewInput = _currentDisplay;
        _calculator.SendCommand((int)Command.Num5);

        // Display should still show error
        Assert.Equal(displayBeforeNewInput, _currentDisplay);
        Assert.True(_calculator.IsInError());

        // Reset should clear error
        _calculator.Reset();
        Assert.False(_calculator.IsInError());

        // Now we should be able to enter new input
        _calculator.SendCommand((int)Command.Num5);
        Assert.Equal("5", _currentDisplay);
    }

    [Fact]
    public void ErrorHandlingSpecificErrorsShowCorrectMessages()
    {
        // Test different error types show appropriate messages

        // 1. Division by zero
        Command[] divByZeroCommands =
        [
            Command.Num1, Command.Div, Command.Num0,
            Command.Equ, Command.None
        ];
        TestCommand(divByZeroCommands, "Cannot divide by zero", "1 \x00F7 ");
        Assert.True(_calculator.IsInError());
        _calculator.Reset();

        // 2. Domain error (log of negative number)
        Command[] domainErrorCommands =
        [
            Command.Num2, Command.Sign, Command.Log, Command.None
        ];
        TestCommand(domainErrorCommands, "Invalid input", "log(-2)");
        Assert.True(_calculator.IsInError());
        _calculator.Reset();

        // 3. Undefined (0/0)
        Command[] undefinedCommands =
        [
            Command.Num0, Command.Div, Command.Num0,
            Command.Equ, Command.None
        ];
        TestCommand(undefinedCommands, "Result is undefined", "0 \x00F7 ");
        Assert.True(_calculator.IsInError());
    }

    [Fact]
    public void ErrorHandlingNegativeSqrtShowsDomainError()
    {
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Scientific);

        // Try to take square root of negative number - domain error
        Command[] negativeCommand =
        [
            Command.Num2, Command.Sign, Command.Sqrt, Command.None
        ];

        // Should show invalid input for sqrt of negative number
        TestCommand(negativeCommand, "Invalid input", "N/A");

        // Verify error state
        Assert.True(_calculator.IsInError());
    }

    [Fact]
    public void ErrorHandlingErrorClearingBehavior()
    {
        _calculator.Reset();

        // Create an error state
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Div);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Equ);

        // Verify error state
        Assert.True(_calculator.IsInError());
        Assert.Equal("Cannot divide by zero", _currentDisplay);

        // Clear should reset error state
        _calculator.SendCommand((int)Command.Clear);

        // After clear, not in error state
        Assert.False(_calculator.IsInError());

        // Can perform new calculations
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Equ);

        // New calculation works
        Assert.Equal("12", _currentDisplay);
        Assert.False(_calculator.IsInError());
    }

    [Fact]
    public void ScientificModeRadiansModeTrigFunctionsWorkCorrectly()
    {
        Command[] commands1 = [Command.Rad, Command.NumPI, Command.Sin, Command.None];
        TestCommand(commands1, "0", "N/A", true, true);

        Command[] commands2 = [Command.Rad, Command.NumPI, Command.Cos, Command.None];
        TestCommand(commands2, "-1", "N/A", true, true);

        Command[] commands3 = [Command.Rad, Command.NumPI, Command.Tan, Command.None];
        TestCommand(commands3, "0", "N/A", true, true);
    }

    [Fact]
    public void ScientificModeGradiansModeTrigFunctionsWorkCorrectly()
    {
        Command[] commands4 =
        [
            Command.Grad, Command.Num4, Command.Num0, Command.Num0,
            Command.Sin, Command.None
        ];
        TestCommand(commands4, "0", "N/A", true, true);

        Command[] commands5 =
        [
            Command.Grad, Command.Num4, Command.Num0, Command.Num0,
            Command.Cos, Command.None
        ];
        TestCommand(commands5, "1", "N/A", true, true);

        Command[] commands6 =
        [
            Command.Grad, Command.Num4, Command.Num0, Command.Num0,
            Command.Tan, Command.None
        ];
        TestCommand(commands6, "0", "N/A", true, true);
    }

    [Fact]
    public void ModeChangesPreservesStateCorrectly()
    {
        Command[] commands1 = [Command.Num1, Command.Num2, Command.Num3, Command.None];
        TestCommand(commands1, "123", "");

        Command[] commands2 = [Command.ModeScientific, Command.None];
        TestCommand(commands2, "0", "");

        Command[] commands3 = [Command.Num1, Command.Num2, Command.Num3, Command.None];
        TestCommand(commands3, "123", "");

        Command[] commands4 = [Command.ModeProgrammer, Command.None];
        TestCommand(commands4, "0", "");

        Command[] commands5 = [Command.Num1, Command.Num2, Command.Num3, Command.None];
        TestCommand(commands5, "123", "");

        Command[] commands6 = [Command.ModeScientific, Command.None];
        TestCommand(commands6, "0", "");

        Command[] commands7 = [Command.Num6, Command.Num7, Command.Add, Command.None];
        TestCommand(commands7, "67", "67 + ");

        Command[] commands8 = [Command.ModeBasic, Command.None];
        TestCommand(commands8, "0", "");
    }

    [Fact]
    public void ProgrammerModeBitwiseOperationsWorkCorrectly()
    {
        _calculator.SetMode(CalcMode.Programmer);

        Command[] commands1 =
        [
            Command.ModeProgrammer, Command.Num5, Command.Num3, Command.Nand,
            Command.Num8, Command.Num3, Command.And, Command.None
        ];
        TestCommand(commands1, "-18", "53 NAND 83 AND ");

        Command[] commands2 =
        [
            Command.ModeProgrammer, Command.Num5, Command.Num3, Command.Nor,
            Command.Num8, Command.Num3, Command.And, Command.None
        ];
        TestCommand(commands2, "-120", "53 NOR 83 AND ");

        Command[] commands3 =
        [
            Command.ModeProgrammer, Command.Num5, Command.Lshf, Command.Num1,
            Command.And, Command.None
        ];
        TestCommand(commands3, "10", "5 Lsh 1 AND ");

        Command[] commands5 =
        [
            Command.ModeProgrammer, Command.Num5, Command.Rshfl, Command.Num1,
            Command.And, Command.None
        ];
        TestCommand(commands5, "2", "5 Rsh 1 AND ");
    }

    [Fact]
    public void ProgrammerModeRadixTypesHexadecimalModeWorksCorrectly()
    {
        // Set up calculator in programmer mode with hex radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Hex);

        // Test basic input in hex mode
        _calculator.SendCommand((int)Command.NumA);
        _calculator.SendCommand((int)Command.NumB);
        _calculator.SendCommand((int)Command.NumC);
        _calculator.SendCommand((int)Command.NumD);

        Assert.Equal("ABCD", _currentDisplay);

        // Test addition in hex mode
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.NumF);
        _calculator.SendCommand((int)Command.Equ);

        Assert.Equal("BE0C", _currentDisplay);

        // Test AND operation in hex mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Hex);

        _calculator.SendCommand((int)Command.NumF);
        _calculator.SendCommand((int)Command.NumF);
        _calculator.SendCommand((int)Command.NumF);
        _calculator.SendCommand((int)Command.NumF);
        _calculator.SendCommand((int)Command.And);
        _calculator.SendCommand((int)Command.NumA);
        _calculator.SendCommand((int)Command.NumA);
        _calculator.SendCommand((int)Command.NumA);
        _calculator.SendCommand((int)Command.NumA);
        _calculator.SendCommand((int)Command.Equ);

        Assert.Equal("AAAA", _currentDisplay);
    }

    [Fact]
    public void ProgrammerModeRadixTypesDecimalModeWorksCorrectly()
    {
        // Set up calculator in programmer mode with decimal radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Dec);

        // Test basic input in decimal mode
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Num4);

        Assert.Equal("1,234", _currentDisplay);

        // Test addition in decimal mode
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Num6);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Num8);
        _calculator.SendCommand((int)Command.Equ);

        Assert.Equal("6,912", _currentDisplay);

        // Test OR operation in decimal mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Dec);

        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.NumOR);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Equ);

        Assert.Equal("111", _currentDisplay);
    }

    [Fact]
    public void ProgrammerModeRadixTypesOctalModeWorksCorrectly()
    {
        // Set up calculator in programmer mode with octal radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Octal);

        // Test basic input in octal mode
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Num4);

        Assert.Equal("1 234", _currentDisplay);

        // Test addition in octal mode
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Num6);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Equ);

        Assert.Equal("2 023", _currentDisplay);

        // Test XOR operation in octal mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Octal);

        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Xor);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Equ);

        Assert.Equal("222", _currentDisplay);
    }

    [Fact]
    public void ProgrammerModeRadixTypesBinaryModeWorksCorrectly()
    {
        // Set up calculator in programmer mode with binary radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Binary);

        // Test basic input in binary mode
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);

        Assert.Equal("1010", _currentDisplay);

        // Test addition in binary mode
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Equ);

        Assert.Equal("1 0111", _currentDisplay);

        // Test NOT operation in binary mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Binary);

        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Not);

        // NOT 1010 in 64-bit binary would be a very long string of 1s and 0s,
        // so we're just checking that the display has changed
        Assert.NotEqual("1010", _currentDisplay);
        Assert.Contains("1", _currentDisplay, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgrammerModeDivisionWorksCorrectly()
    {
        _calculator.SetMode(CalcMode.Programmer);

        Command[] commands11 = { Command.ModeProgrammer, Command.Dec, Command.Num4, Command.Num2, Command.Num9,   Command.Num4,
                                Command.Num9,       Command.Num6,   Command.Num7, Command.Num2, Command.Num9,   Command.Num6,
                                Command.Div,     Command.Num2,   Command.Num5, Command.Num5, Command.Equ, Command.None };
        TestCommand(commands11, "16,843,009", "4294967296 \x00F7 255=");

        Command[] commands12 = {
            Command.ModeProgrammer, Command.Dec, Command.Num4, Command.Num2, Command.Num9,   Command.Num4,
            Command.Num9,       Command.Num6,   Command.Num7, Command.Num3, Command.Num0,   Command.Num3,
            Command.Div,     Command.Num2,   Command.Num5, Command.Num5, Command.Equ, Command.None
        };
        TestCommand(commands12, "16,843,009", "4294967303 \x00F7 255=");

        Command[] commands13 = {
            Command.ModeProgrammer, Command.Dec, Command.Num1, Command.Num0, Command.Num0, Command.Num0,
            Command.Num0, Command.Num0, Command.Num0, Command.Num0, Command.Num0, Command.Num0, Command.Div,
            Command.Num6, Command.Num4, Command.Num4, Command.Num8, Command.Num7, Command.Equ, Command.None
        };
        TestCommand(commands13, "15,507", "1000000000 \x00F7 64487=");

        Command[] commands14 = { Command.ModeProgrammer, Command.Dec, Command.Num1,   Command.Num0,   Command.Num0,
                                 Command.Num0,       Command.Num0,   Command.Num0,   Command.Num0,   Command.Num0,
                                 Command.Num0,       Command.Num0,   Command.Div, Command.Num6,   Command.Num4,
                                 Command.Num4,       Command.Num8,   Command.Num8,   Command.Equ, Command.None };
        TestCommand(commands14, "15,506", "1000000000 \x00F7 64488=");
    }

    [Fact]
    public void ProgrammerModeRadixTypesConversionBetweenRadicesWorksCorrectly()
    {
        // Start with decimal mode and enter a value
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Dec);

        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);

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
        _calculator.SetRadix(CalcRadixType.Dec);
        Assert.Equal("10", _currentDisplay);
    }


    [Fact]
    public void ProgrammerModeRotationWorksCorrectly()
    {
        _calculator.SetMode(CalcMode.Programmer);

        Command[] commands7 = [Command.ModeProgrammer, Command.Num1, Command.Rol, Command.None];
        TestCommand(commands7, "2", "RoL(1)");

        Command[] commands9 = [Command.ModeProgrammer, Command.Num1, Command.Rorc, Command.None];
        TestCommand(commands9, "0", "RoR(1)");

        Command[] commands10 =
        [
            Command.ModeProgrammer, Command.Num1, Command.Rorc, Command.Rorc,
            Command.None
        ];
        TestCommand(commands10, "-9,223,372,036,854,775,808", "RoR(RoR(1))");
    }

    [Fact]
    public void ProgrammerModeDigitGroupingDecimalModeWorksCorrectly()
    {
        // Set up calculator in programmer mode with decimal radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Dec);

        // Enter a number large enough to trigger digit grouping
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Num4);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Num6);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Num8);
        _calculator.SendCommand((int)Command.Num9);

        // Verify digit grouping applied (commas every 3 digits for decimal)
        Assert.Equal("123,456,789", _currentDisplay);
    }

    [Fact]
    public void ProgrammerModeDigitGroupingHexModeWorksCorrectly()
    {
        // Set up calculator in programmer mode with hex radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Hex);

        // Enter a number large enough to trigger digit grouping
        _calculator.SendCommand((int)Command.NumA);
        _calculator.SendCommand((int)Command.NumB);
        _calculator.SendCommand((int)Command.NumC);
        _calculator.SendCommand((int)Command.NumD);
        _calculator.SendCommand((int)Command.NumE);
        _calculator.SendCommand((int)Command.NumF);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);

        // Verify digit grouping applied (spaces every 4 digits for hex)
        Assert.Equal("A BCDE F123", _currentDisplay);
    }

    [Fact]
    public void ProgrammerModeDigitGroupingBinaryModeWorksCorrectly()
    {
        // Set up calculator in programmer mode with binary radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Binary);

        // Enter a binary number large enough to trigger digit grouping
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);

        // Verify digit grouping applied (spaces every 4 digits for binary)
        Assert.Equal("1010 1010 1010", _currentDisplay);
    }

    [Fact]
    public void ProgrammerModeDigitGroupingOctalModeWorksCorrectly()
    {
        // Set up calculator in programmer mode with octal radix
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Programmer);
        _calculator.SetRadix(CalcRadixType.Octal);

        // Enter a number large enough to trigger digit grouping
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Num4);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Num6);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Num0);

        // Verify digit grouping applied (spaces every 3 digits for octal)
        Assert.Equal("12 345 670", _currentDisplay);
    }

    [Fact]
    public void StandardModeDigitGroupingWorksCorrectly()
    {
        // Test digit grouping in standard mode (should use locale-specific grouping)
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Standard);

        // Enter a large number
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Num4);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Num6);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Num8);
        _calculator.SendCommand((int)Command.Num9);

        // Verify digit grouping is applied (commas every 3 digits)
        Assert.Equal("123,456,789", _currentDisplay);
    }

    [Fact]
    public void StandardModeDigitGroupingWithDecimalPointWorksCorrectly()
    {
        // Test digit grouping with decimal point in standard mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Standard);

        // Enter a large number with decimal point
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Num4);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Pnt);
        _calculator.SendCommand((int)Command.Num6);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Num8);
        _calculator.SendCommand((int)Command.Num9);

        // Verify digit grouping is applied before decimal but not after
        Assert.Equal("12,345.6789", _currentDisplay);
    }

    [Fact]
    public void ScientificModeDigitGroupingWorksCorrectly()
    {
        // Test digit grouping in scientific mode
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Scientific);

        // Enter a large number
        _calculator.SendCommand((int)Command.Num9);
        _calculator.SendCommand((int)Command.Num8);
        _calculator.SendCommand((int)Command.Num7);
        _calculator.SendCommand((int)Command.Num6);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Num4);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num1);

        // Verify digit grouping is applied
        Assert.Equal("987,654,321", _currentDisplay);
    }

    [Fact]
    public void DigitGroupingResultOfCalculationWorksCorrectly()
    {
        // Test that the result of a calculation shows proper digit grouping
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Standard);

        // Perform calculation that results in a large number
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Num4);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Mul);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Equ);

        // Verify digit grouping is applied to the result
        Assert.Equal("1,234,500", _currentDisplay);
    }

    [Fact]
    public void DigitGroupingNegativeNumbersWorksCorrectly()
    {
        // Test digit grouping with negative numbers
        _calculator.Reset();
        _calculator.SetMode(CalcMode.Standard);

        // Enter a large negative number
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Num4);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Num6);
        _calculator.SendCommand((int)Command.Sign);

        // Verify digit grouping is applied to negative number
        Assert.Equal("-123,456", _currentDisplay);
    }

    [Fact]
    public void MemoryFeaturesBasicOperationsWorkCorrectly()
    {
        // Test storing a value in memory
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Num1);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.Clear);
        _calculator.MemorizedNumberLoad(0);

        Assert.Equal("1", _currentDisplay);

        // Test storing multiple values
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Num1);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.Clear);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.Clear);

        _calculator.MemorizedNumberLoad(1);
        Assert.Equal("1", _currentDisplay);

        _calculator.MemorizedNumberLoad(0);
        Assert.Equal("2", _currentDisplay);
    }

    [Fact]
    public void MemoryFeaturesMemorizeComplexExpressionsWorkCorrectly()
    {
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Sign);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Equ);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.Mul);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.MemorizeNumber();

        Assert.NotNull(_memorizedNumbers);
        Assert.Equal(3, _memorizedNumbers.Count);
        Assert.Equal("2", _memorizedNumbers[0]);
        Assert.Equal("1", _memorizedNumbers[1]);
        Assert.Equal("-1", _memorizedNumbers[2]);
    }

    [Fact]
    public void MemoryFeaturesMemoryAddSubtractWorkCorrectly()
    {
        // Setup memory with initial values
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Sign);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Equ);
        _calculator.MemorizeNumber();
        _calculator.SendCommand((int)Command.Mul);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.MemorizeNumber();

        // Adding to memory
        _calculator.SendCommand((int)Command.Clear);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.MemorizedNumberAdd(0);
        _calculator.MemorizedNumberAdd(1);
        _calculator.MemorizedNumberAdd(2);

        Assert.NotNull(_memorizedNumbers);
        Assert.Equal(3, _memorizedNumbers.Count);
        Assert.Equal("4", _memorizedNumbers[0]);
        Assert.Equal("3", _memorizedNumbers[1]);
        Assert.Equal("1", _memorizedNumbers[2]);

        // Subtracting from memory
        _calculator.SendCommand((int)Command.Clear);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Pnt);
        _calculator.SendCommand((int)Command.Num5);

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
    public void GetDisplayCommandsSnapshotWorksCorrectly()
    {
        // Test with a simple expression: 2 + 3 (without equals)
        // The snapshot will be empty if we add the equals sign because it completes the calculation
        // and clears the expression history
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num3);

        // Get the snapshot before pressing equals
        var commands = _calculator.GetDisplayCommandsSnapshot();

        // Verify we have commands
        Assert.NotNull(commands);
        Assert.True(commands.Count == 3);

        // Verify the returned commands represents the expression correctly
        string fullExpression = string.Join(" ", commands.Select(c => c.Token));
        Assert.Equal("2 + 3", fullExpression);

        // Now test with equals - this should complete the expression and clear the commands
        _calculator.SendCommand((int)Command.Equ);
        var emptyCommands = _calculator.GetDisplayCommandsSnapshot();
        Assert.Empty(emptyCommands);

        // Test that reset also clears the commands
        _calculator.Reset();
        emptyCommands = _calculator.GetDisplayCommandsSnapshot();
        Assert.Empty(emptyCommands);
    }

    [Fact]
    public void GetDisplayCommandsSnapshotMultipleOperations()
    {
        // Test with a simpler expression: 3 - 2
        _calculator.Reset();
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.Sub);
        _calculator.SendCommand((int)Command.Num2);

        var commands = _calculator.GetDisplayCommandsSnapshot();

        Assert.True(commands.Count == 3);

        string fullExpression = string.Join(" ", commands.Select(c => c.Token));

        Assert.Equal("3 - 2", fullExpression);
    }

    [Fact]
    public void GetDisplayCommandsSnapshotParenthesisHandling()
    {
        // Test with parenthesis: (2 + 3) * 4
        _calculator.Reset();
        _calculator.SendCommand((int)Command.OpenP);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num3);
        _calculator.SendCommand((int)Command.CloseP);
        _calculator.SendCommand((int)Command.Mul);
        _calculator.SendCommand((int)Command.Num4);

        var commands = _calculator.GetDisplayCommandsSnapshot();

        // Check that we have at least some commands
        Assert.True(commands.Count == 7);

        // Combine all tokens and check that the expression contains the expected elements
        string fullExpression = string.Join("", commands.Select(c => c.Token));
        Assert.Contains("(", fullExpression, StringComparison.Ordinal);
        Assert.Contains("2", fullExpression, StringComparison.Ordinal);
        Assert.Contains("3", fullExpression, StringComparison.Ordinal);
        Assert.Contains(")", fullExpression, StringComparison.Ordinal);
        Assert.Contains("4", fullExpression, StringComparison.Ordinal);

        // Check for operators
        bool hasAdd = fullExpression.Contains('+', StringComparison.Ordinal);
        bool hasMul = fullExpression.Contains('×', StringComparison.Ordinal);
        Assert.True(hasAdd, "Expression should contain addition operator");
        Assert.True(hasMul, "Expression should contain multiplication operator");
    }

    [Fact]
    public void GetDisplayCommandsSnapshotUpdatesWithNewInput()
    {
        // Test that snapshot updates when new input is added
        _calculator.Reset();

        // Enter 2
        _calculator.SendCommand((int)Command.Num2);
        var commands1 = _calculator.GetDisplayCommandsSnapshot();
        Assert.Single(commands1);
        Assert.Contains(commands1, cmd => cmd.Token.Contains('2', StringComparison.Ordinal));

        // Add +
        _calculator.SendCommand((int)Command.Add);
        var commands2 = _calculator.GetDisplayCommandsSnapshot();
        Assert.True(commands2.Count == 2);
        Assert.Contains(commands2, cmd => cmd.Token.Contains('+', StringComparison.Ordinal));

        // Add 3
        _calculator.SendCommand((int)Command.Num3);
        var commands3 = _calculator.GetDisplayCommandsSnapshot();
        Assert.True(commands3.Count == 3);
        Assert.Contains(commands3, cmd => cmd.Token.Contains('3', StringComparison.Ordinal));
    }

    [Fact]
    public void GetDisplayCommandsSnapshotHandlesBackspace()
    {
        // Test that snapshot updates correctly when backspace is used
        _calculator.Reset();

        // Enter 2 + 3
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num3);

        var commandsBefore = _calculator.GetDisplayCommandsSnapshot();
        Assert.Contains(commandsBefore, cmd => cmd.Token.Contains('3', StringComparison.Ordinal));

        // Backspace to remove the 3
        _calculator.SendCommand((int)Command.Back);
        var commandsAfter = _calculator.GetDisplayCommandsSnapshot();

        // The snapshot should still contain 2 and + but not 3
        Assert.Contains(commandsAfter, cmd => cmd.Token.Contains('2', StringComparison.Ordinal));
        Assert.Contains(commandsAfter, cmd => cmd.Token.Contains('+', StringComparison.Ordinal));
        Assert.DoesNotContain(commandsAfter, cmd => cmd.Token.Contains('3', StringComparison.Ordinal));
    }

    [Fact]
    public void GetDisplayCommandsSnapshotRetainsCommandTypeInformation()
    {
        // This test verifies that the GetDisplayCommandsSnapshot API returns correctly classified command types
        // (not just raw text) which is critical for state restoration
        _calculator.Reset();

        // Enter a simple expression with different command types
        _calculator.SendCommand((int)Command.OpenP); // Parenthesis type
        _calculator.SendCommand((int)Command.Num2); // Operand type
        _calculator.SendCommand((int)Command.Add); // Binary operation type
        _calculator.SendCommand((int)Command.Num3); // Operand type
        _calculator.SendCommand((int)Command.CloseP); // Parenthesis type

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
        Assert.Contains("(", commands[0].Token, StringComparison.Ordinal);
        Assert.Contains("2", commands[1].Token, StringComparison.Ordinal);
        Assert.Contains("+", commands[2].Token, StringComparison.Ordinal);
        Assert.Contains("3", commands[3].Token, StringComparison.Ordinal);
        Assert.Contains(")", commands[4].Token, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDisplayCommandsSnapshotCanCaptureAndRestoreState()
    {
        // This test demonstrates how the API captures state for snapshot/restore
        // which is how the UWP app uses the functionality for suspend/resume
        _calculator.Reset();

        // Enter an in-progress calculation
        _calculator.SendCommand((int)Command.Num4);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num6);

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
        _calculator.SendCommand((int)Command.Equ);
        Assert.Equal("51", _currentDisplay);

        // After equals, the snapshot should be empty since the calculation is complete
        var emptySnapshot = _calculator.GetDisplayCommandsSnapshot();
        Assert.Empty(emptySnapshot);

        // This demonstrates the core functionality needed for the UWP app's
        // state preservation, where it needs to capture the exact sequence
        // of commands to recreate the calculator state
    }

    [Fact]
    public void MaxDigitsReachedStandardInputTriggersNotification()
    {
        _calculator.Reset();
        _maxDigitsCalledCount = 0;

        // Add digits until reaching max
        for (int i = 0; i < 16; i++)
        {
            _calculator.SendCommand((int)Command.Num1);
        }

        // Try to add one more - should trigger MaxDigitsReached
        _calculator.SendCommand((int)Command.Num2);

        Assert.Equal(1, _maxDigitsCalledCount);
        // The display should still show just the initial digits
        Assert.Equal(16, _currentDisplay.Replace(",", "", StringComparison.Ordinal).Length);
    }

    [Fact]
    public void MaxDigitsReachedLeadingDecimalTriggersNotification()
    {
        _calculator.Reset();
        _maxDigitsCalledCount = 0;

        // Add decimal point
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Pnt);

        // Add digits until reaching max
        for (int i = 0; i < 16; i++)
        {
            _calculator.SendCommand((int)Command.Num1);
        }

        // Try to add one more - should trigger MaxDigitsReached
        _calculator.SendCommand((int)Command.Num2);

        Assert.True(_maxDigitsCalledCount > 0);


        // The display should show "0." plus the digits
        Assert.Equal(16, _currentDisplay.Replace("0.", "", StringComparison.Ordinal).Length);
    }

    [Fact]
    public void MaxDigitsReachedTrailingDecimalTriggersNotification()
    {
        _calculator.Reset();
        _maxDigitsCalledCount = 0;

        // Add some digits
        for (int i = 0; i < 12; i++)
        {
            _calculator.SendCommand((int)Command.Num1);
        }

        // Add decimal point and more digits
        _calculator.SendCommand((int)Command.Pnt);
        for (int i = 0; i < 4; i++)
        {
            _calculator.SendCommand((int)Command.Num1);
        }

        // Try to add one more - should trigger MaxDigitsReached
        _calculator.SendCommand((int)Command.Num2);

        Assert.Equal(1, _maxDigitsCalledCount);
        Assert.Equal("111,111,111,111.1111", _currentDisplay);
    }

    [Fact]
    public void BinaryOperatorReceivedSingleOperatorTriggersNotification()
    {
        _calculator.Reset();
        _binaryOperatorReceivedCount = 0;

        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Add);

        Assert.Equal(1, _binaryOperatorReceivedCount);
        Assert.Equal("1", _currentDisplay);
    }

    [Fact]
    public void BinaryOperatorReceivedMultipleOperatorsTriggersNotification()
    {
        _calculator.Reset();
        _binaryOperatorReceivedCount = 0;

        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Sub);
        _calculator.SendCommand((int)Command.Mul);

        Assert.Equal(3, _binaryOperatorReceivedCount);
        Assert.Equal("1", _currentDisplay);
    }

    [Fact]
    public void BinaryOperatorReceivedComplexExpressionTriggersNotification()
    {
        _calculator.Reset();
        _binaryOperatorReceivedCount = 0;

        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Add);
        _calculator.SendCommand((int)Command.Num2);
        _calculator.SendCommand((int)Command.Mul);
        _calculator.SendCommand((int)Command.Num1);
        _calculator.SendCommand((int)Command.Num0);
        _calculator.SendCommand((int)Command.Sub);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Div);
        _calculator.SendCommand((int)Command.Num5);
        _calculator.SendCommand((int)Command.Equ);

        Assert.Equal(4, _binaryOperatorReceivedCount);
        Assert.Equal("5", _currentDisplay);
    }

    [Fact]
    public void StandardModeOrderOfOperationsWorksCorrectly()
    {
        Command[] commands1 = [Command.Num1, Command.Rec, Command.None];
        TestCommand(commands1, "1", "1/(1)");

        Command[] commands2 = [Command.Num4, Command.Sqrt, Command.None];
        TestCommand(commands2, "2", "\x221A(4)");

        Command[] commands3 =
            [Command.Num1, Command.Add, Command.Num4, Command.Sqrt, Command.None];
        TestCommand(commands3, "2", "1 + \x221A(4)");

        Command[] commands4 =
        [
            Command.Num1, Command.Add, Command.Num4, Command.Sqrt, Command.Sub,
            Command.None
        ];
        TestCommand(commands4, "3", "3 - ");

        Command[] commands5 =
            [Command.Num2, Command.Mul, Command.Num4, Command.Rec, Command.None];
        TestCommand(commands5, "0.25", "2 \x00D7 1/(4)");

        Command[] commands6 =
            [Command.Num5, Command.Div, Command.Num6, Command.Percent, Command.None];
        TestCommand(commands6, "0.06", "5 \x00F7 0.06");

        Command[] commands7 = [Command.Num4, Command.Sqrt, Command.Sub, Command.None];
        TestCommand(commands7, "2", "\x221A(4) - ");

        Command[] commands8 = [Command.Num7, Command.Sqr, Command.Div, Command.None];
        TestCommand(commands8, "49", "sqr(7) \x00F7 ");

        Command[] commands9 = [Command.Num8, Command.Sqr, Command.Sqrt, Command.None];
        TestCommand(commands9, "8", "\x221A(sqr(8))");
    }
}
