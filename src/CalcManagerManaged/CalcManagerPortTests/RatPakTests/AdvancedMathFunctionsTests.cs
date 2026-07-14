using CalcEngine;

namespace CalcManagerPortTests.RatPakTests;

public class AdvancedMathFunctionsTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 64;

    public AdvancedMathFunctionsTests()
    {
        _ratPak = new RatPak(_precision);
    }

    #region Factorial Tests

    [Theory]
    [InlineData("0", "1")]
    [InlineData("1", "1")]
    [InlineData("2", "2")]
    [InlineData("3", "6")]
    [InlineData("4", "24")]
    [InlineData("5", "120")]
    [InlineData("6", "720")]
    [InlineData("10", "3628800")]
    public void FactRatIntegerValuesCorrectResults(string value, string expected)
    {
        ArgumentNullException.ThrowIfNull(value);

        var rat = StringToRat(value);

        _ratPak.factrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FactRatNegativeValuesThrowsException()
    {
        var rat = StringToRat("-1");

        Assert.Throws<CalcErrException>(() => _ratPak.factrat(ref rat, 10, _precision));
    }

    [Fact]
    public void FactRatLargeValueComputesCorrectly()
    {
        // try a larger but still reasonable factorial (15!)
        var rat = StringToRat("15");

        _ratPak.factrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("1307674368000", result);
    }

    #endregion

    #region Root Function Tests

    [Theory]
    [InlineData("4", "2", "2")]
    [InlineData("8", "3", "2")]
    [InlineData("16", "2", "4")]
    [InlineData("16", "4", "2")]
    [InlineData("27", "3", "3")]
    [InlineData("1", "5", "1")]
    [InlineData("100", "2", "10")]
    public void RootRatVariousInputsCorrectResults(string value, string root, string expected)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(root);

        var valueRat = StringToRat(value);
        var rootRat = StringToRat(root);

        _ratPak.rootrat(ref valueRat, rootRat, 10, _precision);

        var result = _ratPak.RatToString(ref valueRat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("4", "1/2", "2")]
    [InlineData("9", "1/2", "3")]
    [InlineData("16", "1/2", "4")]
    [InlineData("8", "1/3", "2")]
    public void PowerAndRootAreInverses(string value, string root, string power)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(root);

        var valueRat = StringToRat(value);
        var rootRat = StringToRat(root);
        //raise to power (e.g., 2^0.5 = sqrt(2))
        _ratPak.powrat(ref valueRat, rootRat, 10, _precision);
        var result0 = _ratPak.RatToString(ref valueRat, NumberFormat.FloatingPoint, 10, _precision);

        Assert.Equal(power, result0);

        // Take the result and raise to reciprocal power (e.g., sqrt(2)^2 = 2)
        var recipRootRat = StringToRat("1");
        _ratPak.divrat(ref recipRootRat, rootRat, _precision);
        _ratPak.powrat(ref valueRat, recipRootRat, 10, _precision);

        //should get back original number
        var result = _ratPak.RatToString(ref valueRat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal(value, result);
    }

    [Fact]
    public void RootRatEquivalentToPowRat()
    {
        // Verify that root(x, n) is equivalent to x^(1/n)

        var value = "16";
        var root = "2";

        // Calculate root(16, 2)
        var valueRat1 = StringToRat(value);
        var rootRat1 = StringToRat(root);
        _ratPak.rootrat(ref valueRat1, rootRat1, 10, _precision);

        // Calculate 16^(1/2)
        var valueRat2 = StringToRat(value);
        var rootRat2 = StringToRat("1");
        var rootRat2Denominator = StringToRat(root);
        _ratPak.divrat(ref rootRat2, rootRat2Denominator, _precision);

        _ratPak.powrat(ref valueRat2, rootRat2, 10, _precision);

        var result1 = _ratPak.RatToString(ref valueRat1, NumberFormat.FloatingPoint, 10, _precision);
        var result2 = _ratPak.RatToString(ref valueRat2, NumberFormat.FloatingPoint, 10, _precision);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void RootRatNegativeBaseEvenRootThrowsException()
    {
        var valueRat = StringToRat("-4");
        var rootRat = StringToRat("2");

        Assert.Throws<CalcErrException>(() => _ratPak.rootrat(ref valueRat, rootRat, 10, _precision));
    }

    [Fact]
    public void RootRatNegativeBaseOddRootReturnsNegativeResult()
    {
        var valueRat = StringToRat("-8");
        var rootRat = StringToRat("3");

        _ratPak.rootrat(ref valueRat, rootRat, 10, _precision);

        var result = _ratPak.RatToString(ref valueRat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("-2", result);
    }

    #endregion

    #region Integer and Fractional Part Tests

    [Theory]
    [InlineData("3.14159", "3")]
    [InlineData("-3.14159", "-3")]
    [InlineData("0.14159", "0")]
    [InlineData("-0.14159", "0")]
    [InlineData("42", "42")]
    [InlineData("0", "0")]
    public void IntRatVariousInputsCorrectResults(string input, string expected)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rat = StringToRat(input);

        _ratPak.intrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("3.14159", "0.14159")]
    [InlineData("-3.14159", "-0.14159")]
    [InlineData("0.14159", "0.14159")]
    [InlineData("-0.14159", "-0.14159")]
    [InlineData("42", "0")]
    [InlineData("0", "0")]
    public void FracRatVariousInputsCorrectResults(string input, string expected)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rat = StringToRat(input);

        _ratPak.fracrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IntAndFracRatSumToOriginal()
    {
        // Verify that int(x) + frac(x) = x

        var value = "3.14159";

        // Calculate int(x)
        var intRat = StringToRat(value);
        _ratPak.intrat(ref intRat, 10, _precision);

        // Calculate frac(x)
        var fracRat = StringToRat(value);
        _ratPak.fracrat(ref fracRat, 10, _precision);

        // Add them together
        _ratPak.addrat(ref intRat, fracRat, _precision);

        var result = _ratPak.RatToString(ref intRat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal(value, result);
    }

    #endregion

    #region FlatRat Function Tests

    [Fact]
    public void FlatRatSimplifiesFractions()
    {
        var rat = StringToRat("2/4"); // Should simplify to 1/2

        _ratPak.flatrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("0.5", result);
    }

    [Fact]
    public void FlatRatHandlesIrrationalNumbers()
    {
        var rat = StringToRat("1.414213562373095048801688724209"); // √2

        _ratPak.flatrat(ref rat, 10, _precision);
        // should still be close to the original value
        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.StartsWith("1.41421", result, StringComparison.Ordinal);
    }

    #endregion

    #region GCD and Fraction Simplification Tests

    [Theory]
    [InlineData("12", "18", "2")]
    [InlineData("48", "36", "12")]
    [InlineData("17", "13", "1")]
    [InlineData("0", "5", "5")]
    [InlineData("5", "0", "5")]
    public void GcdVariousInputsCorrectResults(string a, string b, string expected)
    {
        var numA = _ratPak.StringToNumber(a, 10, _precision);
        Assert.NotNull(numA);
        var numB = _ratPak.StringToNumber(b, 10, _precision);
        Assert.NotNull(numB);

        var result = RatPak.gcd(numA, numB);

        var resultStr = _ratPak.NumberToString(ref result, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal(expected, resultStr);
    }

    [Fact]
    public void GcdRatSimplifiesFractions()
    {
        // create a fraction that needs simplification
        var numNumerator = _ratPak.StringToNumber("12", 10, _precision);
        Assert.NotNull(numNumerator);
        var numDenominator = _ratPak.StringToNumber("18", 10, _precision);
        Assert.NotNull(numDenominator);

        var rat = RatPak.numtorat(numNumerator, 10);
        var denominator = RatPak.numtorat(numDenominator, 10);

        _ratPak.divrat(ref rat, denominator, _precision);

        _ratPak.gcdrat(ref rat, _precision);

        var result = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
        Assert.Equal("0.6666666666666666666666666666666666666666666666666666666666666667", result);

        // We can also verify by getting the internal numerator and denominator
        // Create 2/3 for comparison
        var expectedRat = StringToRat("2/3");
        Assert.True(_ratPak.RatEqu(rat, expectedRat, _precision));
    }

    #endregion

    #region Helper Methods

    private RAT StringToRat(string input)
    {
        // Handle fractions like "1/3"
        if (input.Contains('/', StringComparison.Ordinal))
        {
            string[] parts = input.Split('/');
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
