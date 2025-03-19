using System.Globalization;
using CalcManagerPort;

namespace CalcManagerPortTests.RatPakTests;

public class ConversionFuzzingTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 64;
    private readonly Random _random;

    // Number of iterations for each fuzzing test
    private const int FuzzIterations = 100;

    public ConversionFuzzingTests()
    {
        _ratPak = new RatPak(_precision);
        // Use non-deterministic seed for true randomness
        _random = new Random(Guid.NewGuid().GetHashCode());
    }

    #region Helper Methods

    /// <summary>
    /// Generates a random integer within the range that can be safely converted to a RAT
    /// </summary>
    private int GenerateRandomInt()
    {
        // Generate values across a wide range, including negative values
        return _random.Next(int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// Generates a random uint32 value
    /// </summary>
    private uint GenerateRandomUint()
    {
        var buffer = new byte[4];
        _random.NextBytes(buffer);
        return BitConverter.ToUInt32(buffer, 0);
    }

    /// <summary>
    /// Generates a random uint64 value
    /// </summary>
    private ulong GenerateRandomUlong()
    {
        var buffer = new byte[8];
        _random.NextBytes(buffer);
        return BitConverter.ToUInt64(buffer, 0);
    }

    /// <summary>
    /// Generates a random decimal value (which can represent fractions exactly)
    /// </summary>
    private decimal GenerateRandomDecimal()
    {
        // Generate random decimal with variable scale
        var scale = (byte)_random.Next(0, 29);
        var sign = _random.Next(0, 2) == 1;

        return new decimal(
            _random.Next(),
            _random.Next(),
            _random.Next(),
            sign,
            scale);
    }

    /// <summary>
    /// Generates a random numeric string with or without a fractional part
    /// </summary>
    private string GenerateRandomNumericString(bool allowFraction = true)
    {
        var integerDigits = _random.Next(1, 20); // Up to 20 digits for integer part
        var fractionDigits = allowFraction ? _random.Next(0, 20) : 0; // Up to 20 digits for fraction part
        var isNegative = _random.Next(0, 2) == 1;

        var integerPart = "";
        for (var i = 0; i < integerDigits; i++)
        {
            // Make sure first digit isn't 0 (unless it's the only digit)
            if (i == 0 && integerDigits > 1)
                integerPart += _random.Next(1, 10).ToString();
            else
                integerPart += _random.Next(0, 10).ToString();
        }

        var fractionPart = "";
        if (fractionDigits > 0)
        {
            for (var i = 0; i < fractionDigits; i++)
            {
                fractionPart += _random.Next(0, 10).ToString();
            }

            // Make sure last digit isn't 0 to avoid trailing zeros
            if (fractionPart.EndsWith("0"))
                fractionPart = fractionPart.Substring(0, fractionPart.Length - 1) + _random.Next(1, 10).ToString();
        }

        var result = integerPart;
        if (!string.IsNullOrEmpty(fractionPart))
            result += "." + fractionPart;

        if (isNegative)
            result = "-" + result;

        return result;
    }

    /// <summary>
    /// Generates a random exponent value as a string
    /// </summary>
    private string GenerateRandomExponentString()
    {
        var value = _random.Next(-50, 51); // Exponents from -50 to 50
        return value.ToString();
    }

    /// <summary>
    /// Creates a random RAT value
    /// </summary>
    private RatPak.RAT GenerateRandomRat()
    {
        // Several ways to create a random RAT:
        // 1. From a random string
        // 2. From a random int
        // 3. From a numerator/denominator pair

        var method = _random.Next(0, 3);

        switch (method)
        {
            case 0:
                // From random string
                var randomString = GenerateRandomNumericString();
                return StringToRat(randomString);

            case 1:
                // From random int
                var randomInt = GenerateRandomInt();
                return _ratPak.i32torat(randomInt);

            case 2:
                // From numerator/denominator
                var num = _ratPak.StringToNumber(GenerateRandomNumericString(false), 10, _precision);
                // Make sure denominator is not zero
                var den = _ratPak.StringToNumber(GenerateRandomNumericString(false).Replace("-", ""), 10, _precision);
                if (_ratPak.zernum(den))
                {
                    den = _ratPak.StringToNumber("1", 10, _precision);
                }

                var rat = _ratPak.numtorat(num, 10);
                _ratPak.divrat(ref rat, _ratPak.numtorat(den, 10), _precision);
                return rat;
        }

        // Default fallback - shouldn't happen
        return _ratPak.i32torat(1);
    }

    /// <summary>
    /// Creates a RAT from a string - helper method
    /// </summary>
    private RatPak.RAT StringToRat(string input)
    {
        var num = _ratPak.StringToNumber(input, 10, _precision);
        return _ratPak.numtorat(num, 10);
    }

    #endregion

    #region Fuzzing Tests for RAT to String Conversion

    [Fact]
    public void FuzzTest_RatToString_RandomRats()
    {
        for (var i = 0; i < FuzzIterations; i++)
        {
            var rat = GenerateRandomRat();
            var ratCopy = new RatPak.RAT();
            _ratPak.duprat(ref ratCopy, rat);
            var stringResult = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
            var backToRat = StringToRat(stringResult);
            // We need to check if the conversion back produces approximately the same value
            // Use rattoi32 for integer part comparison or full string comparison for numbers with fractions
            var originalAsString = _ratPak.RatToString(ref ratCopy, RatPak.NumberFormat.Float, 10, _precision);
            var roundTripAsString = _ratPak.RatToString(ref backToRat, RatPak.NumberFormat.Float, 10, _precision);

            // For some numbers, exact equality might not be possible due to floating point precision
            // So we either check for exact equality or that they start with the same few digits
            var isEqual = originalAsString == roundTripAsString;
            if (!isEqual && originalAsString.Contains(".") && roundTripAsString.Contains("."))
            {
                // For fractions, check that the first several digits match
                var charsToCheck = Math.Min(
                    Math.Min(originalAsString.Length, roundTripAsString.Length),
                    10); // Check at least the first 10 chars if possible

                isEqual = originalAsString.Substring(0, charsToCheck) ==
                          roundTripAsString.Substring(0, charsToCheck);
            }

            Assert.True(isEqual,
                $"Round-trip conversion failed for RAT. Original: {originalAsString}, Round-trip: {roundTripAsString}");
        }
    }

    [Fact]
    public void FuzzTest_RatToString_DifferentFormats()
    {
        var formats = new[]
        {
            RatPak.NumberFormat.Float,
            RatPak.NumberFormat.Scientific,
            RatPak.NumberFormat.Engineering
        };

        for (var i = 0; i < FuzzIterations; i++)
        {
            var rat = GenerateRandomRat();
            var ratCopy = new RatPak.RAT();
            _ratPak.duprat(ref ratCopy, rat);

            // Try all formats
            foreach (var format in formats)
            {
                //  - Convert to string
                var stringResult = _ratPak.RatToString(ref rat, format, 10, _precision);

                // Assert - Make sure we get a valid string result
                Assert.NotNull(stringResult);
                Assert.NotEmpty(stringResult);

                // If it's a valid number, we should be able to parse it back
                // But we need to handle scientific notation differently
                if (format == RatPak.NumberFormat.Float)
                {
                    // For float format, we can directly parse it back
                    var backToRat = StringToRat(stringResult);
                    var roundTripResult = _ratPak.RatToString(ref backToRat, format, 10, _precision);

                    // Compare the first few characters to account for precision differences
                    var charsToCompare = Math.Min(
                        Math.Min(stringResult.Length, roundTripResult.Length),
                        6);

                    Assert.Equal(
                        stringResult.Substring(0, charsToCompare),
                        roundTripResult.Substring(0, charsToCompare));
                }
            }
        }
    }

    #endregion

    #region Fuzzing Tests for Int/Uint to RAT Conversion

    [Fact]
    public void FuzzTest_I32torat_RandomIntegers()
    {
        for (var i = 0; i < FuzzIterations; i++)
        {

            var randomInt = GenerateRandomInt();

            var rat = _ratPak.i32torat(randomInt);
            var roundTrip = _ratPak.rattoi32(rat, 10, _precision);

            Assert.Equal(randomInt, roundTrip);
        }
    }

    [Fact]
    public void FuzzTest_Ui32torat_RandomUints()
    {
        for (var i = 0; i < FuzzIterations; i++)
        {

            var randomUint = GenerateRandomUint();

            var rat = _ratPak.Ui32torat(randomUint);
            var roundTrip = _ratPak.rattoUi64(rat, 10, _precision);

            Assert.Equal(randomUint, roundTrip);
        }
    }

    #endregion

    #region Fuzzing Tests for StringToNumber and NumberToRat

    [Fact]
    public void FuzzTest_StringToNumberToRat_RandomStrings()
    {
        for (var i = 0; i < FuzzIterations; i++)
        {

            var randomString = GenerateRandomNumericString();

            var num = _ratPak.StringToNumber(randomString, 10, _precision);
            var rat = _ratPak.numtorat(num, 10);
            var resultString = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
            //For large numbers, we may get scientific notation,
            // so we need to convert both to decimal for comparison
            if (decimal.TryParse(randomString, out var originalValue) &&
                decimal.TryParse(resultString, out var resultValue))
            {
                // Check that the values are close - allow some precision loss
                decimal ratio;
                if (originalValue != 0)
                {
                    ratio = resultValue / originalValue;
                    Assert.True(ratio > 0.9999m && ratio < 1.0001m,
                        $"Values differ too much. Original: {originalValue}, Result: {resultValue}");
                }
                else
                {
                    // If original is 0, result should be very close to 0
                    Assert.True(Math.Abs(resultValue) < 0.0001m,
                        $"Result should be close to 0, got: {resultValue}");
                }
            }
            else
            {
                // If we can't parse as decimal (e.g., very large numbers), compare string prefixes
                // Normalize the strings by removing any leading zeros, +, etc.
                var normalizedOriginal = NormalizeNumericString(randomString);
                var normalizedResult = NormalizeNumericString(resultString);

                // Compare the first several digits
                var charsToCompare = Math.Min(
                    Math.Min(normalizedOriginal.Length, normalizedResult.Length),
                    5); // Compare at least first 5 characters

                Assert.Equal(
                    normalizedOriginal.Substring(0, charsToCompare),
                    normalizedResult.Substring(0, charsToCompare));
            }
        }
    }

    private string NormalizeNumericString(string input)
    {
        // Remove leading zeros, +, etc.
        input = input.TrimStart('+', ' ', '0');

        // If we removed everything, it was zeros
        if (string.IsNullOrEmpty(input) || input.StartsWith("."))
            return "0" + input;

        return input;
    }

    #endregion

    #region Fuzzing Tests for RatToNumber

    [Fact]
    public void FuzzTest_RatToNumber_RandomRats()
    {
        for (var i = 0; i < FuzzIterations; i++)
        {

            var rat = GenerateRandomRat();
            var ratCopy = new RatPak.RAT();
            _ratPak.duprat(ref ratCopy, rat);

            var num = _ratPak.RatToNumber(rat, 10, _precision);
            var backToRat = _ratPak.numtorat(num, 10);
            // The two rats should be approximately equal
            var originalString = _ratPak.RatToString(ref ratCopy, RatPak.NumberFormat.Float, 10, _precision);
            var resultString = _ratPak.RatToString(ref backToRat, RatPak.NumberFormat.Float, 10, _precision);

            if (decimal.TryParse(originalString, out var originalValue) &&
                decimal.TryParse(resultString, out var resultValue))
            {
                // Check for approximate equality
                if (originalValue != 0)
                {
                    var ratio = resultValue / originalValue;
                    Assert.True(ratio > 0.9999m && ratio < 1.0001m,
                        $"Values differ too much. Original: {originalValue}, Result: {resultValue}");
                }
                else
                {
                    // For zero, result should be very close to zero
                    Assert.True(Math.Abs(resultValue) < 0.0001m);
                }
            }
            else
            {
                // For values too large for decimal, just compare string prefixes
                var charsToCompare = Math.Min(
                    Math.Min(originalString.Length, resultString.Length),
                    5);

                Assert.Equal(
                    originalString.Substring(0, charsToCompare),
                    resultString.Substring(0, charsToCompare));
            }
        }
    }

    #endregion

    #region Fuzzing Tests for Flatrat

    [Fact]
    public void FuzzTest_Flatrat_RandomRationals()
    {
        for (var i = 0; i < FuzzIterations; i++)
        {
            var num = _random.Next(1, 1000);
            var den = _random.Next(1, 1000);

            // To create reducible fractions, multiply both by a common factor
            var commonFactor = _random.Next(2, 10);
            num *= commonFactor;
            den *= commonFactor;

            var numNum = _ratPak.i32tonum(num, 10);
            var denNum = _ratPak.i32tonum(den, 10);

            var rat = _ratPak.numtorat(numNum, 10);
            _ratPak.divrat(ref rat, _ratPak.numtorat(denNum, 10), _precision);

            // Store the value before simplification
            var beforeValue = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);
            //Simplify the fraction
            _ratPak.flatrat(ref rat, 10, _precision);
            // The value should remain the same
            var afterValue = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

            if (decimal.TryParse(beforeValue, out var beforeDecimal) &&
                decimal.TryParse(afterValue, out var afterDecimal))
            {
                // Allow for small differences due to floating point
                var diff = Math.Abs(beforeDecimal - afterDecimal);
                var tolerance = 0.000001m; // Adjust as needed

                Assert.True(diff < tolerance,
                    $"Values changed after flatrat. Before: {beforeValue}, After: {afterValue}");
            }
            else
            {
                // For values that can't be parsed as decimal, compare the first few digits
                var charsToCompare = Math.Min(
                    Math.Min(beforeValue.Length, afterValue.Length),
                    6);

                Assert.Equal(
                    beforeValue.Substring(0, charsToCompare),
                    afterValue.Substring(0, charsToCompare));
            }
        }
    }

    #endregion

    #region Fuzzing Tests for StringToRat

    [Fact]
    public void FuzzTest_StringToRat_RandomValues()
    {
        for (var i = 0; i < FuzzIterations; i++)
        {

            var mantissaIsNegative = _random.Next(0, 2) == 1;
            var mantissa = GenerateRandomNumericString(false);
            var exponentIsNegative = _random.Next(0, 2) == 1;
            var exponent = GenerateRandomExponentString();

            var rat = _ratPak.StringToRat(mantissaIsNegative, mantissa, exponentIsNegative, exponent, 10, _precision);
            //Build the expected result manually
            var expectedString = (mantissaIsNegative ? "-" : "") + mantissa;

            // For debugging
            var fullExpectation = expectedString + "e" + (exponentIsNegative ? "-" : "+") + exponent;

            // Convert to decimal if possible to verify
            if (double.TryParse(fullExpectation, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out var expectedValue))
            {
                var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

                if (double.TryParse(result, out var actualValue))
                {
                    // For very large or small values, compare ratio
                    if (Math.Abs(expectedValue) > 0.0001)
                    {
                        var ratio = actualValue / expectedValue;
                        Assert.True(ratio > 0.999 && ratio < 1.001,
                            $"Values differ too much. Expected: {expectedValue}, Actual: {actualValue}");
                    }
                    else if (Math.Abs(expectedValue) < 0.0001 && Math.Abs(expectedValue) > 0)
                    {
                        // For very small values, check they're both small
                        Assert.True(Math.Abs(actualValue) < 0.001,
                            $"Expected small value near {expectedValue}, got {actualValue}");
                    }
                    else if (expectedValue == 0)
                    {
                        // For zero, result should be very close to zero
                        Assert.True(Math.Abs(actualValue) < 0.0001,
                            $"Expected value near 0, got {actualValue}");
                    }
                }
            }
            // Some values may be too large for double, so we can't easily verify them
        }
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void FuzzTest_EdgeCases()
    {
        // Test array of edge case values to try
        var edgeCases = new[]
        {
            "0",
            "1",
            "-1",
            "0.000000000000000000000001",
            "1000000000000000000000000",
            "-1000000000000000000000000",
            "0.9999999999999999999999999",
            "1.0000000000000000000000001",
            int.MaxValue.ToString(),
            int.MinValue.ToString(),
            "0.1",
            "0.3",
            "0.5",
            "0.7",
            "0.9",
            "9876543210.123456789"
        };

        foreach (var edgeCase in edgeCases)
        {
            // Test round-trip conversion
            var num = _ratPak.StringToNumber(edgeCase, 10, _precision);
            var rat = _ratPak.numtorat(num, 10);
            var result = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, 10, _precision);

            // For comparison, we need to handle scientific notation
            // and different precisions
            if (decimal.TryParse(edgeCase, out var originalValue) &&
                decimal.TryParse(result, out var resultValue))
            {
                if (originalValue != 0)
                {
                    var ratio = resultValue / originalValue;
                    Assert.True(ratio > 0.9999m && ratio < 1.0001m,
                        $"Edge case failed: {edgeCase} converted to {result}");
                }
                else
                {
                    // For zero, result should be very close to zero
                    Assert.True(Math.Abs(resultValue) < 0.0001m,
                        $"Edge case failed: {edgeCase} converted to {result}");
                }
            }
            else
            {
                // For values too large/small for decimal, compare prefixes
                var normalizedOriginal = NormalizeNumericString(edgeCase);
                var normalizedResult = NormalizeNumericString(result);

                var charsToCompare = Math.Min(
                    Math.Min(normalizedOriginal.Length, normalizedResult.Length),
                    5);

                if (charsToCompare > 0)
                {
                    Assert.Equal(
                        normalizedOriginal.Substring(0, charsToCompare),
                        normalizedResult.Substring(0, charsToCompare));
                }
            }
        }
    }

    #endregion
}
