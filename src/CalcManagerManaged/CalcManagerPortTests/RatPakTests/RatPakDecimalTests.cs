using CalcEngine;

namespace CalcManagerPortTests.RatPakTests;

public class RatPakDecimalTests
{
    private readonly RatPak _ratPak = new(RatPakDecimal.Precision);

    [Theory]
    [InlineData("0.1", "0.2", "0.3")]
    [InlineData("1e-14", "2e-14", "0.00000000000003")]
    [InlineData("1/3", "2/3", "1")]
    [InlineData("-123456789.0123456789", "0.0000000001", "-123456789.0123456788")]
    public void ParsedValuesAddWithoutBinaryFloatingPointLoss(
        string left,
        string right,
        string expected)
    {
        Rational result = RatPakDecimal.Parse(_ratPak, left) +
                          RatPakDecimal.Parse(_ratPak, right);

        Assert.Equal(expected, Format(result));
    }

    [Theory]
    [InlineData("1.225", 2, RatPakRoundingMode.HalfDown, "1.22")]
    [InlineData("1.226", 2, RatPakRoundingMode.HalfDown, "1.23")]
    [InlineData("-1.225", 2, RatPakRoundingMode.HalfDown, "-1.22")]
    [InlineData("-1.226", 2, RatPakRoundingMode.HalfDown, "-1.23")]
    [InlineData("1.225", 2, RatPakRoundingMode.ToEven, "1.22")]
    [InlineData("1.235", 2, RatPakRoundingMode.ToEven, "1.24")]
    [InlineData("-1.235", 2, RatPakRoundingMode.ToEven, "-1.24")]
    [InlineData("1.225", 2, RatPakRoundingMode.AwayFromZero, "1.23")]
    [InlineData("-1.225", 2, RatPakRoundingMode.AwayFromZero, "-1.23")]
    public void FormatFixedAppliesTheRequestedExactMidpointRule(
        string input,
        int fractionDigits,
        RatPakRoundingMode roundingMode,
        string expected)
    {
        Rational value = RatPakDecimal.Parse(_ratPak, input);

        string result = RatPakDecimal.FormatFixed(
            _ratPak,
            value,
            fractionDigits,
            roundingMode);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1234.5", 0, "1235")]
    [InlineData("1234.5", 2, "1234.50")]
    [InlineData("0.0001", 6, "0.000100")]
    [InlineData("-9", 3, "-9.000")]
    public void FormatFixedProducesTheExactRequestedScale(
        string input,
        int fractionDigits,
        string expected)
    {
        string result = RatPakDecimal.FormatFixed(
            _ratPak,
            RatPakDecimal.Parse(_ratPak, input),
            fractionDigits,
            RatPakRoundingMode.AwayFromZero);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("9.9999999", 6, "1.000000e+1")]
    [InlineData("0.00001234567", 6, "1.234567e-5")]
    [InlineData("-1234567", 6, "-1.234567e+6")]
    [InlineData("0", 6, "0.000000e+0")]
    public void FormatScientificNormalizesMantissaAndCarry(
        string input,
        int fractionDigits,
        string expected)
    {
        string result = RatPakDecimal.FormatScientific(
            _ratPak,
            RatPakDecimal.Parse(_ratPak, input),
            fractionDigits);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 1, 0)]
    [InlineData("0.001", 1, -3)]
    [InlineData("-0.01", 1, -2)]
    [InlineData("9.99", 1, 0)]
    [InlineData("10", 2, 1)]
    [InlineData("-123456", 6, 5)]
    public void DecimalMagnitudeHelpersRemainExact(
        string input,
        int expectedWholeDigits,
        int expectedExponent)
    {
        Rational value = RatPakDecimal.Parse(_ratPak, input);

        Assert.Equal(expectedWholeDigits, RatPakDecimal.GetWholeDigitCount(_ratPak, value));
        Assert.Equal(expectedExponent, RatPakDecimal.GetDecimalExponent(_ratPak, value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("+")]
    [InlineData("1e")]
    [InlineData("1/2/3")]
    [InlineData("1/0")]
    public void ParseRejectsMalformedOrUndefinedValues(string input)
    {
        Assert.ThrowsAny<Exception>(() => RatPakDecimal.Parse(_ratPak, input));
    }

    private static string Format(Rational value) =>
        value.ToString(10, NumberFormat.FloatingPoint, RatPakDecimal.Precision);
}
