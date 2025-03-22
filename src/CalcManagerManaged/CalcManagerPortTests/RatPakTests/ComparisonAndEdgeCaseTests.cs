using CalcEngine;
using  CalcEngine;

namespace CalcManagerPortTests.RatPakTests;

public class ComparisonAndEdgeCaseTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 256;

    public ComparisonAndEdgeCaseTests()
    {
        _ratPak = new RatPak(_precision);
    }

    #region Comparison Tests

    [Fact]
    public void ZerNum_ZeroNumber_ReturnsTrue()
    {
        var num = _ratPak.StringToNumber("0", 10, _precision);

        Assert.True(_ratPak.zernum(num));
    }

    [Fact]
    public void ZerNum_NonZeroNumber_ReturnsFalse()
    {
        var num = _ratPak.StringToNumber("0.000000001", 10, _precision);

        Assert.False(_ratPak.zernum(num));
    }

    [Fact]
    public void ZerRat_ZeroRational_ReturnsTrue()
    {
        var num = _ratPak.StringToNumber("0", 10, _precision);
        var rat = _ratPak.numtorat(num, 10);

        Assert.True(_ratPak.zerrat(rat));
    }

    [Theory]
    [InlineData("5", "5", true)]
    [InlineData("5", "6", false)]
    [InlineData("-5", "-5", true)]
    // Apparently EquNum doesn't check for difference in signs, only the mantissa/magnitude.
    // Already checked with the original.
    // Most reliable way to check for equality is to convert numbers into rationals first.
    [InlineData("-5", "5", true)]
    [InlineData("0", "0", true)]
    [InlineData("1234567890", "1234567890", true)]
    [InlineData("0.1234567890", "0.1234567890", true)]
    public void EquNum_VariousInputs_CorrectResult(string a, string b, bool expected)
    {
        var numA = _ratPak.StringToNumber(a, 10, _precision);
        var numB = _ratPak.StringToNumber(b, 10, _precision);

        Assert.Equal(expected, _ratPak.equnum(numA, numB));
    }

    [Theory]
    [InlineData("1/3", "1/3", true)]
    [InlineData("1/3", "2/6", true)]
    [InlineData("1/2", "2/5", false)]
    [InlineData("1.5", "3/2", true)]
    [InlineData("-0.5", "-1/2", true)]
    [InlineData("-3", "3", false)]
    public void RatEqu_VariousInputs_CorrectResult(string a, string b, bool expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        Assert.Equal(expected, _ratPak.rat_equ(ratA, ratB, _precision));
    }

    [Theory]
    [InlineData("5", "3", true)]
    [InlineData("3", "5", false)]
    [InlineData("-5", "-3", false)]
    [InlineData("-3", "-5", true)]
    [InlineData("0.5", "0.3", true)]
    [InlineData("1/3", "1/4", true)]
    [InlineData("1/3", "-1/4", true)]
    public void RatGt_VariousInputs_CorrectResult(string a, string b, bool expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        Assert.Equal(expected, _ratPak.rat_gt(ratA, ratB, _precision));
    }

    [Theory]
    [InlineData("3", "5", true)]
    [InlineData("5", "3", false)]
    [InlineData("-5", "-3", true)]
    [InlineData("-3", "-5", false)]
    [InlineData("0.3", "0.5", true)]
    [InlineData("1/4", "1/3", true)]
    public void RatLt_VariousInputs_CorrectResult(string a, string b, bool expected)
    {
        var ratA = StringToRat(a);
        var ratB = StringToRat(b);

        Assert.Equal(expected, _ratPak.rat_lt(ratA, ratB, _precision));
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void AddRat_VeryLargeNumbers_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("1.e+100", 10, _precision);
        var num2 = _ratPak.StringToNumber("1.e+99", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10);
        var rat2 = _ratPak.numtorat(num2, 10);

        _ratPak.addrat(ref rat1, rat2, _precision);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Scientific, 10, _precision);
        Assert.Equal("1.1e+100", result);
    }

    [Fact]
    public void AddRat_VerySmallNumbers_CorrectResult()
    {
        var num1 = _ratPak.StringToNumber("1e-100", 10, _precision);
        var num2 = _ratPak.StringToNumber("1e-100", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10);
        var rat2 = _ratPak.numtorat(num2, 10);

        _ratPak.addrat(ref rat1, rat2, _precision);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Scientific, 10, _precision);
        Assert.Equal("2.e-100", result);
    }

    [Fact]
    public void MulRat_OverflowPrevention_HandlesLargeNumbers()
    {
        var num1 = _ratPak.StringToNumber("1e50", 10, _precision);
        var num2 = _ratPak.StringToNumber("1e50", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10);
        var rat2 = _ratPak.numtorat(num2, 10);

        _ratPak.mulrat(ref rat1, rat2, _precision);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Scientific, 10, _precision);
        Assert.Equal("1.e+100", result);
    }

    [Fact]
    public void DivRat_VerySmallDenominator_HandlesCorrectly()
    {
        var num1 = _ratPak.StringToNumber("1", 10, _precision);
        var num2 = _ratPak.StringToNumber("1e-50", 10, _precision);

        var rat1 = _ratPak.numtorat(num1, 10);
        var rat2 = _ratPak.numtorat(num2, 10);

        _ratPak.divrat(ref rat1, rat2, _precision);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Scientific, 10, _precision);
        Assert.Equal("1.e+50", result);
    }

    [Fact]
    public void RatPowi32_PositivePower_CorrectResult()
    {
        var num = _ratPak.StringToNumber("2", 10, _precision);
        var rat = _ratPak.numtorat(num, 10);

        _ratPak.ratpowi32(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("1024", result);
    }

    [Fact]
    public void RatPowi32_NegativePower_CorrectResult()
    {
        var num = _ratPak.StringToNumber("2", 10, _precision);
        var rat = _ratPak.numtorat(num, 10);

        _ratPak.ratpowi32(ref rat, -3, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("0.125", result);
    }

    [Theory]
    [InlineData("1010", 2, "10")]
    [InlineData("12", 8, "10")]
    [InlineData("A", 16, "10")]
    [InlineData("10", 10, "10")]
    public void StringToRat_VariousBases_CorrectResults(string inputStr, uint radix, string base10Expected)
    {
        var otherNum = _ratPak.StringToNumber(inputStr, radix, _precision);
        var otherRat = _ratPak.numtorat(otherNum, radix);

        Assert.Equal(base10Expected, _ratPak.RatToString(ref otherRat, RatPak.NumberFormat.Float, 10, _precision));
    }

    #endregion

    #region Precision Tests

    [Fact]
    public void DivRat_HighPrecision_CorrectResult()
    {
        var highPrecision = 100;
        var num1 = _ratPak.StringToNumber("1", 10, highPrecision);
        var num2 = _ratPak.StringToNumber("3", 10, highPrecision);

        var rat1 = _ratPak.numtorat(num1, 10);
        var rat2 = _ratPak.numtorat(num2, 10);

        _ratPak.divrat(ref rat1, rat2, highPrecision);

        var result = _ratPak.RatToString(ref rat1, RatPak.NumberFormat.Float, 10, highPrecision);

        // Verify high precision result
        Assert.StartsWith("0.33333333333333333333333333333333333333333333333333", result);
        Assert.True(result.Length >= 90); // Ensure we get a long representation
    }

    [Fact]
    public void StringToNumber_LongDecimal_PreservesPrecision()
    {
        var longDecimal = "0.1234567890123456789012345678901234567890123456789012345678";

        var num = _ratPak.StringToNumber(longDecimal, 10, _precision);

        var result = _ratPak.NumberToString(ref num, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal(longDecimal, result);
    }

    #endregion

    #region Constants Tests

    [Fact]
    public void ConstantPi_HasCorrectValue()
    {
        var piString = _ratPak.RatToString(ref _ratPak.pi, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(
            "3.141592653589793238462643383279502884197169399375105820974944592307816406286208998628034825342117067982148086513282306647093844609550582231725359408128481117450284102701938521105559644622948954930381964428810975665933446128475648233786783165271201909145649",
            piString);
    }

    [Fact]
    public void MathematicalConstants_AreInitialized()
    {
        Assert.NotNull(_ratPak.pi);
        Assert.NotNull(_ratPak.two_pi);
        Assert.NotNull(_ratPak.pi_over_two);
        Assert.NotNull(_ratPak.rat_exp);

        // Check the values of a few constants
        var twoPiString = _ratPak.RatToString(ref _ratPak.two_pi, RatPak.NumberFormat.Float, 10, _precision);
        var piOverTwoString =
            _ratPak.RatToString(ref _ratPak.pi_over_two, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(
            "6.283185307179586476925286766559005768394338798750211641949889184615632812572417997256069650684234135964296173026564613294187689219101164463450718816256962234900568205403877042211119289245897909860763928857621951331866892256951296467573566330542403818291297",
            twoPiString);
        Assert.Equal(
            "1.570796326794896619231321691639751442098584699687552910487472296153908203143104499314017412671058533991074043256641153323546922304775291115862679704064240558725142051350969260552779822311474477465190982214405487832966723064237824116893391582635600954572824",
            piOverTwoString);
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
