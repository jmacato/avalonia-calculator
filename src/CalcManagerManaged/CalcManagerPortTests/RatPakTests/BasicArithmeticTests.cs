using CalcEngine;
using  CalcEngine;

namespace CalcManagerPortTests.RatPakTests;

public class BasicArithmeticTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 64;

    public BasicArithmeticTests()
    {
        _ratPak = new RatPak(_precision);
    }

    #region Number Creation Tests

    [Fact]
    public void CreateNumber_FromString_CreatesCorrectNumber()
    {
        var num = _ratPak.StringToNumber("123456789", 10, _precision);

        Assert.NotNull(num);
        Assert.Equal(1, num.sign); // Positive number
        Assert.True(num.cdigit > 0); // Has digits

        // Convert back to string to verify
        var numStr = _ratPak.NumberToString(ref num, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("123456789", numStr);
    }

    [Fact]
    public void CreateNumber_NegativeFromString_CreatesCorrectNumber()
    {
        var num = _ratPak.StringToNumber("-987654321", 10, _precision);

        Assert.NotNull(num);
        Assert.Equal(-1, num.sign); // Negative number
        Assert.True(num.cdigit > 0); // Has digits

        // Convert back to string to verify
        var numStr = _ratPak.NumberToString(ref num, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("-987654321", numStr);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("-1", "-1")]
    [InlineData("123456789", "123456789")]
    [InlineData("-123456789", "-123456789")]
    [InlineData("0.5", "0.5")]
    [InlineData("-0.5", "-0.5")]
    [InlineData("1e10", "10000000000")]
    [InlineData("1.234e5", "123400")]
    [InlineData("1.234e-5", "0.00001234")]
    public void StringToNumber_VariousInputs_ParsesCorrectly(string input, string expected)
    {
        var num = _ratPak.StringToNumber(input, 10, _precision);

        var numStr = _ratPak.NumberToString(ref num, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, numStr);
    }

    #endregion

    #region Addition Tests

    [Fact]
    public void AddNum_SimpleAddition_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("123", 10, _precision);
        var num2 = _ratPak.StringToNumber("456", 10, _precision);

        _ratPak.addnum(ref num1, num2, 10);

        var result = _ratPak.NumberToString(ref num1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("579", result);
    }

    [Fact]
    public void AddNum_NegativeNumbers_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("-123", 10, _precision);
        var num2 = _ratPak.StringToNumber("456", 10, _precision);

        _ratPak.addnum(ref num1, num2, 10);

        var result = _ratPak.NumberToString(ref num1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("333", result);
    }

    [Fact]
    public void AddRat_SimpleFractions_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("1", 10, _precision);
        var num2 = _ratPak.StringToNumber("2", 10, _precision);
        var num3 = _ratPak.StringToNumber("1", 10, _precision);
        var num4 = _ratPak.StringToNumber("3", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10); // 1
        var rat2 = _ratPak.numtorat(num2, 10); // 2
        var rat3 = _ratPak.numtorat(num3, 10); // 1
        var rat4 = _ratPak.numtorat(num4, 10); // 3

        // Create 1/2
        _ratPak.divrat(ref rat1, rat2, _precision);

        // Create 1/3
        _ratPak.divrat(ref rat3, rat4, _precision);

        // 1/2 + 1/3
        _ratPak.addrat(ref rat1, rat3, _precision);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("0.8333333333333333333333333333333333333333333333333333333333333333", result);
    }

    [Theory]
    [InlineData("1", "2", "3")] // 1 + 2 = 3
    [InlineData("0", "0", "0")] // 0 + 0 = 0
    [InlineData("-5", "5", "0")] // -5 + 5 = 0
    [InlineData("999", "1", "1000")] // 999 + 1 = 1000
    [InlineData("-10", "-20", "-30")] // -10 + -20 = -30
    public void AddNum_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var numA = _ratPak.StringToNumber(a, 10, _precision);
        var numB = _ratPak.StringToNumber(b, 10, _precision);

        _ratPak.addnum(ref numA, numB, 10);

        var result = _ratPak.NumberToString(ref numA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Subtraction Tests

    [Fact]
    public void SubRat_SimpleFractions_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("3", 10, _precision);
        var num2 = _ratPak.StringToNumber("4", 10, _precision);
        var num3 = _ratPak.StringToNumber("1", 10, _precision);
        var num4 = _ratPak.StringToNumber("5", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10); // 3
        var rat2 = _ratPak.numtorat(num2, 10); // 4
        var rat3 = _ratPak.numtorat(num3, 10); // 1
        var rat4 = _ratPak.numtorat(num4, 10); // 5

        // Create 3/4
        _ratPak.divrat(ref rat1, rat2, _precision);

        // Create 1/5
        _ratPak.divrat(ref rat3, rat4, _precision);
        // 3 / 4 - 1 / 5
        _ratPak.subrat(ref rat1, rat3, _precision);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("0.55", result);
    }

    [Theory]
    [InlineData("1", "2/3", "0.3333333333333333333333333333333333333333333333333333333333333333")]
    [InlineData("10", "4.5", "5.5")]
    [InlineData("1", "2", "-1")]
    [InlineData("1.5", "0.5", "1")]
    public void SubRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.subrat(ref ratA, ratB, _precision);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Multiplication Tests

    [Fact]
    public void MulNum_SimpleMultiplication_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("123", 10, _precision);
        var num2 = _ratPak.StringToNumber("456", 10, _precision);

        _ratPak.mulnum(ref num1, num2, 10);

        var result = _ratPak.NumberToString(ref num1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("56088", result);
    }

    [Fact]
    public void MulRat_FractionMultiplication_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("2", 10, _precision);
        var num2 = _ratPak.StringToNumber("3", 10, _precision);
        var num3 = _ratPak.StringToNumber("3", 10, _precision);
        var num4 = _ratPak.StringToNumber("7", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10); // 2
        var rat2 = _ratPak.numtorat(num2, 10); // 3
        var rat3 = _ratPak.numtorat(num3, 10); // 3
        var rat4 = _ratPak.numtorat(num4, 10); // 7

        // Create 2/3
        _ratPak.divrat(ref rat1, rat2, _precision);

        // Create 3/7
        _ratPak.divrat(ref rat3, rat4, _precision);
        // (2 / 3) * (3 / 7)
        _ratPak.mulrat(ref rat1, rat3, _precision);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("0.2857142857142857142857142857142857142857142857142857142857142857", result);
    }

    [Theory]
    [InlineData("7", "6", "42")]
    [InlineData("0", "100", "0")]
    [InlineData("-5", "4", "-20")]
    [InlineData("-3", "-2", "6")]
    [InlineData("0.5", "0.5", "0.25")]
    public void MulRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.mulrat(ref ratA, ratB, _precision);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Division Tests

    [Fact]
    public void DivNum_SimpleDivision_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("100", 10, _precision);
        var num2 = _ratPak.StringToNumber("4", 10, _precision);

        _ratPak.divnum(ref num1, num2, 10, _precision);

        var result = _ratPak.NumberToString(ref num1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("25", result);
    }

    [Fact]
    public void DivRat_FractionDivision_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("1", 10, _precision);
        var num2 = _ratPak.StringToNumber("2", 10, _precision);
        var num3 = _ratPak.StringToNumber("3", 10, _precision);
        var num4 = _ratPak.StringToNumber("4", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10); // 1
        var rat2 = _ratPak.numtorat(num2, 10); // 2
        var rat3 = _ratPak.numtorat(num3, 10); // 3
        var rat4 = _ratPak.numtorat(num4, 10); // 4

        // Create 1/2
        _ratPak.divrat(ref rat1, rat2, _precision);

        // Create 3/4
        _ratPak.divrat(ref rat3, rat4, _precision);
        // (1 / 2) / (3 / 4)
        _ratPak.divrat(ref rat1, rat3, _precision);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("0.6666666666666666666666666666666666666666666666666666666666666667", result);
    }

    [Theory]
    [InlineData("10", "2", "5")]
    [InlineData("1", "3", "0.3333333333333333333333333333333333333333333333333333333333333333")]
    [InlineData("0", "5", "0")]
    [InlineData("-12", "4", "-3")]
    [InlineData("-8", "-2", "4")]
    public void DivRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.divrat(ref ratA, ratB, _precision);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Remainder and Modulo Tests

    [Fact]
    public void RemRat_SimpleRemainder_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("10", 10, _precision);
        var num2 = _ratPak.StringToNumber("3", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10); // 10
        var rat2 = _ratPak.numtorat(num2, 10); // 3

        _ratPak.remrat(ref rat1, rat2);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("1", result);
    }

    [Fact]
    public void ModRat_SimpleModulo_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("-10", 10, _precision);
        var num2 = _ratPak.StringToNumber("3", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10); // -10
        var rat2 = _ratPak.numtorat(num2, 10); // 3

        _ratPak.modrat(ref rat1, rat2);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("2", result); // In modular arithmetic, -10 mod 3 = 2
    }

    [Theory]
    [InlineData("10", "3", "1")] // 10 % 3 = 1
    [InlineData("10", "2", "0")] // 10 % 2 = 0
    [InlineData("7", "4", "3")] // 7 % 4 = 3
    [InlineData("-7", "4", "-3")] // -7 % 4 = -3 (reminder takes sign of dividend)
    [InlineData("7", "-4", "3")] // 7 % -4 = 3 (reminder takes sign of dividend)
    public void RemRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.remrat(ref ratA, ratB);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("10", "3", "1")] // 10 mod 3 = 1
    [InlineData("-10", "3", "2")] // -10 mod 3 = 2
    [InlineData("10", "-3", "-2")] // 10 mod -3 = -2
    [InlineData("-10", "-3", "-1")] // -10 mod -3 = -1
    public void ModRat_VariousInputs_CorrectResults(string a, string b, string expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        _ratPak.modrat(ref ratA, ratB);

        var result = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(expected, result);
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
