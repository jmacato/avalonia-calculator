using System.Globalization;
using CalcEngine;

namespace CalcManagerPortTests.RatPakTests;

public class Scale2PiTests
{
    private readonly RatPak _ratPak;
    private readonly UInt32 _radix = 10;
    private readonly Int32 _precision = 64;
    private readonly double epsilon = 1e-38;

    public Scale2PiTests()
    {
        _ratPak = new RatPak(_precision, _radix);
    }

    private double RatToDouble(RAT rat)
    {
        // Helper method to convert a PRAT to a double for easier assertions
        var ratStr = _ratPak.RatToString(ref rat, NumberFormat.FloatingPoint, _radix, _precision);
        return double.Parse(ratStr, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Scale2PiZeroRemainsZero()
    {
        var rat = RatPak.i32torat(0);

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(RatPak.zerrat(rat));
        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiTwoPiReturnsZero()
    {
        var rat = RatPak.createrat();
        RatPak.duprat(ref rat, _ratPak.TwoPi);

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(Math.Abs(RatToDouble(rat)) < epsilon); // Should be very close to zero
        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiPiReturnsPi()
    {
        var rat = RatPak.createrat();
        RatPak.duprat(ref rat, _ratPak.Pi);

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var piValue = RatToDouble(_ratPak.Pi);
        Assert.True(Math.Abs(RatToDouble(rat) - piValue) < epsilon);
        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiThreePiReturnsPi()
    {
        var threenum = _ratPak.StringToNumber("3", 10, _precision);
        Assert.NotNull(threenum);
        var threeblindmice = RatPak.numtorat(threenum, 10);

        var rat = RatPak.createrat();
        RatPak.duprat(ref rat, _ratPak.Pi);
        _ratPak.mulrat(ref rat, threeblindmice, _precision); // Create 3π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var piValue = RatToDouble(_ratPak.Pi);
        Assert.True(Math.Abs(RatToDouble(rat) - piValue) < epsilon);
        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiFourPiReturnsZero()
    {
        var rat = RatPak.createrat();
        RatPak.duprat(ref rat, _ratPak.TwoPi);
        _ratPak.mulrat(ref rat, _ratPak.RatTwo, _precision); // Create 4π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(Math.Abs(RatToDouble(rat)) < epsilon); // Should be very close to zero
        RatPak.destroyrat(ref rat);
    }
    [Fact]
    public void Scale2PiNegativePiReturnsNegativePi()
    {
        var rat = RatPak.createrat();
        RatPak.duprat(ref rat, _ratPak.Pi);
        Assert.NotNull(rat.Pp);
        rat.Pp.Sign = -1; // Make it -π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        // For negative values, scale2pi preserves the sign
        // -π mod 2π (with sign preservation) is -π
        var piValue = RatToDouble(_ratPak.Pi);
        var result = RatToDouble(rat);

        // The result should be approximately -π
        Assert.True(Math.Abs(result + piValue) < epsilon,
            $"Expected result close to -π ({-piValue}), but got {result}");

        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiNegativeTwoPiReturnsZero()
    {
        var rat = RatPak.createrat();
        RatPak.duprat(ref rat, _ratPak.TwoPi);
        Assert.NotNull(rat.Pp);
        rat.Pp.Sign = -1; // Make it -2π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(Math.Abs(RatToDouble(rat)) < epsilon); // Should be very close to zero
        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiLargeMultipleOfTwoPiReturnsZero()
    {
        var rat = RatPak.createrat();
        RatPak.duprat(ref rat, _ratPak.TwoPi);
        _ratPak.mulrat(ref rat, RatPak.i32torat(1000), _precision); // Create 1000*2π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(Math.Abs(RatToDouble(rat)) < epsilon); // Should be very close to zero
        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiLargeMultipleOfTwoPiPlusPiReturnsPi()
    {
        var rat = RatPak.createrat();
        RatPak.duprat(ref rat, _ratPak.TwoPi);
        _ratPak.mulrat(ref rat, RatPak.i32torat(1000), _precision); // Create 1000*2π
        _ratPak.addrat(ref rat, _ratPak.Pi, _precision); // Add π to make 1000*2π + π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var piValue = RatToDouble(_ratPak.Pi);
        Assert.True(Math.Abs(RatToDouble(rat) - piValue) < epsilon);
        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiHalfPiReturnsHalfPi()
    {
        var rat = RatPak.createrat();
        RatPak.duprat(ref rat, _ratPak.PiOverTwo);

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var halfPiValue = RatToDouble(_ratPak.PiOverTwo);
        Assert.True(Math.Abs(RatToDouble(rat) - halfPiValue) < epsilon);
        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiVeryLargeValueScalesCorrectly()
    {
        var rat = RatPak.i32torat(1000000); // A large value

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var result = RatToDouble(rat);
        Assert.True(result >= 0 && result < RatToDouble(_ratPak.TwoPi));
        RatPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2PiVerySmallValueScalesCorrectly()
    {
        var rat = RatPak.i32torat(1);
        _ratPak.divrat(ref rat, RatPak.i32torat(1000000), _precision); // A very small value

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var result = RatToDouble(rat);
        Assert.True(result >= 0 && result < RatToDouble(_ratPak.TwoPi));
        RatPak.destroyrat(ref rat);
    }


    [Fact]
    public void Scale2PiRandomValuesAllResultsInCorrectRange()
    {
        // Test with multiple random values to ensure results are in the correct range
        // For positive inputs: [0, 2π)
        // For negative inputs: [-2π, 0)
        var random = new DeterministicRandom();

        for (var i = 0; i < 100; i++)
        {
            var randomValue = (random.NextDouble() - 0.5) * 10000000; // Range: -500 to 500
            var rat = RatPak.i32torat((int)randomValue);

            // Save the original sign
            var wasNegative = randomValue < 0;

            _ratPak.scale2pi(ref rat, _radix, _precision);

            var result = RatToDouble(rat);
            var twoPiValue = RatToDouble(_ratPak.TwoPi);

            if (wasNegative)
            {
                // For negative inputs, the result should be in [-2π, 0)
                // Account for potential floating-point issues near the boundaries
                Assert.True(result > -twoPiValue - epsilon && result <= epsilon,
                    $"Value {randomValue} resulted in {result}, which is not in [-2π, 0)");
            }
            else
            {
                // For positive inputs, the result should be in [0, 2π)
                // Account for potential floating-point issues near the boundaries
                Assert.True(result >= -epsilon && result < twoPiValue + epsilon,
                    $"Value {randomValue} resulted in {result}, which is not in [0, 2π)");
            }

            RatPak.destroyrat(ref rat);
        }
    }
}
