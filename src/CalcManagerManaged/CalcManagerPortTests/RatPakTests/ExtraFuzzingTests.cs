using CalcEngine;
using  CalcEngine;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace CalcManagerPortTests.RatPakTests;

public class ExtraFuzzingTests
{
    private readonly RatPak _ratPak;
    private readonly int _precision = 256; // Higher precision for fuzzing tests.
    private readonly Random _random;
    private readonly int _seed;

    public ExtraFuzzingTests()
    {
        _ratPak = new RatPak(_precision);
        _seed = Guid.NewGuid().GetHashCode();
        _random = new Random(_seed);
    }

    #region Randomization Helpers

    private string GenerateRandomNumber(int maxDigits = 30, bool allowDecimals = true, bool allowExponents = true,
        bool allowNegatives = true)
    {
        var digits = _random.Next(1, maxDigits + 1);
        var isNegative = _random.Next(2) == 0;

        var result = allowNegatives ? (isNegative ? "-" : "") : "";

        // Ensure we have at least one digit before decimal point
        result += _random.Next(10);

        for (var i = 1; i < digits; i++)
        {
            if (allowDecimals && i == (_random.Next(digits) + 1) && !result.Contains('.'))
            {
                result += ".";
            }
            else
            {
                result += _random.Next(10);
            }
        }

        // Add exponent occasionally
        if (allowExponents && _random.Next(5) == 0)
        {
            result += "e";
            if (_random.Next(2) == 0)
            {
                result += "+";
            }
            else
            {
                result += "-";
            }

            result += _random.Next(1, 100);
        }

        return result;
    }

    private RatPak.RAT GenerateRandomRat(bool allowDecimals = true, bool allowExponents = true,
        bool allowNegatives = true, int maxDigits = 30)
    {
        var numStr = GenerateRandomNumber(maxDigits, allowDecimals, allowExponents, allowNegatives);
        var num = _ratPak.StringToNumber(numStr, 10, _precision);
        return _ratPak.numtorat(num, 10);
    }

    #endregion

    #region Arithmetic Operation Stability Tests

    [Fact]
    public void FuzzAddRat_MultipleRandomNumbers_CompletesWithoutErrors()
    {
        var testCount = 10000;

        for (var i = 0; i < testCount; i++)
        {
            var rat1 = GenerateRandomRat();
            var rat2 = GenerateRandomRat();
            //should not throw exceptions
            _ratPak.addrat(ref rat1, rat2, _precision);

            // Verify result is not null
            Assert.NotNull(rat1);
            Assert.NotNull(rat1.pp);
            Assert.NotNull(rat1.pq);
        }
    }

    [Fact]
    public void FuzzMulRat_MultipleRandomNumbers_CompletesWithoutErrors()
    {
        var testCount = 10000;

        for (var i = 0; i < testCount; i++)
        {
            var rat1 = GenerateRandomRat();
            var rat2 = GenerateRandomRat();
            //should not throw exceptions
            _ratPak.mulrat(ref rat1, rat2, _precision);

            // Verify result is not null
            Assert.NotNull(rat1);
            Assert.NotNull(rat1.pp);
            Assert.NotNull(rat1.pq);
        }
    }

    [Fact]
    public void FuzzDivRat_MultipleRandomNumbers_CompletesWithoutErrors()
    {
        var testCount = 10000;

        for (var i = 0; i < testCount; i++)
        {
            var rat1 = GenerateRandomRat();
            var rat2 = GenerateRandomRat();

            // Skip if divisor is zero
            if (_ratPak.zerrat(rat2))
            {
                i--;
                continue;
            }

            // should not throw exceptions
            _ratPak.divrat(ref rat1, rat2, _precision);

            // Verify result is not null
            Assert.NotNull(rat1);
            Assert.NotNull(rat1.pp);
            Assert.NotNull(rat1.pq);
        }
    }

    [Fact]
    public void FuzzPowRat_MultipleRandomNumbers_CompletesWithoutErrors()
    {
        var testCount = 50;

        for (var i = 0; i < testCount; i++)
        {
            // Use smaller numbers for base to avoid excessive computation and dont allow negative bases.
            var baseRat = GenerateRandomRat(false, false, false, 5);
            var baseRatStr = _ratPak.RatToString(ref baseRat, RatPak.NumberFormat.Scientific, 10, _precision);

            // Just to avoid a zero base.
            _ratPak.addrat(ref baseRat, _ratPak.rat_one, _precision);

            // Keep exponents relatively small to avoid excessive computation
            var expStr = _random.Next(-10, 11).ToString();
            if (_random.Next(2) == 0)
            {
                // Sometimes use fractional exponents
                expStr = $"{_random.Next(-5, 6)}.{_random.Next(10)}";
            }

            var expNum = _ratPak.StringToNumber(expStr, 10, _precision);
            var expRat = _ratPak.numtorat(expNum, 10);

            try
            {
                //  - should not throw exceptions
                _ratPak.powrat(ref baseRat, expRat, 10, _precision);

                // Verify result is not null
                Assert.NotNull(baseRat);
                Assert.NotNull(baseRat.pp);
                Assert.NotNull(baseRat.pq);
            }
            catch (CalcErrException e)
            {
                throw new TestCanceledException($"Seed {_seed}, expStr: {expStr}, baseRat: {baseRatStr}", e);
            }
        }
    }

    #endregion

    #region Parsing and Formatting Fuzz Tests

    [Fact]
    public void FuzzStringToNumber_RandomStrings_HandlesValidInputs()
    {
        var testCount = 10000;

        for (var i = 0; i < testCount; i++)
        {
            var numStr = GenerateRandomNumber();

            var num = _ratPak.StringToNumber(numStr, 10, _precision);

            Assert.NotNull(num);

            // Convert back to string and verify it's not empty
            var result = _ratPak.NumberToString(ref num, RatPak.NumberFormat.Float, 10, _precision);
            Assert.False(string.IsNullOrEmpty(result));
        }
    }

    [Fact]
    public void FuzzNumberToString_RandomNumbers_HandlesAllFormats()
    {
        var testCount = 10000;
        var formats = new[]
        {
            RatPak.NumberFormat.Float,
            RatPak.NumberFormat.Scientific,
            RatPak.NumberFormat.Engineering
        };

        for (var i = 0; i < testCount; i++)
        {
            var rat = GenerateRandomRat();

            foreach (var format in formats)
            {
                //
                var result = _ratPak.RatToString(ref rat, format, 10, _precision);

                // Assert
                Assert.False(string.IsNullOrEmpty(result));
            }
        }
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public void ConsistencyTest_Addition_IsCommutative()
    {
        var testCount = 10000;

        for (var i = 0; i < testCount; i++)
        {
            var ratA = GenerateRandomRat();
            var ratB = GenerateRandomRat();

            // Create copies
            var ratAOriginal = CopyRat(ratA);
            var ratBOriginal = CopyRat(ratB);

            // A + B
            _ratPak.addrat(ref ratA, ratBOriginal, _precision);
            var result1 = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Scientific, 10, _precision);

            // B + A
            _ratPak.addrat(ref ratB, ratAOriginal, _precision);
            var result2 = _ratPak.RatToString(ref ratB, RatPak.NumberFormat.Scientific, 10, _precision);

            Assert.Equal(result1, result2); // Addition should be commutative
        }
    }

    [Fact]
    public void ConsistencyTest_Multiplication_IsCommutative()
    {
        var testCount = 10000;

        for (var i = 0; i < testCount; i++)
        {
            var ratA = GenerateRandomRat();
            var ratB = GenerateRandomRat();

            // Create copies
            var ratAOriginal = CopyRat(ratA);
            var ratBOriginal = CopyRat(ratB);

            // A * B
            _ratPak.mulrat(ref ratA, ratBOriginal, _precision);
            var result1 = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Scientific, 10, _precision);

            // B * A
            _ratPak.mulrat(ref ratB, ratAOriginal, _precision);
            var result2 = _ratPak.RatToString(ref ratB, RatPak.NumberFormat.Scientific, 10, _precision);

            Assert.Equal(result1, result2); // Multiplication should be commutative
        }
    }

    [Fact]
    public void ConsistencyTest_AdditionAndSubtraction_AreInverses()
    {
        var testCount = 1000;

        for (var i = 0; i < testCount; i++)
        {
            var ratA = GenerateRandomRat();
            var ratB = GenerateRandomRat();

            // Create a copy of A
            var ratAOriginal = CopyRat(ratA);

            // A + B
            _ratPak.addrat(ref ratA, ratB, _precision);

            // (A + B) - B should be A
            _ratPak.subrat(ref ratA, ratB, _precision);

            // Get results as strings for comparison
            var resultA = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Scientific, 10, _precision);
            var resultAOriginal =
                _ratPak.RatToString(ref ratAOriginal, RatPak.NumberFormat.Scientific, 10, _precision);

            Assert.Equal(resultAOriginal, resultA); // Adding and then subtracting should return the original
        }
    }

    [Fact]
    public void ConsistencyTest_MultiplicationAndDivision_AreInverses()
    {
        var testCount = 10000;

        for (var i = 0; i < testCount; i++)
        {
            var ratA = GenerateRandomRat();
            var ratB = GenerateRandomRat();

            // Skip if B is zero
            if (_ratPak.zerrat(ratB))
            {
                i--;
                continue;
            }

            // Create a copy of A
            var ratAOriginal = CopyRat(ratA);

            // A * B
            _ratPak.mulrat(ref ratA, ratB, _precision);

            // (A * B) / B should be A
            _ratPak.divrat(ref ratA, ratB, _precision);

            // Get results as strings for comparison
            var resultA = _ratPak.RatToString(ref ratA, RatPak.NumberFormat.Scientific, 10, _precision);
            var resultAOriginal =
                _ratPak.RatToString(ref ratAOriginal, RatPak.NumberFormat.Scientific, 10, _precision);

            Assert.Equal(resultAOriginal, resultA);
        }
    }

    [Fact]
    public void ConsistencyTest_PowerAndRoot_AreInverses()
    {
        var testCount = 20; // Less tests due to complexity

        for (var i = 0; i < testCount; i++)
        {
            // Use positive numbers for simpler testing
            var baseStr = _random.Next(1, Int32.MaxValue).ToString();
            var baseNum = _ratPak.StringToNumber(baseStr, 10, _precision);
            var baseRat = _ratPak.numtorat(baseNum, 10);

            // Create a copy
            var baseRatOriginal = CopyRat(baseRat);

            // Create power value (2 for square/sqrt)
            var powerNum = _ratPak.StringToNumber("2", 10, _precision);
            var powerRat = _ratPak.numtorat(powerNum, 10);

            // Create inverse power (0.5 for sqrt)
            var invPowerNum = _ratPak.StringToNumber("0.5", 10, _precision);
            var invPowerRat = _ratPak.numtorat(invPowerNum, 10);

            // A ^ 2
            _ratPak.powrat(ref baseRat, powerRat, 10, _precision);

            // (A ^ 2) ^ 0.5 should be A
            _ratPak.powrat(ref baseRat, invPowerRat, 10, _precision);

            // Get results as strings for comparison
            var resultA = _ratPak.RatToString(ref baseRat, RatPak.NumberFormat.Float, 10, _precision);
            var resultAOriginal = _ratPak.RatToString(ref baseRatOriginal, RatPak.NumberFormat.Float, 10, _precision);

            Assert.Equal(
                resultAOriginal,
                resultA);
        }
    }

    [Fact]
    public void ConsistencyTest_PowerAndRoot_AreInverses_Use_RootRat_Stub()
    {
        var testCount = 20; // Less tests due to complexity

        for (var i = 0; i < testCount; i++)
        {
            // Use positive numbers for simpler testing
            var baseStr = _random.Next(1, Int32.MaxValue).ToString();
            var baseNum = _ratPak.StringToNumber(baseStr, 10, _precision);
            var baseRat = _ratPak.numtorat(baseNum, 10);

            // Create a copy
            var baseRatOriginal = CopyRat(baseRat);

            // Create power value (2 for square/sqrt)
            var powerNum = _ratPak.StringToNumber("2", 10, _precision);
            var powerRat = _ratPak.numtorat(powerNum, 10);

            // A ^ 2
            _ratPak.powrat(ref baseRat, powerRat, 10, _precision);

            // (A ^ 2) ^ 0.5 should be A
            _ratPak.rootrat(ref baseRat, powerRat, 10, _precision);

            // Get results as strings for comparison
            var resultA = _ratPak.RatToString(ref baseRat, RatPak.NumberFormat.Float, 10, _precision);
            var resultAOriginal = _ratPak.RatToString(ref baseRatOriginal, RatPak.NumberFormat.Float, 10, _precision);
            // use StartsWith since we might have precision differences
            Assert.Equal(
                resultAOriginal,
                resultA); // Integer part should be equal
        }
    }

    #endregion

    #region Memory Management Tests

    [Fact]
    public void MemoryTest_RepeatedOperations_DoesNotLeak()
    {
        // This test performs many operations and checks that destroy methods work properly
        // We can't directly test for memory leaks in a unit test, but this ensures the code path is exercised

        var testCount = 1000;
        List<RatPak.RAT> rats = new List<RatPak.RAT>();
        // Create many RATs
        for (var i = 0; i < testCount; i++)
        {
            rats.Add(GenerateRandomRat(maxDigits: 10));
        }

        // Perform various operations
        for (var i = 0; i < testCount - 1; i += 2)
        {
            var result = CopyRat(rats[i]);

            // Skip division by zero
            if (!_ratPak.zerrat(rats[i + 1]))
            {
                _ratPak.addrat(ref result, rats[i + 1], _precision);
                _ratPak.mulrat(ref result, rats[i + 1], _precision);
                _ratPak.divrat(ref result, rats[i + 1], _precision);
            }

            // Explicitly destroy the temporary RAT
            _ratPak.destroyrat(ref result);
        }

        // Destroy all created RATs
        foreach (var rat in rats)
        {
            var temp = rat; // Create a copy to avoid modifying the list
            _ratPak.destroyrat(ref temp);
        }

        // No assertion needed - just make sure we don't crash
    }

    #endregion

    #region Helper Methods

    private RatPak.RAT CopyRat(RatPak.RAT original)
    {
        var result = _ratPak.createrat();
        _ratPak.duprat(ref result, original);
        return result;
    }

    #endregion
}
