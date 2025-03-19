using CalcManagerPort;

namespace CalcManagerPortTests.RatPakTests;

public class MathFunctionTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 64;

    public MathFunctionTests()
    {
        _ratPak = new RatPak(_precision);
    }

    #region Exponentiation Tests

    [Theory]
    [InlineData("2", "3", "8")]
    [InlineData("10", "2", "100")]
    [InlineData("2", "0.5", "1.414213562373095048801688724209698078569671875376948073176679738")]
    [InlineData("4", "0.5", "2")]
    [InlineData("9", "0.5", "3")]
    [InlineData("8", "1/3", "2")]
    [InlineData("1", "5", "1")]
    [InlineData("-2", "2", "4")]
    [InlineData("-2", "3", "-8")]
    public void PowRat_VariousInputs_CorrectResults(string baseVal, string exponent, string expected)
    {
        var baseRat = StringToRat(baseVal);
        var expRat = StringToRat(exponent);

        _ratPak.powrat(ref baseRat, expRat, 10, _precision);

        var result = _ratPak.RatToString(ref baseRat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Logarithm Tests

    [Theory]
    [InlineData("1", "0")]
    [InlineData("2.718281828459045", "0.9999999999999999134157889710887611625720332265832477611693629941")]
    [InlineData("7.389056098930650", "1.999999999999999969247705739646506380353393326386422158381903506")]
    [InlineData("0.5", "-0.6931471805599453094172321214581765680755001343602552541206800095")]
    public void LogRat_VariousInputs_CorrectResults(string input, string expected)
    {
        var rat = StringToRat(input);

        _ratPak.lograt(ref rat, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1", "0")]
    [InlineData("10", "1")]
    [InlineData("100", "2")]
    [InlineData("0.1", "-1")]
    [InlineData("1000", "3")]
    public void Log10Rat_VariousInputs_CorrectResults(string input, string expected)
    {
        var rat = StringToRat(input);

        _ratPak.log10rat(ref rat, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Integer and Fractional Part Tests

    [Theory]
    [InlineData("3.14159", "3")]
    [InlineData("-3.14159", "-3")]
    [InlineData("0.14159", "0")]
    [InlineData("-0.14159", "0")]
    [InlineData("42", "42")]
    public void IntRat_VariousInputs_CorrectResults(string input, string expected)
    {
        var rat = StringToRat(input);

        _ratPak.intrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("3.14159", "0.14159")]
    [InlineData("-3.14159", "-0.14159")]
    [InlineData("0.14159", "0.14159")]
    [InlineData("-0.14159", "-0.14159")]
    [InlineData("42", "0")]
    public void FracRat_VariousInputs_CorrectResults(string input, string expected)
    {
        var rat = StringToRat(input);

        _ratPak.fracrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Greatest Common Divisor Tests

    [Fact]
    public void GcdRat_SimpleRationalNumbers_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("12", 10, _precision);
        var num2 = _ratPak.StringToNumber("18", 10, _precision);

        var rat = _ratPak.numtorat(num1, 10);
        var denominator = _ratPak.numtorat(num2, 10);

        _ratPak.divrat(ref rat, denominator, _precision);

        _ratPak.gcdrat(ref rat, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("0.6666666666666666666666666666666666666666666666666666666666666667", result);

        // Create 2/3 for comparison
        var expectedRat = StringToRat("2/3");
        Assert.True(_ratPak.rat_equ(rat, expectedRat, _precision));
    }

    #endregion

    #region Exponential Function Tests

    [Theory]
    [InlineData("0", "1")]
    [InlineData("1", "2.718281828459045235360287471352662497757247093699959574966967628")]
    [InlineData("2", "7.389056098930650227230427460575007813180315570551847324087127823")]
    [InlineData("-1", "0.3678794411714423215955237701614608674458111310317678345078368017")]
    public void ExpRat_VariousInputs_CorrectResults(string input, string expected)
    {
        var rat = StringToRat(input);

        _ratPak.exprat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Bitwise Operation Tests

    [Theory]
    [InlineData("5", "3", "1")] // 5 (101) AND 3 (011) = 1 (001)
    [InlineData("12", "10", "8")] // 12 (1100) AND 10 (1010) = 8 (1000)
    [InlineData("255", "15", "15")] // 255 (11111111) AND 15 (00001111) = 15 (00001111)
    public void AndRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.andrat(ref ratA, ratB, 10, _precision);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("5", "3", "7")] // 5 (101) OR 3 (011) = 7 (111)
    [InlineData("12", "10", "14")] // 12 (1100) OR 10 (1010) = 14 (1110)
    [InlineData("240", "15", "255")] // 240 (11110000) OR 15 (00001111) = 255 (11111111)
    public void OrRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.orrat(ref ratA, ratB, 10, _precision);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("5", "3", "6")] // 5 (101) XOR 3 (011) = 6 (110)
    [InlineData("12", "10", "6")] // 12 (1100) XOR 10 (1010) = 6 (0110)
    [InlineData("255", "255", "0")] // 255 (11111111) XOR 255 (11111111) = 0 (00000000)
    [InlineData("255", "0", "255")] // 255 (11111111) XOR 0 (00000000) = 255 (11111111)
    public void XorRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.xorrat(ref ratA, ratB, 10, _precision);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1", "1", "2")] // 1 << 1 = 2
    [InlineData("1", "2", "4")] // 1 << 2 = 4
    [InlineData("1", "3", "8")] // 1 << 3 = 8
    [InlineData("5", "2", "20")] // 5 << 2 = 20
    public void LshRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.lshrat(ref ratA, ratB, 10, _precision);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("8", "1", "4")] // 8 >> 1 = 4
    [InlineData("8", "2", "2")] // 8 >> 2 = 2
    [InlineData("8", "3", "1")] // 8 >> 3 = 1
    [InlineData("20", "2", "5")] // 20 >> 2 = 5
    public void RshRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.rshrat(ref ratA, ratB, 10, _precision);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Number Format Tests

    [Fact]
    public void NumberToString_DifferentFormats_CorrectResults()
    {
        var num = _ratPak.StringToNumber("123456.789", 10, _precision);
        var rat = _ratPak.numtorat(num, 10);


        // Format: Float
        var floatFormat = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("123456.789", floatFormat);

        // Format: Scientific
        var scientificFormat = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Scientific, 10, _precision);
        Assert.Equal("1.23456789e+5", scientificFormat);

        // Format: Engineering
        var engineeringFormat = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Engineering, 10, _precision);
        Assert.Equal("123.456789e+3", engineeringFormat);
    }

    [Fact]
    public void NumberToString_VeryLargeNumber_UsesScientificNotation()
    {
        var num = _ratPak.StringToNumber("1.e100", 10, _precision);
        var rat = _ratPak.numtorat(num, 10);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Contains("e+", result); // Should use scientific notation
        Assert.Equal("1.e+100", result);
    }

    [Fact]
    public void NumberToString_VerySmallNumber_UsesScientificNotation()
    {
        var num = _ratPak.StringToNumber("1.e-100", 10, _precision);
        var rat = _ratPak.numtorat(num, 10);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Contains("e-", result); // Should use scientific notation
        Assert.Equal("1.e-100", result);
    }

    #endregion

    #region Helper Methods

    private RatPak.RAT StringToRat(string input)
    {
        // Handle fractions like "1/3"
        if (input.Contains('/'))
        {
            string[] parts = input.Split('/');
            var numNumerator = _ratPak.StringToNumber(parts[0], 10, _precision);
            var numDenominator = _ratPak.StringToNumber(parts[1], 10, _precision);

            var result = _ratPak.numtorat(numNumerator, 10);
            var denominator = _ratPak.numtorat(numDenominator, 10);

            _ratPak.divrat(ref result, denominator, _precision);
            return result;
        }

        // Handle regular numbers
        var num = _ratPak.StringToNumber(input, 10, _precision);
        return _ratPak.numtorat(num, 10);
    }

    #endregion
}
