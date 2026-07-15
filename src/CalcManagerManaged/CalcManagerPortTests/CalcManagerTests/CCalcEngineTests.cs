using CalcEngine;
using CalculationManager;

namespace CalcEngineTests;

public sealed class CCalcEngineTests : IDisposable
{
    private readonly CCalcEngine _calcEngine;

    public CCalcEngineTests()
    {
        // Setup similar to the C++ test fixture
        IResourceProvider resourceProvider = new DefaultResourceProvider();
        var history = new CalculatorHistory(20); // MAX_HISTORY_SIZE = 20
        _calcEngine = new CCalcEngine(
            false, // Respect Order of Operations
            false, // Set to Integer Mode
            resourceProvider,
            new CalculatorManagerDisplayTester(),
            history);
    }

    public void Dispose()
    {
        // No specific cleanup needed
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("", 10, "")]
    [InlineData("12345678", 9, "12345678")] // Invalid base returns original
    [InlineData("1234567", 8, "1 234 567")] // Octal
    [InlineData("123", 8, "123")] // Minimum octal
    [InlineData("1234567890", 2, "12 3456 7890")] // Binary
    [InlineData("1234", 2, "1234")] // Minimum binary
    [InlineData("1234567890", 16, "12 3456 7890")] // Hexadecimal
    [InlineData("1234", 16, "1234")] // Minimum hex
    [InlineData("1234567890", 10, "1,234,567,890")] // Decimal
    [InlineData("1234567.89", 10, "1,234,567.89")] // Decimal with point
    [InlineData("1234567e89", 10, "1,234,567e89")] // With exponent
    [InlineData("1234567.89e5", 10, "1,234,567.89e5")] // With point and exponent
    [InlineData("-123456789", 10, "-123,456,789")] // Negative
    public void TestGroupDigitsPerRadix(string input, uint radix, string expected)
    {
        Assert.Equal(expected, _calcEngine.GroupDigitsPerRadix(input, radix));
    }

    [Theory]
    [InlineData("0", 2)]
    [InlineData("1", 2)]
    [InlineData("0011", 2)]
    [InlineData("1100", 2)]
    public void ValidBinaryNumbersTest(string input, uint radix)
    {
        Assert.Equal(0, _calcEngine.IsNumberInvalid(input, 0, 0, radix));
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("A", 2)]
    [InlineData("0.1", 2)]
    public void InvalidBinaryNumbersTest(string input, uint radix)
    {
        Assert.Equal(EngineStrings.IdsErrUnkCh, _calcEngine.IsNumberInvalid(input, 0, 0, radix));
    }

    [Theory]
    [InlineData("0", 8)]
    [InlineData("7", 8)]
    [InlineData("01234567", 8)]
    [InlineData("76543210", 8)]
    public void ValidOctalNumbersTest(string input, uint radix)
    {
        Assert.Equal(0, _calcEngine.IsNumberInvalid(input, 0, 0, radix));
    }

    [Theory]
    [InlineData("8", 8)]
    [InlineData("A", 8)]
    [InlineData("0.7", 8)]
    public void InvalidOctalNumbersTest(string input, uint radix)
    {
        Assert.Equal(EngineStrings.IdsErrUnkCh, _calcEngine.IsNumberInvalid(input, 0, 0, radix));
    }

    [Theory]
    [InlineData("0", 16)]
    [InlineData("F", 16)]
    [InlineData("0123456789ABCDEF", 16)]
    [InlineData("FEDCBA9876543210", 16)]
    public void ValidHexNumbersTest(string input, uint radix)
    {
        Assert.Equal(0, _calcEngine.IsNumberInvalid(input, 0, 0, radix));
    }

    [Theory]
    [InlineData("G", 16)]
    [InlineData("abcdef", 16)]
    [InlineData("x", 16)]
    [InlineData("0.1", 16)]
    public void InvalidHexNumbersTest(string input, uint radix)
    {
        Assert.Equal(EngineStrings.IdsErrUnkCh, _calcEngine.IsNumberInvalid(input, 0, 0, radix));
    }

    [Fact]
    public void LongExponentNumberTest()
    {
        string longExp = "1e12345";
        Assert.Equal(0, _calcEngine.IsNumberInvalid(longExp, 5, 100, 10)); // Max exp length = 5, should be valid
        Assert.Equal(EngineStrings.IdsErrInputOverflow,
            _calcEngine.IsNumberInvalid(longExp, 4, 100, 10)); // Max exp length = 4, should overflow
    }

    [Theory]
    [InlineData("10000")]
    [InlineData("10.000")]
    [InlineData("0000012345")]
    [InlineData("123.45")]
    [InlineData("0.00123")]
    [InlineData("0.12345")]
    [InlineData("-123.45e678")]
    public void MantissaLengthTest(string input)
    {
        Assert.Equal(0, _calcEngine.IsNumberInvalid(input, 100, 5, 10)); // Max mantissa length = 5, should be valid
        Assert.Equal(EngineStrings.IdsErrInputOverflow,
            _calcEngine.IsNumberInvalid(input, 100, 4, 10)); // Max mantissa length = 4, should overflow
    }

    [Theory]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1")]
    [InlineData("-")]
    [InlineData("")]
    [InlineData("1234567890")]
    [InlineData("1.0")]
    [InlineData("-.")]
    [InlineData("1.")]
    [InlineData("0.0")]
    [InlineData("0.123456")]
    [InlineData("1e")]
    [InlineData("1.e")]
    [InlineData("-e")]
    [InlineData("1e+12345")]
    [InlineData("1e-12345")]
    [InlineData("1e123")]
    [InlineData("-123.456e+789")]
    public void ValidDecimalFormatTest(string input)
    {
        Assert.Equal(0, _calcEngine.IsNumberInvalid(input, 100, 100, 10));
    }

    [Theory]
    [InlineData("x123")]
    [InlineData("123-")]
    [InlineData("1e1.2")]
    [InlineData("1-e2")]
    public void InvalidDecimalFormatTest(string input)
    {
        Assert.Equal(EngineStrings.IdsErrUnkCh, _calcEngine.IsNumberInvalid(input, 100, 100, 10));
    }

    [Theory]
    [InlineData("", new uint[] { })]
    [InlineData("1", new uint[] { 1 })]
    [InlineData("3", new uint[] { 3 })]
    [InlineData("17", new uint[] { })]
    [InlineData("3;0", new uint[] { 3, 0 })]
    [InlineData("3;0;0", new uint[] { 3, 0, 0 })]
    [InlineData("5;3;2;4;6", new uint[] { 5, 3, 2, 4, 6 })]
    [InlineData("15;15;15;0", new uint[] { 15, 15, 15, 0 })]
    [InlineData("4;16;7;25;0", new uint[] { 4, 7, 0 })] // Oversize elements ignored
    public void TestDigitGroupingStringToGroupingVector(string input, uint[] expected)
    {
        var expectedList = new List<uint>(expected);
        Assert.Equal(expectedList, CCalcEngine.DigitGroupingStringToGroupingVector(input));
    }

    [Theory]
    [InlineData("", "3;0", "1234567", false, "1234567")] // Empty delimiter
    [InlineData(",", "", "1234567", false, "1234567")] // Empty grouping
    [InlineData(",", "3;0", "1234567", false, "1,234,567")] // Standard grouping
    [InlineData(" ", "3;0", "1234567", false, "1 234 567")] // Space delimiter
    [InlineData("|||", "3;0", "1234567", false, "1|||234|||567")] // Long delimiter
    [InlineData(",", "3;0", "12345e67", false, "12,345e67")] // With exponent
    [InlineData(",", "3;0", "12345.67", false, "12,345.67")] // With decimal
    [InlineData(",", "3;0", "1234.56e7", false, "1,234.56e7")] // With decimal and exponent
    [InlineData(",", "3;0", "-1234567", true, "-1,234,567")] // Negative number
    [InlineData(",", "0;0", "1234567890123456", false, "1234567890123456")] // No grouping
    [InlineData(",", "3", "1234567890123456", false, "1234567890123,456")] // Non-repeating grouping
    [InlineData(",", "3;0;0", "1234567890123456", false, "1234567890123,456")] // Expanded non-repeating
    [InlineData(",", "5;3;2;0", "1234567890123456", false, "12,34,56,78,901,23456")] // Multi with repeating
    [InlineData(",", "4;0", "1234567890123456", false, "1234,5678,9012,3456")] // Repeating non-standard
    [InlineData(",", "5;3;2", "1234567890123456", false, "123456,78,901,23456")] // Multi non-repeating
    [InlineData(",", "5;3;2;0;0", "1234567890123456", false, "123456,78,901,23456")] // Expanded multi non-repeating
    public void TestGroupDigits(string delimiter, string groupingStr, string input, bool isNegative, string expected)
    {
        // Convert the grouping string to a List<uint>
        var grouping = CCalcEngine.DigitGroupingStringToGroupingVector(groupingStr);
        var k = _calcEngine.GroupDigits(delimiter, grouping, input, isNegative);

        Assert.Equal(expected, k);
    }

    [Fact]
    public void CalculatorEnginesDoNotShareLastDisplayState()
    {
        IResourceProvider resourceProvider = new DefaultResourceProvider();
        var firstDisplay = new CalculatorManagerDisplayTester();
        var secondDisplay = new CalculatorManagerDisplayTester();
        var first = new CalculatorManager(firstDisplay, resourceProvider);
        var second = new CalculatorManager(secondDisplay, resourceProvider);

        first.SetScientificMode();
        first.SendCommand(Command.Rad);
        first.SendCommand(Command.NumPI);

        // Leave the process-wide cache at zero between the first calculator's
        // PI entry and its unary operation. A shared cache suppresses the first
        // calculator's result callback and leaves its display stuck on PI.
        second.SetScientificMode();
        second.SendCommand(Command.Rad);

        first.SendCommand(Command.Sin);

        Assert.Equal("0", firstDisplay.GetPrimaryDisplay());
    }
}
