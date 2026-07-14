using System.Globalization;
using UnitConversionManager;

namespace CalcEngineTests;

public sealed class UnitConverterTests : IDisposable
{

    // Instance fields for test class
    private readonly UnitConverterTestFixture _fixture;

    // Constructor for the test class
    public UnitConverterTests()
    {
        _fixture = new UnitConverterTestFixture();
    }

    public void Dispose()
    {
        // Reset calculator state after each test
        _fixture.UnitConverter.SendCommand(Command.Reset);
        _fixture.TestVMCallback.Reset();
        GC.SuppressFinalize(this);
    }

    private void ExecuteCommands(IEnumerable<Command> commands)
    {
        foreach (var command in commands)
        {
            if (command != Command.None)
            {
                _fixture.UnitConverter.SendCommand(command);
            }
        }
    }

    [Fact]
    public void UnitConverterTestInit()
    {
        // Test constructor/initialization states
        Assert.Equal(0U, _fixture.XmlLoader.LoadDataCallCount); // shouldn't have initialized the loader yet
        _fixture.UnitConverter.Initialize();
        Assert.Equal(1U, _fixture.XmlLoader.LoadDataCallCount); // now we should have loaded
    }

    [Fact]
    public void UnitConverterTestBasic()
    {
        // Verify a basic input command stream: '3', '0', '.', '0'
        var test1 = new List<(string, Unit)> { ("0.25", _fixture.TestFeet) };
        var test2 = new List<(string, Unit)> { ("2.5", _fixture.TestFeet) };

        _fixture.UnitConverter.SendCommand(Command.Three);
        _fixture.TestVMCallback.CheckDisplayValues("3", "3");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test1));

        _fixture.UnitConverter.SendCommand(Command.Zero);
        _fixture.TestVMCallback.CheckDisplayValues("30", "30");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test2));

        _fixture.UnitConverter.SendCommand(Command.DecimalSeparator);
        _fixture.TestVMCallback.CheckDisplayValues("30.", "30");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test2));

        _fixture.UnitConverter.SendCommand(Command.Zero);
        _fixture.TestVMCallback.CheckDisplayValues("30.0", "30");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test2));
    }

    [Fact]
    public void UnitConverterTestGetters()
    {
        // Check the getter functions
        var expectedCategories = new List<Category> { _fixture.TestLength, _fixture.TestWeight };
        var expectedUnits = new List<Unit> { _fixture.TestInches, _fixture.TestFeet };

        var categories = _fixture.UnitConverter.GetCategories();
        Assert.Equal(expectedCategories.Count, categories.Count);
        for (int i = 0; i < expectedCategories.Count; i++)
        {
            Assert.Equal(expectedCategories[i].Id, categories[i].Id);
            Assert.Equal(expectedCategories[i].Name, categories[i].Name);
        }

        var categoryResult = _fixture.UnitConverter.SetCurrentCategory(expectedCategories[0]);
        var units = categoryResult.Item1;
        Assert.Equal(expectedUnits.Count, units.Count);
        for (int i = 0; i < expectedUnits.Count; i++)
        {
            Assert.Equal(expectedUnits[i].Id, units[i].Id);
            Assert.Equal(expectedUnits[i].Name, units[i].Name);
        }
    }

    [Fact]
    public void UnitConverterTestGetCategory()
    {
        // Test getting category after it has been set
        _fixture.UnitConverter.SetCurrentCategory(_fixture.TestWeight);
        var currentCategory = _fixture.UnitConverter.GetCurrentCategory();

        Assert.Equal(_fixture.TestWeight.Id, currentCategory.Id);
        Assert.Equal(_fixture.TestWeight.Name, currentCategory.Name);
        Assert.Equal(_fixture.TestWeight.SupportsNegative, currentCategory.SupportsNegative);
    }

    [Fact]
    public void UnitConverterTestUnitTypeSwitching()
    {
        // Test switching of unit types
        // Enter 57 into the from field, then switch focus to the to field (making it the new from field)
        _fixture.UnitConverter.SendCommand(Command.Five);
        _fixture.UnitConverter.SendCommand(Command.Seven);
        _fixture.UnitConverter.SwitchActive("57");

        // Now set unit conversion to go from kilograms to pounds
        _fixture.UnitConverter.SetCurrentCategory(_fixture.TestWeight);
        _fixture.UnitConverter.SetCurrentUnitTypes(_fixture.TestKilograms, _fixture.TestPounds);
        _fixture.UnitConverter.SendCommand(Command.Five);

        _fixture.TestVMCallback.CheckDisplayValues("5", "11.0231");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(new List<(string, Unit)>()));
    }

    [Theory]
    [InlineData("Weight", "Weight")]
    [InlineData("{p}Weig;[ht|", "{lb}p{rb}Weig{sc}{lc}ht{p}")]
    [InlineData("{{{t;s}}},:]", "{lb}{lb}{lb}t{sc}s{rb}{rb}{rb}{cm}{co}{rc}")]
    public void UnitConverterTestQuote(string input, string expectedOutput)
    {
        // Test input escaping
        var result = _fixture.UnitConverter.Quote(input);
        Assert.Equal(expectedOutput, result);
    }

    [Theory]
    [InlineData("Weight")]
    [InlineData("{p}Weig;[ht|")]
    [InlineData("{{{t;s}}},:]")]
    public void UnitConverterTestUnquote(string input)
    {
        // Test output unescaping - should round-trip correctly
        var quoted = _fixture.UnitConverter.Quote(input);
        var unquoted = _fixture.UnitConverter.Unquote(quoted);
        Assert.Equal(input, unquoted);
    }

    [Fact]
    public void UnitConverterTestBackspace()
    {
        // Test backspace commands
        var test1 = new List<(string, Unit)> { ("13.66", _fixture.TestKilograms) };
        var test2 = new List<(string, Unit)> { ("13.65", _fixture.TestKilograms) };
        var test3 = new List<(string, Unit)> { ("13.61", _fixture.TestKilograms) };
        var test4 = new List<(string, Unit)> { ("1.36", _fixture.TestKilograms) };
        var emptyList = new List<(string, Unit)>();

        _fixture.UnitConverter.SetCurrentCategory(_fixture.TestWeight);
        _fixture.UnitConverter.SetCurrentUnitTypes(_fixture.TestPounds, _fixture.TestPounds);

        // Enter 30.12
        _fixture.UnitConverter.SendCommand(Command.Three);
        _fixture.UnitConverter.SendCommand(Command.Zero);
        _fixture.UnitConverter.SendCommand(Command.DecimalSeparator);
        _fixture.UnitConverter.SendCommand(Command.One);
        _fixture.UnitConverter.SendCommand(Command.Two);

        _fixture.TestVMCallback.CheckDisplayValues("30.12", "30.12");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test1));

        // Remove the "2"
        _fixture.UnitConverter.SendCommand(Command.Backspace);
        _fixture.TestVMCallback.CheckDisplayValues("30.1", "30.1");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test2));

        // Remove the "1"
        _fixture.UnitConverter.SendCommand(Command.Backspace);
        _fixture.TestVMCallback.CheckDisplayValues("30.", "30");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test3));

        // Remove the decimal point
        _fixture.UnitConverter.SendCommand(Command.Backspace);
        _fixture.TestVMCallback.CheckDisplayValues("30", "30");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test3));

        // Remove the "0"
        _fixture.UnitConverter.SendCommand(Command.Backspace);
        _fixture.TestVMCallback.CheckDisplayValues("3", "3");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test4));

        // Remove the "3" - should show "0"
        _fixture.UnitConverter.SendCommand(Command.Backspace);
        _fixture.TestVMCallback.CheckDisplayValues("0", "0");
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(emptyList));
    }

    [Fact]
    public void UnitConverterTestBackspaceBasic()
    {
        // Verify a basic backspace sequence: '20.43' with backspace
        _fixture.UnitConverter.SendCommand(Command.Two);
        _fixture.UnitConverter.SendCommand(Command.Zero);
        _fixture.UnitConverter.SendCommand(Command.DecimalSeparator);
        _fixture.UnitConverter.SendCommand(Command.Four);
        _fixture.UnitConverter.SendCommand(Command.Three);
        _fixture.UnitConverter.SendCommand(Command.Backspace);

        _fixture.TestVMCallback.CheckDisplayValues("20.4", "20.4");

        _fixture.UnitConverter.SendCommand(Command.Backspace);
        _fixture.TestVMCallback.CheckDisplayValues("20.", "20");

        _fixture.UnitConverter.SendCommand(Command.Backspace);
        _fixture.TestVMCallback.CheckDisplayValues("20", "20");

        _fixture.UnitConverter.SendCommand(Command.Backspace);
        _fixture.TestVMCallback.CheckDisplayValues("2", "2");

        _fixture.UnitConverter.SendCommand(Command.Backspace);
        _fixture.TestVMCallback.CheckDisplayValues("0", "0");
    }

    [Fact]
    public void UnitConverterTestClear()
    {
        // Test the Clear command
        _fixture.UnitConverter.SendCommand(Command.Two);
        _fixture.UnitConverter.SendCommand(Command.Zero);
        _fixture.UnitConverter.SendCommand(Command.DecimalSeparator);
        _fixture.UnitConverter.SendCommand(Command.Four);
        _fixture.UnitConverter.SendCommand(Command.Three);

        // Verify the display shows the entered value
        _fixture.TestVMCallback.CheckDisplayValues("20.43", "20.43");

        // Clear the input
        _fixture.UnitConverter.SendCommand(Command.Clear);

        // Verify the display is cleared to "0"
        _fixture.TestVMCallback.CheckDisplayValues("0", "0");
    }

    [Fact]
    public void UnitConverterTestScientificInputs()
    {
        // Test handling of very small and very large numbers
        _fixture.UnitConverter.SetCurrentCategory(_fixture.TestWeight);
        _fixture.UnitConverter.SetCurrentUnitTypes(_fixture.TestPounds, _fixture.TestKilograms);

        // Enter a very small number (0.00000000000001)
        _fixture.UnitConverter.SendCommand(Command.DecimalSeparator);
        for (int i = 0; i < 13; i++)
        {
            _fixture.UnitConverter.SendCommand(Command.Zero);
        }

        _fixture.UnitConverter.SendCommand(Command.One);

        // Verify scientific notation is used for very small numbers
        _fixture.TestVMCallback.CheckDisplayValues("0.00000000000001", "4.535920e-15");

        // Switch active and enter a very large number
        _fixture.UnitConverter.SwitchActive("4.535920e-15");
        for (int i = 0; i < 15; i++)
        {
            _fixture.UnitConverter.SendCommand(Command.Nine);
        }

        // Verify scientific notation is used for very large numbers
        _fixture.TestVMCallback.CheckDisplayValues("999999999999999", "2.204620e+15");

        // Switch active again and enter a more normal number
        _fixture.UnitConverter.SwitchActive("2.20463e+15");
        _fixture.UnitConverter.SendCommand(Command.One);
        _fixture.UnitConverter.SendCommand(Command.Two);
        _fixture.UnitConverter.SendCommand(Command.Three);
        _fixture.UnitConverter.SendCommand(Command.Four);
        _fixture.UnitConverter.SendCommand(Command.Five);
        _fixture.UnitConverter.SendCommand(Command.Six);
        _fixture.UnitConverter.SendCommand(Command.Seven);

        // Verify normal notation is used for normal-sized numbers
        _fixture.TestVMCallback.CheckDisplayValues("1234567", "559989.7");

        // One more switch to check conversion accuracy
        _fixture.UnitConverter.SwitchActive("559989.7");
        _fixture.UnitConverter.SendCommand(Command.One);
        _fixture.UnitConverter.SendCommand(Command.Two);
        _fixture.UnitConverter.SendCommand(Command.Three);
        _fixture.UnitConverter.SendCommand(Command.Four);
        _fixture.UnitConverter.SendCommand(Command.Five);
        _fixture.UnitConverter.SendCommand(Command.Six);
        _fixture.UnitConverter.SendCommand(Command.Seven);
        _fixture.UnitConverter.SendCommand(Command.Eight);

        _fixture.TestVMCallback.CheckDisplayValues("12345678", "27217529");
    }

    [Fact]
    public void UnitConverterTestSupplementaryResultRounding()
    {
        // Test rounding behavior for suggested values
        var test1 = new List<(string, Unit)> { ("27.75", _fixture.TestFeet) };
        var test2 = new List<(string, Unit)> { ("277.8", _fixture.TestFeet) };
        var test3 = new List<(string, Unit)> { ("2778", _fixture.TestFeet) };

        // Enter "333"
        _fixture.UnitConverter.SendCommand(Command.Three);
        _fixture.UnitConverter.SendCommand(Command.Three);
        _fixture.UnitConverter.SendCommand(Command.Three);

        // Check the suggested values are properly rounded
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test1));

        // Enter "3333"
        _fixture.UnitConverter.SendCommand(Command.Three);
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test2));

        // Enter "33333"
        _fixture.UnitConverter.SendCommand(Command.Three);
        Assert.True(_fixture.TestVMCallback.CheckSuggestedValues(test3));
    }

    [Fact]
    public void UnitConverterTestMaxDigitsReached()
    {
        // Test that MaxDigitsReached callback is triggered when max digits are reached
        var commands = new[]
        {
            Command.One, Command.Two, Command.Three, Command.Four, Command.Five,
            Command.Six, Command.Seven, Command.Eight, Command.Nine, Command.One,
            Command.Zero, Command.One, Command.One, Command.One, Command.Two
        };

        ExecuteCommands(commands);

        // MaxDigitsReached should not have been called yet
        Assert.Equal(0, _fixture.TestVMCallback.GetMaxDigitsReachedCallCount());

        // Adding one more digit should trigger MaxDigitsReached
        _fixture.UnitConverter.SendCommand(Command.One);

        // Verify MaxDigitsReached was called once
        Assert.Equal(1, _fixture.TestVMCallback.GetMaxDigitsReachedCallCount());
    }

    [Fact]
    public void UnitConverterTestMaxDigitsReachedLeadingDecimal()
    {
        // Test MaxDigitsReached with a leading decimal point
        var commands = new[]
        {
            Command.Zero, Command.DecimalSeparator, Command.One, Command.Two, Command.Three,
            Command.Four, Command.Five, Command.Six, Command.Seven, Command.Eight,
            Command.Nine, Command.One, Command.Zero, Command.One, Command.One,
            Command.One
        };

        ExecuteCommands(commands);

        // MaxDigitsReached should not have been called yet
        Assert.Equal(0, _fixture.TestVMCallback.GetMaxDigitsReachedCallCount());

        // Adding one more digit should trigger MaxDigitsReached
        _fixture.UnitConverter.SendCommand(Command.Two);

        // Verify MaxDigitsReached was called once
        Assert.Equal(1, _fixture.TestVMCallback.GetMaxDigitsReachedCallCount());
    }

    [Fact]
    public void UnitConverterTestMaxDigitsReachedTrailingDecimal()
    {
        // Test MaxDigitsReached with a trailing decimal point
        var commands = new[]
        {
            Command.One, Command.Two, Command.Three, Command.Four, Command.Five,
            Command.Six, Command.Seven, Command.Eight, Command.Nine, Command.One,
            Command.Zero, Command.One, Command.One, Command.One, Command.Two,
            Command.DecimalSeparator
        };

        ExecuteCommands(commands);

        // MaxDigitsReached should not have been called yet
        Assert.Equal(0, _fixture.TestVMCallback.GetMaxDigitsReachedCallCount());

        // Adding a digit after the decimal should trigger MaxDigitsReached
        _fixture.UnitConverter.SendCommand(Command.One);

        // Verify MaxDigitsReached was called once
        Assert.Equal(1, _fixture.TestVMCallback.GetMaxDigitsReachedCallCount());
    }

    [Fact]
    public void UnitConverterTestMaxDigitsReachedMultipleTimes()
    {
        // Test that MaxDigitsReached callback is triggered multiple times
        // when trying to exceed the digit limit repeatedly

        // First enter the maximum number of digits (15)
        var commands = new[]
        {
            Command.One, Command.Two, Command.Three, Command.Four, Command.Five,
            Command.Six, Command.Seven, Command.Eight, Command.Nine, Command.One,
            Command.Zero, Command.One, Command.One, Command.One, Command.Two
        };

        ExecuteCommands(commands);

        // Verify MaxDigitsReached has not been called yet
        Assert.Equal(0, _fixture.TestVMCallback.GetMaxDigitsReachedCallCount());

        // Try to add more digits 10 times and check that MaxDigitsReached
        // is called each time
        for (int count = 1; count <= 10; count++)
        {
            _fixture.UnitConverter.SendCommand(Command.Three);

            // Verify MaxDigitsReached was called exactly 'count' times
            Assert.Equal(count, _fixture.TestVMCallback.GetMaxDigitsReachedCallCount());
        }
    }

    [Theory]
    [InlineData("1", "1", "12", "Inches", "Feet")] // 1 foot = 12 inches
    [InlineData("12", "12", "1", "Feet", "Inches")] // 12 inches = 1 foot
    [InlineData("0", "0", "0", "Inches", "Feet")] // Zero test
    [InlineData("1.5", "1.5", "18", "Inches", "Feet")] // Decimal test
    [InlineData("100", "100", "8.333333", "Feet", "Inches")] // Larger value test
    [InlineData("0.0833333", "0.0833333", "0.9999996", "Inches", "Feet")] // Small value test
    [InlineData("1", "1", "2.20462", "Pounds", "Kilograms")] // 1 pound = 0.453592 kg
    [InlineData("2.20462", "2.20462", "0.999998", "Kilograms", "Pounds")] // 1 kg = 2.20462 pounds
    [InlineData("-10", "-10", "-22.0462", "Pounds", "Kilograms")] // Negative value test for Weight
    [InlineData("-10", "-10", "-120", "Inches", "Feet")] // Negative value test for Length
    public void TestUnitConversionLogic(string inputValue, string expectedFromDisplay,
        string expectedToDisplay, string fromUnitName, string toUnitName)
    {
        // Setup - determine which category we're testing based on the units
        Category category;
        Unit fromUnit;
        Unit toUnit;

        if (fromUnitName == "Inches" || fromUnitName == "Feet")
        {
            category = _fixture.TestLength;
            fromUnit = fromUnitName == "Inches" ? _fixture.TestInches : _fixture.TestFeet;
            toUnit = toUnitName == "Inches" ? _fixture.TestInches : _fixture.TestFeet;
        }
        else
        {
            category = _fixture.TestWeight;
            fromUnit = fromUnitName == "Pounds" ? _fixture.TestPounds : _fixture.TestKilograms;
            toUnit = toUnitName == "Pounds" ? _fixture.TestPounds : _fixture.TestKilograms;
        }

        // Reset calculator state from any previous tests
        _fixture.UnitConverter.SendCommand(Command.Reset);
        _fixture.TestVMCallback.Reset();

        // Set the category and unit types
        _fixture.UnitConverter.SetCurrentCategory(category);
        _fixture.UnitConverter.SetCurrentUnitTypes(fromUnit, toUnit);

        // Start with a clean slate to ensure proper testing
        _fixture.UnitConverter.SendCommand(Command.Clear);

        // Enter the input value by switching active (which simulates pasting a value)
        _fixture.UnitConverter.SwitchActive(inputValue);

        // Calculate if needed (the SwitchActive should trigger calculation automatically)
        _fixture.UnitConverter.Calculate();

        // Verify the display values are as expected
        // Allowing slight differences in string representation due to formatting differences
        _fixture.TestVMCallback.CheckDisplayValues(
            expectedFromDisplay,
            expectedToDisplay);
    }

    [Fact]
    public void TestExtensiveConversionScenarios()
    {
        // Reset calculator state from any previous tests
        _fixture.UnitConverter.SendCommand(Command.Reset);
        _fixture.TestVMCallback.Reset();

        // Test a range of values to ensure conversion works properly
        _fixture.UnitConverter.SetCurrentCategory(_fixture.TestWeight);
        _fixture.UnitConverter.SetCurrentUnitTypes(_fixture.TestPounds, _fixture.TestKilograms);

        // Test very small value
        _fixture.UnitConverter.SendCommand(Command.Clear);
        _fixture.UnitConverter.SwitchActive("0.0001");
        _fixture.UnitConverter.Calculate();
        _fixture.TestVMCallback.CheckDisplayValues("0.0001", "0.00022");

        // Test normal value
        _fixture.UnitConverter.SendCommand(Command.Clear);
        _fixture.UnitConverter.SwitchActive("100");
        _fixture.UnitConverter.Calculate();
        _fixture.TestVMCallback.CheckDisplayValues("100", "45.3592");

        // Test large value
        _fixture.UnitConverter.SendCommand(Command.Clear);
        _fixture.UnitConverter.SwitchActive("1000000");
        _fixture.UnitConverter.Calculate();
        _fixture.TestVMCallback.CheckDisplayValues("1000000", "2204620");

        // Test compound conversion (chain multiple conversions)
        // Convert lb → kg then immediately kg → lb should give back original value (approximately)
        _fixture.UnitConverter.SendCommand(Command.Clear);
        _fixture.UnitConverter.SetCurrentUnitTypes(_fixture.TestPounds, _fixture.TestKilograms);
        _fixture.UnitConverter.SwitchActive("123.456");
        _fixture.UnitConverter.Calculate();
        // Get the first conversion result
        _fixture.TestVMCallback.CheckDisplayValues("123.456", "272.1736");
        var kgResult = _fixture.TestVMCallback._lastTo; // Using the cached result

        // Now convert it back
        _fixture.UnitConverter.SetCurrentUnitTypes(_fixture.TestKilograms, _fixture.TestPounds);
        _fixture.UnitConverter.SwitchActive(kgResult);
        _fixture.UnitConverter.Calculate();

        // The result should be close to the original value (accounting for rounding errors)
        string lbResult = _fixture.TestVMCallback._lastTo; // Using the cached result
        double originalValue = 123.456;
        double finalValue = double.Parse(lbResult, CultureInfo.InvariantCulture);

        // Allow for a small error margin due to rounding in conversions
        Assert.True(Math.Abs(originalValue - finalValue) < 0.002,
            $"Round-trip conversion error: {originalValue} → {kgResult} → {finalValue}");

        // Test with length units to ensure multiple unit types work
        _fixture.UnitConverter.SendCommand(Command.Clear);
        _fixture.UnitConverter.SetCurrentCategory(_fixture.TestLength);
        _fixture.UnitConverter.SetCurrentUnitTypes(_fixture.TestFeet, _fixture.TestInches);
        _fixture.UnitConverter.SwitchActive("36");
        _fixture.UnitConverter.Calculate();
        _fixture.TestVMCallback.CheckDisplayValues("36", "3");
    }
}
