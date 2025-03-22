using CalcEngine;

namespace CalcEngineTests;

public class CalcInputTest
{
    private CalcInput m_calcInput;

    public CalcInputTest()
    {
        // Setup for each test (equivalent to TEST_METHOD_INITIALIZE)
        m_calcInput = new CalcInput('.');
    }

    // Helper method to clean up after each test
    private void Cleanup()
    {
        m_calcInput.Clear();
        m_calcInput.SetDecimalSymbol('.');
    }

    [Fact]
    public void Clear_ShouldResetToZero()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        m_calcInput.TryToggleSign(false, "999");
        m_calcInput.TryAddDecimalPt();
        m_calcInput.TryAddDigit(2, 10, false, "999", 64, 32);
        m_calcInput.TryBeginExponent();
        m_calcInput.TryAddDigit(3, 10, false, "999", 64, 32);

        // Assert pre-condition
        Assert.Equal("-1.2e+3", m_calcInput.ToString(10));

        // Act
        m_calcInput.Clear();

        // Assert
        Assert.Equal("0", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryToggleSign_OnZero_ShouldNotCreateNegativeZero()
    {
        // Act
        bool result = m_calcInput.TryToggleSign(false, "999");

        // Assert
        Assert.True(result);
        Assert.Equal("0", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryToggleSign_WithExponent_ShouldToggleExponentSign()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        m_calcInput.TryBeginExponent();
        m_calcInput.TryAddDigit(2, 10, false, "999", 64, 32);

        // Act - toggle once
        bool result1 = m_calcInput.TryToggleSign(false, "999");

        // Assert
        Assert.True(result1);
        Assert.Equal("1.e-2", m_calcInput.ToString(10));

        // Act - toggle again
        bool result2 = m_calcInput.TryToggleSign(false, "999");

        // Assert
        Assert.True(result2);
        Assert.Equal("1.e+2", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryToggleSign_OnBase_ShouldToggleBaseSign()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);

        // Act - toggle once
        bool result1 = m_calcInput.TryToggleSign(false, "999");

        // Assert
        Assert.True(result1);
        Assert.Equal("-1", m_calcInput.ToString(10));

        // Act - toggle again
        bool result2 = m_calcInput.TryToggleSign(false, "999");

        // Assert
        Assert.True(result2);
        Assert.Equal("1", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryToggleSign_BaseInIntegerMode_ShouldToggleSign()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);

        // Act
        bool result = m_calcInput.TryToggleSign(true, "999");

        // Assert
        Assert.True(result);
        Assert.Equal("-1", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryToggleSign_Rollover_ShouldFailOnRollover()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        m_calcInput.TryAddDigit(2, 10, false, "999", 64, 32);

        // First toggle should succeed
        bool result1 = m_calcInput.TryToggleSign(true, "127");
        Assert.True(result1);

        m_calcInput.TryAddDigit(8, 10, false, "999", 64, 32);

        // Act - try to toggle on value that would exceed limit
        bool result2 = m_calcInput.TryToggleSign(true, "127");

        // Assert
        Assert.False(result2);
        Assert.Equal("-128", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryAddDigit_LeadingZeroes_ShouldBeIgnored()
    {
        // Act
        bool result1 = m_calcInput.TryAddDigit(0, 10, false, "999", 64, 32);
        bool result2 = m_calcInput.TryAddDigit(0, 10, false, "999", 64, 32);
        bool result3 = m_calcInput.TryAddDigit(0, 10, false, "999", 64, 32);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
        Assert.Equal("0", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryAddDigit_MaxCount_ShouldRespectMaxDigitLimit()
    {
        // Act - Base part
        bool result1 = m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        bool result2 = m_calcInput.TryAddDigit(2, 10, false, "999", 64, 1);

        // Assert - Base part
        Assert.True(result1);
        Assert.False(result2);
        Assert.Equal("1", m_calcInput.ToString(10));

        // Act - Exponent part
        m_calcInput.TryBeginExponent();
        bool result3 = m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        bool result4 = m_calcInput.TryAddDigit(2, 10, false, "999", 64, 32);
        bool result5 = m_calcInput.TryAddDigit(3, 10, false, "999", 64, 32);
        bool result6 = m_calcInput.TryAddDigit(4, 10, false, "999", 64, 32);
        bool result7 = m_calcInput.TryAddDigit(5, 10, false, "999", 64, 32);

        // Assert - Exponent part
        Assert.True(result3);
        Assert.True(result4);
        Assert.True(result5);
        Assert.True(result6);
        Assert.False(result7);
        Assert.Equal("1.e+1234", m_calcInput.ToString(10));

        // Additional test for decimal point and leading zero
        m_calcInput.Clear();
        m_calcInput.TryAddDecimalPt();
        bool result8 = m_calcInput.TryAddDigit(1, 10, false, "999", 64, 1);
        Assert.True(result8);
        Assert.Equal("0.1", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(24)]
    public void TryAddDigit_VariousValues_ShouldAcceptValidDigits(uint digit)
    {
        // Act
        bool result = m_calcInput.TryAddDigit(digit, 10, false, "999", 64, 32);

        // Assert
        Assert.True(result);

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryAddDigit_RolloverBaseCheck_ShouldFailForNonOctalDecimal()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);

        // Act - Try with hexadecimal
        bool resultHex = m_calcInput.TryAddDigit(2, 16, true, "999", 64, 1);

        // Assert
        Assert.False(resultHex);

        // Act - Try with binary
        bool resultBinary = m_calcInput.TryAddDigit(1, 2, true, "999", 64, 1);

        // Assert
        Assert.False(resultBinary);

        // Cleanup
        Cleanup();
    }


    [Fact]
    public void TryAddDigit_RolloverOctalByte_ShouldCheckFirstDigit()
    {
        // Arrange & Act - First digit <= 3 should allow another digit
        m_calcInput.TryAddDigit(1, 8, true, "777", 64, 32);
        bool result1 = m_calcInput.TryAddDigit(2, 8, true, "377", 8, 1);

        // Assert
        Assert.True(result1);

        // Arrange & Act - First digit > 3 should not allow another digit
        m_calcInput.Clear();
        m_calcInput.TryAddDigit(4, 8, true, "777", 64, 32);
        bool result2 = m_calcInput.TryAddDigit(2, 8, true, "377", 8, 1);

        // Assert
        Assert.False(result2);

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryAddDigit_RolloverOctalWord_ShouldCheckFirstDigit()
    {
        // Arrange & Act - First digit == 1 should allow another digit
        m_calcInput.TryAddDigit(1, 8, true, "777", 64, 32);
        bool result1 = m_calcInput.TryAddDigit(2, 8, true, "377", 16, 1);

        // Assert
        Assert.True(result1);

        // Arrange & Act - First digit > 1 should not allow another digit
        m_calcInput.Clear();
        m_calcInput.TryAddDigit(2, 8, true, "777", 64, 32);
        bool result2 = m_calcInput.TryAddDigit(2, 8, true, "377", 16, 1);

        // Assert
        Assert.False(result2);

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryAddDigit_RolloverOctalDword_ShouldCheckFirstDigit()
    {
        // Arrange & Act - First digit <= 3 should allow another digit
        m_calcInput.TryAddDigit(1, 8, true, "777", 64, 32);
        bool result1 = m_calcInput.TryAddDigit(2, 8, true, "377", 32, 1);

        // Assert
        Assert.True(result1);

        // Arrange & Act - First digit > 3 should not allow another digit
        m_calcInput.Clear();
        m_calcInput.TryAddDigit(4, 8, true, "777", 64, 32);
        bool result2 = m_calcInput.TryAddDigit(2, 8, true, "377", 32, 1);

        // Assert
        Assert.False(result2);

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryAddDigit_RolloverOctalQword_ShouldCheckFirstDigit()
    {
        // Arrange & Act - First digit == 1 should allow another digit
        m_calcInput.TryAddDigit(1, 8, true, "777", 64, 32);
        bool result1 = m_calcInput.TryAddDigit(2, 8, true, "377", 64, 1);

        // Assert
        Assert.True(result1);

        // Arrange & Act - First digit > 1 should not allow another digit
        m_calcInput.Clear();
        m_calcInput.TryAddDigit(2, 8, true, "777", 64, 32);
        bool result2 = m_calcInput.TryAddDigit(2, 8, true, "377", 64, 1);

        // Assert
        Assert.False(result2);

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryAddDigit_RolloverDecimal_ShouldValidateAgainstMax()
    {
        // Test 1: Cannot add digit if input size matches maxStr size
        m_calcInput.TryAddDigit(1, 10, true, "127", 64, 32);
        bool result1 = m_calcInput.TryAddDigit(0, 10, true, "1", 8, 1);
        Assert.False(result1);

        // Test 2: Cannot add digit if n char comparison > 0
        m_calcInput.TryAddDigit(2, 10, true, "127", 64, 32);
        bool result2 = m_calcInput.TryAddDigit(2, 10, true, "110", 8, 2);
        Assert.False(result2);

        // Test 3: Can add digit if n char comparison < 0
        bool result3 = m_calcInput.TryAddDigit(7, 10, true, "130", 8, 2);
        Assert.True(result3);

        // Test 4: Cannot add digit if digit exceeds max value
        m_calcInput.Clear();
        m_calcInput.TryAddDigit(1, 10, true, "127", 64, 32);
        m_calcInput.TryAddDigit(2, 10, true, "127", 64, 32);
        bool result4 = m_calcInput.TryAddDigit(8, 10, true, "127", 8, 2);
        Assert.False(result4);

        // Test 5: Can add digit if digit does not exceed max value
        bool result5 = m_calcInput.TryAddDigit(7, 10, true, "127", 8, 2);
        Assert.True(result5);

        // Test 6: Negative values - cannot add digit if digit exceeds max value
        m_calcInput.Backspace();
        m_calcInput.TryToggleSign(true, "127");
        bool result6 = m_calcInput.TryAddDigit(9, 10, true, "127", 8, 2);
        Assert.False(result6);

        // Test 7: Negative values - can add digit if digit does not exceed max value
        bool result7 = m_calcInput.TryAddDigit(8, 10, true, "127", 8, 2);
        Assert.True(result7);

        // Cleanup
        Cleanup();
    }


    [Fact]
    public void TryAddDecimalPt_Empty_ShouldAddDecimalToEmptyInput()
    {
        // Arrange & Assert pre-condition
        Assert.False(m_calcInput.HasDecimalPt());

        // Act
        bool result = m_calcInput.TryAddDecimalPt();

        // Assert
        Assert.True(result);
        Assert.True(m_calcInput.HasDecimalPt());
        Assert.Equal("0.", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryAddDecimalPoint_Twice_ShouldFailOnSecondAttempt()
    {
        // Arrange & Assert pre-condition
        Assert.False(m_calcInput.HasDecimalPt());

        // Act - First attempt
        bool result1 = m_calcInput.TryAddDecimalPt();

        // Assert
        Assert.True(result1);
        Assert.True(m_calcInput.HasDecimalPt());

        // Act - Second attempt
        bool result2 = m_calcInput.TryAddDecimalPt();

        // Assert
        Assert.False(result2);

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryAddDecimalPoint_WithExponent_ShouldFail()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        m_calcInput.TryBeginExponent();
        m_calcInput.TryAddDigit(2, 10, false, "999", 64, 32);

        // Act
        bool result = m_calcInput.TryAddDecimalPt();

        // Assert
        Assert.False(result);

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryBeginExponent_NoExponent_ShouldSucceed()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);

        // Act
        bool result = m_calcInput.TryBeginExponent();

        // Assert
        Assert.True(result);
        Assert.Equal("1.e+0", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void TryBeginExponent_WithExponent_ShouldFail()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        bool result1 = m_calcInput.TryBeginExponent();

        // Assert preparation
        Assert.True(result1);

        // Act
        bool result2 = m_calcInput.TryBeginExponent();

        // Assert
        Assert.False(result2);

        // Cleanup
        Cleanup();
    }


    [Fact]
    public void Backspace_OnZero_ShouldRemainZero()
    {
        // Act
        m_calcInput.Backspace();

        // Assert
        Assert.Equal("0", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void Backspace_SingleChar_ShouldResetToZero()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);

        // Assert preparation
        Assert.Equal("1", m_calcInput.ToString(10));

        // Act
        m_calcInput.Backspace();

        // Assert
        Assert.Equal("0", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void Backspace_MultiChar_ShouldRemoveLastChar()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        m_calcInput.TryAddDigit(2, 10, false, "999", 64, 32);

        // Assert preparation
        Assert.Equal("12", m_calcInput.ToString(10));

        // Act
        m_calcInput.Backspace();

        // Assert
        Assert.Equal("1", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void Backspace_Decimal_ShouldRemoveDecimalPoint()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        m_calcInput.TryAddDecimalPt();

        // Assert preparation
        Assert.Equal("1.", m_calcInput.ToString(10));
        Assert.True(m_calcInput.HasDecimalPt());

        // Act
        m_calcInput.Backspace();

        // Assert
        Assert.Equal("1", m_calcInput.ToString(10));
        Assert.False(m_calcInput.HasDecimalPt());

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void Backspace_MultiCharDecimal_ShouldRemoveLastDigit()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        m_calcInput.TryAddDecimalPt();
        m_calcInput.TryAddDigit(2, 10, false, "999", 64, 32);
        m_calcInput.TryAddDigit(3, 10, false, "999", 64, 32);

        // Assert preparation
        Assert.Equal("1.23", m_calcInput.ToString(10));

        // Act
        m_calcInput.Backspace();

        // Assert
        Assert.Equal("1.2", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }


    [Fact]
    public void Backspace_ZeroDecimalWithoutPrefixZeros_ShouldHandleLeadingZeroesCorrectly()
    {
        // Arrange
        m_calcInput.TryAddDigit(0, 10, false, "999", 64, 32);
        m_calcInput.TryAddDecimalPt();

        // Assert preparation
        Assert.Equal("0.", m_calcInput.ToString(10));

        // Act
        m_calcInput.Backspace();
        m_calcInput.TryAddDigit(0, 10, false, "999", 64, 32);

        // Assert
        Assert.Equal("0", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void SetDecimalSymbol_ShouldChangeDisplayedDecimalSymbol()
    {
        // Arrange
        m_calcInput.TryAddDecimalPt();

        // Assert preparation
        Assert.Equal("0.", m_calcInput.ToString(10));

        // Act
        m_calcInput.SetDecimalSymbol(',');

        // Assert
        Assert.Equal("0,", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void ToString_Empty_ShouldReturnZero()
    {
        // Act & Assert
        Assert.Equal("0", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void ToString_Negative_ShouldIncludeNegativeSign()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        m_calcInput.TryToggleSign(false, "999");

        // Act & Assert
        Assert.Equal("-1", m_calcInput.ToString(10));

        // Cleanup
        Cleanup();
    }

    [Fact]
    public void ToRational_ShouldConvertCorrectly()
    {
        // Arrange
        m_calcInput.TryAddDigit(1, 10, false, "999", 64, 32);
        m_calcInput.TryAddDigit(2, 10, false, "999", 64, 32);
        m_calcInput.TryAddDigit(3, 10, false, "999", 64, 32);

        // Assert preparation
        Assert.Equal("123", m_calcInput.ToString(10));

        // Act
        var rat = m_calcInput.ToRational(new RatPak(), 10, 32);

        // Assert
        // Assert.Single(rat.P.Mantissa);
        Assert.Equal((uint)123, rat.P.Mantissa[0]);

        // Cleanup
        Cleanup();
    }
}
