using  CalcEngine;

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

    private double RatToDouble(RatPak.RAT rat)
    {
        // Helper method to convert a PRAT to a double for easier assertions
        var ratStr = _ratPak.RatToString(ref rat, RatPak.NumberFormat.Float, _radix, _precision);
        return double.Parse(ratStr);
    }

    [Fact]
    public void Scale2Pi_Zero_RemainsZero()
    {
        var rat = _ratPak.i32torat(0);

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(_ratPak.zerrat(rat));
        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_TwoPi_ReturnsZero()
    {
        RatPak.RAT rat = null;
        _ratPak.duprat(ref rat, _ratPak.two_pi);

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(Math.Abs(RatToDouble(rat)) < epsilon); // Should be very close to zero
        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_Pi_ReturnsPi()
    {
        RatPak.RAT rat = null;
        _ratPak.duprat(ref rat, _ratPak.pi);

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var piValue = RatToDouble(_ratPak.pi);
        Assert.True(Math.Abs(RatToDouble(rat) - piValue) < epsilon);
        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_ThreePi_ReturnsPi()
    {
        var threenum = _ratPak.StringToNumber("3", 10, _precision);
        var threeblindmice = _ratPak.numtorat(threenum, 10);

        RatPak.RAT rat = null;
        _ratPak.duprat(ref rat, _ratPak.pi);
        _ratPak.mulrat(ref rat, threeblindmice, _precision); // Create 3π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var piValue = RatToDouble(_ratPak.pi);
        Assert.True(Math.Abs(RatToDouble(rat) - piValue) < epsilon);
        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_FourPi_ReturnsZero()
    {
        RatPak.RAT rat = null;
        _ratPak.duprat(ref rat, _ratPak.two_pi);
        _ratPak.mulrat(ref rat, _ratPak.rat_two, _precision); // Create 4π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(Math.Abs(RatToDouble(rat)) < epsilon); // Should be very close to zero
        _ratPak.destroyrat(ref rat);
    }
    [Fact]
    public void Scale2Pi_NegativePi_ReturnsNegativePi()
    {
        RatPak.RAT rat = null;
        _ratPak.duprat(ref rat, _ratPak.pi);
        rat.pp.sign = -1; // Make it -π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        // For negative values, scale2pi preserves the sign
        // -π mod 2π (with sign preservation) is -π
        var piValue = RatToDouble(_ratPak.pi);
        var result = RatToDouble(rat);

        // The result should be approximately -π
        Assert.True(Math.Abs(result + piValue) < epsilon,
            $"Expected result close to -π ({-piValue}), but got {result}");

        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_NegativeTwoPi_ReturnsZero()
    {
        RatPak.RAT rat = null;
        _ratPak.duprat(ref rat, _ratPak.two_pi);
        rat.pp.sign = -1; // Make it -2π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(Math.Abs(RatToDouble(rat)) < epsilon); // Should be very close to zero
        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_LargeMultipleOfTwoPi_ReturnsZero()
    {
        RatPak.RAT rat = null;
        _ratPak.duprat(ref rat, _ratPak.two_pi);
        _ratPak.mulrat(ref rat, _ratPak.i32torat(1000), _precision); // Create 1000*2π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        Assert.True(Math.Abs(RatToDouble(rat)) < epsilon); // Should be very close to zero
        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_LargeMultipleOfTwoPiPlusPi_ReturnsPi()
    {
        RatPak.RAT rat = null;
        _ratPak.duprat(ref rat, _ratPak.two_pi);
        _ratPak.mulrat(ref rat, _ratPak.i32torat(1000), _precision); // Create 1000*2π
        _ratPak.addrat(ref rat, _ratPak.pi, _precision); // Add π to make 1000*2π + π

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var piValue = RatToDouble(_ratPak.pi);
        Assert.True(Math.Abs(RatToDouble(rat) - piValue) < epsilon);
        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_HalfPi_ReturnsHalfPi()
    {
        RatPak.RAT rat = null;
        _ratPak.duprat(ref rat, _ratPak.pi_over_two);

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var halfPiValue = RatToDouble(_ratPak.pi_over_two);
        Assert.True(Math.Abs(RatToDouble(rat) - halfPiValue) < epsilon);
        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_VeryLargeValue_ScalesCorrectly()
    {
        var rat = _ratPak.i32torat(1000000); // A large value

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var result = RatToDouble(rat);
        Assert.True(result >= 0 && result < RatToDouble(_ratPak.two_pi));
        _ratPak.destroyrat(ref rat);
    }

    [Fact]
    public void Scale2Pi_VerySmallValue_ScalesCorrectly()
    {
        var rat = _ratPak.i32torat(1);
        _ratPak.divrat(ref rat, _ratPak.i32torat(1000000), _precision); // A very small value

        _ratPak.scale2pi(ref rat, _radix, _precision);

        var result = RatToDouble(rat);
        Assert.True(result >= 0 && result < RatToDouble(_ratPak.two_pi));
        _ratPak.destroyrat(ref rat);
    }


    [Fact]
    public void Scale2Pi_RandomValues_AllResultsInCorrectRange()
    {
        // Test with multiple random values to ensure results are in the correct range
        // For positive inputs: [0, 2π)
        // For negative inputs: [-2π, 0)
        var random = new Random();

        for (var i = 0; i < 100; i++)
        {
                        var randomValue = (random.NextDouble() - 0.5) * 10000000; // Range: -500 to 500
            var rat = _ratPak.i32torat((int)randomValue);

            // Save the original sign
            var wasNegative = randomValue < 0;

            _ratPak.scale2pi(ref rat, _radix, _precision);

            var result = RatToDouble(rat);
            var twoPiValue = RatToDouble(_ratPak.two_pi);

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

            _ratPak.destroyrat(ref rat);
        }
    }
}
