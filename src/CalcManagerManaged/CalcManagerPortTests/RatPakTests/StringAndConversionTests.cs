using CalcEngine;

namespace CalcManagerPortTests.RatPakTests;

public class StringAndConversionTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 64;

    public StringAndConversionTests()
    {
        _ratPak = new RatPak(_precision);
    }

    #region Number Format Tests

    [Theory]
    [InlineData("123.456", NumberFormat.FloatingPoint, "123.456")]
    [InlineData("123.456", NumberFormat.Scientific, "1.23456e+2")]
    [InlineData("123.456", NumberFormat.Engineering, "123.456e+0")]
    [InlineData("0.000123456", NumberFormat.FloatingPoint, "0.000123456")]
    [InlineData("0.000123456", NumberFormat.Scientific, "1.23456e-4")]
    [InlineData("0.000123456", NumberFormat.Engineering, "123456e-3")]
    [InlineData("123456789", NumberFormat.FloatingPoint, "123456789")]
    [InlineData("123456789", NumberFormat.Scientific, "1.23456789e+8")]
    [InlineData("123456789", NumberFormat.Engineering, "123.456789e+6")]
    public void RatToStringDifferentFormatsCorrectResults(string input, NumberFormat format, string expected)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rat = StringToRat(input);

        var result = _ratPak.RatToString(ref rat, format, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123456789", NumberFormat.FloatingPoint, 2, "111010110111100110100010101")]
    [InlineData("123456789", NumberFormat.FloatingPoint, 8, "726746425")]
    [InlineData("123456789", NumberFormat.FloatingPoint, 16, "75BCD15")]
    [InlineData("255", NumberFormat.FloatingPoint, 2, "11111111")]
    [InlineData("255", NumberFormat.FloatingPoint, 8, "377")]
    [InlineData("255", NumberFormat.FloatingPoint, 16, "ff")]
    [InlineData("15.5", NumberFormat.FloatingPoint, 16, "f.8")]
    public void RatToStringDifferentRadixesCorrectResults(string input, NumberFormat format, uint radix,
        string expected)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rat = StringToRat(input);

        var result = _ratPak.RatToString(ref rat, format, radix, _precision);

        // For hex output, compare case-insensitive
        if (radix == 16)
        {
            Assert.Equal(expected, result, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void RatToStringVeryLargeNumberUsesScientificNotation()
    {
        var rat = StringToRat("1e100");

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);

        Assert.Equal("1.e+100", result);
    }

    [Fact]
    public void RatToStringVerySmallNumberUsesScientificNotation()
    {
        var rat = StringToRat("1e-100");

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);

        Assert.Equal("1.e-100", result);
    }

    [Fact]
    public void SetDecimalSeparatorChangesOutputFormat()
    {
        // First test with the default separator
        var rat = StringToRat("123.456");
        var defaultResult = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("123.456", defaultResult);

        // Change separator to comma
        _ratPak.SetDecimalSeparator(',');

        // Test again
        var commaResult = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("123,456", commaResult);

        // Reset back to period for other tests
        _ratPak.SetDecimalSeparator('.');
    }

    #endregion

    #region String Parsing Tests

    [Theory]
    [InlineData("0", "0")]
    [InlineData("123", "123")]
    [InlineData("-456", "-456")]
    [InlineData("123.456", "123.456")]
    [InlineData("-789.012", "-789.012")]
    [InlineData("1e10", "10000000000")]
    [InlineData("1.23e-5", "0.0000123")]
    [InlineData("1.23E+5", "1.23")] // doesn't handle the capital e in the original it seems.
    public void StringToNumberVariousInputsParsesCorrectly(string input, string expected)
    {
        var num = _ratPak.StringToNumber(input, 10, _precision);
        Assert.NotNull(num);

        var result = _ratPak.NumberToString(ref num, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StringToRatWithExponentParsesCorrectly()
    {
        var mantissaIsNegative = false;
        var mantissa = "1.23";
        var exponentIsNegative = true;
        var exponent = "4";

        var rat = _ratPak.StringToRat(mantissaIsNegative, mantissa, exponentIsNegative, exponent, 10, _precision);
        Assert.NotNull(rat);

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("0.000123", result);
    }

    #endregion

    #region Radix Conversion Tests

    [Fact]
    public void RadixConversionRoundTripPreservesValues()
    {
        // Test that we can convert from one radix to another and back again
        // start with a decimal number
        var original = "123.45600000000000000255351295663786004297435283660888671875";
        var num = _ratPak.StringToNumber(original, 10, _precision);
        Assert.NotNull(num);
        var rat1 = RatPak.numtorat(num, 10);
        // convert to binary
        var binaryStr = _ratPak.RatToString(ref rat1, NumberFormat.FloatingPoint, 2, _precision);

        // Parse the binary string
        var binaryNum = _ratPak.StringToNumber(binaryStr, 2, _precision);
        Assert.NotNull(binaryNum);
        var rat2 = RatPak.numtorat(binaryNum, 2);

        // Convert back to decimal
        var result = _ratPak.RatToString(ref rat2, NumberFormat.FloatingPoint, 10, _precision);
        //should get the original value back
        Assert.Equal(original, result);
    }

    [Fact]
    public void NumtonRadixxAndNRadixxtonumAreInternalConversionFunctions()
    {
        // These are internal conversion functions, so we'll just verify basic functionality

        var num = _ratPak.StringToNumber("255", 10, _precision);
        Assert.NotNull(num);
        // convert to internal base
        var internalBase = RatPak.numtonRadixx(num, 10);

        // Convert back to decimal
        var result = _ratPak.nRadixxtonum(internalBase, 10, _precision);

        var resultStr = _ratPak.NumberToString(ref result, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("255", resultStr);
    }

    #endregion

    #region Conversion Between Types Tests

    [Theory]
    [InlineData("42", 42)]
    [InlineData("-123", -123)]
    [InlineData("0", 0)]
    [InlineData("2147483647", 2147483647)] // Int32.MaxValue
    [InlineData("-2147483648", -2147483648)] // Int32.MinValue
    [InlineData("123.99", 123)] // Truncation
    [InlineData("-123.99", -123)] // Truncation
    public void NumToI32VariousInputsConvertsCorrectly(string input, int expected)
    {
        var num = _ratPak.StringToNumber(input, 10, _precision);
        Assert.NotNull(num);

        var result = RatPak.numtoi32(num, 10);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("-123", -123)]
    [InlineData("0", 0)]
    [InlineData("2147483647", 2147483647)] // Int32.MaxValue
    [InlineData("-2147483648", -2147483648)] // Int32.MinValue
    [InlineData("123.99", 123)] // Truncation
    [InlineData("-123.99", -123)] // Truncation
    public void RatToI32VariousInputsConvertsCorrectly(string input, int expected)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rat = StringToRat(input);

        var result = _ratPak.rattoi32(rat, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("42", 42u)]
    [InlineData("0", 0u)]
    [InlineData("4294967295", 4294967295u)] // UInt32.MaxValue
    [InlineData("123.99", 123u)] // Truncation
    public void RatToUi32VariousInputsConvertsCorrectly(string input, uint expected)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rat = StringToRat(input);

        var result = _ratPak.rattoUi32(rat, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("42", 42ul)]
    [InlineData("0", 0ul)]
    [InlineData("18446744073709551615", 18446744073709551615ul)] // UInt64.MaxValue
    [InlineData("123.99", 123ul)] // Truncation
    public void RatToUi64VariousInputsConvertsCorrectly(string input, ulong expected)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rat = StringToRat(input);

        var result = _ratPak.rattoUi64(rat, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void NumToRatReturnsCorrectRational()
    {
        var num = _ratPak.StringToNumber("123.456", 10, _precision);
        Assert.NotNull(num);

        var rat = RatPak.numtorat(num, 10);

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("123.456", result);
    }

    [Fact]
    public void RatToNumberPerformsDivision()
    {
        //- create a fraction
        var rat = StringToRat("22/7"); // Approximation of π

        var num = _ratPak.RatToNumber(rat, 10, _precision);

        var result = _ratPak.NumberToString(ref num, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("3.142857142857142857142857142857142857142857142857142857142857143", result);
    }

    #endregion

    #region Helper Methods

    private RAT StringToRat(string input)
    {
        // Handle fractions like "1/3"
        if (input.Contains('/', StringComparison.Ordinal))
        {
            var parts = input.Split('/');
            var numNumerator = _ratPak.StringToNumber(parts[0], 10, _precision);
            Assert.NotNull(numNumerator);
            var numDenominator = _ratPak.StringToNumber(parts[1], 10, _precision);
            Assert.NotNull(numDenominator);

            var result = RatPak.numtorat(numNumerator, 10);
            var denominator = RatPak.numtorat(numDenominator, 10);

            _ratPak.divrat(ref result, denominator, _precision);
            return result;
        }

        // Handle regular numbers
        var num = _ratPak.StringToNumber(input, 10, _precision);
        Assert.NotNull(num);
        return RatPak.numtorat(num, 10);
    }

    #endregion
}
