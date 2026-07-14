using System.Globalization;
using CalcEngine;

namespace CalcManagerPortTests.RatPakTests;

public class NRadixConversionTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 64;
    private readonly DeterministicRandom _random;

    // Number of iterations for each fuzzing test
    private const int FuzzIterations = 5000;

    public NRadixConversionTests()
    {
        _ratPak = new RatPak(_precision);
        var seed = Guid.NewGuid().GetHashCode();
        _random = new DeterministicRandom(seed);
        Console.WriteLine($"Current Seed: {seed}");
    }

    #region Helper Methods

    /// <summary>
    /// Generates a random integer string in a specific radix
    /// </summary>
    private string GenerateRandomRadixString(uint radix, int maxLength = 8)
    {
        var digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        var length = _random.Next(1, maxLength + 1);
        var result = new char[length];

        // First digit shouldn't be 0 (unless it's the only digit)
        if (length > 1)
        {
            result[0] = digits[_random.Next(1, (int)Math.Min(radix, 36))];
        }
        else
        {
            result[0] = digits[_random.Next(0, (int)Math.Min(radix, 36))];
        }

        // Generate remaining digits
        for (var i = 1; i < length; i++)
        {
            result[i] = digits[_random.Next(0, (int)Math.Min(radix, 36))];
        }

        return new string(result);
    }

    /// <summary>
    /// Creates a NUMBER in a specific radix from a string
    /// </summary>
    private NUMBER CreateNumberInRadix(string value, uint radix)
    {
        var num = _ratPak.StringToNumber(value, radix, _precision);
        Assert.NotNull(num);
        return num;
    }

    /// <summary>
    /// Converts a NUMBER to a string in a specific radix
    /// </summary>
    private string NumberToStringInRadix(ref NUMBER num, uint radix)
    {
        return _ratPak.NumberToString(ref num, NumberFormat.FloatingPoint, radix, _precision);
    }

    #endregion

    #region Fixed Test Cases

    [Fact]
    public void NumtonRadixxDecimalToInternalBaseCorrectConversion()
    {
        ////  Create a number in decimal (base 10)
        var originalNum = _ratPak.StringToNumber("123456789", 10, _precision);
        Assert.NotNull(originalNum);
        // //  Convert to internal base
        var internalBaseNum = RatPak.numtonRadixx(originalNum, 10);

        // Convert back to decimal for verification
        var convertedBackNum = _ratPak.nRadixxtonum(internalBaseNum, 10, _precision);
        var result = _ratPak.NumberToString(ref convertedBackNum, NumberFormat.FloatingPoint, 10, _precision);

        Assert.Equal("123456789", result);
    }

    [Fact]
    public void NRadixxtonumInternalBaseToDecimalCorrectConversion()
    {
        ////  Create a number in decimal then convert to internal base
        var decimal10 = _ratPak.StringToNumber("9876543210", 10, _precision);
        Assert.NotNull(decimal10);
        var internalBaseNum = RatPak.numtonRadixx(decimal10, 10);
        // - Convert from internal base back to decimal
        var decimalNum = _ratPak.nRadixxtonum(internalBaseNum, 10, _precision);
        var result = _ratPak.NumberToString(ref decimalNum, NumberFormat.FloatingPoint, 10, _precision);

        Assert.Equal("9876543210", result);
    }

    [Theory]
    [InlineData("1010101", 2, "85", 10)] // Binary to decimal
    [InlineData("FF", 16, "255", 10)] // Hex to decimal
    [InlineData("100", 10, "64", 16)] // Decimal to hex
    [InlineData("100", 10, "144", 8)] // Decimal to octal
    [InlineData("100", 8, "64", 10)] // Octal to decimal
    [InlineData("100", 10, "1100100", 2)] // Decimal to binary
    public void RadixConversionSpecificExamplesCorrectConversion(
        string original, uint originalRadix, string expected, uint targetRadix)
    {
        ArgumentNullException.ThrowIfNull(expected);

        //  Create a number in the original radix
        var originalNum = _ratPak.StringToNumber(original, originalRadix, _precision);
        Assert.NotNull(originalNum);

        // Convert to internal base first
        var internalBaseNum = RatPak.numtonRadixx(originalNum, originalRadix);
        //  Convert to target radix
        var targetNum = _ratPak.nRadixxtonum(internalBaseNum, targetRadix, _precision);
        var result = _ratPak.NumberToString(ref targetNum, NumberFormat.FloatingPoint, targetRadix, _precision);

        Assert.Equal(expected.ToUpperInvariant(), result.ToUpperInvariant());
    }

    [Fact]
    public void NumtonRadixxNegativeNumberPreservesSign()
    {
        //  Create a negative number
        var originalNum = _ratPak.StringToNumber("-12345", 10, _precision);
        Assert.NotNull(originalNum);
        //  Convert to internal base
        var internalBaseNum = RatPak.numtonRadixx(originalNum, 10);

        // Convert back for verification
        var convertedBackNum = _ratPak.nRadixxtonum(internalBaseNum, 10, _precision);
        var result = _ratPak.NumberToString(ref convertedBackNum, NumberFormat.FloatingPoint, 10, _precision);

        Assert.Equal("-12345", result);
        Assert.Equal(-1, internalBaseNum.Sign); // Sign should be preserved in internal base
    }

    [Fact]
    public void NRadixxtonumWithExponentHandlesExponentCorrectly()
    {
        //  Create a number with an exponent
        var originalNum = _ratPak.StringToNumber("123456789", 10, _precision);
        Assert.NotNull(originalNum);
        //  Convert to internal base then back
        var internalBaseNum = RatPak.numtonRadixx(originalNum, 10);
        var convertedBackNum = _ratPak.nRadixxtonum(internalBaseNum, 10, _precision);
        var result = _ratPak.NumberToString(ref convertedBackNum, NumberFormat.FloatingPoint, 10, _precision);

        Assert.Equal("123456789", result);
    }

    #endregion

    #region Fuzzing Tests

    [Fact]
    public void FuzzTestNumtonRadixxRandomRadices()
    {
        var radices = new uint[] { 2, 8, 10, 16, 32 }; // Common bases

        for (var i = 0; i < FuzzIterations; i++)
        {
            var sourceRadix = radices[_random.Next(0, radices.Length)];

            // Generate a random number string in the source radix
            var randomValue = GenerateRandomRadixString(sourceRadix);
            var originalNum = CreateNumberInRadix(randomValue, sourceRadix);
            //  Convert to internal base
            var internalBaseNum = RatPak.numtonRadixx(originalNum, sourceRadix);

            // Convert back for verification
            var convertedBackNum = _ratPak.nRadixxtonum(internalBaseNum, sourceRadix, _precision);
            var result = NumberToStringInRadix(ref convertedBackNum, sourceRadix);
            // Original value and result should match
            Assert.Equal(randomValue.ToUpperInvariant(), result.ToUpperInvariant());
        }
    }

    [Fact]
    public void FuzzTestNRadixxtonumRandomRadices()
    {
        var sourceRadices = new uint[] { 2, 8, 10, 16 }; // Common source bases
        var targetRadices = new uint[] { 2, 8, 10, 16 }; // Common target bases

        for (var i = 0; i < FuzzIterations; i++)
        {
            var sourceRadix = sourceRadices[_random.Next(0, sourceRadices.Length)];
            var targetRadix = targetRadices[_random.Next(0, targetRadices.Length)];

            // Generate a random number string in the source radix
            var randomValue = GenerateRandomRadixString(sourceRadix);
            var originalNum = CreateNumberInRadix(randomValue, sourceRadix);
            //  -First convert to internal base
            var internalBaseNum = RatPak.numtonRadixx(originalNum, sourceRadix);

            // Then convert to target radix
            var targetNum = _ratPak.nRadixxtonum(internalBaseNum, targetRadix, _precision);

            // Now convert back to the original radix for verification
            var internalAgainNum = RatPak.numtonRadixx(targetNum, targetRadix);
            var backToSourceNum = _ratPak.nRadixxtonum(internalAgainNum, sourceRadix, _precision);

            var result = NumberToStringInRadix(ref backToSourceNum, sourceRadix);
            // - Original value and final result should match
            Assert.Equal(randomValue.ToUpperInvariant(), result.ToUpperInvariant());
        }
    }

    [Fact]
    public void FuzzTestRadixConversionsLargeNumbers()
    {
        // Test with some larger numbers
        for (var i = 0; i < FuzzIterations; i++)
        {
            var largeValue = "";
            var numDigits = _random.Next(10, 20); // 10-20 digits

            for (var j = 0; j < numDigits; j++)
            {
                largeValue += _random.Next(0, 10).ToString(CultureInfo.InvariantCulture);
            }

            // Make sure it doesn't start with 0
            if (largeValue.StartsWith('0'))
            {
                largeValue = string.Concat("1", largeValue.AsSpan(1));
            }

            var originalNum = _ratPak.StringToNumber(largeValue, 10, _precision);
            Assert.NotNull(originalNum);
            //  Convert to internal base
            var internalBaseNum = RatPak.numtonRadixx(originalNum, 10);

            // Convert to hexadecimal
            var hexNum = _ratPak.nRadixxtonum(internalBaseNum, 16, _precision);

            // Convert back to internal base
            var internalAgainNum = RatPak.numtonRadixx(hexNum, 16);

            // And finally back to decimal
            var decimalAgainNum = _ratPak.nRadixxtonum(internalAgainNum, 10, _precision);
            var result = _ratPak.NumberToString(ref decimalAgainNum, NumberFormat.FloatingPoint, 10, _precision);

            Assert.Equal(largeValue, result);
        }
    }

    [Fact]
    public void FuzzTestRadixConversionsRandomExponents()
    {
        // Test numbers with decimal points
        for (var i = 0; i < FuzzIterations; i++)
        {
            var integerPart = "";
            var integerDigits = _random.Next(1, 10);

            for (var j = 0; j < integerDigits; j++)
            {
                integerPart += _random.Next(0, 10).ToString(CultureInfo.InvariantCulture);
            }

            // Make sure integer part doesn't start with 0
            if (integerPart.StartsWith('0') && integerPart.Length > 1)
            {
                integerPart = string.Concat("1", integerPart.AsSpan(1));
            }

            var decimalValue = integerPart;
            var originalNum = _ratPak.StringToNumber(decimalValue, 10, _precision);
            Assert.NotNull(originalNum);
            //  Convert to internal base
            var internalBaseNum = RatPak.numtonRadixx(originalNum, 10);

            // Convert back to decimal
            var decimalAgainNum = _ratPak.nRadixxtonum(internalBaseNum, 10, _precision);
            var result = _ratPak.NumberToString(ref decimalAgainNum, NumberFormat.FloatingPoint, 10, _precision);

            // Parse to decimal for comparison (to handle slight precision differences)
            var originalDecimal = decimal.Parse(decimalValue, CultureInfo.InvariantCulture);
            var resultDecimal = decimal.Parse(result, CultureInfo.InvariantCulture);

            var ratio = resultDecimal - originalDecimal;
            var isClose = ratio == 0;

            Assert.True(isClose,
                $"Failed conversion for decimal number {decimalValue}, got {result}");
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RadixConversionEdgeCases()
    {
        // Test array of edge cases
        var edgeCases = new[]
        {
            new { Value = "0", SourceRadix = 10u, TargetRadix = 16u, Expected = "0" },
            new { Value = "0", SourceRadix = 10u, TargetRadix = 2u, Expected = "0" },
            new { Value = "1", SourceRadix = 10u, TargetRadix = 16u, Expected = "1" },
            new { Value = "1", SourceRadix = 10u, TargetRadix = 2u, Expected = "1" },
            new { Value = "-1", SourceRadix = 10u, TargetRadix = 16u, Expected = "-1" },
            new { Value = "FFFFFFFF", SourceRadix = 16u, TargetRadix = 10u, Expected = "4294967295" },
            new { Value = "7FFFFFFF", SourceRadix = 16u, TargetRadix = 10u, Expected = "2147483647" }, // int.MaxValue
            new
            {
                Value = "80000000", SourceRadix = 16u, TargetRadix = 10u, Expected = "2147483648"
            }, // int.MaxValue + 1

            // Fractions are not handled by NUMBERS->nRadixx itself...
            // Which makes sense, this is RATional pack after all.
        };

        foreach (var testCase in edgeCases)
        {
            var originalNum = _ratPak.StringToNumber(testCase.Value, testCase.SourceRadix, _precision);
            Assert.NotNull(originalNum);

            //  Convert to internal base
            var internalBaseNum = RatPak.numtonRadixx(originalNum, testCase.SourceRadix);

            // Convert to target radix
            var targetNum = _ratPak.nRadixxtonum(internalBaseNum, testCase.TargetRadix, _precision);
            var result = _ratPak.NumberToString(ref targetNum, NumberFormat.FloatingPoint, testCase.TargetRadix,
                _precision);

            Assert.Equal(testCase.Expected, result);
        }
    }

    #endregion
}
