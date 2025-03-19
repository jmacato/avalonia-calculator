using CalcManagerPort;

namespace CalcManagerPortTests.RatPakTests;

public class ConversionFunctionsTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 64;

    public ConversionFunctionsTests()
    {
        _ratPak = new RatPak(_precision);
    }

    #region RAT to String Conversion Tests

    [Fact]
    public void RatToString_SimpleRational_CorrectString()
    {
        var num1 = _ratPak.StringToNumber("1", 10, _precision);
        var num2 = _ratPak.StringToNumber("2", 10, _precision);
        var rat = _ratPak.numtorat(num1, 10); // 1

        // Create 1/2
        _ratPak.divrat(ref rat, _ratPak.numtorat(num2, 10), _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal("0.5", result);
    }

    [Fact]
    public void RatToString_ComplexRational_CorrectString()
    {
        var num1 = _ratPak.StringToNumber("22", 10, _precision);
        var num2 = _ratPak.StringToNumber("7", 10, _precision);
        var rat = _ratPak.numtorat(num1, 10); // 22

        // Create 22/7 (approximation of PI)
        _ratPak.divrat(ref rat, _ratPak.numtorat(num2, 10), _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.StartsWith("3.142857", result); // Should start with 3.142857...
    }

    [Fact]
    public void RatToString_DifferentFormats_CorrectResults()
    {
        var num1 = _ratPak.StringToNumber("1000000", 10, _precision);
        var rat = _ratPak.numtorat(num1, 10); // 1000000
        var floatResult = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        // Scientific format
        var scientificResult = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Scientific, 10, _precision);

        // Engineering format
        var engineeringResult = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Engineering, 10, _precision);

        Assert.Equal("1000000", floatResult);
        Assert.Equal("1.e+6", scientificResult);
        Assert.Equal("1.e+6", engineeringResult); // Engineering format with multiples of 3
    }

    #endregion

    #region RatToNumber Conversion Tests

    [Fact]
    public void RatToNumber_SimpleRational_CorrectNumberResult()
    {
        var num1 = _ratPak.StringToNumber("5", 10, _precision);
        var num2 = _ratPak.StringToNumber("2", 10, _precision);
        var rat = _ratPak.numtorat(num1, 10); // 5

        // Create 5/2
        _ratPak.divrat(ref rat, _ratPak.numtorat(num2, 10), _precision);

        var result = _ratPak.RatToNumber(rat, 10, _precision);
        var resultStr = _ratPak.NumberToString(ref result, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal("2.5", resultStr);
    }

    [Fact]
    public void RatToNumber_ComplexRational_CorrectNumberResult()
    {
        var num1 = _ratPak.StringToNumber("1", 10, _precision);
        var num2 = _ratPak.StringToNumber("3", 10, _precision);
        var rat = _ratPak.numtorat(num1, 10); // 1

        // Create 1/3
        _ratPak.divrat(ref rat, _ratPak.numtorat(num2, 10), _precision);

        var result = _ratPak.RatToNumber(rat, 10, _precision);
        var resultStr = _ratPak.NumberToString(ref result, RatPak.NumberFormat.Float, 10, _precision);

        Assert.StartsWith("0.3333333", resultStr); // Should start with 0.3333333...
    }

    #endregion

    #region Int/Uint to RAT Conversion Tests

    [Fact]
    public void I32torat_PositiveValue_CorrectRat()
    {
        var rat = _ratPak.i32torat(42);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("42", result);
    }

    [Fact]
    public void I32torat_NegativeValue_CorrectRat()
    {
        var rat = _ratPak.i32torat(-123);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("-123", result);
    }

    [Fact]
    public void Ui32torat_Value_CorrectRat()
    {
        var rat = _ratPak.Ui32torat(4294967295); // Max uint32 value

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("4294967295", result);
    }

    #endregion

    #region RAT to Int/Uint Conversion Tests

    [Fact]
    public void Rattoi32_SimpleRational_CorrectInt()
    {
        var rat = _ratPak.i32torat(42);

        var result = _ratPak.rattoi32(rat, 10, _precision);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Rattoi32_FractionalRat_Truncates()
    {
        var num1 = _ratPak.StringToNumber("5", 10, _precision);
        var num2 = _ratPak.StringToNumber("2", 10, _precision);
        var rat = _ratPak.numtorat(num1, 10); // 5

        // Create 5/2 = 2.5
        _ratPak.divrat(ref rat, _ratPak.numtorat(num2, 10), _precision);

        var result = _ratPak.rattoi32(rat, 10, _precision);

        Assert.Equal(2, result); // Should truncate 2.5 to 2
    }

    [Fact]
    public void RattoUi64_PositiveRat_CorrectUint()
    {
        var rat = _ratPak.i32torat(123456789);

        var result = _ratPak.rattoUi64(rat, 10, _precision);

        Assert.Equal(123456789UL, result);
    }

    [Fact]
    public void RattoUi64_LargeValue_CorrectUint()
    {
        var num = _ratPak.StringToNumber("9223372036854775808", 10, _precision); // 2^63
        var rat = _ratPak.numtorat(num, 10);

        var result = _ratPak.rattoUi64(rat, 10, _precision);

        Assert.Equal(9223372036854775808UL, result); // 2^63 as UInt64
    }

    #endregion

    #region Number to RAT Conversion Tests

    [Fact]
    public void Numtorat_PositiveNumber_CorrectRat()
    {
        var num = _ratPak.StringToNumber("42", 10, _precision);

        var rat = _ratPak.numtorat(num, 10);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("42", result);
    }

    [Fact]
    public void Numtorat_NegativeNumber_CorrectRat()
    {
        var num = _ratPak.StringToNumber("-123.456", 10, _precision);

        var rat = _ratPak.numtorat(num, 10);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("-123.456", result);
    }

    #endregion

    #region Flatrat Tests

    [Fact]
    public void Flatrat_SimplifiesFraction()
    {
        var num1 = _ratPak.StringToNumber("4", 10, _precision);
        var num2 = _ratPak.StringToNumber("8", 10, _precision);
        var rat = _ratPak.numtorat(num1, 10); // 4

        // Create 4/8
        _ratPak.divrat(ref rat, _ratPak.numtorat(num2, 10), _precision);

        _ratPak.flatrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("0.5", result); // 4/8 simplified to 1/2 = 0.5
    }

    [Fact]
    public void Flatrat_ComplexFraction_Simplifies()
    {
        var num1 = _ratPak.StringToNumber("15", 10, _precision);
        var num2 = _ratPak.StringToNumber("35", 10, _precision);
        var rat = _ratPak.numtorat(num1, 10); // 15

        // Create 15/35
        _ratPak.divrat(ref rat, _ratPak.numtorat(num2, 10), _precision);

        _ratPak.flatrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("0.4285714285714285714285714285714285714285714285714285714285714286",
            result); // 15/35 = 3/7 ≈ 0.42857...
    }

    #endregion

    #region StringToRat Tests

    [Fact]
    public void StringToRat_PositiveValues_CorrectResult()
    {
        var rat = _ratPak.StringToRat(false, "123", false, "2", 10, _precision);
        //This should create 123 * 10^2 = 12300
        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("12300", result);
    }

    [Fact]
    public void StringToRat_NegativeValues_CorrectResult()
    {
        var rat = _ratPak.StringToRat(true, "456", true, "3", 10, _precision);
        //This should create - 456 * 10 ^ -3 = -0.456
        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("-0.456", result);
    }

    [Fact]
    public void StringToRat_MixedSigns_CorrectResult()
    {
        var rat = _ratPak.StringToRat(true, "789", false, "1", 10, _precision);
        //This should create - 789 * 10 ^ 1 = -7890
        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
        Assert.Equal("-7890", result);
    }

    #endregion
}
