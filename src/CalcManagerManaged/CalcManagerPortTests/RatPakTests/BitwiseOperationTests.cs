using CalcEngine;

namespace CalcManagerPortTests.RatPakTests;

public class BitwiseOperationTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 64;
    private readonly DeterministicRandom _random;

    // Number of iterations for each fuzzing test
    private const int FuzzIterations = 5000;

    public BitwiseOperationTests()
    {
        _ratPak = new RatPak(_precision);
        var seed = Guid.NewGuid().GetHashCode();
        _random = new DeterministicRandom(seed);
        Console.WriteLine($"Current Seed: {seed}");
    }

    #region Helper Methods

    /// <summary>
    /// Converts an integer to a rational number
    /// </summary>
    private static RAT IntToRat(int value)
    {
        return RatPak.i32torat(value);
    }

    /// <summary>
    /// Converts a rational number to a string for comparison
    /// </summary>
    private string RatToString(ref RAT rat)
    {
        return _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, 10, _precision);
    }

    /// <summary>
    /// Generates a random integer between min and max
    /// </summary>
    private int GenerateRandomInt(int min = -1000, int max = 1000)
    {
        return _random.Next(min, max + 1);
    }

    #endregion

    #region Bitwise AND Tests

    [Fact]
    public void AndRatPositiveNumbersCorrectBitwiseAnd()
    {
        var a = IntToRat(12); // 1100 in binary
        var b = IntToRat(5); // 0101 in binary

        _ratPak.andrat(ref a, b, 10, _precision);

        Assert.Equal("4", RatToString(ref a)); // 0100 in binary
    }

    [Fact]
    public void AndRatNegativeNumbersCorrectBitwiseAnd()
    {
        var a = IntToRat(-12); // Negative number
        var b = IntToRat(-5); // Another negative number

        _ratPak.andrat(ref a, b, 10, _precision);

        Assert.NotEqual("0", RatToString(ref a));
    }

    [Fact]
    public void AndRatZeroAndNumberReturnsZero()
    {
        var a = IntToRat(0);
        var b = IntToRat(42);

        _ratPak.andrat(ref a, b, 10, _precision);

        Assert.Equal("0", RatToString(ref a));
    }

    #endregion

    #region Bitwise OR Tests

    [Fact]
    public void OrRatPositiveNumbersCorrectBitwiseOr()
    {
        var a = IntToRat(12); // 1100 in binary
        var b = IntToRat(5); // 0101 in binary

        _ratPak.orrat(ref a, b, 10, _precision);

        Assert.Equal("13", RatToString(ref a)); // 1101 in binary
    }

    [Fact]
    public void OrRatNegativeNumbersCorrectBitwiseOr()
    {
        var a = IntToRat(-12); // Negative number
        var b = IntToRat(-5); // Another negative number

        _ratPak.orrat(ref a, b, 10, _precision);

        Assert.NotEqual("0", RatToString(ref a));
    }

    [Fact]
    public void OrRatZeroAndNumberReturnsNumber()
    {
        var a = IntToRat(0);
        var b = IntToRat(42);

        _ratPak.orrat(ref a, b, 10, _precision);

        Assert.Equal("42", RatToString(ref a));
    }

    #endregion

    #region Bitwise XOR Tests

    [Fact]
    public void XorRatPositiveNumbersCorrectBitwiseXor()
    {
        var a = IntToRat(12); // 1100 in binary
        var b = IntToRat(5); // 0101 in binary

        _ratPak.xorrat(ref a, b, 10, _precision);

        Assert.Equal("9", RatToString(ref a)); // 1001 in binary
    }

    [Fact]
    public void XorRatSameNumbersReturnsZero()
    {
        var a = IntToRat(42);
        var b = IntToRat(42);

        _ratPak.xorrat(ref a, b, 10, _precision);

        Assert.Equal("0", RatToString(ref a));
    }

    [Fact]
    public void XorRatZeroAndNumberReturnsNumber()
    {
        var a = IntToRat(0);
        var b = IntToRat(42);

        _ratPak.xorrat(ref a, b, 10, _precision);

        Assert.Equal("42", RatToString(ref a));
    }

    #endregion

    #region Left Shift Tests

    [Fact]
    public void LshRatPositiveShiftCorrectLeftShift()
    {
        var a = IntToRat(5); // 101 in binary
        var b = IntToRat(2); // Shift by 2

        _ratPak.lshrat(ref a, b, 10, _precision);

        Assert.Equal("20", RatToString(ref a)); // 10100 in binary
    }

    [Fact]
    public void LshRatZeroShiftNoChange()
    {
        var a = IntToRat(42);
        var b = IntToRat(0);

        _ratPak.lshrat(ref a, b, 10, _precision);

        Assert.Equal("42", RatToString(ref a));
    }

    #endregion

    #region Right Shift Tests

    [Fact]
    public void RshRatPositiveShiftCorrectRightShift()
    {
        var a = IntToRat(20); // 10100 in binary
        var b = IntToRat(2); // Shift by 2

        _ratPak.rshrat(ref a, b, 10, _precision);

        Assert.Equal("5", RatToString(ref a)); // 101 in binary
    }

    [Fact]
    public void RshRatZeroShiftNoChange()
    {
        var a = IntToRat(42);
        var b = IntToRat(0);

        _ratPak.rshrat(ref a, b, 10, _precision);

        Assert.Equal("42", RatToString(ref a));
    }

    #endregion

    #region Fuzz Testing

    [Fact]
    public void FuzzBitwiseOperationsRandomInputsNoExceptions()
    {
        for (var i = 0; i < FuzzIterations; i++)
        {
            // Generate random integers
            var a = IntToRat(GenerateRandomInt());
            var b = IntToRat(GenerateRandomInt());

            // Store original values for reference
            var originalA = RatToString(ref a);
            var originalB = RatToString(ref b);

            try
            {
                // Perform all bitwise operations
                var aCopy1 = a;
                var aCopy2 = a;
                var aCopy3 = a;
                var aCopy4 = a;
                _ratPak.andrat(ref aCopy1, b, 10, _precision);
                _ratPak.orrat(ref aCopy2, b, 10, _precision);
                _ratPak.xorrat(ref aCopy3, b, 10, _precision);

                // Only perform shifts if shift value is within reasonable range
                var shiftB = IntToRat(Math.Abs(GenerateRandomInt(0, 32)));
                _ratPak.lshrat(ref aCopy4, shiftB, 10, _precision);
            }
            catch (CalcErrException ex)
            {
                Assert.Fail($"Bitwise operation failed. " +
                            $"A: {originalA}, B: {originalB}, " +
                            $"Error: {ex.Message}");
            }
        }
    }

    #endregion
}
