using CalcEngine;
using System.Reflection;

namespace CalcManagerPortTests.RatPakTests;

public class RationalWrapperTests
{
    private const int Precision = 128;
    private readonly RatPak _ratPak = new(Precision);

    [Theory]
    [InlineData(2u, 30)]
    [InlineData(10u, 9)]
    [InlineData(16u, 7)]
    public void RatPakConstructorHonorsRequestedRadix(uint radix, int expectedRatio)
    {
        RatPak ratPak = new(Precision, radix);

        Assert.Equal(expectedRatio, ratPak.GRatio);
    }

    [Fact]
    public void ConstructorFromPratPreservesIndependentDigitCounts()
    {
        RAT raw = ParseRaw("0.00000000000001");

        Rational wrapped = new(_ratPak, raw);

        Assert.Equal(raw.Pp.Cdigit, wrapped.P.CDigits);
        Assert.Equal(raw.Pq.Cdigit, wrapped.Q.CDigits);
        Assert.Equal(FormatRaw(raw), wrapped.ToString(10, NumberFormat.FloatingPoint, Precision));
    }

    [Fact]
    public void ConstructorFromPratPreservesNumeratorAndDenominatorExponents()
    {
        RAT raw = RatPak.i32torat(3);
        raw.Pp.Exp = 2;
        raw.Pq.Exp = 1;
        string expected = FormatRaw(raw);

        Rational wrapped = new(_ratPak, raw);

        Assert.Equal(raw.Pp.Exp, wrapped.P.Exp);
        Assert.Equal(raw.Pq.Exp, wrapped.Q.Exp);
        Assert.Equal(expected, wrapped.ToString(10, NumberFormat.FloatingPoint, Precision));
    }

    [Fact]
    public void ConstructorFromEngineNumberCreatesAUnitDenominator()
    {
        EngineNumber number = new(1, 0, 3, new uint[] { 1, 2, 3 });

        Rational rational = new(_ratPak, number);

        Assert.Equal(1, rational.Q.CDigits);
        Assert.Equal(1u, rational.Q.Mantissa[0]);
    }

    [Fact]
    public void EngineNumberCopiesOnlyLogicalMantissaDigits()
    {
        NUMBER raw = new()
        {
            Sign = 1,
            Exp = 0,
            Cdigit = 1
        };
        uint[] storage = Assert.IsType<uint[]>(raw.Mant);
        storage[0] = 0;
        storage[1] = 123;

        EngineNumber number = new(raw);

        Assert.True(number.IsZero());
        Assert.Equal(number.CDigits, number.Mantissa.Count);
        Assert.Equal(0u, number.Mantissa[0]);
        Assert.DoesNotContain(123u, number.Mantissa);
    }

    [Fact]
    public void EngineNumberToPnumberRetainsNativeWorkStorage()
    {
        EngineNumber number = new(1, 0, 3, new uint[] { 1, 2, 3 });

        NUMBER raw = number.ToPNUMBER();

        Assert.Equal(number.CDigits + 10, raw.Mant.Count);
        Assert.Equal(new uint[] { 1, 2, 3 }, raw.Mant.Take(number.CDigits));
        Assert.All(raw.Mant.Skip(number.CDigits), digit => Assert.Equal(0u, digit));
    }

    [Fact]
    public void DupnumRetainsNativeWorkStorage()
    {
        NUMBER source = RatPak.i32tonum(42, 10);
        NUMBER? duplicate = null;

        RatPak.dupnum(ref duplicate, source);

        Assert.Equal(source.Cdigit + 9, duplicate.Mant.Count);
        Assert.Equal(source.Mant.Take(source.Cdigit), duplicate.Mant.Take(duplicate.Cdigit));
    }

    [Fact]
    public void IncrementCarryReplacesTheCallersNumber()
    {
        NUMBER number = new()
        {
            Sign = 1,
            Exp = 0,
            Cdigit = 1
        };
        uint[] storage = Assert.IsType<uint[]>(number.Mant);
        storage[0] = 0x7fffffff;
        MethodInfo? increment = typeof(RatPak).GetMethod(
            "INC",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(increment);
        object?[] arguments = [number];

        increment.Invoke(_ratPak, arguments);

        NUMBER result = Assert.IsType<NUMBER>(arguments[0]);
        Assert.NotSame(number, result);
        Assert.Equal(2, result.Cdigit);
        Assert.Equal(0u, result.Mant[0]);
        Assert.Equal(1u, result.Mant[1]);
    }

    [Theory]
    [InlineData(0ul)]
    [InlineData(1ul)]
    [InlineData(4294967296ul)]
    [InlineData(18446744073709551615ul)]
    public void UInt64ConstructorRoundTrips(ulong value)
    {
        Rational rational = new(_ratPak, value);

        Assert.Equal(value, rational.ToUInt64T());
    }

    [Theory]
    [InlineData("0.00000000000001")]
    [InlineData("123456789012345678901234567890")]
    [InlineData("0.0000000000000000001602176565")]
    [InlineData("-987654321.123456789")]
    public void PratRationalPratRoundTripPreservesValue(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        RAT raw = ParseRaw(input);
        string expected = FormatRaw(raw);

        Rational wrapped = new(_ratPak, raw);
        RAT roundTripped = wrapped.ToPRAT();

        Assert.Equal(expected, FormatRaw(roundTripped));
    }

    private RAT ParseRaw(string input)
    {
        int exponentSeparator = input.IndexOf('e', StringComparison.Ordinal);
        if (exponentSeparator < 0)
        {
            exponentSeparator = input.IndexOf('E', StringComparison.Ordinal);
        }
        string mantissa = exponentSeparator < 0 ? input : input.Substring(0, exponentSeparator);
        string exponent = exponentSeparator < 0 ? string.Empty : input.Substring(exponentSeparator + 1);
        bool mantissaIsNegative = mantissa.StartsWith('-');
        bool exponentIsNegative = exponent.StartsWith('-');
        mantissa = mantissa.TrimStart('+', '-');
        exponent = exponent.TrimStart('+', '-');
        return _ratPak.StringToRat(
            mantissaIsNegative,
            mantissa,
            exponentIsNegative,
            exponent,
            10,
            Precision)!;
    }

    private string FormatRaw(RAT value)
    {
        RAT copy = new();
        RatPak.duprat(ref copy, value);
        return _ratPak.RatToString(ref copy, NumberFormat.FloatingPoint, 10, Precision);
    }
}
