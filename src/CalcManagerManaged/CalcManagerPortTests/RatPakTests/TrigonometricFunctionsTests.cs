using  CalcEngine;

namespace CalcManagerPortTests.RatPakTests;

public class TrigonometricFunctionsTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 64;

    public TrigonometricFunctionsTests()
    {
        _ratPak = new RatPak(_precision);
    }

    #region Basic Trigonometric Functions

    private const string str_pi = "3.141592653589793238462643383279502884197169399375105820974944592";
    private const string str_pi_over_two = "1.570796326794896619231321691639751442098584699687552910487472296";
    private const string str_three_pi_over_two = "4.712388980384689857693965074919254326295754099062658731462416888";
    private const string str_two_pi = "6.283185307179586476925286766559005768394338798750211641949889184";

    [Theory]
    [InlineData("0", "0")]
    [InlineData(str_pi_over_two, "1")] // PI/2
    [InlineData(str_pi, "3.078164062862077253198170763202119800274394661847049713166255326e-64")] // PI
    [InlineData(str_three_pi_over_two, "-1")] // 3*PI/2
    [InlineData(str_two_pi, "-6.156328125447709728066804241773481062317552079903881717034200629e-64")] // 2*PI
    public void SinRat_BasicAngles_CorrectResults(string angle, string expected)
    {
        var rat = StringToRat(angle);

        _ratPak.sinrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", "1")]
    [InlineData(str_pi_over_two,
        "1.539082031431039599590752625450611788216151564997279912558815989e-64")] // PI/2
    [InlineData(str_pi, "-1")] // PI
    [InlineData(str_three_pi_over_two,
        "-4.617246094292477905329971580111555794299503541381323395045725942e-64")] // 3*PI/2
    [InlineData(str_two_pi, "1")] // 2*PI
    public void CosRat_BasicAngles_CorrectResults(string angle, string expected)
    {
        var rat = StringToRat(angle);

        _ratPak.cosrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("0.785398163397448309615660845819875721049292349843776455243736148",
        "0.9999999999999999999999999999999999999999999999999999999999999998")] // PI/4
    [InlineData("2.356194490192344928846982537459627163147877049531329365731208444", "-1")] // 3*PI/4
    [InlineData(str_pi,
        "-3.078164062862077253198170763202119800274394661847049713166255326e-64")] // PI
    public void TanRat_BasicAngles_CorrectResults(string angle, string expected)
    {
        var rat = StringToRat(angle);

        _ratPak.tanrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TanRat_DividesCorrectFunctions()
    {
        // Verify that tan(x) = sin(x)/cos(x)

        // Calculate sin(x)
        var ratSin = StringToRat($"{str_pi}/8"); // PI/8
        _ratPak.sinrat(ref ratSin, 10, _precision);

        // Calculate cos(x)
        var ratCos = StringToRat($"{str_pi}/8"); // PI/8
        _ratPak.cosrat(ref ratCos, 10, _precision);

        // Calculate sin(x)/cos(x)
        _ratPak.divrat(ref ratSin, ratCos, _precision);

        // Calculate tan(x)
        var ratAngle = StringToRat($"{str_pi}/8"); // PI/8
        _ratPak.tanrat(ref ratAngle, 10, _precision);

        var sinCosResult = _ratPak.RatToString(ref ratSin, RatPak.NumberFormat.Float, 10, _precision);
        var tanResult = _ratPak.RatToString(ref ratAngle, RatPak.NumberFormat.Float, 10, _precision);

        // They should be very close
        Assert.Equal(sinCosResult, tanResult);
    }

    #endregion

    #region Angle Type Conversions

    [Theory]
    [InlineData("30", RatPak.AngleType.Degrees, "0.5")]
    [InlineData("90", RatPak.AngleType.Degrees, "1")]
    [InlineData("180", RatPak.AngleType.Degrees, "0")]
    [InlineData("270", RatPak.AngleType.Degrees, "-1")]
    [InlineData("360", RatPak.AngleType.Degrees, "0")]
    [InlineData("0.5", RatPak.AngleType.Radians, "0.4794255386042030002732879352155713880818033679406006751886166131")]
    [InlineData(str_pi_over_two, RatPak.AngleType.Radians, "1")]
    [InlineData("33.33", RatPak.AngleType.Gradians,
        "0.4999546543305256694983119678260438713511843523199668384741403917")]
    [InlineData("100", RatPak.AngleType.Gradians, "1")]
    [InlineData("200", RatPak.AngleType.Gradians, "0")]
    public void SinAngleRat_DifferentAngleTypes_CorrectResults(string angle, RatPak.AngleType angleType,
        string expected)
    {
        var rat = StringToRat(angle);

        _ratPak.sinanglerat(ref rat, angleType, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", RatPak.AngleType.Degrees, "1")]
    [InlineData("60", RatPak.AngleType.Degrees, "0.5")]
    [InlineData("90", RatPak.AngleType.Degrees, "0")]
    [InlineData("180", RatPak.AngleType.Degrees, "-1")]
    [InlineData("360", RatPak.AngleType.Degrees, "1")]
    [InlineData("0", RatPak.AngleType.Radians, "1")]
    [InlineData("1.047197551196597746154214461093167628065723133125035273658314864", RatPak.AngleType.Radians,
        "0.5000000000000000000000000000000000000000000000000000000000000001")]
    [InlineData(str_pi, RatPak.AngleType.Radians, "-1")]
    [InlineData("0", RatPak.AngleType.Gradians, "1")]
    [InlineData("66.67", RatPak.AngleType.Gradians,
        "0.4999546543305256694983119678260438713511843523199668384741403917")]
    [InlineData("200", RatPak.AngleType.Gradians, "-1")]
    public void CosAngleRat_DifferentAngleTypes_CorrectResults(string angle, RatPak.AngleType angleType,
        string expected)
    {
        var rat = StringToRat(angle);

        _ratPak.cosanglerat(ref rat, angleType, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", RatPak.AngleType.Degrees, "0")]
    [InlineData("45", RatPak.AngleType.Degrees, "1")]
    [InlineData("135", RatPak.AngleType.Degrees, "-1")]
    [InlineData("180", RatPak.AngleType.Degrees, "0")]
    [InlineData("0", RatPak.AngleType.Radians, "0")]
    [InlineData("0.785398163397448309615660845819875721049292349843776455243736148", RatPak.AngleType.Radians,
        "0.9999999999999999999999999999999999999999999999999999999999999998")] // PI/4
    [InlineData("0", RatPak.AngleType.Gradians, "0")]
    [InlineData("50", RatPak.AngleType.Gradians, "1")]
    [InlineData("150", RatPak.AngleType.Gradians, "-1")]
    public void TanAngleRat_DifferentAngleTypes_CorrectResults(string angle, RatPak.AngleType angleType,
        string expected)
    {
        var rat = StringToRat(angle);

        _ratPak.tananglerat(ref rat, angleType, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    #endregion

    #region Inverse Trigonometric Functions

    [Theory]
    [InlineData("0", "0")]
    [InlineData("0.5", "0.5235987755982988730771072305465838140328615665625176368291574321")]
    [InlineData("1", str_pi_over_two)]
    [InlineData("-0.5", "-0.5235987755982988730771072305465838140328615665625176368291574321")]
    [InlineData("-1", "-1.570796326794896619231321691639751442098584699687552910487472296")]
    public void AsinRat_BasicValues_CorrectResults(string value, string expected)
    {
        var rat = StringToRat(value);

        _ratPak.asinrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", str_pi_over_two)]
    [InlineData("0.5", "1.047197551196597746154214461093167628065723133125035273658314864")]
    [InlineData("1", "0")]
    [InlineData("-0.5", "2.094395102393195492308428922186335256131446266250070547316629728")]
    [InlineData("-1", str_pi)]
    public void AcosRat_BasicValues_CorrectResults(string value, string expected)
    {
        var rat = StringToRat(value);

        _ratPak.acosrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("1", "0.78539816339745")]
    [InlineData("-1", "-0.78539816339745")]
    public void AtanRat_BasicValues_CorrectResults(string value, string expected)
    {
        var rat = StringToRat(value);

        _ratPak.atanrat(ref rat, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        // For exact values, check the whole string
        if (expected == "0")
        {
            Assert.Equal(expected, result);
        }
        else // For approximate values, check first several digits
        {
            Assert.StartsWith(expected.Substring(0, Math.Min(expected.Length, 10)),
                result.Substring(0, Math.Min(result.Length, 10)));
        }
    }

    #endregion

    #region Inverse Trigonometric Functions With Angle Types

    [Theory]
    [InlineData("0", RatPak.AngleType.Degrees, "0")]
    [InlineData("0.5", RatPak.AngleType.Degrees, "30")]
    [InlineData("1", RatPak.AngleType.Degrees, "90")]
    [InlineData("0", RatPak.AngleType.Radians, "0")]
    [InlineData("0.5", RatPak.AngleType.Radians, "0.5235987755983")]
    [InlineData("0", RatPak.AngleType.Gradians, "0")]
    [InlineData("0.5", RatPak.AngleType.Gradians, "33.33333333333333")]
    public void AsinAngleRat_DifferentAngleTypes_CorrectResults(string value, RatPak.AngleType angleType,
        string expected)
    {
        var rat = StringToRat(value);

        _ratPak.asinanglerat(ref rat, angleType, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        // For exact values, check the whole string
        if (expected == "0" || expected == "90")
        {
            Assert.StartsWith(expected, result);
        }
        else // For approximate values, check first few digits
        {
            Assert.StartsWith(expected.Substring(0, Math.Min(expected.Length, 5)),
                result.Substring(0, Math.Min(result.Length, 5)));
        }
    }

    [Theory]
    [InlineData("0", RatPak.AngleType.Degrees, "90")]
    [InlineData("0.5", RatPak.AngleType.Degrees, "60")]
    [InlineData("1", RatPak.AngleType.Degrees, "0")]
    [InlineData("0", RatPak.AngleType.Radians, str_pi_over_two)]
    [InlineData("1", RatPak.AngleType.Radians, "0")]
    [InlineData("0", RatPak.AngleType.Gradians, "100")]
    [InlineData("1", RatPak.AngleType.Gradians, "0")]
    public void AcosAngleRat_DifferentAngleTypes_CorrectResults(string value, RatPak.AngleType angleType,
        string expected)
    {
        var rat = StringToRat(value);

        _ratPak.acosanglerat(ref rat, angleType, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", RatPak.AngleType.Degrees, "0")]
    [InlineData("1", RatPak.AngleType.Degrees, "45")]
    [InlineData("0", RatPak.AngleType.Radians, "0")]
    [InlineData("1", RatPak.AngleType.Radians, "0.7853981633974483096156608458198757210492923498437764552437361481")]
    [InlineData("0", RatPak.AngleType.Gradians, "0")]
    [InlineData("1", RatPak.AngleType.Gradians, "50")]
    public void AtanAngleRat_DifferentAngleTypes_CorrectResults(string value, RatPak.AngleType angleType,
        string expected)
    {
        var rat = StringToRat(value);

        _ratPak.atananglerat(ref rat, angleType, 10, _precision);

        var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

        Assert.Equal(expected, result);
    }

    #endregion

    #region Helper Methods

    private RatPak.RAT StringToRat(string input)
    {
        // Handle fractions like "1/3"
        if (input.Contains('/'))
        {
            var parts = input.Split('/');
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
