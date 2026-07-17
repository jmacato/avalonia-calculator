using System.Globalization;
using Graphing.Symbolics;

namespace GraphingTests;

public sealed class RatPakExactRationalTests
{
    [Theory]
    [InlineData(int.MinValue, "-2147483648", -1)]
    [InlineData(0, "0", 0)]
    [InlineData(int.MaxValue, "2147483647", 1)]
    public void IntConstructorRoundTripsBoundaryValues(int value, string canonical, int sign)
    {
        ExactInteger integer = new(value);

        Assert.Equal(canonical, integer.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(sign, integer.Sign);
        Assert.Equal(value == 0, integer.IsZero);
        Assert.Equal(value, (int)integer);
        Assert.Equal(integer, ExactInteger.Parse(canonical));
    }

    [Theory]
    [InlineData(long.MinValue, "-9223372036854775808", -1)]
    [InlineData(0L, "0", 0)]
    [InlineData(long.MaxValue, "9223372036854775807", 1)]
    public void LongConstructorRoundTripsBoundaryValues(long value, string canonical, int sign)
    {
        ExactInteger integer = new(value);

        Assert.Equal(canonical, integer.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(sign, integer.Sign);
        Assert.Equal(value == 0, integer.IsZero);
        Assert.Equal(value, (long)integer);
        Assert.Equal(integer, ExactInteger.Parse(canonical));
    }

    [Fact]
    public void SmallIntegerAndRationalArithmeticIsCanonical()
    {
        Assert.Equal("2", new ExactInteger(2).ToString(CultureInfo.InvariantCulture));
        Assert.Equal("1", (new ExactInteger(3) / new ExactInteger(2)).ToString(CultureInfo.InvariantCulture));
        Assert.Equal("1", (new ExactInteger(3) % new ExactInteger(2)).ToString(CultureInfo.InvariantCulture));
        Assert.Equal("-1", (new ExactInteger(-3) / new ExactInteger(2)).ToString(CultureInfo.InvariantCulture));
        Assert.Equal("-1", (new ExactInteger(-3) % new ExactInteger(2)).ToString(CultureInfo.InvariantCulture));

        Assert.Equal("1/2", new BigRational(1, 2).ToString());
        Assert.Equal("3/2", (BigRational.One + new BigRational(1, 2)).ToString());
        Assert.Equal("1/6", (new BigRational(1, 2) / new BigRational(3)).ToString());
        Assert.Equal("5/6", (new BigRational(1, 2) + new BigRational(1, 3)).ToString());
    }

    [Fact]
    public void FactorialSeriesTermsRemainExactAndOrdered()
    {
        BigRational term = BigRational.One;
        BigRational partial = BigRational.One;
        for (int index = 1; index <= 32; index++)
        {
            BigRational previous = partial;
            term /= new BigRational(index);
            partial += term;
            Assert.True(term > BigRational.Zero, $"term {index}: {term}");
            BigRational reparsed = BigRational.Parse(partial.ToString());
            Assert.Equal(reparsed, partial);
            Assert.True(partial > previous, $"partial {index}: {partial} <= {previous}");
        }

        BigRational upper = partial + term / new BigRational(32);
        Assert.True(partial < upper, $"{partial} >= {upper}");
        Assert.True(partial > new BigRational(2));
        Assert.True(partial < new BigRational(3));
    }

    [Fact]
    public void MultiLimbIntegerComparisonUsesMostSignificantDigits()
    {
        ExactInteger left = ExactInteger.Parse("297104706949") * ExactInteger.Parse("3113510400");
        ExactInteger right = ExactInteger.Parse("8463398743") * ExactInteger.Parse("87178291200");
        Assert.True(left > right, $"{left} <= {right}; bits {left.GetBitLength()} / {right.GetBitLength()}");
    }

    [Fact]
    public void PerfectSquareDetectionTerminatesForExactAndIrrationalInputs()
    {
        Assert.True(BigRational.TrySquareRoot(new BigRational(72, 2), out BigRational exact));
        Assert.Equal(new BigRational(6), exact);
        Assert.False(BigRational.TrySquareRoot(new BigRational(2), out _));
    }

    [Theory]
    [InlineData("1.25", "5/4")]
    [InlineData("-0.00125", "-1/800")]
    [InlineData("12.5e3", "12500")]
    [InlineData("12.5e-3", "1/80")]
    [InlineData("000.000", "0")]
    public void DecimalParsingProducesExactCanonicalRationals(string source, string canonical)
    {
        BigRational value = BigRational.Parse(source);

        Assert.Equal(canonical, value.ToString());
        Assert.Equal(value, BigRational.Parse(value.ToString()));
    }

    [Theory]
    [InlineData(2_147_483_648L)]
    [InlineData(-2_147_483_648L)]
    [InlineData(4_611_686_018_427_387_904L)]
    [InlineData(-4_611_686_018_427_387_904L)]
    public void RadixAlignedLongValuesAreCanonical(long value)
    {
        var integer = new ExactInteger(value);
        ExactInteger reparsed = ExactInteger.Parse(integer.ToString(CultureInfo.InvariantCulture));

        Assert.Equal(integer, reparsed);
        Assert.Equal(integer.GetHashCode(), reparsed.GetHashCode());
    }

    [Fact]
    public void DivRemNormalizesRatPakRadixScaledIntegerRemainder()
    {
        ExactInteger dividend = ExactInteger.Parse(
            "-5345607925149942546238802396676677958848506309306679288280132015608310144291742420918263072787673535323237893403662621192763075320609230551252338479587761241583948670290997906823990798386949");
        ExactInteger divisor = ExactInteger.Parse("-16246275683267000094802763415");

        ExactInteger quotient = ExactInteger.DivRem(dividend, divisor, out ExactInteger remainder);

        Assert.Equal(dividend, quotient * divisor + remainder);
        Assert.True(ExactInteger.Abs(remainder) < ExactInteger.Abs(divisor));
        Assert.Equal(dividend.Sign, remainder.Sign);
    }

    [Theory]
    [InlineData(17, 5, 3, 2)]
    [InlineData(17, -5, -3, 2)]
    [InlineData(-17, 5, -3, -2)]
    [InlineData(-17, -5, 3, -2)]
    public void DivRemTruncatesTowardZeroAndKeepsDividendRemainderSign(
        long dividend,
        long divisor,
        long expectedQuotient,
        long expectedRemainder)
    {
        ExactInteger quotient = ExactInteger.DivRem(
            new ExactInteger(dividend),
            new ExactInteger(divisor),
            out ExactInteger remainder);

        Assert.Equal(new ExactInteger(expectedQuotient), quotient);
        Assert.Equal(new ExactInteger(expectedRemainder), remainder);
    }

    [Fact]
    public void ZeroShiftsDoNotConstructUnboundedIntermediatePowers()
    {
        Assert.Equal(ExactInteger.Zero, ExactInteger.Zero << int.MaxValue);
        Assert.Equal(ExactInteger.Zero, ExactInteger.Zero << int.MinValue);
        Assert.Equal(ExactInteger.Zero, ExactInteger.Zero >> int.MaxValue);
        Assert.Equal(ExactInteger.Zero, ExactInteger.Zero >> int.MinValue);
    }

    [Theory]
    [InlineData(
        "123456789012345678901234567890123456789",
        "98765432109876543210987654321098765431",
        "222222221122222222112222222211222222220",
        "24691356902469135690246913569024691358",
        "12193263113702179522618503273386678859312909618620042676540879439121975461059",
        "1",
        "24691356902469135690246913569024691358",
        "1")]
    [InlineData(
        "-3141592653589793238462643383279502884197",
        "271828182845904523536028747135266249775",
        "-2869764470743888714926614636144236634422",
        "-3413420836435697761998672130414769133972",
        "-853973422267356706546355086954657449501166064419434261719440306930624302305675",
        "-11",
        "-151482642284843479566327164791574136672",
        "1")]
    [InlineData(
        "9999999999999999999999999999999999999999",
        "-123456789012345678901234567890123456789",
        "9876543210987654321098765432109876543210",
        "10123456789012345678901234567890123456788",
        "-1234567890123456789012345678901234567889876543210987654321098765432109876543211",
        "-81",
        "90000000009000000000900000000090",
        "9000000000900000000090000000009")]
    [InlineData(
        "-12193263113702179522618503273362292333223746380111126352690",
        "-987654321098765432109876543210",
        "-12193263113702179522618503274349946654322511812221002895900",
        "-12193263113702179522618503272374638012124980948001249809480",
        "12042729002542144812886096473765479139748815014187112021837470763452373471373112484734900",
        "12345678901234567890123456789",
        "0",
        "987654321098765432109876543210")]
    [InlineData(
        "29710560942849126597578981376",
        "23058430092136939520",
        "29710560965907556689715920896",
        "29710560919790696505442041856",
        "685078892498860742907977265335757665463718379520",
        "1288490188",
        "18446744073709551616",
        "4611686018427387904")]
    [InlineData(
        "-170141183460469231731687303715760648939",
        "39614081257132168797759629489",
        "-170141183420855150474555134918001019450",
        "-170141183500083312988819472513520278428",
        "-6739986666787659948834794446774803210545446762862598019382840962171",
        "-4294967295",
        "-39614081252890225789188086684",
        "1")]
    public void MultiLimbArithmeticMatchesFixedExactOracleVectors(
        string leftText,
        string rightText,
        string sumText,
        string differenceText,
        string productText,
        string quotientText,
        string remainderText,
        string gcdText)
    {
        ExactInteger left = ExactInteger.Parse(leftText);
        ExactInteger right = ExactInteger.Parse(rightText);

        Assert.Equal(sumText, (left + right).ToString(CultureInfo.InvariantCulture));
        Assert.Equal(differenceText, (left - right).ToString(CultureInfo.InvariantCulture));
        Assert.Equal(productText, (left * right).ToString(CultureInfo.InvariantCulture));
        Assert.Equal(quotientText, ExactInteger.DivRem(left, right, out ExactInteger remainder).ToString(CultureInfo.InvariantCulture));
        Assert.Equal(remainderText, remainder.ToString(CultureInfo.InvariantCulture));
        ExactInteger gcd = ExactInteger.GreatestCommonDivisor(left, right);
        ExactInteger expectedGcd = ExactInteger.Parse(gcdText);
        Assert.Equal(expectedGcd, gcd);
        Assert.Equal(expectedGcd.GetHashCode(), gcd.GetHashCode());
        Assert.Equal(expectedGcd.IsOne, gcd.IsOne);
        Assert.Equal(left, (left / right) * right + left % right);
    }

    [Fact]
    public void UInt64GcdFastPathReturnsCanonicalSmallValues()
    {
        ExactInteger gcd = ExactInteger.GreatestCommonDivisor(6, 4);

        Assert.Equal(new ExactInteger(2), gcd);
        Assert.Equal(new ExactInteger(2).GetHashCode(), gcd.GetHashCode());
    }

    [Fact]
    public void LargeIntegerAndRationalLiteralPathsShareTheSameParseBudget()
    {
        string digits = new('9', 4_927);

        BigRational integerForm = BigRational.Parse(digits);
        BigRational rationalForm = BigRational.Parse(string.Concat(digits, "/1"));

        Assert.Equal(rationalForm, integerForm);
        Assert.Equal(digits, integerForm.ToString());
    }
}
